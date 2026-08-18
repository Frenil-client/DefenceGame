using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Synthesis.Presentation;

namespace Synthesis.Editor
{
    // 에디터 툴 - UI 계층을 프리팹에 미리 구성한다(런타임 생성 금지). 컴포넌트의 [SerializeField] 참조를 빌드시 연결한다.
    //   HUD: Resources/UI/HUD/*         (HudView/InventoryView/MonsterHealthBarHud)
    //   팝업: Resources/UI/*            (CombinePopup, SamplePopup)
    //   목록 아이템: Resources/UI/Items/* (UnitButton, RecipeRow, HpBar)
    public static class UIPrefabBuilder
    {
        private const string UiDir = "Assets/_Project/Resources/UI";
        private const string HudDir = "Assets/_Project/Resources/UI/HUD";
        private const string ItemDir = "Assets/_Project/Resources/UI/Items";

        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("Synthesis/Build UI Prefabs")]
        public static void BuildUiPrefabs()
        {
            EnsureFolder(UiDir);
            EnsureFolder(HudDir);
            EnsureFolder(ItemDir);

            // 아이템 프리팹 먼저(메인 프리팹이 참조).
            UnitButtonView unitButton = BuildUnitButtonItem();
            RecipeRowView recipeRow = BuildRecipeRowItem();
            MonsterHpBarView hpBar = BuildHpBarItem();

            BuildHudView();
            BuildInventoryView(unitButton);
            BuildMonsterHpHud(hpBar);
            BuildCombinePopup(recipeRow);
            BuildShopPopup(recipeRow);
            BuildResultPopup();
            BuildSamplePopup();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UIPrefabBuilder] UI 프리팹 생성 완료: " + UiDir);
        }

        // ---- 목록 아이템 프리팹 ----

        private static UnitButtonView BuildUnitButtonItem()
        {
            GameObject root = MakeRect("UnitButton", null, new Vector2(150f, 72f));
            Image img = root.AddComponent<Image>();
            img.color = new Color(0.20f, 0.24f, 0.32f, 0.95f);
            Button btn = root.AddComponent<Button>();
            LayoutElement le = root.AddComponent<LayoutElement>();
            le.preferredWidth = 150f; le.preferredHeight = 72f;
            UnitButtonView view = root.AddComponent<UnitButtonView>();

            Text label = MakeLabel(root.transform, "Label", "", 16, TextAnchor.MiddleCenter);
            Stretch((RectTransform)label.transform);

            SetRef(view, "label", label);
            SetRef(view, "button", btn);
            return SaveAs<UnitButtonView>(root, ItemDir + "/UnitButton.prefab");
        }

        private static RecipeRowView BuildRecipeRowItem()
        {
            GameObject root = MakeRect("RecipeRow", null, new Vector2(680f, 56f));
            Image img = root.AddComponent<Image>();
            img.color = new Color(0.24f, 0.24f, 0.28f, 0.9f);
            Button btn = root.AddComponent<Button>();
            LayoutElement le = root.AddComponent<LayoutElement>();
            le.preferredHeight = 56f; le.flexibleWidth = 1f;
            RecipeRowView view = root.AddComponent<RecipeRowView>();

            Text label = MakeLabel(root.transform, "Label", "", 18, TextAnchor.MiddleLeft);
            RectTransform lrt = (RectTransform)label.transform;
            Stretch(lrt);
            lrt.offsetMin = new Vector2(16f, 0f);
            lrt.offsetMax = new Vector2(-16f, 0f);

            SetRef(view, "label", label);
            SetRef(view, "button", btn);
            SetRef(view, "background", img);
            return SaveAs<RecipeRowView>(root, ItemDir + "/RecipeRow.prefab");
        }

        private static MonsterHpBarView BuildHpBarItem()
        {
            GameObject root = MakeRect("HpBar", null, new Vector2(44f, 6f));
            RectTransform rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.zero; rootRt.pivot = new Vector2(0f, 0.5f);
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.12f, 0.9f);
            MonsterHpBarView view = root.AddComponent<MonsterHpBarView>();

            Image fillImg = MakeImage(root.transform, "Fill", new Color(0.35f, 0.85f, 0.35f, 0.95f));
            RectTransform fill = fillImg.rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            fill.pivot = new Vector2(0f, 0.5f);

            SetRef(view, "rect", rootRt);
            SetRef(view, "fill", fill);
            return SaveAs<MonsterHpBarView>(root, ItemDir + "/HpBar.prefab");
        }

        // ---- HUD 프리팹 ----

        private static void BuildHudView()
        {
            GameObject root = MakeRect("HudView", null, Vector2.zero);
            Stretch((RectTransform)root.transform);
            HudView view = root.AddComponent<HudView>();

            Text stats = MakeLabel(root.transform, "Stats", "", 18, TextAnchor.UpperLeft);
            RectTransform srt = (RectTransform)stats.transform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(0f, 1f); srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(16f, -16f);
            srt.sizeDelta = new Vector2(520f, 200f);

            BuildSpeedButton(root.transform, view, "1x", 16f, 1f);
            BuildSpeedButton(root.transform, view, "2x", 76f, 2f);
            BuildSpeedButton(root.transform, view, "4x", 136f, 4f);
            BuildShopButton(root.transform, view, 200f);
            Button skip = BuildSkipButton(root.transform, view, 296f);

            SetRef(view, "statsText", stats);
            SetRef(view, "skipButton", skip);
            SaveAs<HudView>(root, HudDir + "/HudView.prefab");
        }

        // 웨이브 스킵 버튼(하단). 스폰 완료 시 HudView 가 interactable 을 켠다.
        private static Button BuildSkipButton(Transform parent, HudView view, float x)
        {
            GameObject go = MakeRect("Skip", parent, new Vector2(110f, 36f));
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, 16f);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.28f, 0.34f, 0.46f, 0.95f);
            Button btn = go.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(btn.onClick, view.OnSkipClicked);

            Text t = MakeLabel(go.transform, "Label", "웨이브 스킵", 16, TextAnchor.MiddleCenter);
            Stretch((RectTransform)t.transform);
            return btn;
        }

        // 상점 열기 버튼(하단, 배속 버튼 옆).
        private static void BuildShopButton(Transform parent, HudView view, float x)
        {
            GameObject go = MakeRect("Shop", parent, new Vector2(84f, 36f));
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, 16f);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.30f, 0.42f, 0.30f, 0.95f);
            Button btn = go.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(btn.onClick, view.OpenShop);

            Text t = MakeLabel(go.transform, "Label", "상점", 18, TextAnchor.MiddleCenter);
            Stretch((RectTransform)t.transform);
        }

        private static void BuildSpeedButton(Transform parent, HudView view, string label, float x, float value)
        {
            GameObject go = MakeRect("Speed " + label, parent, new Vector2(52f, 36f));
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, 16f);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.24f, 0.32f, 0.9f);
            Button btn = go.AddComponent<Button>();
            UnityEventTools.AddFloatPersistentListener(btn.onClick, view.SetSpeed, value);

            Text t = MakeLabel(go.transform, "Label", label, 18, TextAnchor.MiddleCenter);
            Stretch((RectTransform)t.transform);
        }

        private static void BuildInventoryView(UnitButtonView unitButtonPrefab)
        {
            GameObject root = MakeRect("InventoryView", null, Vector2.zero);
            Stretch((RectTransform)root.transform);
            InventoryView view = root.AddComponent<InventoryView>();

            // 하단 유닛 바 컨테이너(가로 레이아웃).
            GameObject content = MakeRect("Content", root.transform, Vector2.zero);
            RectTransform crt = (RectTransform)content.transform;
            // 배속 버튼(하단 좌측, y<=52)과 겹치지 않게 유닛 바를 그 위에 둔다.
            crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(1f, 0f); crt.pivot = new Vector2(0f, 0f);
            crt.offsetMin = new Vector2(24f, 64f); crt.offsetMax = new Vector2(-24f, 144f);
            HorizontalLayoutGroup h = content.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f; h.childControlWidth = false; h.childControlHeight = false;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.LowerLeft;

            SetRef(view, "content", crt);
            SetRef(view, "unitButtonPrefab", unitButtonPrefab);
            SaveAs<InventoryView>(root, HudDir + "/InventoryView.prefab");
        }

        private static void BuildMonsterHpHud(MonsterHpBarView barPrefab)
        {
            GameObject root = MakeRect("MonsterHealthBarHud", null, Vector2.zero);
            Stretch((RectTransform)root.transform);
            MonsterHealthBarHud view = root.AddComponent<MonsterHealthBarHud>();

            SetRef(view, "barPrefab", barPrefab);
            SaveAs<MonsterHealthBarHud>(root, HudDir + "/MonsterHealthBarHud.prefab");
        }

        // ---- 팝업 프리팹 ----

        private static void BuildCombinePopup(RecipeRowView rowPrefab)
        {
            GameObject root = MakeRect("CombinePopup", null, Vector2.zero);
            Stretch((RectTransform)root.transform);
            Image backdrop = root.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.5f); // 모달(아래 입력 차단)
            CombinePopup popup = root.AddComponent<CombinePopup>();
            popup.panelId = "CombinePopup";
            popup.modal = true;

            GameObject box = MakeRect("Box", root.transform, new Vector2(720f, 480f));
            RectTransform brt = (RectTransform)box.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            Image boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.16f, 0.18f, 0.24f, 0.98f);

            Text title = MakeLabel(box.transform, "Title", "조합", 30, TextAnchor.MiddleCenter);
            RectTransform trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0f, 64f); trt.anchoredPosition = new Vector2(0f, -16f);

            GameObject list = MakeRect("List", box.transform, Vector2.zero);
            RectTransform lrt = (RectTransform)list.transform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(24f, 88f); lrt.offsetMax = new Vector2(-24f, -80f);
            VerticalLayoutGroup v = list.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f; v.childControlWidth = true; v.childControlHeight = false;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;

            GameObject close = MakeRect("Close", box.transform, new Vector2(180f, 56f));
            RectTransform crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f); crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 16f);
            Image closeImg = close.AddComponent<Image>();
            closeImg.color = new Color(0.30f, 0.36f, 0.48f, 1f);
            Button closeBtn = close.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(closeBtn.onClick, popup.Close);
            Text closeLabel = MakeLabel(close.transform, "Label", "닫기", 22, TextAnchor.MiddleCenter);
            Stretch((RectTransform)closeLabel.transform);

            SetRef(popup, "titleText", title);
            SetRef(popup, "listRoot", lrt);
            SetRef(popup, "rowPrefab", rowPrefab);
            SaveAs<CombinePopup>(root, UiDir + "/CombinePopup.prefab");
        }

        private static void BuildShopPopup(RecipeRowView rowPrefab)
        {
            GameObject root = MakeRect("ShopPopup", null, Vector2.zero);
            Stretch((RectTransform)root.transform);
            Image backdrop = root.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.5f); // 모달(아래 입력 차단)
            ShopPopup popup = root.AddComponent<ShopPopup>();
            popup.panelId = "ShopPopup";
            popup.modal = true;

            GameObject box = MakeRect("Box", root.transform, new Vector2(720f, 480f));
            RectTransform brt = (RectTransform)box.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            Image boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.16f, 0.18f, 0.24f, 0.98f);

            Text title = MakeLabel(box.transform, "Title", "상점", 30, TextAnchor.MiddleCenter);
            RectTransform trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0f, 64f); trt.anchoredPosition = new Vector2(0f, -16f);

            GameObject list = MakeRect("List", box.transform, Vector2.zero);
            RectTransform lrt = (RectTransform)list.transform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(24f, 88f); lrt.offsetMax = new Vector2(-24f, -80f);
            VerticalLayoutGroup v = list.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f; v.childControlWidth = true; v.childControlHeight = false;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;

            GameObject close = MakeRect("Close", box.transform, new Vector2(180f, 56f));
            RectTransform crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f); crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 16f);
            Image closeImg = close.AddComponent<Image>();
            closeImg.color = new Color(0.30f, 0.36f, 0.48f, 1f);
            Button closeBtn = close.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(closeBtn.onClick, popup.Close);
            Text closeLabel = MakeLabel(close.transform, "Label", "닫기", 22, TextAnchor.MiddleCenter);
            Stretch((RectTransform)closeLabel.transform);

            SetRef(popup, "titleText", title);
            SetRef(popup, "listRoot", lrt);
            SetRef(popup, "rowPrefab", rowPrefab);
            SaveAs<ShopPopup>(root, UiDir + "/ShopPopup.prefab");
        }

        private static void BuildResultPopup()
        {
            GameObject root = MakeRect("ResultPopup", null, Vector2.zero);
            Stretch((RectTransform)root.transform);
            Image backdrop = root.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.6f); // 모달
            ResultPopup popup = root.AddComponent<ResultPopup>();
            popup.panelId = "ResultPopup";
            popup.modal = true;

            GameObject box = MakeRect("Box", root.transform, new Vector2(560f, 320f));
            RectTransform brt = (RectTransform)box.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            Image boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.16f, 0.18f, 0.24f, 0.98f);

            Text msg = MakeLabel(box.transform, "Message", "", 40, TextAnchor.MiddleCenter);
            RectTransform mrt = (RectTransform)msg.transform;
            mrt.anchorMin = new Vector2(0f, 0.4f); mrt.anchorMax = new Vector2(1f, 1f); mrt.pivot = new Vector2(0.5f, 1f);
            mrt.offsetMin = Vector2.zero; mrt.offsetMax = new Vector2(0f, -24f);

            GameObject restart = MakeRect("Restart", box.transform, new Vector2(220f, 64f));
            RectTransform rrt = (RectTransform)restart.transform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0f); rrt.pivot = new Vector2(0.5f, 0f);
            rrt.anchoredPosition = new Vector2(0f, 32f);
            Image rimg = restart.AddComponent<Image>();
            rimg.color = new Color(0.30f, 0.42f, 0.30f, 1f);
            Button rbtn = restart.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(rbtn.onClick, popup.OnRestartClicked);
            Text rlabel = MakeLabel(restart.transform, "Label", "재시작", 24, TextAnchor.MiddleCenter);
            Stretch((RectTransform)rlabel.transform);

            SetRef(popup, "messageText", msg);
            SaveAs<ResultPopup>(root, UiDir + "/ResultPopup.prefab");
        }

        private static void BuildSamplePopup()
        {
            GameObject root = MakeRect("SamplePopup", null, Vector2.zero);
            Stretch((RectTransform)root.transform);
            Image backdrop = root.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.5f);
            UIPanel panel = root.AddComponent<UIPanel>();
            panel.panelId = "SamplePopup";
            panel.modal = true;

            GameObject box = MakeRect("Box", root.transform, new Vector2(600f, 360f));
            RectTransform brt = (RectTransform)box.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            Image boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.16f, 0.18f, 0.24f, 0.98f);

            Text title = MakeLabel(box.transform, "Title", "샘플 팝업", 32, TextAnchor.MiddleCenter);
            RectTransform trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0f, 80f); trt.anchoredPosition = new Vector2(0f, -24f);

            GameObject close = MakeRect("CloseButton", box.transform, new Vector2(200f, 64f));
            RectTransform crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f); crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 32f);
            Image closeImg = close.AddComponent<Image>();
            closeImg.color = new Color(0.30f, 0.36f, 0.48f, 1f);
            Button closeBtn = close.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(closeBtn.onClick, panel.Close);
            Text closeLabel = MakeLabel(close.transform, "Label", "닫기", 24, TextAnchor.MiddleCenter);
            Stretch((RectTransform)closeLabel.transform);

            SaveAs<UIPanel>(root, UiDir + "/SamplePopup.prefab");
        }

        // ---- 헬퍼 ----

        private static GameObject MakeRect(string name, Transform parent, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            if (size != Vector2.zero) ((RectTransform)go.transform).sizeDelta = size;
            return go;
        }

        private static Image MakeImage(Transform parent, string name, Color color)
        {
            GameObject go = MakeRect(name, parent, Vector2.zero);
            Image img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text MakeLabel(Transform parent, string name, string text, int size, TextAnchor anchor)
        {
            GameObject go = MakeRect(name, parent, Vector2.zero);
            Text t = go.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetRef(Component comp, string prop, Object value)
        {
            var so = new SerializedObject(comp);
            var p = so.FindProperty(prop);
            if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedProperties(); }
            else Debug.LogWarning("[UIPrefabBuilder] 프로퍼티 없음: " + comp.GetType().Name + "." + prop);
        }

        private static T SaveAs<T>(GameObject root, string path) where T : Component
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved != null ? saved.GetComponent<T>() : null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Directory.CreateDirectory(Path.GetFullPath(path));
            AssetDatabase.Refresh();
        }
    }
}
