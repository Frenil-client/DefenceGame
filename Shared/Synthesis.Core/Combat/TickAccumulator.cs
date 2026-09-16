namespace Synthesis.Core.Combat
{
    // STEP 3. 기반 도구 - 프레임 시간을 Fixed 로 바꾸면서 잔여분을 다음 프레임으로 넘긴다.
    //   Fixed 의 최소 단위는 0.001 초라 프레임 dt 를 그때그때 반올림하면 남거나 모자란 만큼이 그냥 사라진다.
    //   그 결과 같은 1초라도 30/60/120 FPS 에서 장판 피해 총량이 달라졌다(점검 보고서 4-B).
    //   여기서는 마이크로초로 받아 밀리초만 소비하고 나머지를 들고 있으므로 총량이 프레임률과 무관해진다.
    public struct TickAccumulator
    {
        public const long MicrosPerMilli = 1000;

        private long pendingMicros; // 아직 1밀리초를 못 채운 잔여 시간

        // 이번 프레임 경과분을 넣고, 소비 가능한 밀리초만 Fixed 로 돌려준다. 남는 마이크로초는 보존한다.
        public Fixed Consume(long deltaMicros)
        {
            if (deltaMicros > 0) pendingMicros += deltaMicros;
            if (pendingMicros < MicrosPerMilli) return Fixed.Zero;

            long milli = pendingMicros / MicrosPerMilli;
            pendingMicros -= milli * MicrosPerMilli;
            return Fixed.FromMilli(milli);
        }

        // 런 재시작 등으로 이전 누산을 버릴 때 호출한다.
        public void Reset()
        {
            pendingMicros = 0;
        }
    }
}
