using System;

namespace Synthesis.Core.Random
{
    // STEP 1. 기반 도구 - 시드 주입 xorshift128 PRNG.
    // UnityEngine.Random 과 System.Random 은 런타임마다 구현이 달라 결정성을 깬다 (ARCHITECTURE.md 4-1).
    // 난수 소비 순서가 곧 결과이므로 소비 순서를 바꾸는 리팩터링은 결과를 바꾼다.
    //
    // 비트 연산 사용 사유: xorshift 는 시프트와 XOR 자체가 알고리즘의 본질이라 회피 불가능하다
    // (CLAUDE.md 3-3 예외 조항).
    public sealed class DeterministicRandom
    {
        private uint stateX;
        private uint stateY;
        private uint stateZ;
        private uint stateW;

        public DeterministicRandom(long seed)
        {
            Reseed(seed);
        }

        public void Reseed(long seed)
        {
            // splitmix 계열로 시드 1개를 4개 상태로 확산한다. 상태가 전부 0이면 수열이 죽으므로 방지한다.
            ulong z = (ulong)seed;

            stateX = SplitMixNext(ref z);
            stateY = SplitMixNext(ref z);
            stateZ = SplitMixNext(ref z);
            stateW = SplitMixNext(ref z);

            if (stateX == 0 && stateY == 0 && stateZ == 0 && stateW == 0)
            {
                stateW = 0x9E3779B9u;
            }
        }

        private static uint SplitMixNext(ref ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            ulong r = z;
            r = (r ^ (r >> 30)) * 0xBF58476D1CE4E5B9UL;
            r = (r ^ (r >> 27)) * 0x94D049BB133111EBUL;
            r = r ^ (r >> 31);
            return (uint)(r & 0xFFFFFFFFUL);
        }

        // xorshift128. 0 이상 2^32 미만 균등.
        public uint NextUInt()
        {
            uint t = stateX ^ (stateX << 11);
            stateX = stateY;
            stateY = stateZ;
            stateZ = stateW;
            stateW = stateW ^ (stateW >> 19) ^ t ^ (t >> 8);
            return stateW;
        }

        // [0, maxExclusive) 균등. 모듈러 편향을 거절 샘플링으로 제거한다.
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));

            uint bound = (uint)maxExclusive;
            uint threshold = (uint)(-(int)bound) % bound; // 편향 구간 하한
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold)
                {
                    return (int)(r % bound);
                }
            }
        }

        // [minInclusive, maxExclusive) 균등.
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            long span = (long)maxExclusive - minInclusive;
            return minInclusive + NextInt((int)span);
        }

        // [0, 1) 고정소수점 균등. 스케일 1000 이므로 1000단계 해상도다.
        public Fixed NextUnitFixed()
        {
            int milli = NextInt((int)Fixed.Scale);
            return Fixed.FromMilli(milli);
        }
    }
}
