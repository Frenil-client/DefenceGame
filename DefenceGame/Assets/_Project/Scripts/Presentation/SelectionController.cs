using UnityEngine;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // STEP 3. 핵심 - 유닛/몬스터 선택 상태. 입력(UnitMoveController)이 갱신하고 HUD 와 뷰가 읽는다.
    // 선택은 순수 표현 계층 상태다. Core 는 선택을 모르고 전투 판정에도 영향을 주지 않는다.
    // 한 번에 하나만 선택된다. 유닛을 고르면 몬스터 선택이 풀리고 반대도 같다.
    public sealed class SelectionController : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private EntityView entityView;   // 사거리 링을 켤 UnitView 를 찾는다. 인스펙터 등록
        [SerializeField] private CombatController combat; // 실제 전투가 쓰는 사거리를 그대로 그린다. 인스펙터 등록

        public LoopUnit SelectedUnit { get; private set; }
        public LoopMonster SelectedMonster { get; private set; }

        private LoopUnit ringUnit; // 사거리 링을 켜 둔 유닛. 선택이 바뀌면 이쪽을 먼저 끈다
        private int lastRunId = -1;

        public void SelectUnit(LoopUnit u)
        {
            if (u == null) { Clear(); return; }
            SelectedMonster = null;
            SelectedUnit = u;
        }

        public void SelectMonster(LoopMonster m)
        {
            if (m == null || !m.alive) { Clear(); return; }
            SelectedUnit = null;
            SelectedMonster = m;
        }

        public void Clear()
        {
            SelectedUnit = null;
            SelectedMonster = null;
        }

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid())
            {
                Clear();
                HideRing();
                return;
            }

            // 재시작하면 이전 런의 선택은 무효다.
            if (lastRunId != game.RunId)
            {
                lastRunId = game.RunId;
                Clear();
                HideRing();
            }

            DropDeadSelection();
            UpdateRing();
        }

        // 선택 대상이 사라졌으면(몬스터 사망, 유닛 회수) 선택을 푼다.
        private void DropDeadSelection()
        {
            if (SelectedMonster != null && !SelectedMonster.alive) SelectedMonster = null;
            if (SelectedUnit == null) return;

            var list = game.Context.sim.state.unitList;
            for (int i = 0; i < list.Count; ++i)
            {
                if (list[i] == SelectedUnit)
                {
                    return;
                }
            }
            SelectedUnit = null;
        }

        // 선택한 유닛의 일반 공격 사거리 링만 켠다. 오라 반경은 그리지 않는다.
        // 사거리는 아군 버프로 매 프레임 달라질 수 있으므로 계속 갱신한다.
        private void UpdateRing()
        {
            if (ringUnit != null && ringUnit != SelectedUnit) HideRing();
            if (SelectedUnit == null) return;

            UnitView view;
            if (entityView == null || !entityView.TryGetUnitView(SelectedUnit, out view))
            {
                ringUnit = null;
                return;
            }

            float radius = combat != null
                ? combat.GetEffectiveRange(SelectedUnit)
                : (float)SelectedUnit.data.range.ToDoubleForDisplay();

            view.ShowRange(radius);
            ringUnit = SelectedUnit;
        }

        private void HideRing()
        {
            if (ringUnit == null) return;

            UnitView view;
            if (entityView != null && entityView.TryGetUnitView(ringUnit, out view)) view.HideRange();
            ringUnit = null;
        }
    }
}
