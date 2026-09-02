using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Simulation;
using Synthesis.Core.Combat;

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

        private readonly Dictionary<LoopUnit, float> cooldownByUnit = new Dictionary<LoopUnit, float>();
        private readonly Dictionary<LoopUnit, LineRenderer> beamByUnit = new Dictionary<LoopUnit, LineRenderer>();
        private readonly Dictionary<LoopUnit, float> beamTimerByUnit = new Dictionary<LoopUnit, float>();
        private Material beamMaterial;
        private int lastRunId = -1;

        // 스킬 런타임 상태. 부여 목록은 units.csv 의 skillIds 이며 분배 근거는 Docs/UNIT_SKILLS.md 다.
        private readonly Dictionary<LoopUnit, List<SkillData>> skillsByUnit = new Dictionary<LoopUnit, List<SkillData>>();
        private readonly Dictionary<LoopUnit, int> attackCountByUnit = new Dictionary<LoopUnit, int>();
        private readonly Dictionary<LoopMonster, MonsterStatus> statusByMonster = new Dictionary<LoopMonster, MonsterStatus>();
        private readonly List<LoopMonster> statusScratch = new List<LoopMonster>();
        private readonly List<LoopMonster> extraScratch = new List<LoopMonster>();  // 다중타격/관통 대상 중복 방지용
        private readonly List<SkillData> onHitSlowScratch = new List<SkillData>();  // 이번 평타의 온힛 감속 스킬
        private readonly List<SkillData> areaScratch = new List<SkillData>();       // 이번 평타의 광역 스킬

        // 필드 오라 표본. 매 프레임 한 번만 모아 두고 아군 버프/방깎/감속/장판 질의가 공유한다.
        // 유닛마다 전체 유닛을 다시 훑으면 유닛 수의 제곱으로 늘어난다.
        private struct AuraSample
        {
            public string skillId;
            public float x;
            public float y;
            public float radius;
            public Fixed magnitude;
            public SkillEffect effect;
            public BuffStat stat;
        }
        private readonly List<AuraSample> auraScratch = new List<AuraSample>();
        private readonly HashSet<string> stackScratch = new HashSet<string>(); // 중첩 판정용 스킬 id 집합

        // 몬스터에 걸린 온힛 감속. 스킬 id 별로 따로 들고 있어야 약한 스킬이 강한 스킬을 덮어쓰지 않는다.
        private sealed class SlowSource
        {
            public float pct;       // 0~1
            public float remaining; // 초
        }

        private sealed class MonsterStatus
        {
            public readonly Dictionary<string, SlowSource> slowBySkill = new Dictionary<string, SlowSource>();
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
            CollectAuras(sim); // 이번 프레임의 오라 표본. 아래 전부가 이걸 읽는다

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
            aps = aps * (1.0 + AllyBuffRatio(u, BuffStat.AtkSpeed).ToDoubleForDisplay());
            if (aps <= 0.0) return 1f;
            return (float)(1.0 / aps);
        }

        // ---- 데미지 처리(전투 스크립트 소유). 방어력 감소는 Core 의 ArmorFormula 한 벌을 쓴다. ----

        // 몬스터에 피해를 적용한다(유효 방어력 반영). 죽으면 로스터 갱신.
        private void HitMonster(LoopSimulator sim, LoopMonster m, Fixed dmg)
        {
            if (m == null || !m.alive || dmg.raw <= 0) return;
            m.hp = m.hp - ArmorFormula.Reduced(dmg, EffectiveArmor(sim, m));
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

            // 스킬 id 가 다르면 효과가 같아도 따로 적용된다(중첩 규칙, UNIT_SKILLS.md 3장).
            // 한 유닛이 같은 효과를 두 개 들고 있어도 덮어쓰지 않고 둘 다 쌓인다.
            Fixed mult = Fixed.One;
            int extra = 0;
            onHitSlowScratch.Clear();
            areaScratch.Clear();

            for (int i = 0; i < skills.Count; ++i)
            {
                SkillData s = skills[i];
                if (!TriggerFires(s, count)) continue;
                switch (s.effect)
                {
                    case SkillEffect.BonusDamage: mult = mult + s.magnitude; break;
                    case SkillEffect.Crit: mult = mult * s.magnitude; break;
                    case SkillEffect.MultiTarget: extra += Mathf.Max(s.count - 1, 0); break;
                    case SkillEffect.Pierce: extra += Mathf.Max(s.count - 1, 0); break;
                    case SkillEffect.AreaDamage: areaScratch.Add(s); break;
                    case SkillEffect.Slow: if (s.radius.raw <= 0) onHitSlowScratch.Add(s); break;
                    // AllyBuff / ArmorReduction / DamageZone / 오라 Slow(radius>0) 는 오라라 여기서 처리하지 않는다.
                }
            }

            Fixed hit = atk * mult;
            HitMonster(sim, primary, hit);

            for (int i = 0; i < onHitSlowScratch.Count; ++i)
            {
                SkillData s = onHitSlowScratch[i];
                float pct = (float)s.magnitude.ToDoubleForDisplay();
                float dur = (float)s.duration.ToDoubleForDisplay();
                if (pct > 0f && dur > 0f) ApplySlow(primary, s.id, pct, dur);
            }

            if (extra > 0) HitExtraTargets(sim, primary, extra, hit);

            for (int i = 0; i < areaScratch.Count; ++i)
            {
                SkillData s = areaScratch[i];
                float radius = (float)s.radius.ToDoubleForDisplay();
                if (radius > 0f && s.magnitude.raw > 0) HitAreaTargets(sim, primary, radius, hit, s.magnitude);
            }
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
            Fixed bonus = AllyBuffRatio(u, BuffStat.Atk);
            return baseAtk + baseAtk * bonus;
        }

        // ---- 오라 중첩 규칙 (UNIT_SKILLS.md 3장) ----
        //   스택 키는 스킬 id 다. 같은 스킬은 필드에 몇 기가 깔려 있어도 1회만 센다.
        //   프리스트 4기를 겹쳐도 WARCRY1 은 한 번이고, 프리스트 + 크루세이더는 스킬이 달라 둘 다 쌓인다.
        //   효과가 같아도 스킬 id 가 다르면 각각 더해진다. 합산은 전부 덧셈이며 상한만 효과별로 다르다.

        // 이번 프레임의 오라 표본을 모은다. 반경 0(온힛)과 비패시브는 오라가 아니다.
        private void CollectAuras(LoopSimulator sim)
        {
            auraScratch.Clear();
            var units = sim.state.unitList;
            for (int i = 0; i < units.Count; ++i)
            {
                List<SkillData> os = GetSkills(units[i]);
                if (os.Count == 0) continue;
                Vector2 oc = UnitCell(units[i]);
                for (int j = 0; j < os.Count; ++j)
                {
                    SkillData s = os[j];
                    if (s.trigger != SkillTrigger.Passive) continue;
                    if (s.radius.raw <= 0) continue;
                    if (!IsAuraEffect(s.effect))
                    {
                        continue;
                    }

                    AuraSample sample;
                    sample.skillId   = s.id;
                    sample.x         = oc.x;
                    sample.y         = oc.y;
                    sample.radius    = (float)s.radius.ToDoubleForDisplay();
                    sample.magnitude = s.magnitude;
                    sample.effect    = s.effect;
                    sample.stat      = s.buffStat;
                    auraScratch.Add(sample);
                }
            }
        }

        private static bool IsAuraEffect(SkillEffect effect)
        {
            return effect == SkillEffect.AllyBuff
                || effect == SkillEffect.ArmorReduction
                || effect == SkillEffect.Slow
                || effect == SkillEffect.DamageZone;
        }

        // 대상 위치에 걸리는 오라 세기의 합. 같은 스킬 id 는 1회만 센다.
        private Fixed AuraSumAt(SkillEffect effect, BuffStat stat, float targetx, float targety)
        {
            if (auraScratch.Count == 0) return Fixed.Zero;

            stackScratch.Clear();
            Fixed total = Fixed.Zero;
            for (int i = 0; i < auraScratch.Count; ++i)
            {
                AuraSample a = auraScratch[i];
                if (a.effect != effect) continue;
                if (effect == SkillEffect.AllyBuff && a.stat != stat) continue;
                if (stackScratch.Contains(a.skillId)) continue;

                float dx = a.x - targetx, dy = a.y - targety;
                if (dx * dx + dy * dy > a.radius * a.radius) continue;

                stackScratch.Add(a.skillId);
                total = total + a.magnitude;
            }
            return total;
        }

        // 유닛이 받는 아군 버프 합산 비율. 버프를 거는 유닛 자신도 반경 안이라 자기 자신에게도 걸린다(거리 0).
        private Fixed AllyBuffRatio(LoopUnit u, BuffStat stat)
        {
            Vector2 uc = UnitCell(u);
            return AuraSumAt(SkillEffect.AllyBuff, stat, uc.x, uc.y);
        }

        // 몬스터의 유효 방어력 = 기본 - 방깎 오라 합산(절대값). 방어력은 0 밑으로 내려가지 않는다.
        private Fixed EffectiveArmor(LoopSimulator sim, LoopMonster m)
        {
            Fixed armor = m.armor;
            if (armor.raw <= 0) return armor;

            Fixed fx, fy; sim.GetMonsterPosition(m, out fx, out fy);
            Fixed cut = AuraSumAt(SkillEffect.ArmorReduction, BuffStat.None,
                (float)fx.ToDoubleForDisplay(), (float)fy.ToDoubleForDisplay());

            armor = armor - cut;
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

        // 온힛 감속을 건다. 같은 스킬이면 지속시간만 새로 고치고, 다른 스킬이면 따로 쌓인다.
        // 조건 없이 덮어쓰면 약한 스킬이 강한 스킬을 지운다(하위 유닛이 상위 유닛 효과를 무효화).
        private void ApplySlow(LoopMonster m, string skillId, float pct, float dur)
        {
            MonsterStatus st = GetStatus(m);
            SlowSource src;
            if (!st.slowBySkill.TryGetValue(skillId, out src))
            {
                src = new SlowSource();
                st.slowBySkill[skillId] = src;
            }
            src.pct = pct;
            src.remaining = dur;
        }

        // 몬스터 상태 진행: 장판(DamageZone) 피해 + 감속(온힛 + 오라) 재계산해 moveSpeed 를 갱신.
        //   지속 피해는 몬스터에 붙는 디버프가 아니라 유닛 주위에 깔린 장판이다. 안에 있는 동안만 아프다.
        private void TickStatus(LoopSimulator sim, float dt)
        {
            if (dt <= 0f) return;

            Fixed dtFixed = FixedFromFloat(dt);
            var monsters = sim.state.monsterList;
            for (int i = 0; i < monsters.Count; ++i)
            {
                LoopMonster m = monsters[i];
                if (!m.alive) continue;

                Fixed fx, fy; sim.GetMonsterPosition(m, out fx, out fy);
                float mx = (float)fx.ToDoubleForDisplay(), my = (float)fy.ToDoubleForDisplay();

                // 장판 피해. 방어력을 그대로 통과시키지 않고 평타와 같은 감소 공식을 태운다.
                Fixed zoneDps = AuraSumAt(SkillEffect.DamageZone, BuffStat.None, mx, my);
                if (zoneDps.raw > 0)
                {
                    HitMonster(sim, m, zoneDps * dtFixed);
                    if (!m.alive)
                    {
                        continue;
                    }
                }

                Fixed slow = AuraSumAt(SkillEffect.Slow, BuffStat.None, mx, my) + TickOnHitSlow(m, dt);
                if (slow.raw <= 0)
                {
                    if (m.moveSpeed.raw != m.baseMoveSpeed.raw) m.moveSpeed = m.baseMoveSpeed;
                    continue;
                }
                m.moveSpeed = m.baseMoveSpeed * CombatRules.SpeedRatioAfterSlow(slow);
            }

            CleanupStatus();
        }

        // 몬스터에 걸린 온힛 감속을 진행시키고 살아남은 것들의 합을 낸다. 스킬 id 별로 하나씩이라 중복되지 않는다.
        private Fixed TickOnHitSlow(LoopMonster m, float dt)
        {
            MonsterStatus st;
            if (!statusByMonster.TryGetValue(m, out st) || st.slowBySkill.Count == 0) return Fixed.Zero;

            Fixed total = Fixed.Zero;
            foreach (var pair in st.slowBySkill)
            {
                SlowSource src = pair.Value;
                if (src.remaining <= 0f)
                {
                    continue;
                }
                src.remaining -= dt;
                total = total + FixedFromFloat(src.pct);
            }
            return total;
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
            double range = GetEffectiveRange(u);
            return range * range;
        }

        // ---- 표시용 조회 (HUD, 선택 사거리 링) ----
        // 오라 표본은 Update 앞에서 갱신되므로 호출 순서에 따라 한 프레임 늦을 수 있다. 표시 용도라 문제 없다.

        // 일반 공격 사거리(셀). 아군 사거리 버프를 반영한 실제 사거리다.
        public float GetEffectiveRange(LoopUnit u)
        {
            if (u == null || u.data == null) return 0f;
            double range = u.data.range.ToDoubleForDisplay();
            range = range * (1.0 + AllyBuffRatio(u, BuffStat.Range).ToDoubleForDisplay());
            return (float)range;
        }

        public float GetEffectiveAtk(LoopUnit u)
        {
            if (u == null || u.data == null) return 0f;
            return (float)EffectiveAtk(game.Context.sim, u).ToDoubleForDisplay();
        }

        public float GetEffectiveAtkSpeed(LoopUnit u)
        {
            if (u == null || u.data == null) return 0f;
            double aps = u.data.atkSpeed.ToDoubleForDisplay();
            return (float)(aps * (1.0 + AllyBuffRatio(u, BuffStat.AtkSpeed).ToDoubleForDisplay()));
        }

        // 방깎 오라를 반영한 몬스터의 실제 방어력. 감소율 계산에 그대로 넘길 수 있게 Fixed 로 낸다.
        public Fixed GetEffectiveArmor(LoopMonster m)
        {
            if (m == null) return Fixed.Zero;
            return EffectiveArmor(game.Context.sim, m);
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
