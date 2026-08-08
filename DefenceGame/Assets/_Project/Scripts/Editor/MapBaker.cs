using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Synthesis.Core.Map;
using Synthesis.Presentation;

namespace Synthesis.Editor
{
    // STEP 2/3. 에디터 툴 - MapView 에 CSV 기반 타일을 굽는다. 런타임이 아니라 편집 시점에 만든다.
    // 굽힌 타일은 씬(또는 프리팹)에 저장되어 손으로 편집할 수 있다.
    public static class MapBaker
    {
        public static void Bake(MapView view)
        {
            if (view == null) return;

            MapData map = view.LoadMapData();
            if (map == null)
            {
                Debug.LogError("[MapBaker] 맵 데이터를 읽지 못했습니다: " + view.mapId);
                return;
            }

            Clear(view);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Transform root = view.transform;
            for (int y = 0; y < map.height; ++y)
            {
                for (int x = 0; x < map.width; ++x)
                {
                    CellType cell = map.GetCell(x, y);
                    if (cell == CellType.Empty) continue;

                    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = "cell_" + x + "_" + y + "_" + cell;
                    tile.transform.SetParent(root, false);
                    tile.transform.localPosition = view.CellToWorld(x, y) + new Vector3(0f, -0.05f, 0f);
                    tile.transform.localScale = new Vector3(view.cellSize * 0.95f, 0.1f, view.cellSize * 0.95f);

                    Collider col = tile.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);

                    Renderer r = tile.GetComponent<Renderer>();
                    if (r != null && shader != null)
                    {
                        Material mat = new Material(shader);
                        Color color = MapView.CellColor(cell);
                        mat.color = color;
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                        r.sharedMaterial = mat;
                    }
                }
            }

            EditorUtility.SetDirty(view);
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            }
            Debug.Log("[MapBaker] 타일 굽기 완료: " + view.mapId + " (" + map.width + "x" + map.height + ")");
        }

        public static void Clear(MapView view)
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
    }

    [CustomEditor(typeof(MapView))]
    public sealed class MapViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            MapView view = (MapView)target;

            EditorGUILayout.Space();
            if (GUILayout.Button("CSV 에서 타일 굽기"))
            {
                MapBaker.Bake(view);
            }
            if (GUILayout.Button("타일 지우기"))
            {
                MapBaker.Clear(view);
            }
        }
    }
}
