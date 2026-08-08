using UnityEngine;
using Synthesis.Core.Map;

namespace Synthesis.Presentation
{
    // STEP 2/3. 최소 View - 씬/프리팹에 존재하는 맵. 타일은 런타임이 아니라 에디터에서 굽는다(bake).
    // 이 컴포넌트만으로도 독립적으로 맵을 배치/편집할 수 있다(부트스트랩과 무관).
    // 타일 굽기/지우기는 에디터 인스펙터 버튼(MapViewEditor) 또는 Synthesis 메뉴로 한다.
    public sealed class MapView : MonoBehaviour
    {
        [Tooltip("Data/maps/{mapId}_grid.csv 와 _path.csv 를 읽는다")]
        public string mapId = "map01";
        public float cellSize = 1f;

        public Vector3 CellToWorld(float x, float y)
        {
            return new Vector3(x * cellSize, 0f, -y * cellSize);
        }

        // 시뮬레이션 로직용 맵 데이터(CSV 가 원본). 시각 타일과 별개다.
        public MapData LoadMapData()
        {
            return RuntimeDataLoader.LoadMap(mapId);
        }

        public static Color CellColor(CellType cell)
        {
            switch (cell)
            {
                case CellType.Path:     return new Color(0.35f, 0.35f, 0.40f);
                case CellType.Melee:    return new Color(0.25f, 0.45f, 0.85f);
                case CellType.Ranged:   return new Color(0.25f, 0.70f, 0.45f);
                case CellType.Obstacle: return new Color(0.15f, 0.15f, 0.18f);
                case CellType.Spawn:    return new Color(0.85f, 0.35f, 0.35f);
                case CellType.Exit:     return new Color(0.85f, 0.80f, 0.30f);
                default:                return Color.gray;
            }
        }
    }
}
