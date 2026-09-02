using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Synthesis.Core;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // 유닛 명령 입력(Unity 실시간). 유닛을 홀드 후 클릭업하는데,
    //   - 포인터가 몬스터/석상 위면 그 대상을 추적(집중 명령): 대상에게 이동하며 사거리에 들면 공격한다(SPEC 3-4).
    //   - 빈 곳이면 가장 가까운 배치칸으로 재배치(집중 해제).
    // 칸 재할당은 Core LoopSimulator.RelocateUnit, 걷기/추격 연출은 EntityView 보간, 공격은 CombatController 가 처리한다.
    public sealed class UnitMoveController : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private LoopMapView mapView;
        [SerializeField] private Camera cam;
        [SerializeField] private EntityView entityView;     // 유닛 현재 위치 출처(집중 중 유닛도 집을 수 있게)
        [SerializeField] private SelectionController selection; // 클릭 선택 결과를 여기에 넣는다. 인스펙터 등록
        [SerializeField] private GameObject tileIndicator;  // 드래그 중 대상/선택 칸 2D 표시(선택)
        [SerializeField] private float pickRadius = 0.75f;   // 유닛 집기 허용 반경(셀)
        [SerializeField] private float objectRadius = 0.6f;  // 몬스터/석상 추적 대상 판정 반경(셀)
        [SerializeField] private float tileIndicatorY = 0.11f;
        // 클릭과 드래그를 가르는 화면 이동량(픽셀). 이 안에서 떼면 명령이 아니라 선택으로 본다.
        [SerializeField] private float dragThresholdPixels = 8f;

        private LoopUnit held;
        private Vector2 pressScreen;
        private bool pressing;   // 지면에서 누르기 시작했는가(UI 위 클릭은 제외)
        private bool dragging;   // 임계값을 넘겨 드래그로 확정됐는가

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            if (mapView == null || cam == null) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            LoopSimulator sim = game.Context.sim;

            if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI())
            {
                pressScreen = mouse.position.ReadValue();
                pressing = true;
                dragging = false;

                Vector2 cell;
                held = TryGetGroundCell(pressScreen, out cell) ? PickUnit(sim, cell) : null;
            }

            if (!pressing) return;

            // 누른 자리에서 임계값 이상 움직였으면 드래그(명령)로 확정한다. 그 전까지는 클릭(선택) 후보다.
            if (!dragging && (mouse.position.ReadValue() - pressScreen).magnitude > dragThresholdPixels) dragging = true;

            if (mouse.leftButton.wasReleasedThisFrame && !dragging)
            {
                SelectAtCursor(sim, mouse.position.ReadValue());
                pressing = false;
                held = null;
                HideIndicator();
                return;
            }

            if (held == null)
            {
                if (mouse.leftButton.wasReleasedThisFrame) pressing = false;
                return;
            }

            // 드래그로 확정되기 전에는 인디케이터를 띄우지 않는다(아직 클릭 후보 단계다).
            if (!dragging) return;

            Vector2 hover;
            bool onGround = TryGetGroundCell(mouse.position.ReadValue(), out hover);

            // 우선순위: 커서 아래 몬스터 > 석상 > 배치 칸. 오브젝트 위면 그 대상을 추적(집중), 아니면 재배치.
            LoopMonster mUnder = onGround ? FindMonsterUnder(sim, hover) : null;
            LoopStatue sUnder = (mUnder == null && onGround) ? FindStatueUnder(sim, hover) : null;

            int focusx = 0, focusy = 0;
            bool hasCell = false;
            if (mUnder != null) ShowIndicatorAt(MonsterCell(sim, mUnder));
            else if (sUnder != null) ShowIndicatorAt(new Vector2(sUnder.cellX, sUnder.cellY));
            else
            {
                hasCell = onGround && FindNearestValidCell(sim, hover, out focusx, out focusy);
                UpdateIndicator(hasCell, focusx, focusy);
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (mUnder != null) { held.focusMonster = mUnder; held.focusStatue = null; }
                else if (sUnder != null) { held.focusStatue = sUnder; held.focusMonster = null; }
                else if (hasCell) { held.focusMonster = null; held.focusStatue = null; sim.RelocateUnit(held, focusx, focusy); }

                // 명령을 내린 유닛은 그대로 선택 상태로 둔다. 옮기고 나서 사거리를 바로 확인할 수 있다.
                if (selection != null) selection.SelectUnit(held);

                held = null;
                pressing = false;
                HideIndicator();
            }
        }

        // 클릭(드래그 아님) 처리. 커서 아래 유닛을 우선하고, 없으면 몬스터를, 둘 다 없으면 선택을 푼다.
        private void SelectAtCursor(LoopSimulator sim, Vector2 screen)
        {
            if (selection == null) return;

            Vector2 cell;
            if (!TryGetGroundCell(screen, out cell))
            {
                selection.Clear();
                return;
            }

            LoopUnit unit = PickUnit(sim, cell);
            if (unit != null)
            {
                selection.SelectUnit(unit);
                return;
            }

            LoopMonster monster = FindMonsterUnder(sim, cell);
            if (monster != null)
            {
                selection.SelectMonster(monster);
                return;
            }

            selection.Clear();
        }

        // 커서 아래(objectRadius 안) 가장 가까운 살아있는 몬스터. 없으면 null.
        private LoopMonster FindMonsterUnder(LoopSimulator sim, Vector2 cursor)
        {
            LoopMonster best = null;
            float bestSq = objectRadius * objectRadius;
            var list = sim.state.monsterList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopMonster m = list[i];
                if (!m.alive) continue;
                Fixed fx, fy;
                sim.GetMonsterPosition(m, out fx, out fy);
                float dx = (float)fx.ToDoubleForDisplay() - cursor.x;
                float dy = (float)fy.ToDoubleForDisplay() - cursor.y;
                float d2 = dx * dx + dy * dy;
                if (d2 <= bestSq) { bestSq = d2; best = m; }
            }
            return best;
        }

        // 커서 아래(objectRadius 안) 가장 가까운 살아있는 석상. 없으면 null.
        private LoopStatue FindStatueUnder(LoopSimulator sim, Vector2 cursor)
        {
            LoopStatue best = null;
            float bestSq = objectRadius * objectRadius;
            var list = sim.state.statueList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopStatue s = list[i];
                if (!s.alive) continue;
                float dx = s.cellX - cursor.x;
                float dy = s.cellY - cursor.y;
                float d2 = dx * dx + dy * dy;
                if (d2 <= bestSq) { bestSq = d2; best = s; }
            }
            return best;
        }

        private Vector2 MonsterCell(LoopSimulator sim, LoopMonster m)
        {
            Fixed fx, fy;
            sim.GetMonsterPosition(m, out fx, out fy);
            return new Vector2((float)fx.ToDoubleForDisplay(), (float)fy.ToDoubleForDisplay());
        }

        // 인디케이터를 소수 셀 좌표 위치에 표시한다(추격 대상 강조용).
        private void ShowIndicatorAt(Vector2 cellF)
        {
            if (tileIndicator == null) return;
            Vector3 pos = mapView.CellToWorldF(cellF.x, cellF.y);
            pos.y = tileIndicatorY;
            tileIndicator.transform.position = pos;
            tileIndicator.SetActive(true);
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
        // 유닛의 현재 렌더 위치 기준이라 집중 추격 중인 유닛도 그 자리에서 집을 수 있다.
        private LoopUnit PickUnit(LoopSimulator sim, Vector2 cell)
        {
            LoopUnit best = null;
            float bestSq = pickRadius * pickRadius;
            var list = sim.state.unitList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopUnit u = list[i];
                Vector2 uc = UnitCurrentCell(u);
                float dx = uc.x - cell.x;
                float dy = uc.y - cell.y;
                float d2 = dx * dx + dy * dy;
                if (d2 <= bestSq) { bestSq = d2; best = u; }
            }
            return best;
        }

        // 유닛의 현재 렌더 위치의 셀 좌표. 뷰가 없으면 홈 셀.
        private Vector2 UnitCurrentCell(LoopUnit u)
        {
            Vector3 w;
            if (entityView != null && entityView.TryGetUnitWorld(u, out w)) return mapView.WorldToCellF(w);
            return new Vector2(u.cellX, u.cellY);
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
