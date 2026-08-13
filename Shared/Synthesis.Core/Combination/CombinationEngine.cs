using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Core.Combination
{
    // STEP 3. 핵심 - 조합 판정 (v0.4). 레시피는 재료 2~4기의 고정 구성이며 같은 재료가 반복될 수 있다.
    public sealed class CombinationEngine
    {
        private readonly List<RecipeData> recipeList;
        private readonly Dictionary<string, RecipeData> byResult;

        public CombinationEngine(List<RecipeData> recipes)
        {
            recipeList = recipes;
            byResult = new Dictionary<string, RecipeData>();
            foreach (var recipe in recipes)
            {
                if (recipe == null || string.IsNullOrEmpty(recipe.resultId)) continue;
                byResult[recipe.resultId] = recipe;
            }
        }

        public IReadOnlyList<RecipeData> Recipes => recipeList;

        public bool TryGetRecipe(string resultId, out RecipeData recipe)
        {
            return byResult.TryGetValue(resultId, out recipe);
        }

        // 재료별 필요 개수를 센다(같은 재료 반복 처리).
        public static Dictionary<string, int> Needs(RecipeData recipe)
        {
            Dictionary<string, int> need = new Dictionary<string, int>();
            foreach (var mat in recipe.materials)
            {
                if (!need.ContainsKey(mat)) need[mat] = 0;
                need[mat] += 1;
            }
            return need;
        }

        // 보유 개수(unitId -> count)로 이 레시피를 만들 수 있는가.
        public bool CanCraft(RecipeData recipe, Dictionary<string, int> haveCounts)
        {
            foreach (var pair in Needs(recipe))
            {
                int have;
                haveCounts.TryGetValue(pair.Key, out have);
                if (have < pair.Value) return false;
            }
            return true;
        }
    }
}
