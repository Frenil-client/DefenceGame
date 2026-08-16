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

        [Tooltip("맵 그리드 크기. 원점 중심 배치에 쓴다. 런타임엔 로드한 맵 크기로 GameManager 가 설정한다")]
        public int gridWidth = 16;
        public int gridHeight = 12;

        // 그리드를 원점 중심으로 배치한다(MapAuthoring 과 동일 규칙). 카메라도 원점을 바라본다.
        public Vector3 CellToWorld(int x, int y)
        {
            return CellToWorldF(x, y);
        }

        // 몬스터처럼 셀 사이를 보간한 위치용(소수 좌표).
        public Vector3 CellToWorldF(float x, float y)
        {
            float ox = (gridWidth - 1) * 0.5f;
            float oy = (gridHeight - 1) * 0.5f;
            return new Vector3((x - ox) * cellSize, 0f, -(y - oy) * cellSize);
        }

        // 월드 좌표(지면)를 셀 소수 좌표로 역변환(CellToWorldF 의 역). 유닛 재배치 입력에 쓴다.
        public Vector2 WorldToCellF(Vector3 world)
        {
            float ox = (gridWidth - 1) * 0.5f;
            float oy = (gridHeight - 1) * 0.5f;
            return new Vector2(world.x / cellSize + ox, -world.z / cellSize + oy);
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
