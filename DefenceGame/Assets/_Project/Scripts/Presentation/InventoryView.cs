using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Synthesis.Core.Data;
using Synthesis.Core.Units;
using Synthesis.Core.Combination;

namespace Synthesis.Presentation
{
    // STEP 3(재작업). 뷰 - 인벤토리/유닛 선택 + 조합 UI. 1920x1080 기준.
    // 하단: 보유 유닛 종류 버튼(클릭=배치할 유닛 선택). 그 위: 지금 가능한 조합 버튼(클릭=조합 실행).
    // 인벤토리가 바뀌면 다시 그린다.
    public sealed class InventoryView : MonoBehaviour
    {
        [SerializeField] private GameManager game;

        private RectTransform panel;
        private Font font;
        private string lastSignature = "";
        private string lastSelected = "";

        private void Start()
        {
            if (game == null) game = Object.FindFirstObjectByType<GameManager>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
        }

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;

            string sig = Signature();
            if (sig != lastSignature || game.SelectedUnitId != lastSelected)
            {
                lastSignature = sig;
                lastSelected = game.SelectedUnitId;
                Rebuild();
            }
        }

        private void BuildCanvas()
        {
            GameObject canvasGo = new GameObject("Inventory Canvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject panelGo = new GameObject("InvPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            panel = panelGo.AddComponent<RectTransform>();
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }

        private string Signature()
        {
            var inv = game.Context.inventory.ownedList;
            long sum = inv.Count * 1000003L;
            for (int i = 0; i < inv.Count; ++i) sum += inv[i].instanceId;
            return sum.ToString();
        }

        private void Rebuild()
        {
            for (int i = panel.childCount - 1; i >= 0; --i)
            {
                Destroy(panel.GetChild(i).gameObject);
            }

            RunContext ctx = game.Context;

            // 보유 유닛 종류별 개수 (첫 등장 순서 유지)
            List<string> order = new List<string>();
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (var owned in ctx.inventory.ownedList)
            {
                if (!counts.ContainsKey(owned.unitId)) { counts[owned.unitId] = 0; order.Add(owned.unitId); }
                counts[owned.unitId] += 1;
            }

            // 하단: 인벤토리 유닛 선택 버튼
            float x = 24f;
            foreach (var id in order)
            {
                UnitData data;
                ctx.unitById.TryGetValue(id, out data);
                int cost = data != null ? data.cost : 0;
                string label = id + " x" + counts[id] + "\n(" + cost + ")";
                bool selected = id == game.SelectedUnitId;
                Color bg = selected ? new Color(0.30f, 0.55f, 0.30f, 0.95f) : new Color(0.20f, 0.24f, 0.32f, 0.95f);

                string captured = id;
                MakeButton(label, x, 24f, 150f, 72f, bg, () =>
                {
                    game.SelectedUnitId = (game.SelectedUnitId == captured) ? "" : captured;
                });
                x += 160f;
            }

            // 그 위: 지금 가능한 조합 버튼 (결과별 첫 쌍)
            BuildCombineButtons(ctx);
        }

        private void BuildCombineButtons(RunContext ctx)
        {
            CombinationEngine engine = ctx.combination;

            // 지금 인벤토리로 만들 수 있는 레시피 버튼 (v0.4: 재료 2~4기 고정 조합).
            Dictionary<string, int> counts = ctx.inventory.CountsByUnit();
            float x = 24f;

            var recipes = engine.Recipes;
            for (int i = 0; i < recipes.Count; ++i)
            {
                RecipeData recipe = recipes[i];
                if (!engine.CanCraft(recipe, counts)) continue;

                string resultId = recipe.resultId;
                string label = resultId + " 조합\n" + string.Join("+", recipe.materials);
                MakeButton(label, x, 108f, 170f, 64f, new Color(0.36f, 0.30f, 0.46f, 0.95f), () =>
                {
                    OwnedUnit made;
                    game.Context.inventory.TryCraft(engine, resultId, out made);
                });
                x += 180f;
            }
        }

        private void MakeButton(string label, float x, float y, float w, float h, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("Btn");
            go.transform.SetParent(panel, false);
            Image img = go.AddComponent<Image>();
            img.color = bg;
            Button btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            GameObject t = new GameObject("Label");
            t.transform.SetParent(go.transform, false);
            Text txt = t.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 16;
            txt.text = label;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = Vector2.zero;
            txt.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
