using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Synthesis.Core.Map;
using Synthesis.Presentation;

namespace Synthesis.Editor
{
    // STEP 2/3(재작업). 에디터 툴 - 게임 플레이 씬을 매니저 계층으로 구성한다.
    // 매니저/뷰는 FindFirstObjectByType 로 서로를 찾으므로 별도 와이어링이 필요없다.
    public static class LoopGameSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/LoopGame.unity";

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

            MapGenParams p = RuntimeDataLoader.LoadMapGenParams();
            Vector3 center = view.CellToWorld(p.gridWidth / 2, p.gridHeight / 2);
            GameObject camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(env.transform, false);
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.13f);
            camGo.transform.position = center + new Vector3(0f, 14f, 9f);
            camGo.transform.LookAt(center);

            // ---- Managers ----
            GameObject managers = new GameObject("Managers");
            GameObject gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(managers.transform, false);
            gmGo.AddComponent<GameManager>();
            GameObject wmGo = new GameObject("WaveManager");
            wmGo.transform.SetParent(managers.transform, false);
            wmGo.AddComponent<WaveManager>();
            // 유닛은 중앙에서 바깥으로 자동 배치되므로 클릭 배치(PlacementManager)는 씬에 두지 않는다.

            // ---- View (엔티티 렌더 + 풀) ----
            GameObject viewGo = new GameObject("EntityView");
            viewGo.AddComponent<EntityView>();

            // ---- UI ----
            GameObject uiGo = new GameObject("UI");
            uiGo.AddComponent<HudView>();
            uiGo.AddComponent<InventoryView>();
            uiGo.AddComponent<MonsterHealthBarHud>();

            // ---- EventSystem (UGUI 입력) ----
            // 프로젝트가 Input System(신) 패키지를 쓰므로 InputSystemUIInputModule 을 붙인다.
            // 레거시 StandaloneInputModule 은 신 입력 활성 시 런타임 예외를 던진다.
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            System.Type inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null) es.AddComponent(inputModuleType);
            else es.AddComponent<StandaloneInputModule>();

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
    }
}
