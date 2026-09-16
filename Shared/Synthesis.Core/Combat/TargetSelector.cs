using System.Collections.Generic;

namespace Synthesis.Core.Combat
{
    // 대상 후보 하나. index 는 호출자의 몬스터 목록 인덱스다.
    public struct TargetPoint
    {
        public int index;
        public Fixed x;
        public Fixed y;
    }

    // STEP 3. 핵심 - 추가 대상과 광역 대상 선택. 전투 스크립트와 테스트가 같은 한 벌을 쓴다(CLAUDE.md 4-7).
    //   거리를 double 로 재던 것을 Fixed 로 바꿨다. 좌표가 이미 Fixed 라 변환 없이 비교하는 편이 결정적이다.
    public static class TargetSelector
    {
        // 중심에서 가까운 순으로 최대 count 개를 고른다. 같은 거리면 목록에서 먼저 나온 쪽이 이긴다.
        //   excludeIndex 는 주 대상이라 제외한다. 후보가 모자라면 있는 만큼만 넣는다.
        public static void SelectNearest(List<TargetPoint> candidateList, Fixed cx, Fixed cy,
            int count, int excludeIndex, List<int> resultList)
        {
            if (resultList == null) return;
            resultList.Clear();
            if (candidateList == null || count <= 0) return;

            for (int picked = 0; picked < count; ++picked)
            {
                int bestIndex = -1;
                Fixed bestSq = Fixed.Zero;
                bool found = false;

                for (int i = 0; i < candidateList.Count; ++i)
                {
                    TargetPoint t = candidateList[i];
                    if (t.index == excludeIndex) continue;
                    if (AlreadyPicked(resultList, t.index)) continue;

                    Fixed d2 = DistanceSq(t.x, t.y, cx, cy);
                    if (found && d2.raw >= bestSq.raw) continue;

                    bestSq = d2;
                    bestIndex = t.index;
                    found = true;
                }

                if (!found) break;
                resultList.Add(bestIndex);
            }
        }

        // 중심에서 반경 안에 있는 후보를 전부 고른다. 경계는 포함이다.
        public static void SelectWithinRadius(List<TargetPoint> candidateList, Fixed cx, Fixed cy,
            Fixed radius, int excludeIndex, List<int> resultList)
        {
            if (resultList == null) return;
            resultList.Clear();
            if (candidateList == null || radius.raw <= 0) return;

            Fixed r2 = radius * radius;
            for (int i = 0; i < candidateList.Count; ++i)
            {
                TargetPoint t = candidateList[i];
                if (t.index == excludeIndex) continue;
                if (DistanceSq(t.x, t.y, cx, cy).raw > r2.raw) continue;
                resultList.Add(t.index);
            }
        }

        public static Fixed DistanceSq(Fixed ax, Fixed ay, Fixed bx, Fixed by)
        {
            Fixed dx = ax - bx;
            Fixed dy = ay - by;
            return dx * dx + dy * dy;
        }

        private static bool AlreadyPicked(List<int> resultList, int index)
        {
            for (int i = 0; i < resultList.Count; ++i)
            {
                if (resultList[i] == index) return true;
            }
            return false;
        }
    }
}
