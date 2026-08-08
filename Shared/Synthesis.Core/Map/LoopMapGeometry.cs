using System.Collections.Generic;

namespace Synthesis.Core.Map
{
    // STEP 1. 기반 도구 - 루프 폴리곤 기하 헬퍼. 생성기와 검증기가 공유한다(두 벌 금지, MAP_SPEC 9).
    public static class LoopMapGeometry
    {
        public static int Sign(int v)
        {
            if (v > 0) return 1;
            if (v < 0) return -1;
            return 0;
        }

        // 축 정렬 꼭짓점(닫힌) 목록을 순서 있는 셀 목록으로 래스터화한다.
        // cornerIndexList 에는 각 꼭짓점에 해당하는 셀 인덱스를 채운다.
        public static List<GridPos> Rasterize(List<GridPos> verts, List<int> cornerIndexOut)
        {
            List<GridPos> cells = new List<GridPos>();
            int n = verts.Count;
            for (int i = 0; i < n; ++i)
            {
                GridPos a = verts[i];
                GridPos b = verts[(i + 1) % n];
                int dx = Sign(b.x - a.x);
                int dy = Sign(b.y - a.y);

                if (cornerIndexOut != null) cornerIndexOut.Add(cells.Count);

                int cx = a.x;
                int cy = a.y;
                while (cx != b.x || cy != b.y)
                {
                    cells.Add(new GridPos(cx, cy));
                    cx += dx;
                    cy += dy;
                }
            }
            return cells;
        }

        // 셀 목록이 격자 안에 있고, 반복 셀이 없으며(단순), 루프에서 멀리 떨어진 셀끼리
        // 최소 간격을 지키는지 검사한다. minGap 미만으로 붙으면 false.
        public static bool IsCleanLoop(List<GridPos> cells, int gridWidth, int gridHeight, int minGap)
        {
            int n = cells.Count;
            if (n < 8) return false;

            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < n; ++i)
            {
                GridPos c = cells[i];
                if (c.x < 0 || c.y < 0 || c.x >= gridWidth || c.y >= gridHeight) return false;
                int key = c.y * gridWidth + c.x;
                if (!seen.Add(key)) return false; // 반복 = 자기교차
            }

            // 루프 순서상 3칸 이상 떨어진 셀끼리는 체비셰프 거리 minGap 이상이어야 한다.
            for (int i = 0; i < n; ++i)
            {
                for (int j = i + 1; j < n; ++j)
                {
                    int loopDist = j - i;
                    if (loopDist > n - loopDist) loopDist = n - loopDist;
                    if (loopDist < 3) continue;

                    int dx = cells[i].x - cells[j].x; if (dx < 0) dx = -dx;
                    int dy = cells[i].y - cells[j].y; if (dy < 0) dy = -dy;
                    int cheb = dx > dy ? dx : dy;
                    if (cheb < minGap) return false;
                }
            }
            return true;
        }

        // 루프 바깥에서 flood 하여 내부(배치 가능) 셀을 찾는다.
        public static List<GridPos> FindInterior(List<GridPos> pathCells, int gridWidth, int gridHeight)
        {
            bool[] path = new bool[gridWidth * gridHeight];
            foreach (var c in pathCells)
            {
                path[c.y * gridWidth + c.x] = true;
            }

            bool[] outside = new bool[gridWidth * gridHeight];
            Queue<int> queue = new Queue<int>();
            for (int x = 0; x < gridWidth; ++x)
            {
                EnqueueIfOpen(0, x, path, outside, queue, gridWidth);
                EnqueueIfOpen((gridHeight - 1) * gridWidth + x, x, path, outside, queue, gridWidth);
            }
            for (int y = 0; y < gridHeight; ++y)
            {
                EnqueueIfOpen(y * gridWidth, 0, path, outside, queue, gridWidth);
                EnqueueIfOpen(y * gridWidth + (gridWidth - 1), gridWidth - 1, path, outside, queue, gridWidth);
            }

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                int x = idx % gridWidth;
                int y = idx / gridWidth;
                TryFlood(x + 1, y, path, outside, queue, gridWidth, gridHeight);
                TryFlood(x - 1, y, path, outside, queue, gridWidth, gridHeight);
                TryFlood(x, y + 1, path, outside, queue, gridWidth, gridHeight);
                TryFlood(x, y - 1, path, outside, queue, gridWidth, gridHeight);
            }

            List<GridPos> interior = new List<GridPos>();
            for (int y = 0; y < gridHeight; ++y)
            {
                for (int x = 0; x < gridWidth; ++x)
                {
                    int idx = y * gridWidth + x;
                    if (!path[idx] && !outside[idx]) interior.Add(new GridPos(x, y));
                }
            }
            return interior;
        }

        private static void EnqueueIfOpen(int idx, int x, bool[] path, bool[] outside, Queue<int> queue, int gridWidth)
        {
            if (path[idx] || outside[idx]) return;
            outside[idx] = true;
            queue.Enqueue(idx);
        }

        private static void TryFlood(int x, int y, bool[] path, bool[] outside, Queue<int> queue, int gridWidth, int gridHeight)
        {
            if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight) return;
            int idx = y * gridWidth + x;
            if (path[idx] || outside[idx]) return;
            outside[idx] = true;
            queue.Enqueue(idx);
        }
    }
}
