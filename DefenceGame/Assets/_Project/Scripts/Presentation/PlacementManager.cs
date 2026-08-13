using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Units;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 매니저 - 플레이어 입력 배치. 좌클릭: 빈 BUILD 타일에 배치, 우클릭: 회수.
    // 어느 유닛을 놓을지는 지금은 "감당 가능한 인벤토리의 첫 유닛"(임시). 유닛 선택 UI 는 이후.
    // 신 Input System(Mouse) 을 읽는다. 실시간 조작이 없으므로 클릭 이벤트만 쓴다.
    public sealed class PlacementManager : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private Camera cam;

        private void Awake()
        {
            if (game == null) game = Object.FindFirstObjectByType<GameManager>();
        }

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            if (game.Context.sim.state.defeated) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            bool left = mouse.leftButton.wasPressedThisFrame;
            bool right = mouse.rightButton.wasPressedThisFrame;
            if (!left && !right) return;

            // UI 위 클릭은 무시
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 screen = mouse.position.ReadValue();
            int gx, gy;
            if (!ScreenToCell(screen, out gx, out gy)) return;

            if (left) TryPlace(gx, gy);
            else if (right) game.Context.sim.RecallUnit(gx, gy);
        }

        private void TryPlace(int gx, int gy)
        {
            RunContext ctx = game.Context;
            string wanted = game.SelectedUnitId;

            for (int i = 0; i < ctx.inventory.ownedList.Count; ++i)
            {
                OwnedUnit owned = ctx.inventory.ownedList[i];
                // 선택된 유닛이 있으면 그 종류만, 없으면 감당 가능한 첫 유닛
                if (!string.IsNullOrEmpty(wanted) && owned.unitId != wanted) continue;

                UnitData data;
                if (!ctx.unitById.TryGetValue(owned.unitId, out data)) continue;
                if (ctx.sim.state.cost < Fixed.FromInt(data.cost)) continue;

                if (ctx.sim.PlaceUnit(data, gx, gy))
                {
                    ctx.inventory.RemoveByInstance(owned.instanceId);
                    return;
                }
                // 칸이 불가/점유면 유닛을 바꿔도 동일하니 중단
                return;
            }
        }

        // 카메라 광선을 y=0 평면과 교차시켜 그리드 셀로 변환.
        private bool ScreenToCell(Vector2 screen, out int gx, out int gy)
        {
            gx = 0; gy = 0;
            if (cam == null) cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) return false;

            float cellSize = game.MapView != null ? game.MapView.cellSize : 1f;
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            Ray ray = cam.ScreenPointToRay(screen);
            float dist;
            if (!ground.Raycast(ray, out dist)) return false;

            Vector3 wp = ray.GetPoint(dist);
            gx = Mathf.RoundToInt(wp.x / cellSize);
            gy = Mathf.RoundToInt(-wp.z / cellSize);
            return true;
        }
    }
}
