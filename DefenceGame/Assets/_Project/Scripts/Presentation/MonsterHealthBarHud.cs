using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Synthesis.Core;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). HUD - 몬스터 HP 게이지를 화면공간에 그린다(3D 아님).
    // 매 프레임 각 몬스터의 월드 위치를 스크린으로 투영해 바를 배치한다. 바는 재사용한다.
    public sealed class MonsterHealthBarHud : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private float barWidth = 44f;
        [SerializeField] private float barHeight = 6f;
        [SerializeField] private float screenYOffset = 30f;

        private Camera cam;
        private RectTransform panel;
        private readonly List<Bar> bars = new List<Bar>();
        private readonly Dictionary<LoopMonster, long> maxHp = new Dictionary<LoopMonster, long>();

        private struct Bar
        {
            public GameObject root;
            public RectTransform rect;
            public RectTransform fill;
        }

        private void Start()
        {
            if (game == null) game = Object.FindFirstObjectByType<GameManager>();
            BuildCanvas();
        }

        private void BuildCanvas()
        {
            GameObject canvasGo = new GameObject("MonsterHp Canvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 월드 추적 바는 픽셀 좌표로 배치하므로 상수 픽셀 스케일을 쓴다(월드->스크린과 1:1).
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            panel = canvasGo.GetComponent<RectTransform>();
        }

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            LoopSimulator sim = game.Context.sim;
            LoopMapView mapView = game.MapView;

            int used = 0;
            var list = sim.state.monsterList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopMonster m = list[i];
                if (!m.alive) { maxHp.Remove(m); continue; }

                long max;
                if (!maxHp.TryGetValue(m, out max)) { max = m.hp.raw > 0 ? m.hp.raw : 1; maxHp[m] = max; }

                Fixed fx, fy;
                sim.GetMonsterPosition(m, out fx, out fy);
                Vector3 world = mapView.CellToWorldF((float)fx.ToDoubleForDisplay(), (float)fy.ToDoubleForDisplay()) + new Vector3(0f, 0.35f, 0f);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z < 0f) continue; // 카메라 뒤

                Bar bar = GetBar(used);
                ++used;
                bar.rect.anchoredPosition = new Vector2(screen.x - barWidth * 0.5f, screen.y + screenYOffset);
                float ratio = Mathf.Clamp01((float)m.hp.raw / max);
                bar.fill.localScale = new Vector3(ratio, 1f, 1f);
                bar.root.SetActive(ratio < 1f); // 가득 차면 숨김
            }

            for (int j = used; j < bars.Count; ++j)
            {
                bars[j].root.SetActive(false);
            }
        }

        private Bar GetBar(int index)
        {
            while (bars.Count <= index) bars.Add(CreateBar());
            Bar bar = bars[index];
            bar.root.SetActive(true);
            return bar;
        }

        private Bar CreateBar()
        {
            GameObject bg = new GameObject("HpBar");
            bg.transform.SetParent(panel, false);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.10f, 0.10f, 0.12f, 0.9f);
            RectTransform bgRect = bgImg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.zero;
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.sizeDelta = new Vector2(barWidth, barHeight);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(bg.transform, false);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.35f, 0.85f, 0.35f, 0.95f);
            RectTransform fillRect = fillImg.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);

            return new Bar { root = bg, rect = bgRect, fill = fillRect };
        }
    }
}
