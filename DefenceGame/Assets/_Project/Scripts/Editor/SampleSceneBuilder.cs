using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Synthesis.Core.Map;
using Synthesis.Presentation;

namespace Synthesis.Editor
{
    // STEP 2/3. 에디터 툴 - 샘플 씬을 authored 오브젝트로 구성한다.
    // 맵 타일/카메라/조명은 씬에 저장되고(런타임 생성 아님), 부트스트랩은 그것들을 소비만 한다.
    // 씬에서 MapView 를 선택해 타일을 다시 굽거나 손으로 편집할 수 있다.
    public static class SampleSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sample.unity";

        [MenuItem("Synthesis/Create Sample Scene")]
        public static void CreateSampleScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 조명
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 맵 (타일을 지금 굽는다 = 씬에 저장됨)
            GameObject mapGo = new GameObject("MapView");
            MapView mapView = mapGo.AddComponent<MapView>();
            mapView.mapId = "map01";
            mapView.cellSize = 1f;
            MapBaker.Bake(mapView);

            // 카메라 (맵 중심을 바라보는 고정 쿼터뷰)
            Vector3 center = Vector3.zero;
            MapData map = mapView.LoadMapData();
            if (map != null) center = mapView.CellToWorld(map.width / 2f, map.height / 2f);

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.13f);
            camGo.transform.position = center + new Vector3(0f, 9f, 7f);
            camGo.transform.LookAt(center);

            // 부트스트랩 (동적 요소와 HUD 만 담당)
            GameObject bootGo = new GameObject("SampleSceneBootstrap");
            SampleSceneBootstrap boot = bootGo.AddComponent<SampleSceneBootstrap>();
            SerializedObject so = new SerializedObject(boot);
            SerializedProperty prop = so.FindProperty("mapView");
            if (prop != null)
            {
                prop.objectReferenceValue = mapView;
                so.ApplyModifiedProperties();
            }

            string dir = Path.GetDirectoryName(ScenePath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(Path.GetFullPath(dir));
                AssetDatabase.Refresh();
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[Synthesis] 샘플 씬 생성: " + ScenePath + "  (열려 있는 이 씬에서 Play 를 누르세요)");
        }
    }
}
