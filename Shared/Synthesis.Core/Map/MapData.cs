namespace Synthesis.Core.Map
{
    // STEP 1. 기반 도구 - 격자 좌표. 루프 맵과 시뮬레이션이 공유하는 최소 구조.
    // (선형 경로 맵/CellType 은 v0.3 루프 전환에서 폐기됨)
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
}
