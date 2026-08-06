using System.Collections.Generic;

namespace Synthesis.Core.Map
{
    // STEP 2. 뼈대 - 그리드 맵 데이터.
    // 맵 스키마는 문서 초안(BALANCE_SPEC 10)에 없어 STEP 2 에서 최소 스키마로 새로 정의한다.
    //   grid.csv: 셀 코드 격자 (# 장애물, p 경로, m 근접칸, r 원거리칸, S 스폰, X 출구, . 빈칸)
    //   path.csv: 순서 있는 경로 웨이포인트 (x,y). 적은 이 순서대로 이동한다.
    // 구역(zone) 번호는 파견/해금이 없는 STEP 2 에선 불필요하므로 STEP 5 로 연기한다(전부 ZONE 0 취급).

    public enum CellType
    {
        Empty,
        Path,
        Melee,
        Ranged,
        Obstacle,
        Spawn,
        Exit
    }

    public struct GridPos
    {
        public int x;
        public int y;

        public GridPos(int px, int py)
        {
            x = px;
            y = py;
        }
    }

    public sealed class MapData
    {
        public int width;
        public int height;
        public CellType[] cellList;                 // index = y * width + x
        public List<GridPos> pathList = new List<GridPos>(); // 스폰에서 출구까지 순서 있는 웨이포인트

        public CellType GetCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return CellType.Empty;
            return cellList[y * width + x];
        }

        // 경로 총 길이(셀 단위). 인접 웨이포인트 사이 거리를 맨해튼으로 합산한다.
        public int GetPathLength()
        {
            int total = 0;
            for (int i = 1; i < pathList.Count; ++i)
            {
                int dx = pathList[i].x - pathList[i - 1].x;
                int dy = pathList[i].y - pathList[i - 1].y;
                if (dx < 0) dx = -dx;
                if (dy < 0) dy = -dy;
                total += dx + dy;
            }
            return total;
        }
    }
}
