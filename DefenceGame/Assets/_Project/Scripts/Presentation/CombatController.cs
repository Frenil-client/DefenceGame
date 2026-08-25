using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // 실시간 전투 - 배치된 유닛이 사거리 내 몬스터/석상을 자동 공격한다.
    // 데미지 계산(방어력 공식)과 hp/처치 처리를 여기서 소유한다. 시뮬은 스폰/순회/배치/경제만 다루고,
    // 로스터(aliveCount) 갱신만 LoopSimulator.OnMonsterKilled 로 알린다.
    public sealed class CombatController : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private LoopMapView mapView;     // 유닛/몬스터 셀->월드 변환. 인스펙터 등록
        [SerializeField] private EntityView entityView;   // 보간된 몬스터 월드 위치(빔 끝점). 인스펙터 등록
        [SerializeField] private float beamVisibleSeconds = 0.08f;

        private static readonly Color BeamColor = new Color(1f, 0.92f, 0.35f);
        // [TEMP] 워크래프트3 계열 방어력 공식 상수. 실피해 = 원피해 / (1 + K*방어력). 방어력 1당 유효체력 +6%,
        // 감소율은 100%에 점근하므로 완전 차단이 없다(관통 하한 불필요). K와 방어력 값은 시뮬로 재확정한다.
        private static readonly Fixed ArmorK = Fixed.FromRatio(6, 100); // 0.06

        private readonly Dictionary<LoopUnit, float> cooldownByUnit = new Dictionary<LoopUnit, float>();
        private readonly Dictionary<LoopUnit, LineRenderer> beamByUnit = new Dictionary<LoopUnit, LineRenderer>();
        private readonly Dictionary<LoopUnit, float> beamTimerByUnit = new Dictionary<LoopUnit, float>();
        private Material beamMaterial;
        private int lastRunId = -1;

        // 스킬 런타임 상태. 스킬은 units 에 아직 미부여라 기본적으로 비활성(프레임워크만 준비).
        private readonly Dictionary<LoopUnit, List<SkillData>> skillsByUnit = new Dictionary<LoopUnit, List<SkillData>>();
        private readonly Dictionary<LoopUnit, int> attackCountByUnit = new Dictionary<LoopUnit, int>();
        private readonly Dictionary<LoopMonster, MonsterStatus> statusByMonster = new Dictionary<LoopMonster, MonsterStatus>();
        private readonly List<LoopMonster> statusScratch = new List<LoopMonster>();
        private readonly List<LoopMonster> extraScratch = new List<LoopMonster>();  // 다중타격/관통 대상 중복 방지용
        private readonly List<Vector4> slowAuraScratch = new List<Vector4>(); // (x, y, radius, pct) 감속 오라 원본, 매 프레임 수집

        // 몬스터에 걸린 상태이상(도트/온힛 감속). 오라 감속은 매 프레임 근접 유닛에서 재계산한다.
        private sealed class MonsterStatus
        {
            public float dotRemaining;
            public Fixed dotDps;
            public float slowRemaining;
            public float slowPct; // 0~1 (온힛 감속)
        }

        // 재시작(RunId 변화) 시 이전 런의 쿨다운/빔/스킬 상태를 정리한다.
        private void ResetRun()
        {
            foreach (var pair in beamByUnit) { if (pair.Value != null) Destroy(pair.Value.gameObject); }
            beamByUnit.Clear();
            cooldownByUnit.Clear();
            beamTimerByUnit.Clear();
            skillsByUnit.Clear();
            attackCountByUnit.Clear();
            statusByMonster.Clear();
        }

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            if (mapView == null) return;
            if (lastRunId != game.RunId) { lastRunId = game.RunId; ResetRun(); }

            LoopSimulator sim = game.Context.sim;
            if (sim.state.defeated) return;

            float dt = Time.deltaTime * game.Speed;
            var units = sim.state.unitList;

            CleanupStale(units);

            for (int i = 0; i < units.Count; ++i)
            {
                LoopUnit u = units[i];

                float cd;
                cooldownByUnit.TryGetValue(u, out cd);
                if (cd > 0f) cd -= dt;

                Vector2 uCell = UnitCell(u); // 현재 렌더 위치의 셀 좌표(집중 추격 중에는 홈 셀이 아니다)

                if (cd <= 0f)
                {
                    if (u.focusMonster != null && u.focusMonster.alive)
                    {
                        // 집중(추격): 대상이 사거리 안이면 이동 중에도 공격한다. 대상 사망 처리와 재배치는 EntityView 가 맡는다.
                        LoopMonster fm = u.focusMonster;
                        if (InRangeMonster(sim, u, uCell, fm))
                        {
                            AttackMonster(sim, u, fm);
                            ShowBeamTo(u, MonsterWorld(sim, fm));
                            cd = AttackInterval(u);
                        }
                    }
                    else if (u.focusStatue != null && u.focusStatue.alive)
                    {
                        LoopStatue fs = u.focusStatue;
                        if (InRangeStatue(u, uCell, fs))
                        {
                            bool destroyed = DamageStatue(fs, u.data.atk);
                            ShowBeamTo(u, StatueWorld(fs));
                            if (destroyed) game.Context.selectionTokens += game.Context.statueTokenReward;
                            cd = AttackInterval(u);
                        }
                    }
                    else if (entityView == null || entityView.IsUnitArrived(u))
                    {
                        // 집중 없음: 홈에 도착한 경우에만 자동 공격(재배치 이동 중에는 공격 안 함). 몬스터 우선, 없으면 석상.
                        LoopMonster mTarget = FindMonsterTarget(sim, u, uCell);
                        if (mTarget != null)
                        {
                            AttackMonster(sim, u, mTarget);
                            ShowBeamTo(u, MonsterWorld(sim, mTarget));
                            cd = AttackInterval(u);
                        }
                        else
                        {
                            LoopStatue sTarget = FindStatueTarget(sim, u, uCell);
                            if (sTarget != null)
                            {
                                bool destroyed = DamageStatue(sTarget, u.data.atk);
                                ShowBeamTo(u, StatueWorld(sTarget));
                                if (destroyed) game.Context.selectionTokens += game.Context.statueTokenReward;
                                cd = AttackInterval(u);
                            }
                        }
                    }
                }
                cooldownByUnit[u] = cd;

                TickBeam(u, dt);
            }

            TickStatus(sim, dt); // 도트/감속 등 몬스터 상태이상 진행
        }

        private float AttackInterval(LoopUnit u)
        {
            double aps = u.data.atkSpeed.ToDoubleForDisplay();
            if (aps <= 0.0) return 1f;
            return (float)(1.0 / aps);
        }

        // ---- 데미지 처리(전투 스크립트 소유). 방어력 곱연산 감소 후 hp/처치 처리, 로스터만 시뮬에 알린다. ----

        // 방어력을 곱연산으로 감소시킨다(WC3 공식). 실피해 = 원피해 / (1 + K*방어력). 방어력 0이면 원 피해 그대로.
        private static Fixed ArmorReduced(Fixed atk, Fixed armor)
        {
            Fixed divisor = Fixed.One + ArmorK * armor;
            return divisor.raw > 0 ? atk / divisor : atk;
        }

        // 몬스터에 피해를 적용한다(유효 방어력 반영). 죽으면 로스터 갱신.
        private void HitMonster(LoopSimulator sim, LoopMonster m, Fixed dmg)
        {
            if (m == null || !m.alive || dmg.raw <= 0) return;
            m.hp = m.hp - ArmorReduced(dmg, EffectiveArmor(sim, m));
            if (m.hp.raw <= 0)
            {
                m.hp = Fixed.Zero;
                m.alive = false;
                sim.OnMonsterKilled();
            }
        }

        // ---- 스킬(패시브) 적용: 트리거 x 효과. 유닛에 스킬 미부여면 평타 단일공격과 동일하게 동작한다. ----

        // 한 번의 평타 처리. 스킬을 해석해 배수/다중/광역/도트/감속을 조립 적용한다.
        private void AttackMonster(LoopSimulator sim, LoopUnit u, LoopMonster primary)
        {
            List<SkillData> skills = GetSkills(u);
            int count = AdvanceAttackCount(u);
            Fixed atk = EffectiveAtk(sim, u);

            Fixed mult = Fixed.One;
            int multi = 1, pierce = 0;
            float areaRadius = 0f; Fixed areaRatio = Fixed.Zero;
            Fixed dotDps = Fixed.Zero; float dotDur = 0f;
            float slowPct = 0f, slowDur = 0f;

            for (int i = 0; i < skills.Count; ++i)
            {
                SkillData s = skills[i];
                if (!TriggerFires(s, count)) continue;
                switch (s.effect)
                {
                    case SkillEffect.BonusDamage: mult = mult + s.magnitude; break;
                    case SkillEffect.Crit: mult = mult * s.magnitude; break;
                    case SkillEffect.MultiTarget: if (s.count > multi) multi = s.count; break;
                    case SkillEffect.Pierce: if (s.count > pierce) pierce = s.count; break;
                    case SkillEffect.AreaDamage: areaRadius = (float)s.radius.ToDoubleForDisplay(); areaRatio = s.magnitude; break;
                    case SkillEffect.DamageOverTime: dotDps = s.magnitude; dotDur = (float)s.duration.ToDoubleForDisplay(); break;
                    case SkillEffect.Slow: if (s.radius.raw <= 0) { slowPct = (float)s.magnitude.ToDoubleForDisplay(); slowDur = (float)s.duration.ToDoubleForDisplay(); } break;
                    // AllyBuff / ArmorReduction / 오라 Slow(radius>0) 는 패시브 오라라 여기서 처리하지 않는다.
                }
            }

            Fixed hit = atk * mult;
            HitMonster(sim, primary, hit);
            if (dotDps.raw > 0 && dotDur > 0f) ApplyDot(primary, dotDps, dotDur);
            if (slowPct > 0f && slowDur > 0f) ApplySlow(primary, slowPct, slowDur);

            int extra = Mathf.Max(multi - 1, pierce - 1);
            if (extra > 0) HitExtraTargets(sim, primary, extra, hit);
            if (areaRadius > 0f && areaRatio.raw > 0) HitAreaTargets(sim, primary, areaRadius, hit, areaRatio);
        }

        // 주 대상 주변 가까운 몬스터 count 명에 풀 피해(다중타격/관통 근사).
        private void HitExtraTargets(LoopSimulator sim, LoopMonster primary, int count, Fixed hit)
        {
            Fixed px, py; sim.GetMonsterPosition(primary, out px, out py);
            double cx = px.ToDoubleForDisplay(), cy = py.ToDoubleForDisplay();
            var list = sim.state.monsterList;
            for (int picked = 0; picked < count; ++picked)
            {
                LoopMonster best = null; double bestSq = double.MaxValue;
                for (int i = 0; i < list.Count; ++i)
                {
                    LoopMonster m = list[i];
                    if (!m.alive || m == primary || extraScratch.Contains(m)) continue;
                    Fixed fx, fy; sim.GetMonsterPosition(m, out fx, out fy);
                    double dx = fx.ToDoubleForDisplay() - cx, dy = fy.ToDoubleForDisplay() - cy;
                    double d2 = dx * dx + dy * dy;
                    if (d2 < bestSq) { bestSq = d2; best = m; }
                }
                if (best == null) break;
                extraScratch.Add(best);
                HitMonster(sim, best, hit);
            }
            extraScratch.Clear();
        }

        // 주 대상 반경 내 몬스터에 hit*ratio 광역 피해.
        private void HitAreaTargets(LoopSimulator sim, LoopMonster primary, float radius, Fixed hit, Fixed ratio)
        {
            Fixed px, py; sim.GetMonsterPosition(primary, out px, out py);
            double cx = px.ToDoubleForDisplay(), cy = py.ToDoubleForDisplay();
            double r2 = radius * radius;
            Fixed dmg = hit * ratio;
            var list = sim.state.monsterList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopMonster m = list[i];
                if (!m.alive || m == primary) continue;
                Fixed fx, fy; sim.GetMonsterPosition(m, out fx, out fy);
                double dx = fx.ToDoubleForDisplay() - cx, dy = fy.ToDoubleForDisplay() - cy;
                if (dx * dx + dy * dy <= r2) HitMonster(sim, m, dmg);
            }
        }

        // 유닛의 유효 공격력 = 기본 + 반경 내 아군 공격력 버프 오라 합산.
        private Fixed EffectiveAtk(LoopSimulator sim, LoopUnit u)
        {
            Fixed baseAtk = u.data.atk;
            Fixed bonus = Fixed.Zero;
            Vector2 uc = UnitCell(u);
            var units = sim.state.unitList;
            for (int i = 0; i < units.Count; ++i)
            {
                List<SkillData> os = GetSkills(units[i]);
                if (os.Count == 0) continue;
                Vector2 oc = UnitCell(units[i]);
                for (int j = 0; j < os.Count; ++j)
                {
                    SkillData s = os[j];
                    if (s.trigger != SkillTrigger.Passive || s.effect != SkillEffect.AllyBuff || s.buffStat != BuffStat.Atk) continue;
                    double r = s.radius.ToDoubleForDisplay();
                    double dx = oc.x - uc.x, dy = oc.y - uc.y;
                    if (dx * dx + dy * dy <= r * r) bonus = bonus + s.magnitude;
                }
            }
            return baseAtk + baseAtk * bonus;
        }

        // 몬스터의 유효 방어력 = 기본 - 반경 내 방깎(ArmorReduction) 오라 합산(절대값, 0 미만은 0). 방어력 0이면 스캔 생략.
        private Fixed EffectiveArmor(LoopSimulator sim, LoopMonster m)
        {
            Fixed armor = m.armor;
            if (armor.raw <= 0) return armor;
            Fixed fx, fy; sim.GetMonsterPosition(m, out fx, out fy);
            double mx = fx.ToDoubleForDisplay(), my = fy.ToDoubleForDisplay();
            var units = sim.state.unitList;
            for (int i = 0; i < units.Count; ++i)
            {
                List<SkillData> os = GetSkills(units[i]);
                if (os.Count == 0) continue;
                Vector2 oc = UnitCell(units[i]);
                for (int j = 0; j < os.Count; ++j)
                {
                    SkillData s = os[j];
                    if (s.trigger != SkillTrigger.Passive || s.effect != SkillEffect.ArmorReduction) continue;
                    double r = s.radius.ToDoubleForDisplay();
                    double dx = oc.x - mx, dy = oc.y - my;
                    if (dx * dx + dy * dy <= r * r) armor = armor - s.magnitude;
                }
            }
            if (armor.raw < 0) armor = Fixed.Zero;
            return armor;
        }

        // 유닛의 스킬 정의를 해석해 캐시한다. 스킬 미부여면 빈 목록.
        private List<SkillData> GetSkills(LoopUnit u)
        {
            List<SkillData> list;
            if (skillsByUnit.TryGetValue(u, out list)) return list;
            list = new List<SkillData>();
            var ids = u.data.skillIds;
            var reg = game.Context.skillById;
            if (ids != null && reg != null)
            {
                for (int i = 0; i < ids.Count; ++i)
                {
                    SkillData s;
                    if (reg.TryGetValue(ids[i], out s)) list.Add(s);
                }
            }
            skillsByUnit[u] = list;
            return list;
        }

        private int AdvanceAttackCount(LoopUnit u)
        {
            int c;
            attackCountByUnit.TryGetValue(u, out c);
            c += 1;
            attackCountByUnit[u] = c;
            return c;
        }

        // 트리거가 이번 평타에 발동하는가. Passive 항상, EveryNth 는 N배수, Chance 는 확률.
        private bool TriggerFires(SkillData s, int attackCount)
        {
            switch (s.trigger)
            {
                case SkillTrigger.Passive: return true;
                case SkillTrigger.EveryNthAttack:
                    int n = (int)s.triggerN.ToIntRounded();
                    return n > 0 && attackCount % n == 0;
                case SkillTrigger.ChanceOnAttack:
                    return Random.value < (float)s.triggerN.ToDoubleForDisplay();
                default: return false;
            }
        }

        private MonsterStatus GetStatus(LoopMonster m)
        {
            MonsterStatus st;
            if (!statusByMonster.TryGetValue(m, out st)) { st = new MonsterStatus(); statusByMonster[m] = st; }
            return st;
        }

        private void ApplyDot(LoopMonster m, Fixed dps, float dur)
        {
            MonsterStatus st = GetStatus(m);
            st.dotDps = dps; st.dotRemaining = dur;
        }

        private void ApplySlow(LoopMonster m, float pct, float dur)
        {
            MonsterStatus st = GetStatus(m);
            st.slowPct = pct; st.slowRemaining = dur;
        }

        // 몬스터 상태이상 진행: 도트 틱 + 감속(온힛/오라) 재계산해 moveSpeed 를 실시간 갱신.
        private void TickStatus(LoopSimulator sim, float dt)
        {
            if (dt <= 0f) return;

            slowAuraScratch.Clear();
            var units = sim.state.unitList;
            for (int i = 0; i < units.Count; ++i)
            {
                List<SkillData> os = GetSkills(units[i]);
                if (os.Count == 0) continue;
                Vector2 oc = UnitCell(units[i]);
                for (int j = 0; j < os.Count; ++j)
                {
                    SkillData s = os[j];
                    if (s.trigger == SkillTrigger.Passive && s.effect == SkillEffect.Slow && s.radius.raw > 0)
                        slowAuraScratch.Add(new Vector4(oc.x, oc.y, (float)s.radius.ToDoubleForDisplay(), (float)s.magnitude.ToDoubleForDisplay()));
                }
            }
            bool anyAura = slowAuraScratch.Count > 0;

            var monsters = sim.state.monsterList;
            for (int i = 0; i < monsters.Count; ++i)
            {
                LoopMonster m = monsters[i];
                if (!m.alive) continue;
                MonsterStatus st;
                statusByMonster.TryGetValue(m, out st);
                bool hasStatus = st != null && (st.dotRemaining > 0f || st.slowRemaining > 0f);

                if (!hasStatus && !anyAura)
                {
                    if (m.moveSpeed.raw != m.baseMoveSpeed.raw) m.moveSpeed = m.baseMoveSpeed;
                    continue;
                }

                if (st != null && st.dotRemaining > 0f && st.dotDps.raw > 0)
                {
                    m.hp = m.hp - st.dotDps * FixedFromFloat(dt);
                    st.dotRemaining -= dt;
                    if (m.hp.raw <= 0) { m.hp = Fixed.Zero; m.alive = false; sim.OnMonsterKilled(); continue; }
                }

                float onHit = 0f;
                if (st != null && st.slowRemaining > 0f) { onHit = st.slowPct; st.slowRemaining -= dt; }
                float aura = AuraSlowAt(sim, m);
                float total = Mathf.Clamp01(Mathf.Max(onHit, aura));
                m.moveSpeed = ScaleSpeed(m.baseMoveSpeed, total);
            }

            CleanupStatus();
        }

        private float AuraSlowAt(LoopSimulator sim, LoopMonster m)
        {
            if (slowAuraScratch.Count == 0) return 0f;
            Fixed fx, fy; sim.GetMonsterPosition(m, out fx, out fy);
            double mx = fx.ToDoubleForDisplay(), my = fy.ToDoubleForDisplay();
            float best = 0f;
            for (int i = 0; i < slowAuraScratch.Count; ++i)
            {
                Vector4 a = slowAuraScratch[i];
                double dx = a.x - mx, dy = a.y - my;
                if (dx * dx + dy * dy <= (double)a.z * a.z && a.w > best) best = a.w;
            }
            return best;
        }

        private void CleanupStatus()
        {
            if (statusByMonster.Count == 0) return;
            statusScratch.Clear();
            foreach (var pair in statusByMonster) if (!pair.Key.alive) statusScratch.Add(pair.Key);
            for (int i = 0; i < statusScratch.Count; ++i) statusByMonster.Remove(statusScratch[i]);
            statusScratch.Clear();
        }

        private static Fixed FixedFromFloat(float v)
        {
            return Fixed.FromRatio((long)Mathf.Round(v * 1000f), 1000);
        }

        private static Fixed ScaleSpeed(Fixed baseSpeed, float slow)
        {
            return baseSpeed * FixedFromFloat(1f - slow);
        }

        // 석상에 피해를 적용한다(석상은 방어력 없음). 반환값은 이번 타격으로 파괴됐는지 여부.
        private bool DamageStatue(LoopStatue s, Fixed atk)
        {
            if (s == null || !s.alive || atk.raw <= 0) return false;
            s.hp = s.hp - atk;
            if (s.hp.raw <= 0)
            {
                s.hp = Fixed.Zero;
                s.alive = false;
                return true;
            }
            return false;
        }

        // 사거리 내 가장 가까운 살아있는 몬스터(유닛 현재 셀 기준, 거리 제곱 비교). 없으면 null.
        private LoopMonster FindMonsterTarget(LoopSimulator sim, LoopUnit u, Vector2 uCell)
        {
            double rangeSq = RangeSq(u);
            LoopMonster best = null;
            double bestSq = double.MaxValue;
            var list = sim.state.monsterList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopMonster m = list[i];
                if (!m.alive) continue;

                Fixed fx, fy;
                sim.GetMonsterPosition(m, out fx, out fy);
                double dx = fx.ToDoubleForDisplay() - uCell.x;
                double dy = fy.ToDoubleForDisplay() - uCell.y;
                double d2 = dx * dx + dy * dy;
                if (d2 > rangeSq) continue;
                if (d2 < bestSq) { bestSq = d2; best = m; }
            }
            return best;
        }

        // 사거리 내 가장 가까운 살아있는 석상. 없으면 null.
        private LoopStatue FindStatueTarget(LoopSimulator sim, LoopUnit u, Vector2 uCell)
        {
            double rangeSq = RangeSq(u);
            LoopStatue best = null;
            double bestSq = double.MaxValue;
            var list = sim.state.statueList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopStatue s = list[i];
                if (!s.alive) continue;

                double dx = s.cellX - uCell.x;
                double dy = s.cellY - uCell.y;
                double d2 = dx * dx + dy * dy;
                if (d2 > rangeSq) continue;
                if (d2 < bestSq) { bestSq = d2; best = s; }
            }
            return best;
        }

        private bool InRangeMonster(LoopSimulator sim, LoopUnit u, Vector2 uCell, LoopMonster m)
        {
            Fixed fx, fy;
            sim.GetMonsterPosition(m, out fx, out fy);
            double dx = fx.ToDoubleForDisplay() - uCell.x;
            double dy = fy.ToDoubleForDisplay() - uCell.y;
            return dx * dx + dy * dy <= RangeSq(u);
        }

        private bool InRangeStatue(LoopUnit u, Vector2 uCell, LoopStatue s)
        {
            double dx = s.cellX - uCell.x;
            double dy = s.cellY - uCell.y;
            return dx * dx + dy * dy <= RangeSq(u);
        }

        private double RangeSq(LoopUnit u)
        {
            double range = u.data.range.ToDoubleForDisplay();
            return range * range;
        }

        // 유닛의 현재 렌더 위치를 셀 소수 좌표로. 뷰가 없으면 홈 셀.
        private Vector2 UnitCell(LoopUnit u)
        {
            Vector3 w;
            if (entityView != null && entityView.TryGetUnitWorld(u, out w)) return mapView.WorldToCellF(w);
            return new Vector2(u.cellX, u.cellY);
        }

        // 유닛의 현재 렌더 월드 위치(빔 시작점). 뷰가 없으면 홈 셀 월드.
        private Vector3 UnitWorld(LoopUnit u)
        {
            Vector3 w;
            if (entityView != null && entityView.TryGetUnitWorld(u, out w)) return w;
            return mapView.CellToWorldF(u.cellX, u.cellY) + new Vector3(0f, 0.35f, 0f);
        }

        // 보간된 몬스터 몸체 위치(없으면 시뮬 위치).
        private Vector3 MonsterWorld(LoopSimulator sim, LoopMonster m)
        {
            Vector3 world;
            if (entityView != null && entityView.TryGetMonsterWorld(m, out world)) return world;
            Fixed fx, fy;
            sim.GetMonsterPosition(m, out fx, out fy);
            return mapView.CellToWorldF((float)fx.ToDoubleForDisplay(), (float)fy.ToDoubleForDisplay()) + new Vector3(0f, 0.35f, 0f);
        }

        private Vector3 StatueWorld(LoopStatue s)
        {
            return mapView.CellToWorldF(s.cellX, s.cellY) + new Vector3(0f, 0.5f, 0f);
        }

        // ---- 공격 빔(짧게 번쩍이는 선) ----

        private void ShowBeamTo(LoopUnit u, Vector3 to)
        {
            LineRenderer lr = GetBeam(u);
            Vector3 from = UnitWorld(u); // 유닛의 현재 위치에서 발사(추격 중에도 실제 위치 기준)
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.enabled = true;
            beamTimerByUnit[u] = beamVisibleSeconds;
        }

        private void TickBeam(LoopUnit u, float dt)
        {
            float t;
            if (!beamTimerByUnit.TryGetValue(u, out t)) return;
            if (t <= 0f) return;
            t -= dt;
            beamTimerByUnit[u] = t;
            if (t <= 0f)
            {
                LineRenderer lr;
                if (beamByUnit.TryGetValue(u, out lr) && lr != null) lr.enabled = false;
            }
        }

        private LineRenderer GetBeam(LoopUnit u)
        {
            LineRenderer lr;
            if (beamByUnit.TryGetValue(u, out lr) && lr != null) return lr;

            GameObject go = new GameObject("AttackBeam");
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.widthMultiplier = 0.07f;
            lr.numCapVertices = 2;
            lr.sharedMaterial = GetBeamMaterial();
            lr.startColor = BeamColor;
            lr.endColor = new Color(BeamColor.r, BeamColor.g, BeamColor.b, 0.35f);
            lr.enabled = false;
            beamByUnit[u] = lr;
            return lr;
        }

        private Material GetBeamMaterial()
        {
            if (beamMaterial != null) return beamMaterial;
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Color");
            beamMaterial = new Material(s);
            beamMaterial.color = BeamColor;
            if (beamMaterial.HasProperty("_BaseColor")) beamMaterial.SetColor("_BaseColor", BeamColor);
            return beamMaterial;
        }

        // 회수/조합 등으로 필드에서 빠진 유닛의 쿨다운/빔/스킬 캐시를 정리한다.
        private void CleanupStale(List<LoopUnit> units)
        {
            if (beamByUnit.Count == 0 && cooldownByUnit.Count == 0 && skillsByUnit.Count == 0) return;

            HashSet<LoopUnit> present = new HashSet<LoopUnit>(units);
            HashSet<LoopUnit> staleSet = null;
            foreach (var pair in cooldownByUnit)
            {
                if (present.Contains(pair.Key)) continue;
                if (staleSet == null) staleSet = new HashSet<LoopUnit>();
                staleSet.Add(pair.Key);
            }
            foreach (var pair in skillsByUnit)
            {
                if (present.Contains(pair.Key)) continue;
                if (staleSet == null) staleSet = new HashSet<LoopUnit>();
                staleSet.Add(pair.Key);
            }
            if (staleSet == null) return;
            List<LoopUnit> stale = new List<LoopUnit>(staleSet);

            foreach (var u in stale)
            {
                cooldownByUnit.Remove(u);
                beamTimerByUnit.Remove(u);
                skillsByUnit.Remove(u);
                attackCountByUnit.Remove(u);
                LineRenderer lr;
                if (beamByUnit.TryGetValue(u, out lr))
                {
                    if (lr != null) Destroy(lr.gameObject);
                    beamByUnit.Remove(u);
                }
            }
        }
    }
}
