using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core;
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

        // 재시작(RunId 변화) 시 이전 런의 쿨다운/빔을 정리한다.
        private void ResetRun()
        {
            foreach (var pair in beamByUnit) { if (pair.Value != null) Destroy(pair.Value.gameObject); }
            beamByUnit.Clear();
            cooldownByUnit.Clear();
            beamTimerByUnit.Clear();
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
                            DamageMonster(sim, fm, u.data.atk);
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
                            DamageMonster(sim, mTarget, u.data.atk);
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

        // 몬스터에 피해를 적용한다. 죽으면 alive=false 로 처리하고 시뮬 로스터를 갱신한다.
        private void DamageMonster(LoopSimulator sim, LoopMonster m, Fixed atk)
        {
            if (m == null || !m.alive || atk.raw <= 0) return;
            m.hp = m.hp - ArmorReduced(atk, m.armor);
            if (m.hp.raw <= 0)
            {
                m.hp = Fixed.Zero;
                m.alive = false;
                sim.OnMonsterKilled();
            }
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

        // 회수/조합 등으로 필드에서 빠진 유닛의 쿨다운/빔을 정리한다.
        private void CleanupStale(List<LoopUnit> units)
        {
            if (beamByUnit.Count == 0 && cooldownByUnit.Count == 0) return;

            HashSet<LoopUnit> present = new HashSet<LoopUnit>(units);
            List<LoopUnit> stale = null;
            foreach (var pair in cooldownByUnit)
            {
                if (present.Contains(pair.Key)) continue;
                if (stale == null) stale = new List<LoopUnit>();
                stale.Add(pair.Key);
            }
            if (stale == null) return;

            foreach (var u in stale)
            {
                cooldownByUnit.Remove(u);
                beamTimerByUnit.Remove(u);
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
