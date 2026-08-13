using System.Collections.Generic;
using UnityEngine;

namespace Synthesis.Presentation
{
    // 맵 저작용 데이터 컨테이너. Scene 뷰에서 타일을 클릭해 경로와 스폰을 지정한다(에디터: MapAuthoringEditor).
    // 여기에 담긴 경로/스폰을 MapSO 로 저장하면 런타임이 그 맵을 로드한다.
    // 좌표계는 LoopMapView 와 동일: cell(x,y) -> (x*cellSize, 0, -y*cellSize). 오브젝트는 원점에 두는 것을 전제한다.
    public sealed class MapAuthoring : MonoBehaviour
    {
        public int gridWidth = 16;
        public int gridHeight = 12;
        public float cellSize = 1f;

        [Tooltip("순회 순서대로의 경로 셀. 마지막 다음은 첫 셀로 이어져 루프가 된다")]
        public List<Vector2Int> path = new List<Vector2Int>();

        [Tooltip("스폰 지점(path 인덱스)")]
        public List<int> spawnIndices = new List<int>();

#if UNITY_EDITOR
        // 컴포넌트를 붙일 때 그리드 크기를 mapgen.csv 기준으로 맞춘다.
        private void Reset()
        {
            var p = RuntimeDataLoader.LoadMapGenParams();
            if (p != null)
            {
                gridWidth = p.gridWidth;
                gridHeight = p.gridHeight;
            }
        }
#endif

        public Vector3 CellToWorld(int x, int y)
        {
            return new Vector3(x * cellSize, 0f, -y * cellSize);
        }

        public int IndexOfCell(Vector2Int cell)
        {
            for (int i = 0; i < path.Count; ++i)
            {
                if (path[i] == cell) return i;
            }
            return -1;
        }

        public bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < gridWidth && cell.y < gridHeight;
        }
    }
}
