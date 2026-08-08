using System.Collections.Generic;

namespace Synthesis.Core.Map
{
    // STEP 1. 기반 도구 - 커버 효율 지수 (MAP_SPEC.md 5).
    // 각 배치 타일에서 반경 R 안에 들어오는 루프 타일 수의 평균.
    public static class CoverageIndex
    {
        // 커버 0인 배치 타일 수를 out 으로 돌려주고, 평균 커버를 Fixed 로 반환한다.
        public static Fixed Compute(LoopMap map, int radius, out int zeroCoverBuildCount)
        {
            zeroCoverBuildCount = 0;
            // 커버 지수는 루프 내부 타일 기준으로 통일한다(맵끼리 비교 가능한 형태 지표).
            if (map.interiorTileList.Count == 0)
            {
                return Fixed.Zero;
            }

            int radiusSq = radius * radius;
            long totalCover = 0;
            for (int i = 0; i < map.interiorTileList.Count; ++i)
            {
                GridPos b = map.interiorTileList[i];
                int cover = 0;
                for (int j = 0; j < map.loopWaypointList.Count; ++j)
                {
                    GridPos w = map.loopWaypointList[j];
                    int dx = w.x - b.x;
                    int dy = w.y - b.y;
                    if (dx * dx + dy * dy <= radiusSq) ++cover;
                }
                if (cover == 0) ++zeroCoverBuildCount;
                totalCover += cover;
            }

            return Fixed.FromRatio(totalCover, map.interiorTileList.Count);
        }
    }
}
