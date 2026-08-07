using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Combination;

namespace Synthesis.Core.Tests
{
    // STEP 3. 검증 - 뽑기 보장 규칙 G1 부터 G5. 다수 시드로 반례가 없음을 확인한다.
    public class GachaTests
    {
        private const int SeedCount = 300;

        private static List<UnitData> units;
        private static List<RecipeData> recipes;

        private static void EnsureLoaded()
        {
            if (units == null) units = CsvParsers.LoadUnits(TestPaths.ReadData("units.csv"));
            if (recipes == null) recipes = CsvParsers.LoadRecipes(TestPaths.ReadData("recipes.csv"));
        }

        private static List<string> RunGacha(long seed, int waveCount)
        {
            EnsureLoaded();
            var gacha = new GachaEngine(units, recipes, seed);
            List<string> result = new List<string>();
            for (int w = 1; w <= waveCount; ++w)
            {
                result.Add(gacha.GrantForWave(w));
            }
            return result;
        }

        private static UnitData FindUnit(string id)
        {
            foreach (var unit in units)
            {
                if (unit.id == id) return unit;
            }
            return null;
        }

        [Fact]
        public void G2_GuardKeyByWave8_AllSeeds()
        {
            for (long seed = 1; seed <= SeedCount; ++seed)
            {
                var h = RunGacha(seed, 7); // 1..7 웨이브 지급
                bool hasGuard = h.Contains("C05") || h.Contains("C09");
                Assert.True(hasGuard, "시드 " + seed + ": 8웨이브 이전 관통/방깎 미보장");
            }
        }

        [Fact]
        public void G1_MeleeRangedByWave5_AllSeeds()
        {
            for (long seed = 1; seed <= SeedCount; ++seed)
            {
                var h = RunGacha(seed, 5);
                HashSet<string> melee = new HashSet<string>();
                HashSet<string> ranged = new HashSet<string>();
                foreach (var id in h)
                {
                    UnitData unit = FindUnit(id);
                    if (unit.placement == Placement.Melee) melee.Add(id);
                    else ranged.Add(id);
                }
                Assert.True(melee.Count >= 2, "시드 " + seed + ": 근접 " + melee.Count + "종");
                Assert.True(ranged.Count >= 2, "시드 " + seed + ": 원거리 " + ranged.Count + "종");
            }
        }

        [Fact]
        public void G3_NoFourInARow_AllSeeds()
        {
            for (long seed = 1; seed <= SeedCount; ++seed)
            {
                var h = RunGacha(seed, 12);
                for (int i = 3; i < h.Count; ++i)
                {
                    bool four = h[i] == h[i - 1] && h[i] == h[i - 2] && h[i] == h[i - 3];
                    Assert.False(four, "시드 " + seed + ": " + h[i] + " 4연속");
                }
            }
        }

        [Fact]
        public void G4_NoElementOverFourInSix_AllSeeds()
        {
            for (long seed = 1; seed <= SeedCount; ++seed)
            {
                var h = RunGacha(seed, 12);
                for (int start = 0; start + 6 <= h.Count; ++start)
                {
                    Dictionary<Element, int> countByElement = new Dictionary<Element, int>();
                    for (int i = start; i < start + 6; ++i)
                    {
                        Element e = FindUnit(h[i]).element;
                        if (!countByElement.ContainsKey(e)) countByElement[e] = 0;
                        countByElement[e] += 1;
                    }
                    foreach (var pair in countByElement)
                    {
                        Assert.True(pair.Value <= 4, "시드 " + seed + ": " + pair.Key + " " + pair.Value + "회/6");
                    }
                }
            }
        }

        [Fact]
        public void G5_TwoRareCombosByWave12_AllSeeds()
        {
            var engine = new CombinationEngine(recipes ?? (recipes = CsvParsers.LoadRecipes(TestPaths.ReadData("recipes.csv"))));
            for (long seed = 1; seed <= SeedCount; ++seed)
            {
                var h = RunGacha(seed, 11);
                HashSet<string> owned = new HashSet<string>(h);
                int combos = 0;
                foreach (var recipe in recipes)
                {
                    if (recipe.isHidden || recipe.conditionType == ConditionType.Fixed) continue;
                    if (owned.Contains(recipe.mat1) && owned.Contains(recipe.mat2)) ++combos;
                }
                Assert.True(combos >= 2, "시드 " + seed + ": 레어 조합 가능 " + combos + "회");
            }
        }

        [Fact]
        public void Gacha_IsDeterministic()
        {
            var a = RunGacha(12345, 12);
            var b = RunGacha(12345, 12);
            Assert.Equal(a, b);
        }
    }
}
