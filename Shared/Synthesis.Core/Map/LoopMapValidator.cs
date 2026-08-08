using System.Collections.Generic;

namespace Synthesis.Core.Map
{
    // STEP 1. 기반 도구 - 루프 맵 제약 검증 MAP-01 부터 MAP-07 (MAP_SPEC.md 4).
    // 생성기와 린터가 이 한 벌을 공유한다.
    public static class LoopMapValidator
    {
        public static bool Validate(LoopMap map, MapGenParams p, List<string> failuresOut)
        {
            bool ok = true;

            // MAP-01 둘레
            if (map.perimeter < p.perimeterMin || map.perimeter > p.perimeterMax)
            {
                ok = false;
                Add(failuresOut, "MAP-01 둘레 " + map.perimeter + " (범위 " + p.perimeterMin + "-" + p.perimeterMax + ")");
            }

            // MAP-02 코너 수
            int corners = map.cornerIndexList.Count;
            if (corners < p.cornerMin || corners > p.cornerMax)
            {
                ok = false;
                Add(failuresOut, "MAP-02 코너 " + corners + " (범위 " + p.cornerMin + "-" + p.cornerMax + ")");
            }

            // MAP-03 내부 면적 (형태 지표. 배치 가능 타일은 경로 외 전체이나, 면적 지표는 내부로 본다)
            if (map.interiorArea < p.areaMin || map.interiorArea > p.areaMax)
            {
                ok = false;
                Add(failuresOut, "MAP-03 내부면적 " + map.interiorArea + " (범위 " + p.areaMin + "-" + p.areaMax + ")");
            }

            // MAP-07 자기교차 없음 + MAP-04 최소 간격 (IsCleanLoop 이 둘 다 본다)
            if (!LoopMapGeometry.IsCleanLoop(map.loopWaypointList, map.gridWidth, map.gridHeight, p.minLaneGap))
            {
                ok = false;
                Add(failuresOut, "MAP-04/07 자기교차 또는 최소 간격 위반");
            }

            // MAP-05 커버 효율 지수 (min,max 가 0,0 이면 검사 생략)
            if (!(p.coverageMin == 0 && p.coverageMax == 0))
            {
                long coverInt = map.coverageIndex.ToIntRounded();
                if (coverInt < p.coverageMin || coverInt > p.coverageMax)
                {
                    ok = false;
                    Add(failuresOut, "MAP-05 커버지수 " + map.coverageIndex + " (범위 " + p.coverageMin + "-" + p.coverageMax + ")");
                }
            }

            // MAP-06 커버 0인 배치 타일 비율 5% 미만
            int zeroCover;
            CoverageIndex.Compute(map, p.coverageRadius, out zeroCover);
            if (map.interiorArea > 0)
            {
                double ratio = (double)zeroCover / map.interiorArea;
                if (ratio >= 0.05)
                {
                    ok = false;
                    Add(failuresOut, "MAP-06 커버0 타일 비율 " + (ratio * 100).ToString("0.0") + "% (5% 미만 필요)");
                }
            }

            return ok;
        }

        private static void Add(List<string> list, string message)
        {
            if (list != null) list.Add(message);
        }
    }
}
