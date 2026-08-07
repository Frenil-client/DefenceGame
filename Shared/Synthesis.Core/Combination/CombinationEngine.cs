using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Core.Combination
{
    // STEP 3. 핵심 - 조합 판정. 두 유닛을 상위 유닛으로 합성한다 (BALANCE_SPEC.md 4).
    // 조합은 recipes.csv 에 정의된 재료 쌍(순서 무관) 조회다. 3합체는 없다.
    // 성립 조건(같은 속성/같은 역할/고정)은 레시피가 이미 반영하므로 여기서는 쌍 조회만 한다.
    public sealed class CombinationEngine
    {
        private readonly List<RecipeData> recipeList;
        private readonly Dictionary<string, RecipeData> recipeByPair;

        public CombinationEngine(List<RecipeData> recipes)
        {
            recipeList = recipes;
            recipeByPair = new Dictionary<string, RecipeData>();
            foreach (var recipe in recipes)
            {
                if (recipe == null) continue;
                recipeByPair[PairKey(recipe.mat1, recipe.mat2)] = recipe;
            }
        }

        // 두 유닛 id 로 조합을 시도한다. 성립하면 resultId 를 채우고 true.
        public bool TryCombine(string idA, string idB, out string resultId)
        {
            RecipeData recipe;
            if (recipeByPair.TryGetValue(PairKey(idA, idB), out recipe))
            {
                resultId = recipe.resultId;
                return true;
            }
            resultId = null;
            return false;
        }

        public bool TryGetRecipe(string idA, string idB, out RecipeData recipe)
        {
            return recipeByPair.TryGetValue(PairKey(idA, idB), out recipe);
        }

        // 등급 격자 도달성: 흔함을 무한 공급한다고 볼 때 조합으로 만들 수 있는 모든 유닛 집합.
        // 고아 레시피가 있으면 그 결과는 집합에 들어오지 못한다(도달 불가).
        public HashSet<string> ComputeBuildable(List<UnitData> unitList)
        {
            HashSet<string> buildable = new HashSet<string>();
            foreach (var unit in unitList)
            {
                if (unit.grade == Grade.Common)
                {
                    buildable.Add(unit.id);
                }
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var recipe in recipeList)
                {
                    if (recipe == null) continue;
                    if (buildable.Contains(recipe.resultId)) continue;
                    if (buildable.Contains(recipe.mat1) && buildable.Contains(recipe.mat2))
                    {
                        buildable.Add(recipe.resultId);
                        changed = true;
                    }
                }
            }
            return buildable;
        }

        // 재료 쌍을 순서 무관 키로 만든다. 작은 id 를 앞에 둔다.
        private static string PairKey(string a, string b)
        {
            if (string.CompareOrdinal(a, b) <= 0) return a + "|" + b;
            return b + "|" + a;
        }
    }
}
