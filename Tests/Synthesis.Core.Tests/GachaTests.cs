using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Combination;

namespace Synthesis.Core.Tests
{
    // STEP 3(v0.4). 검증 - 균등 1/6 뽑기. T1 6종만 지급되고 결정적이어야 한다.
    public class GachaTests
    {
        private static List<UnitData> units;

        private static void EnsureLoaded()
        {
            if (units == null) units = CsvParsers.LoadUnits(TestPaths.ReadData("units.csv"));
        }

        private static List<string> RunGacha(long seed, int waveCount)
        {
            EnsureLoaded();
            var gacha = new GachaEngine(units, seed);
            List<string> result = new List<string>();
            for (int w = 1; w <= waveCount; ++w) result.Add(gacha.GrantForWave(w));
            return result;
        }

        [Fact]
        public void GrantsOnlyTier1()
        {
            EnsureLoaded();
            HashSet<string> tier1 = new HashSet<string>();
            foreach (var unit in units)
            {
                if (unit.tier == 1) tier1.Add(unit.id);
            }

            var h = RunGacha(7, 200);
            foreach (var id in h) Assert.Contains(id, tier1);
        }

        [Fact]
        public void AllSixKlassesReachable()
        {
            var h = RunGacha(42, 400);
            HashSet<string> distinct = new HashSet<string>(h);
            Assert.Equal(6, distinct.Count);
        }

        [Fact]
        public void Gacha_IsDeterministic()
        {
            var a = RunGacha(12345, 60);
            var b = RunGacha(12345, 60);
            Assert.Equal(a, b);
        }
    }
}
