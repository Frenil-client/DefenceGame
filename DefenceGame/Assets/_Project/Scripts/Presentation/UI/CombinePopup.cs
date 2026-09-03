using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Synthesis.Core.Text;
using Synthesis.Core.Data;

namespace Synthesis.Presentation
{
    // 독립 UI(팝업) - 고른 유닛을 재료로 하는 조합식을 보여주고 하나를 선택해 조합한다(UNIT_RECIPES.md 1장 UI).
    // UI 틀(배경/박스/제목/목록/닫기)은 프리팹에 미리 만들어 두고, 조합식 행만 아이템 프리팹으로 채운다.
    public sealed class CombinePopup : UIPanel
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform listRoot;      // 조합식 행이 담길 컨테이너(레이아웃 그룹)
        [SerializeField] private RecipeRowView rowPrefab;     // 조합식 행 아이템 프리팹

        private RunContext ctx;
        private string unitId;

        // 인벤토리 하단 바에서 유닛을 클릭하면 호출한다.
        public void Setup(RunContext context, string selectedUnitId)
        {
            ctx = context;
            unitId = selectedUnitId;
            Refresh();
        }

        private void Refresh()
        {
            if (ctx == null || listRoot == null || rowPrefab == null) return;

            if (titleText != null) titleText.text = StringManager.Format("str.popup.combine.title",
                new StringValues().Set("name", DisplayName(unitId)));

            for (int i = listRoot.childCount - 1; i >= 0; --i) Destroy(listRoot.GetChild(i).gameObject);

            List<RecipeData> recipes = ctx.combination.RecipesLedBy(unitId);
            foreach (var recipe in recipes)
            {
                bool canCraft = ctx.CanCraftMerged(recipe.resultId);
                string label = DisplayName(recipe.resultId) + "\n" + MaterialsLabel(recipe);
                string resultId = recipe.resultId;

                RecipeRowView row = Instantiate(rowPrefab, listRoot);
                row.Set(label, canCraft, () =>
                {
                    // 조합하면 팝업을 결과 유닛 기준으로 전환한다(그 유닛으로 이어서 만들 수 있는 조합식을 보여준다).
                    if (ctx.TryCraftFromField(resultId))
                    {
                        unitId = resultId;
                        Refresh();
                    }
                });
            }
        }

        private string DisplayName(string id)
        {
            UnitData data;
            if (ctx != null && ctx.unitById.TryGetValue(id, out data) && !string.IsNullOrEmpty(data.name)) return data.name;
            return id;
        }

        private string MaterialsLabel(RecipeData recipe)
        {
            List<string> parts = new List<string>();
            foreach (var mat in recipe.materials) parts.Add(DisplayName(mat));
            return string.Join(" + ", parts);
        }
    }
}
