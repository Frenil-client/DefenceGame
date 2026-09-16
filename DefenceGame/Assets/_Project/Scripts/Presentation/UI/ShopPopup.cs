using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Synthesis.Core.Text;
using Synthesis.Core.Data;
using UISystem;

namespace Synthesis.Presentation
{
    // 독립 UI(팝업) - 선택권으로 원하는 1성 1기를 구매한다(SPEC 2-2).
    // UI 틀(배경/박스/제목/목록/닫기)은 프리팹에 미리 만들어 두고, 구매 행만 아이템 프리팹(RecipeRowView)으로 채운다.
    // 닫기 버튼은 프리팹 onClick 에서 UIBase.CloseCancelled 에 직접 물린다(래퍼를 두지 않는다).
    // 구매 로직은 RunContext.BuySelectedUnit 에 있다(상점에 종속시키지 않아 나중에 히어로 기능으로 옮길 수 있다).
    public sealed class ShopPopup : UIPopup
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform listRoot;   // 구매 행이 담길 컨테이너(레이아웃 그룹)
        [SerializeField] private RecipeRowView rowPrefab;   // 구매 행 아이템 프리팹(조합 행과 공용)

        private RunContext ctx;

        public void Setup(RunContext context)
        {
            ctx = context;
            Refresh();
        }

        private void Refresh()
        {
            if (ctx == null || listRoot == null || rowPrefab == null) return;

            // 치트가 켜져 있으면 제목과 행 문구를 바꿔 지금이 정상 상점이 아님을 드러낸다.
            string titleKey = ctx.cheatFreeShop ? "str.popup.shop.title.cheat" : "str.popup.shop.title";
            string buyKey = ctx.cheatFreeShop ? "str.popup.shop.buy.free" : "str.popup.shop.buy";

            if (titleText != null)
                titleText.text = StringManager.Format(titleKey,
                    new StringValues().Set("token", ctx.selectionTokens.ToString()).Set("cost", ctx.GetBuyCost().ToString()));

            for (int i = listRoot.childCount - 1; i >= 0; --i) Destroy(listRoot.GetChild(i).gameObject);

            bool canBuy = ctx.CanBuySelected();
            List<UnitData> list = ctx.PurchasableUnitList();
            foreach (var data in list)
            {
                string id = data.id;
                string label = StringManager.Format(buyKey,
                    new StringValues().Set("name", DisplayName(data))
                        .Set("cost", ctx.GetBuyCost().ToString())
                        .Set("tier", data.tier.ToString()));

                RecipeRowView row = Instantiate(rowPrefab, listRoot);
                row.Set(label, canBuy, () =>
                {
                    if (ctx.BuySelectedUnit(id)) Refresh();
                });
            }
        }

        // 화면에는 이름만 낸다. ID 는 내부 식별자라 노출하지 않는다(조합 팝업과 같은 규칙).
        private string DisplayName(UnitData data)
        {
            if (!string.IsNullOrEmpty(data.name)) return data.name;
            return data.id;
        }
    }
}
