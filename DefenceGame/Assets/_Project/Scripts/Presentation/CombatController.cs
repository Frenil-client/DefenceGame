using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // 실시간 전투 - 배치된 유닛이 사거리 내 몬스터를 자동 공격한다.
    // 전투 판단(타겟팅/쿨다운)은 결정적 시뮬이 아니라 여기서 프레임 단위로 처리한다(SPEC: 시뮬은 배치/스폰/순회만).
    // 몬스터 hp/처치 상태 전이는 LoopSimulator.DamageMonster 로 위임해 aliveCount 소유를 Core 에 남긴다.
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

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            if (mapView == null) return;

            LoopSimulator sim = game.Context.sim;
            if (sim.state.defeated) return;

            float dt = Time.deltaTime * game.Speed;
            var units = sim.state.unitList;

            CleanupStale(units);

            for (int i = 0; i < units.Count; ++i)
            {
                LoopUnit u = units[i];
                if (u.data.isDoppel) continue; // 도플갱어는 변환 전 공격 불가(SPEC 2-2)

                float cd;
                cooldownByUnit.TryGetValue(u, out cd);
                if (cd > 0f) cd -= dt;

                if (cd <= 0f)
                {
                    LoopMonster target = FindTarget(sim, u);
                    if (target != null)
                    {
                        sim.DamageMonster(target, u.data.atk);
                        ShowBeam(sim, u, target);
                        cd = AttackInterval(u);
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

        // 사거리 내 가장 가까운 살아있는 몬스터(셀 거리 제곱 비교). 없으면 null.
        private LoopMonster FindTarget(LoopSimulator sim, LoopUnit u)
        {
            double ux = u.cellX;
            double uy = u.cellY;
            double range = u.data.range.ToDoubleForDisplay();
            double rangeSq = range * range;

            LoopMonster best = null;
            double bestSq = double.MaxValue;
            var list = sim.state.monsterList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopMonster m = list[i];
                if (!m.alive) continue;

                Fixed fx, fy;
                sim.GetMonsterPosition(m, out fx, out fy);
                double dx = fx.ToDoubleForDisplay() - ux;
                double dy = fy.ToDoubleForDisplay() - uy;
                double d2 = dx * dx + dy * dy;
                if (d2 > rangeSq) continue;
                if (d2 < bestSq) { bestSq = d2; best = m; }
            }
            return best;
        }

        // ---- 공격 빔(짧게 번쩍이는 선) ----

        private void ShowBeam(LoopSimulator sim, LoopUnit u, LoopMonster target)
        {
            LineRenderer lr = GetBeam(u);

            Vector3 from = mapView.CellToWorldF(u.cellX, u.cellY) + new Vector3(0f, 0.35f, 0f);
            Vector3 to;
            if (entityView == null || !entityView.TryGetMonsterWorld(target, out to))
            {
                Fixed fx, fy;
                sim.GetMonsterPosition(target, out fx, out fy);
                to = mapView.CellToWorldF((float)fx.ToDoubleForDisplay(), (float)fy.ToDoubleForDisplay()) + new Vector3(0f, 0.35f, 0f);
            }

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
