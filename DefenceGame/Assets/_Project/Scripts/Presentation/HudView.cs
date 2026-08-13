using UnityEngine;
using UnityEngine.UI;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 뷰 - HUD. GameManager/WaveManager 상태를 읽어 갱신. 배속 버튼으로 GameManager.Speed 변경.
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private WaveManager waves;

        private Text hudText;

        private void Start()
        {
            if (game == null) game = Object.FindFirstObjectByType<GameManager>();
            if (waves == null) waves = Object.FindFirstObjectByType<WaveManager>();
            BuildHud();
        }

        private void Update()
        {
            if (hudText == null || game == null || game.Context == null || !game.Context.IsValid()) return;

            var s = game.Context.sim.state;
            int next = waves != null ? waves.NextWave : 1;
            string granted = waves != null ? waves.LastGranted : "-";
            int shownWave = Mathf.Clamp(next - 1, 0, game.MaxWave);
            string phaseLabel = s.defeated ? "패배"
                : (next > game.MaxWave && s.aliveCount == 0 ? "클리어"
                : (s.pendingSpawns > 0 ? "전투" : "대기"));

            hudText.text =
                "웨이브 " + shownWave + " / " + game.MaxWave + "  [" + phaseLabel + "]  x" + game.Speed + "\n"
                + "코스트 " + s.cost + " / " + s.costCap + "\n"
                + "필드 몬스터 " + s.aliveCount + "   처치 " + s.killedCount + "\n"
                + "인벤토리 " + game.Context.inventory.Count + "   최근 뽑기 " + granted;
        }

        private void BuildHud()
        {
            GameObject canvasGo = new GameObject("HUD Canvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject textGo = new GameObject("Stats");
            textGo.transform.SetParent(canvasGo.transform, false);
            hudText = textGo.AddComponent<Text>();
            hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hudText.fontSize = 18;
            hudText.color = Color.white;
            hudText.alignment = TextAnchor.UpperLeft;
            RectTransform rt = hudText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -16f);
            rt.sizeDelta = new Vector2(520f, 200f);

            BuildSpeedButton(canvasGo.transform, "1x", 16f, 1f);
            BuildSpeedButton(canvasGo.transform, "2x", 76f, 2f);
            BuildSpeedButton(canvasGo.transform, "4x", 136f, 4f);
        }

        private void BuildSpeedButton(Transform parent, string label, float x, float value)
        {
            GameObject go = new GameObject("Speed " + label);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.24f, 0.32f, 0.9f);
            Button btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => { if (game != null) game.Speed = value; });

            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, 16f);
            rt.sizeDelta = new Vector2(52f, 36f);

            GameObject t = new GameObject("Label");
            t.transform.SetParent(go.transform, false);
            Text txt = t.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = label;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = Vector2.zero;
            txt.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
