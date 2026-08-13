using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Combination;

namespace Synthesis.Core.Tests
{
    // STEP 3(v0.4). 검증 - 조합 판정(멀티 재료).
    public class CombinationTests
    {
        private static CombinationEngine Engine()
        {
            return new CombinationEngine(CsvParsers.LoadRecipes(TestPaths.ReadData("recipes.csv")));
        }

        [Fact]
        public void Recipe_HasCorrectMaterials()
        {
            var engine = Engine();
            RecipeData r;
            Assert.True(engine.TryGetRecipe("T2-WAR-01", out r));
            Assert.Equal(2, r.materials.Count); // 전사+전사
        }

        [Fact]
        public void CanCraft_WithEnoughMaterials()
        {
            var engine = Engine();
            RecipeData r;
            engine.TryGetRecipe("T2-WAR-01", out r); // T1-WAR x2

            var have = new Dictionary<string, int> { { "T1-WAR", 2 } };
            Assert.True(engine.CanCraft(r, have));

            var notEnough = new Dictionary<string, int> { { "T1-WAR", 1 } };
            Assert.False(engine.CanCraft(r, notEnough));
        }

        [Fact]
        public void T5_RequiresFourMaterials()
        {
            var engine = Engine();
            RecipeData r;
            Assert.True(engine.TryGetRecipe("T5-WAR-01", out r));
            Assert.Equal(4, r.materials.Count);
        }
    }
}
