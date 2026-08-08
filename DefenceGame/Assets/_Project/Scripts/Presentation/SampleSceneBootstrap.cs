using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // STEP 2/3. 최소 View - 씬에 이미 놓인 MapView(굽힌 타일)를 소비해 RunController 를 프레임 단위로 구동한다.
    // 맵/카메라/조명은 씬에 authored 되어 있어야 한다(런타임에 만들지 않는다). 여기서는 동적 요소만 다룬다:
    // 적/유닛 프리미티브 스폰과 이동, HUD 갱신. 아트는 STEP 8 이전에 넣지 않는다(ROADMAP 0).
    public sealed class SampleSceneBootstrap : MonoBehaviour
    {
        private enum Phase { Prep, Wave, Done }

        [Header("참조")]
        [Tooltip("비우면 씬에서 자동으로 찾는다")]
        [SerializeField] private MapView mapView;

        [Header("실행 설정")]
        [SerializeField] private long seed = 1;
        [SerializeField] private int maxWave = 24;
        [SerializeField] private float prepSeconds = 1.5f;

        private const float TickInterval = 1f / 20f; // 20틱/초 (ARCHITECTURE 4-2)

        private RunController run;
        private float speed = 1f;
        private Phase phase;
        private int waveIndex;
        private float prepTimer;
        private float tickAccum;
        private WaveOutcome lastOutcome;

        private readonly Dictionary<EnemyInstance, GameObject> enemyViewDict = new Dictionary<EnemyInstance, GameObject>();
        private readonly Dictionary<UnitInstance, GameObject> unitViewDict = new Dictionary<UnitInstance, GameObject>();

        private Text hudText;

        private void Start()
        {
            if (mapView == null) mapView = Object.FindFirstObjectByType<MapView>();
            if (mapView == null)
            {
                Debug.LogError("[Sample] 씬에 MapView 가 없습니다. Synthesis > Create Sample Scene 으로 만들거나 MapView 를 추가하세요.");
                enabled = false;
                return;
            }

            GameDatabase db = RuntimeDataLoader.LoadDatabase();
            var map = mapView.LoadMapData();
            if (map == null || db.unitList.Count == 0)
            {
                Debug.LogError("[Sample] Data 를 읽지 못했습니다.");
                enabled = false;
                return;
            }

            run = new RunController(db, map, seed);
            BuildHud();

            waveIndex = 1;
            phase = Phase.Prep;
            prepTimer = prepSeconds;
        }

        private void Update()
        {
            if (run == null) return;

            float dt = Time.deltaTime * speed;
            if (phase == Phase.Prep)
            {
                prepTimer -= dt;
                if (prepTimer <= 0f)
                {
                    lastOutcome = run.BeginWave(waveIndex);
                    phase = Phase.Wave;
                    tickAccum = 0f;
                }
            }
            else if (phase == Phase.Wave)
            {
                tickAccum += dt;
                while (tickAccum >= TickInterval && phase == Phase.Wave)
                {
                    tickAccum -= TickInterval;
                    run.StepTick();
                    if (run.IsWaveComplete())
                    {
                        lastOutcome = run.EndWave();
                        ++waveIndex;
                        if (waveIndex > maxWave)
                        {
                            phase = Phase.Done;
                        }
                        else
                        {
                            phase = Phase.Prep;
                            prepTimer = prepSeconds;
                        }
                    }
                }
            }

            SyncEnemyViews();
            SyncUnitViews();
            UpdateHud();
        }

        // ---- HUD (동적 UI 라 런타임 생성) ----

        private void BuildHud()
        {
            GameObject canvasGo = new GameObject("HUD Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
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
            rt.sizeDelta = new Vector2(520f, 220f);

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
            btn.onClick.AddListener(() => speed = value);

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

        // ---- 매 프레임 동기화 ----

        private void SyncEnemyViews()
        {
            var state = run.sim.state;
            for (int i = 0; i < state.enemyList.Count; ++i)
            {
                EnemyInstance enemy = state.enemyList[i];
                GameObject go;
                bool has = enemyViewDict.TryGetValue(enemy, out go);

                if (!enemy.alive)
                {
                    if (has)
                    {
                        Destroy(go);
                        enemyViewDict.Remove(enemy);
                    }
                    continue;
                }

                if (!has)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                    SetColor(go, new Color(0.85f, 0.25f, 0.25f));
                    enemyViewDict.Add(enemy, go);
                }

                Fixed fx, fy;
                run.sim.GetEnemyPosition(enemy, out fx, out fy);
                go.transform.position = mapView.CellToWorld((float)fx.ToDoubleForDisplay(), (float)fy.ToDoubleForDisplay()) + new Vector3(0f, 0.3f, 0f);
            }
        }

        private void SyncUnitViews()
        {
            var state = run.sim.state;
            for (int i = 0; i < state.unitList.Count; ++i)
            {
                UnitInstance unit = state.unitList[i];
                GameObject go;
                bool has = unitViewDict.TryGetValue(unit, out go);

                if (!unit.alive)
                {
                    if (has)
                    {
                        Destroy(go);
                        unitViewDict.Remove(unit);
                    }
                    continue;
                }

                if (!has)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                    SetColor(go, ElementColor(unit.data.element));
                    unitViewDict.Add(unit, go);
                }
                go.transform.position = mapView.CellToWorld(unit.cellX, unit.cellY) + new Vector3(0f, 0.3f, 0f);
            }
        }

        private void UpdateHud()
        {
            if (hudText == null) return;
            var s = run.sim.state;
            string phaseLabel = phase == Phase.Prep ? "준비" : (phase == Phase.Done ? "종료" : "전투");
            string granted = lastOutcome != null ? lastOutcome.grantedUnitId : "-";
            string enemyLabel = lastOutcome != null ? lastOutcome.enemyLabel : "-";

            hudText.text =
                "웨이브 " + Mathf.Min(waveIndex, maxWave) + " / " + maxWave + "  [" + phaseLabel + "]  x" + speed + "\n"
                + "코스트 " + s.cost + " / " + s.costCap + "\n"
                + "처치 " + s.killedCount + "   누출 " + s.leakedCount + "\n"
                + "인벤토리 " + run.inventory.Count + "   최근 뽑기 " + granted + "\n"
                + "현재 적 " + enemyLabel;
        }

        // ---- 헬퍼 ----

        private static Color ElementColor(Element element)
        {
            switch (element)
            {
                case Element.Fire:     return new Color(0.90f, 0.45f, 0.20f);
                case Element.Ice:      return new Color(0.45f, 0.75f, 0.95f);
                case Element.Thunder:  return new Color(0.90f, 0.85f, 0.30f);
                case Element.Physical: return new Color(0.75f, 0.75f, 0.78f);
                case Element.Holy:     return new Color(0.95f, 0.92f, 0.70f);
                default:               return Color.white;
            }
        }

        private static void SetColor(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }
    }
}
