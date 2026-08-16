using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // 유닛 재배치 입력(Unity 실시간). 유닛을 홀드 후 원하는 위치에서 클릭업하면 그 자리의 가장 가까운 빈 배치칸으로 이동한다.
    // 칸 재할당(점유/빈칸 탐색)은 Core LoopSimulator.RelocateUnit 이 담당하고, 걷는 연출은 EntityView 보간이 처리한다.
    // 유닛끼리 충돌은 없다. 드래그 중 선택 칸을 지면에 눕힌 2D 쿼드로 표시한다.
    public sealed class UnitMoveController : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private LoopMapView mapView;
        [SerializeField] private Camera cam;
        [SerializeField] private GameObject tileIndicator;  // 드래그 중 선택 칸 2D 표시(선택)
        [SerializeField] private float pickRadius = 0.75f;   // 유닛 집기 허용 반경(셀)
        [SerializeField] private float tileIndicatorY = 0.11f;

        private LoopUnit held;

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            if (mapView == null || cam == null) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            LoopSimulator sim = game.Context.sim;

            if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI())
            {
                Vector2 cell;
                if (TryGetGroundCell(mouse.position.ReadValue(), out cell))
                {
                    held = PickUnit(sim, cell);
                }
            }

            if (held == null) return;

            // 커서의 실제 지면 위치(소수 셀 좌표)를 각 유효 타일의 중심과 직접 비교해 가장 가까운 배치칸을 포커싱한다.
            // 경로 타일, 이미 유닛이 있는 칸, 타일이 아닌 공간은 포커싱하지 않고 가장 가까운 배치칸으로 스냅한다.
            Vector2 hover;
            bool onGround = TryGetGroundCell(mouse.position.ReadValue(), out hover);
            int focusx = 0, focusy = 0;
            bool hasFocus = onGround && FindNearestValidCell(sim, hover, out focusx, out focusy);
            UpdateIndicator(hasFocus, focusx, focusy);

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (hasFocus) sim.RelocateUnit(held, focusx, focusy);
                held = null;
                HideIndicator();
            }
        }

        // 커서 소수 셀 좌표에서 각 배치 가능 타일의 중심까지 거리를 비교해 가장 가까운 칸을 찾는다(타일 중심 기준).
        private bool FindNearestValidCell(LoopSimulator sim, Vector2 cursorCell, out int resultx, out int resulty)
        {
            resultx = 0;
            resulty = 0;
            var tiles = game.Context.map.buildTileList;
            float bestSq = float.MaxValue;
            int bestIndex = -1;
            for (int i = 0; i < tiles.Count; ++i)
            {
                var c = tiles[i];
                if (!sim.CanPlaceUnitAt(held, c.x, c.y)) continue;
                float dx = c.x - cursorCell.x;
                float dy = c.y - cursorCell.y;
                float d2 = dx * dx + dy * dy;
                if (d2 < bestSq) { bestSq = d2; bestIndex = i; }
            }
            if (bestIndex < 0) return false;

            resultx = tiles[bestIndex].x;
            resulty = tiles[bestIndex].y;
            return true;
        }

        // 클릭 셀에서 pickRadius 안의 가장 가까운 유닛을 집는다(유닛에 콜라이더가 없어 셀 거리로 판정).
        private LoopUnit PickUnit(LoopSimulator sim, Vector2 cell)
        {
            LoopUnit best = null;
            float bestSq = pickRadius * pickRadius;
            var list = sim.state.unitList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopUnit u = list[i];
                float dx = u.cellX - cell.x;
                float dy = u.cellY - cell.y;
                float d2 = dx * dx + dy * dy;
                if (d2 <= bestSq) { bestSq = d2; best = u; }
            }
            return best;
        }

        // 화면 좌표에서 지면(y=0) 교점을 구해 셀 소수 좌표로 변환. 카메라와 지면이 평행이거나 뒤면 false.
        private bool TryGetGroundCell(Vector2 screen, out Vector2 cell)
        {
            cell = Vector2.zero;
            Ray ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return false;
            Vector3 world = ray.origin + ray.direction * t;
            cell = mapView.WorldToCellF(world);
            return true;
        }

        // 포커싱된 유효 칸에만 인디케이터를 표시한다. 유효 칸이 없으면 숨긴다.
        private void UpdateIndicator(bool hasFocus, int cellX, int cellY)
        {
            if (tileIndicator == null) return;
            if (!hasFocus) { tileIndicator.SetActive(false); return; }

            Vector3 pos = mapView.CellToWorldF(cellX, cellY);
            pos.y = tileIndicatorY;
            tileIndicator.transform.position = pos;
            tileIndicator.SetActive(true);
        }

        private void HideIndicator()
        {
            if (tileIndicator != null) tileIndicator.SetActive(false);
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
