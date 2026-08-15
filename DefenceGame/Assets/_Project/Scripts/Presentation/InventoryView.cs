using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core.Data;

namespace Synthesis.Presentation
{
    // 뷰(HUD) - 하단 보유 유닛 바. UI 틀은 프리팹에 미리 만들어 두고, 유닛 버튼만 아이템 프리팹으로 채운다.
    // 유닛(인벤토리+필드 합산)을 종류별로 보여주고, 클릭하면 그 유닛을 재료로 하는 조합 팝업(CombinePopup)을 연다.
    public sealed class InventoryView : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private RectTransform content;          // 유닛 버튼이 담길 컨테이너(레이아웃 그룹)
        [SerializeField] private UnitButtonView unitButtonPrefab; // 유닛 버튼 아이템 프리팹

        private string lastSignature = "";

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;

            string sig = Signature();
            if (sig != lastSignature)
            {
                lastSignature = sig;
                Rebuild();
            }
        }

        private string Signature()
        {
            var counts = game.Context.MergedCounts();
            long sum = counts.Count * 1000003L;
            foreach (var pair in counts) sum += pair.Key.GetHashCode() * 31L + pair.Value;
            return sum.ToString();
        }

        private void Rebuild()
        {
            if (content == null || unitButtonPrefab == null) return;

            for (int i = content.childCount - 1; i >= 0; --i) Destroy(content.GetChild(i).gameObject);

            RunContext ctx = game.Context;
            Dictionary<string, int> counts = ctx.MergedCounts();
            List<string> order = new List<string>(counts.Keys);
            order.Sort((a, b) =>
            {
                int ta = TierOf(ctx, a), tb = TierOf(ctx, b);
                if (ta != tb) return ta.CompareTo(tb);
                return string.CompareOrdinal(a, b);
            });

            foreach (var id in order)
            {
                UnitData data;
                ctx.unitById.TryGetValue(id, out data);
                string name = data != null && !string.IsNullOrEmpty(data.name) ? data.name : id;
                string captured = id;

                UnitButtonView item = Instantiate(unitButtonPrefab, content);
                item.Set(name + " x" + counts[id], () => OpenCombinePopup(captured));
            }
        }

        private void OpenCombinePopup(string unitId)
        {
            if (UIManager.Instance == null)
            {
                Debug.LogWarning("[InventoryView] 씬에 UIManager 가 없어 조합 팝업을 열 수 없습니다.");
                return;
            }
            CombinePopup popup = UIManager.Instance.Open("CombinePopup") as CombinePopup;
            if (popup != null) popup.Setup(game.Context, unitId);
        }

        private static int TierOf(RunContext ctx, string id)
        {
            UnitData data;
            return ctx.unitById.TryGetValue(id, out data) ? data.tier : 0;
        }
    }
}
