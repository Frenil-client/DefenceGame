using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Synthesis.Core.Map;
using Synthesis.Presentation;

namespace Synthesis.Editor
{
    // STEP 1/2. 에디터 툴 - LoopMapView 에 시드 기반 루프 맵 타일을 굽는다(런타임 아님).
    public static class LoopMapBaker
    {
        public static void Bake(LoopMapView view)
        {
            if (view == null) return;

            MapGenParams p = RuntimeDataLoader.LoadMapGenParams();
            LoopMap map = LoopMapGenerator.Generate(p, view.seed);

            Clear(view);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            // 스폰 위치(웨이포인트 인덱스)를 셀 좌표 집합으로
            HashSet<int> spawnKeys = new HashSet<int>();
            foreach (int idx in map.spawnIndexList)
            {
                if (idx < 0 || idx >= map.loopWaypointList.Count) continue;
                GridPos s = map.loopWaypointList[idx];
                spawnKeys.Add(s.y * map.gridWidth + s.x);
            }

            // 배치(BUILD) 타일: 조금 올려서 놓을 칸이 읽히게
            foreach (var c in map.buildTileList)
            {
                MakeTile(view, shader, c.x, c.y, 0.0f, 0.2f, LoopMapView.TileColor(LoopTile.Build));
            }

            // 경로(PATH) 타일: 평평하게. 스폰 칸은 빨강
            foreach (var c in map.loopWaypointList)
            {
                bool isSpawn = spawnKeys.Contains(c.y * map.gridWidth + c.x);
                Color color = isSpawn ? LoopMapView.SpawnColor() : LoopMapView.TileColor(LoopTile.Path);
                MakeTile(view, shader, c.x, c.y, -0.05f, 0.1f, color);
            }

            EditorUtility.SetDirty(view);
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            }
            Debug.Log("[LoopMapBaker] 시드 " + map.seed
                + " 둘레 " + map.perimeter
                + " 코너 " + map.cornerIndexList.Count
                + " 배치면적 " + map.buildArea
                + " 커버지수 " + map.coverageIndex
                + (map.isFallback ? " (폴백)" : ""));
        }

        private static void MakeTile(LoopMapView view, Shader shader, int x, int y, float yOffset, float height, Color color)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "tile_" + x + "_" + y;
            tile.transform.SetParent(view.transform, false);
            tile.transform.localPosition = view.CellToWorld(x, y) + new Vector3(0f, yOffset, 0f);
            tile.transform.localScale = new Vector3(view.cellSize * 0.92f, height, view.cellSize * 0.92f);

            Collider col = tile.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            Renderer r = tile.GetComponent<Renderer>();
            if (r != null && shader != null)
            {
                Material mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                r.sharedMaterial = mat;
            }
        }

        public static void Clear(LoopMapView view)
        {
            if (view == null) return;
            List<GameObject> toDelete = new List<GameObject>();
            foreach (Transform child in view.transform)
            {
                toDelete.Add(child.gameObject);
            }
            foreach (var go in toDelete)
            {
                Object.DestroyImmediate(go);
            }
        }

        [MenuItem("Synthesis/Create Loop Map Preview")]
        public static void CreatePreviewScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject viewGo = new GameObject("LoopMapView");
            LoopMapView view = viewGo.AddComponent<LoopMapView>();
            view.seed = 1;
            view.cellSize = 1f;
            Bake(view);

            MapGenParams p = RuntimeDataLoader.LoadMapGenParams();
            Vector3 center = view.CellToWorld(p.gridWidth / 2, p.gridHeight / 2);
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.13f);
            camGo.transform.position = center + new Vector3(0f, 14f, 9f);
            camGo.transform.LookAt(center);

            Debug.Log("[Synthesis] 루프 맵 미리보기 씬 생성. LoopMapView 를 선택해 시드를 바꾸고 생성하세요.");
        }
    }

    [CustomEditor(typeof(LoopMapView))]
    public sealed class LoopMapViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            LoopMapView view = (LoopMapView)target;

            EditorGUILayout.Space();
            if (GUILayout.Button("시드로 생성"))
            {
                LoopMapBaker.Bake(view);
            }
            if (GUILayout.Button("시드 +1 재생성"))
            {
                ++view.seed;
                LoopMapBaker.Bake(view);
            }
            if (GUILayout.Button("지우기"))
            {
                LoopMapBaker.Clear(view);
            }
        }
    }
}
