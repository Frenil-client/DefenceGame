using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Combination;

namespace Synthesis.Core.Tests
{
    // STEP 3. 검증 - 조합 판정과 등급 격자 도달성.
    public class CombinationTests
    {
        private static List<RecipeData> LoadRecipes()
        {
            return CsvParsers.LoadRecipes(TestPaths.ReadData("recipes.csv"));
        }

        private static List<UnitData> LoadUnits()
        {
            return CsvParsers.LoadUnits(TestPaths.ReadData("units.csv"));
        }

        [Fact]
        public void Combine_CommonsToRare()
        {
            var engine = new CombinationEngine(LoadRecipes());
            string result;
            Assert.True(engine.TryCombine("C01", "C02", out result));
            Assert.Equal("R01", result);
        }

        [Fact]
        public void Combine_IsOrderIndependent()
        {
            var engine = new CombinationEngine(LoadRecipes());
            string ab, ba;
            engine.TryCombine("C01", "C02", out ab);
            engine.TryCombine("C02", "C01", out ba);
            Assert.Equal(ab, ba);
        }

        [Fact]
        public void Combine_UniquePairToHidden()
        {
            var engine = new CombinationEngine(LoadRecipes());
            string result;
            Assert.True(engine.TryCombine("U01", "U02", out result));
            Assert.Equal("H06", result);
        }

        [Fact]
        public void Combine_InvalidPairFails()
        {
            var engine = new CombinationEngine(LoadRecipes());
            string result;
            // 같은 역할(single)이지만 정의된 레시피가 아닌 쌍은 성립하지 않는다.
            Assert.False(engine.TryCombine("C01", "C06", out result));
            Assert.Null(result);
        }

        [Fact]
        public void GradeLattice_AllUnitsReachable()
        {
            var engine = new CombinationEngine(LoadRecipes());
            var units = LoadUnits();
            HashSet<string> buildable = engine.ComputeBuildable(units);

            // 흔함에서 시작해 모든 레어/유니크/히든에 도달할 수 있어야 한다(고아 없음).
            foreach (var unit in units)
            {
                Assert.True(buildable.Contains(unit.id), unit.id + " (" + unit.name + ") 도달 불가");
            }
        }

        [Fact]
        public void GradeLattice_AllSixHiddenReachable()
        {
            var engine = new CombinationEngine(LoadRecipes());
            var units = LoadUnits();
            HashSet<string> buildable = engine.ComputeBuildable(units);

            string[] hiddenList = { "H01", "H02", "H03", "H04", "H05", "H06" };
            foreach (var id in hiddenList)
            {
                Assert.Contains(id, buildable);
            }
        }
    }
}
