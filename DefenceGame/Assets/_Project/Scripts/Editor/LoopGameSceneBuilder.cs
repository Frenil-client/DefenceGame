using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Synthesis.Presentation;

namespace Synthesis.Editor
{
    // STEP 2/3(재작업). 에디터 툴 - 게임 플레이 씬을 매니저 계층으로 구성한다.
    // 런타임 생성/탐색을 쓰지 않으므로, 필요한 오브젝트를 씬에 미리 배치하고 참조를 코드로 와이어링한다.
    // (먼저 Synthesis > Build UI Prefabs 로 HUD/팝업 프리팹을 만들어 두어야 UI 가 배치된다.)
    public static class LoopGameSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/LoopGame.unity";
        private const string UiDir = "Assets/_Project/Resources/UI";
        private const string HudDir = "Assets/_Project/Resources/UI/HUD";

        [MenuItem("Synthesis/Create Gameplay Scene")]
        public static void CreateGameplayScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- Environment ----
            GameObject env = new GameObject("Environment");

            GameObject lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(env.transform, false);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject mapGo = new GameObject("Map");
            mapGo.transform.SetParent(env.transform, false);
            LoopMapView view = mapGo.AddComponent<LoopMapView>();
            view.seed = 1;
            view.cellSize = 1f;
            LoopMapBaker.Bake(view);

            // 맵은 원점 중심으로 그려지므로 카메라도 원점을 -z 쪽 위에서 내려다본다. Y축 회전(요)=0.
            GameObject camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(env.transform, false);
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.13f);
            camGo.transform.position = new Vector3(0f, 14f, -9f);
            float pitch = Mathf.Atan2(14f, 9f) * Mathf.Rad2Deg;
            camGo.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);

            // ---- Managers ----
            GameObject managers = new GameObject("Managers");

            GameObject gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(managers.transform, false);
            GameManager gm = gmGo.AddComponent<GameManager>();

            GameObject wmGo = new GameObject("WaveManager");
            wmGo.transform.SetParent(managers.transform, false);
            WaveManager wm = wmGo.AddComponent<WaveManager>();

            GameObject rendGo = new GameObject("MapRenderer");
            rendGo.transform.SetParent(managers.transform, false);
            LoopMapRuntimeRenderer renderer = rendGo.AddComponent<LoopMapRuntimeRenderer>();

            // ---- View (엔티티 렌더 + 풀) ----
            GameObject viewGo = new GameObject("EntityView");
            viewGo.transform.SetParent(managers.transform, false);
            EntityView entityView = viewGo.AddComponent<EntityView>();

            // ---- UI ----
            Canvas baseCanvas = CreateBaseCanvas();
            UIManager uiManager = baseCanvas.gameObject.AddComponent<UIManager>();

            // HUD 프리팹을 Canvas 아래에 인스턴스로 배치(미리 배치 방식, 런타임 마운트 아님).
            HudView hud = InstantiateHudPrefab<HudView>("HudView", baseCanvas.transform);
            InventoryView inv = InstantiateHudPrefab<InventoryView>("InventoryView", baseCanvas.transform);
            MonsterHealthBarHud hpBars = InstantiateHudPrefab<MonsterHealthBarHud>("MonsterHealthBarHud", baseCanvas.transform);

            // ---- EventSystem (UGUI 입력, 신 Input System) ----
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            System.Type inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null) es.AddComponent(inputModuleType);
            else es.AddComponent<StandaloneInputModule>();

            // ---- 참조 와이어링 (인스펙터 직접 등록) ----
            SetRef(gm, "mapView", view);
            SetRef(wm, "game", gm);
            SetRef(renderer, "game", gm);
            SetRef(renderer, "mapView", view);
            SetRef(entityView, "game", gm);
            SetRef(uiManager, "baseCanvas", baseCanvas);
            SetPopupPrefabs(uiManager);
            if (hud != null) { SetRef(hud, "game", gm); SetRef(hud, "waves", wm); }
            if (inv != null) SetRef(inv, "game", gm);
            if (hpBars != null) { SetRef(hpBars, "game", gm); SetRef(hpBars, "cam", cam); SetRef(hpBars, "baseCanvas", baseCanvas); }

            string dir = Path.GetDirectoryName(ScenePath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(Path.GetFullPath(dir));
                AssetDatabase.Refresh();
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[Synthesis] 게임 플레이 씬 생성: " + ScenePath + "  (Play 를 누르세요)");
        }

        private static Canvas CreateBaseCanvas()
        {
            GameObject go = new GameObject("UI Canvas");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static T InstantiateHudPrefab<T>(string name, Transform parent) where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudDir + "/" + name + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning("[SceneBuilder] HUD 프리팹 없음: " + HudDir + "/" + name + ".prefab (먼저 Build UI Prefabs 실행)");
                return null;
            }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return go.GetComponent<T>();
        }

        // UIManager.popupPrefabs 에 팝업 프리팹(UIPanel)을 등록한다.
        private static void SetPopupPrefabs(UIManager manager)
        {
            UIPanel combine = AssetDatabase.LoadAssetAtPath<UIPanel>(UiDir + "/CombinePopup.prefab");
            UIPanel sample = AssetDatabase.LoadAssetAtPath<UIPanel>(UiDir + "/SamplePopup.prefab");

            var so = new SerializedObject(manager);
            var arr = so.FindProperty("popupPrefabs");
            arr.ClearArray();
            AddIfNotNull(arr, combine);
            AddIfNotNull(arr, sample);
            so.ApplyModifiedProperties();
        }

        private static void AddIfNotNull(SerializedProperty arr, Object value)
        {
            if (value == null) return;
            arr.arraySize += 1;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = value;
        }

        private static void SetRef(Component comp, string prop, Object value)
        {
            var so = new SerializedObject(comp);
            var p = so.FindProperty(prop);
            if (p != null)
            {
                p.objectReferenceValue = value;
                so.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[SceneBuilder] 프로퍼티 없음: " + comp.GetType().Name + "." + prop);
            }
        }
    }
}
