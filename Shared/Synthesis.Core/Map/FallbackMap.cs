using System.Collections.Generic;

namespace Synthesis.Core.Map
{
    // STEP 1. 기반 도구 - 생성 재시도가 모두 실패하면 반환할 고정 템플릿 (MAP_SPEC.md 3 STEP 5).
    // 16x12, inset 2 기본 링에 안쪽 요철 3개(위/오른쪽/아래)를 넣어 제약을 통과하도록 손으로 구성했다.
    public static class FallbackMap
    {
        public static LoopMap Create(MapGenParams p, long seed)
        {
            List<GridPos> verts = new List<GridPos>
            {
                new GridPos(2, 2),
                new GridPos(5, 2), new GridPos(5, 4), new GridPos(8, 4), new GridPos(8, 2),
                new GridPos(13, 2),
                new GridPos(13, 4), new GridPos(11, 4), new GridPos(11, 7), new GridPos(13, 7),
                new GridPos(13, 9),
                new GridPos(8, 9), new GridPos(8, 7), new GridPos(5, 7), new GridPos(5, 9),
                new GridPos(2, 9)
            };
            return LoopMapGenerator.BuildFromVertices(verts, p, seed, true);
        }
    }
}
