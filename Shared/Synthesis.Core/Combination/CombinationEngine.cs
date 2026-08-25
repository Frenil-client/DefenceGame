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

        // 이 유닛을 재료로 쓰는 조합식 목록(역참조). 조합 UI 가 "이 유닛으로 만들 수 있는 것"을 보여줄 때 쓴다.
        public List<RecipeData> RecipesUsing(string unitId)
        {
            List<RecipeData> result = new List<RecipeData>();
            for (int i = 0; i < recipeList.Count; ++i)
            {
                RecipeData recipe = recipeList[i];
                if (recipe == null || recipe.materials == null) continue;
                for (int m = 0; m < recipe.materials.Count; ++m)
                {
                    if (recipe.materials[m] == unitId) { result.Add(recipe); break; }
                }
            }
            return result;
        }

        // 이 유닛이 조합식의 "첫 재료"인 조합식만(대표 재료 기준). 조합 UI 에서 한 유닛이 여러 조합식에 중복 표시되지 않게 한다.
        //   예: 법사 선택 시 주술사(법사 + 정령)는 첫 재료가 법사라 표시하고, 마법전사(전사 + 법사)는 첫 재료가 전사라 숨긴다.
        public List<RecipeData> RecipesLedBy(string unitId)
        {
            List<RecipeData> result = new List<RecipeData>();
            for (int i = 0; i < recipeList.Count; ++i)
            {
                RecipeData recipe = recipeList[i];
                if (recipe == null || recipe.materials == null || recipe.materials.Count == 0) continue;
                if (recipe.materials[0] == unitId) result.Add(recipe);
            }
            return result;
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
