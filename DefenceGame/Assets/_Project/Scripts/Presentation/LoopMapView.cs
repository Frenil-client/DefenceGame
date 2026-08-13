using UnityEngine;
using Synthesis.Core.Map;

namespace Synthesis.Presentation
{
    // STEP 1/2. 최소 View - 루프 맵 미리보기 컴포넌트. 타일은 에디터에서 "시드로 생성"으로 굽는다.
    // 부트스트랩과 무관하게 단독으로 맵 형태를 확인/편집할 수 있다.
    public sealed class LoopMapView : MonoBehaviour
    {
        [Tooltip("이 시드로 mapgen.csv 파라미터를 써서 루프 맵을 생성한다")]
        public long seed = 1;
        [Tooltip("켜면 요철 없는 기본 직사각형 맵을 쓴다(미리보기와 런타임 동일). 끄면 시드 변주 맵")]
        public bool useDefaultMap = false;
        public float cellSize = 1f;

        public Vector3 CellToWorld(int x, int y)
        {
            return new Vector3(x * cellSize, 0f, -y * cellSize);
        }

        // 몬스터처럼 셀 사이를 보간한 위치용(소수 좌표).
        public Vector3 CellToWorldF(float x, float y)
        {
            return new Vector3(x * cellSize, 0f, -y * cellSize);
        }

        public static Color TileColor(LoopTile tile)
        {
            switch (tile)
            {
                case LoopTile.Path:  return new Color(0.35f, 0.35f, 0.40f);
                case LoopTile.Build: return new Color(0.22f, 0.42f, 0.80f);
                default:             return new Color(0.12f, 0.12f, 0.14f);
            }
        }

        public static Color SpawnColor()
        {
            return new Color(0.85f, 0.30f, 0.30f);
        }
    }
}
