using Synthesis.Core.Random;

namespace Synthesis.Core.Tests
{
    // STEP 1. 검증 - 결정성 테스트 (ARCHITECTURE.md 4-5).
    // 같은 시드로 두 번 돌린 PRNG 수열이 완전히 일치해야 한다. 이것이 시뮬 재현성의 토대다.
    public class DeterminismTests
    {
        [Fact]
        public void SameSeed_ProducesIdenticalSequence()
        {
            var a = new DeterministicRandom(12345);
            var b = new DeterministicRandom(12345);

            for (int i = 0; i < 1000; ++i)
            {
                Assert.Equal(a.NextUInt(), b.NextUInt());
            }
        }

        [Fact]
        public void Reseed_ResetsSequence()
        {
            var rng = new DeterministicRandom(999);
            uint[] first = new uint[64];
            for (int i = 0; i < first.Length; ++i)
            {
                first[i] = rng.NextUInt();
            }

            rng.Reseed(999);
            for (int i = 0; i < first.Length; ++i)
            {
                Assert.Equal(first[i], rng.NextUInt());
            }
        }

        [Fact]
        public void DifferentSeed_DivergesQuickly()
        {
            var a = new DeterministicRandom(1);
            var b = new DeterministicRandom(2);

            bool diverged = false;
            for (int i = 0; i < 8; ++i)
            {
                if (a.NextUInt() != b.NextUInt())
                {
                    diverged = true;
                    break;
                }
            }
            Assert.True(diverged);
        }

        [Fact]
        public void NextInt_StaysInRange()
        {
            var rng = new DeterministicRandom(77);
            for (int i = 0; i < 5000; ++i)
            {
                int value = rng.NextInt(10);
                Assert.InRange(value, 0, 9);
            }
        }

        [Fact]
        public void NextInt_MinMax_StaysInRange()
        {
            var rng = new DeterministicRandom(77);
            for (int i = 0; i < 5000; ++i)
            {
                int value = rng.NextInt(5, 8);
                Assert.InRange(value, 5, 7);
            }
        }
    }
}
