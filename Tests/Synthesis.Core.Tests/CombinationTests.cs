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

        [Fact]
        public void RecipesUsing_ReverseIndex()
        {
            var engine = Engine();
            var recipes = engine.RecipesUsing("T1-WAR");

            var ids = new HashSet<string>();
            foreach (var r in recipes) ids.Add(r.resultId);

            // 전사가 재료로 들어가는 조합식(UNIT_RECIPES 역참조): T2-WAR-01(전사x2), T2-WAR-07, T2-WAR-08, T3-WAR-01, T3-WAR-02
            Assert.Equal(5, recipes.Count);
            Assert.Contains("T2-WAR-01", ids);
            Assert.Contains("T2-WAR-07", ids);
            Assert.Contains("T2-WAR-08", ids);
            Assert.Contains("T3-WAR-01", ids);
            Assert.Contains("T3-WAR-02", ids);
        }
    }
}
