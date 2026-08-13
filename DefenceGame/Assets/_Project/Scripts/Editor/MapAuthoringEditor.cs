using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Synthesis.Core;
using Synthesis.Core.Map;
using Synthesis.Data;
using Synthesis.Presentation;

namespace Synthesis.Editor
{
    // Scene 뷰에서 타일을 클릭해 이동 경로와 스폰 지점을 지정하는 맵 에디터.
    //   경로 모드: 좌클릭으로 다음 경로 셀 추가(순서대로). 마지막 셀을 다시 클릭하면 취소.
    //   스폰 모드: 경로 셀을 좌클릭하면 스폰 지점 토글.
    //   지우기 모드: 경로 셀을 좌클릭하면 경로에서 제거.
    // 완성한 경로는 "MapSO로 저장" 으로 저장하고, GameManager 의 Map Asset 에 넣으면 런타임이 그 경로를 쓴다.
    [CustomEditor(typeof(MapAuthoring))]
    public sealed class MapAuthoringEditor : UnityEditor.Editor
    {
        private enum Mode { Path, Spawn, Erase }
        private Mode mode = Mode.Path;
        private MapSO loadTarget;

        private static readonly Color GridColor = new Color(0.5f, 0.5f, 0.55f, 0.4f);
        private static readonly Color PathColor = new Color(0.35f, 0.55f, 0.95f, 0.55f);
        private static readonly Color SpawnColor = new Color(0.90f, 0.30f, 0.30f, 0.75f);
        private static readonly Color HoverColor = new Color(0.95f, 0.95f, 0.4f, 0.7f);

        [MenuItem("Synthesis/Create Map Authoring Object")]
        public static void CreateAuthoringObject()
        {
            GameObject go = new GameObject("MapAuthoring");
            go.AddComponent<MapAuthoring>();
            go.transform.position = Vector3.zero;
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Map Authoring");
            SceneView.RepaintAll();
            Debug.Log("[MapAuthoring] 저작 오브젝트 생성. Scene 뷰에서 타일을 클릭해 경로/스폰을 지정하세요.");
        }

        public override void OnInspectorGUI()
        {
            MapAuthoring m = (MapAuthoring)target;

            EditorGUILayout.HelpBox(
                "Scene 뷰에서 타일 클릭으로 편집.\n" +
                "경로: 좌클릭으로 순서대로 추가(마지막 재클릭=취소)\n" +
                "스폰: 경로 셀 좌클릭으로 토글\n" +
                "지우기: 경로 셀 좌클릭으로 제거", MessageType.Info);

            mode = (Mode)GUILayout.Toolbar((int)mode, new string[] { "경로", "스폰", "지우기" });

            EditorGUILayout.Space();
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("경로 " + m.path.Count + " 셀 / 스폰 " + m.spawnIndices.Count + " 곳");

            if (GUILayout.Button("경로 비우기"))
            {
                Undo.RecordObject(m, "Clear Map Path");
                m.path.Clear();
                m.spawnIndices.Clear();
                EditorUtility.SetDirty(m);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("MapSO로 저장"))
            {
                SaveToSO(m);
            }

            EditorGUILayout.BeginHorizontal();
            loadTarget = (MapSO)EditorGUILayout.ObjectField(loadTarget, typeof(MapSO), false);
            using (new EditorGUI.DisabledScope(loadTarget == null))
            {
                if (GUILayout.Button("불러오기", GUILayout.Width(80f))) LoadFromSO(m, loadTarget);
            }
            EditorGUILayout.EndHorizontal();

            if (GUI.changed) SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            MapAuthoring m = (MapAuthoring)target;
            Event e = Event.current;

            // 클릭이 선택 해제로 새지 않도록 기본 컨트롤을 잡는다.
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            Vector2Int cell;
            bool hasCell = TryGetCell(m, e.mousePosition, out cell);

            DrawGrid(m);
            DrawPath(m);
            DrawSpawns(m);
            if (hasCell) DrawCell(m, cell, HoverColor, 0.42f);

            // alt 는 카메라 조작이므로 건드리지 않는다.
            if (!e.alt && e.type == EventType.MouseDown && e.button == 0 && hasCell)
            {
                Undo.RecordObject(m, "Edit Map");
                ApplyClick(m, cell);
                EditorUtility.SetDirty(m);
                e.Use();
            }

            if (e.type == EventType.MouseMove) SceneView.RepaintAll();
        }

        private void ApplyClick(MapAuthoring m, Vector2Int cell)
        {
            if (mode == Mode.Path)
            {
                if (m.path.Count > 0 && m.path[m.path.Count - 1] == cell)
                {
                    RemoveAt(m, m.path.Count - 1); // 마지막 재클릭 = 취소
                    return;
                }
                if (m.IndexOfCell(cell) >= 0) return; // 이미 경로에 있으면 무시
                m.path.Add(cell);
            }
            else if (mode == Mode.Spawn)
            {
                int idx = m.IndexOfCell(cell);
                if (idx < 0) return; // 경로 셀만 스폰 가능
                if (m.spawnIndices.Contains(idx)) m.spawnIndices.Remove(idx);
                else m.spawnIndices.Add(idx);
            }
            else // Erase
            {
                int idx = m.IndexOfCell(cell);
                if (idx >= 0) RemoveAt(m, idx);
            }
        }

        // 경로에서 index 제거 + 스폰 인덱스 보정(제거/시프트).
        private void RemoveAt(MapAuthoring m, int index)
        {
            m.path.RemoveAt(index);
            List<int> fixedList = new List<int>();
            foreach (int s in m.spawnIndices)
            {
                if (s == index) continue;
                fixedList.Add(s > index ? s - 1 : s);
            }
            m.spawnIndices = fixedList;
        }

        private bool TryGetCell(MapAuthoring m, Vector2 mousePos, out Vector2Int cell)
        {
            cell = default;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            float enter;
            if (!plane.Raycast(ray, out enter)) return false;
            Vector3 wp = ray.GetPoint(enter);
            int x = Mathf.RoundToInt(wp.x / m.cellSize);
            int y = Mathf.RoundToInt(-wp.z / m.cellSize);
            cell = new Vector2Int(x, y);
            return m.InBounds(cell);
        }

        private void DrawGrid(MapAuthoring m)
        {
            Handles.color = GridColor;
            for (int x = 0; x <= m.gridWidth; ++x)
            {
                Vector3 a = new Vector3(x * m.cellSize - m.cellSize * 0.5f, 0f, m.cellSize * 0.5f);
                Vector3 b = new Vector3(x * m.cellSize - m.cellSize * 0.5f, 0f, -(m.gridHeight - 0.5f) * m.cellSize);
                Handles.DrawLine(a, b);
            }
            for (int y = 0; y <= m.gridHeight; ++y)
            {
                Vector3 a = new Vector3(-m.cellSize * 0.5f, 0f, -(y * m.cellSize - m.cellSize * 0.5f));
                Vector3 b = new Vector3((m.gridWidth - 0.5f) * m.cellSize, 0f, -(y * m.cellSize - m.cellSize * 0.5f));
                Handles.DrawLine(a, b);
            }
        }

        private void DrawPath(MapAuthoring m)
        {
            for (int i = 0; i < m.path.Count; ++i)
            {
                DrawCell(m, m.path[i], PathColor, 0.4f);
                Handles.Label(m.CellToWorld(m.path[i].x, m.path[i].y) + new Vector3(0f, 0.05f, 0f), i.ToString());
            }

            Handles.color = PathColor;
            for (int i = 0; i < m.path.Count; ++i)
            {
                Vector2Int a = m.path[i];
                Vector2Int b = m.path[(i + 1) % m.path.Count]; // 마지막 -> 첫 셀(루프)
                if (m.path.Count < 2) break;
                Handles.DrawLine(m.CellToWorld(a.x, a.y), m.CellToWorld(b.x, b.y));
            }
        }

        private void DrawSpawns(MapAuthoring m)
        {
            Handles.color = SpawnColor;
            foreach (int idx in m.spawnIndices)
            {
                if (idx < 0 || idx >= m.path.Count) continue;
                Vector2Int c = m.path[idx];
                Handles.DrawSolidDisc(m.CellToWorld(c.x, c.y) + new Vector3(0f, 0.02f, 0f), Vector3.up, m.cellSize * 0.28f);
            }
        }

        private void DrawCell(MapAuthoring m, Vector2Int cell, Color color, float half)
        {
            Vector3 c = m.CellToWorld(cell.x, cell.y);
            float h = m.cellSize * half;
            Vector3[] verts =
            {
                c + new Vector3(-h, 0f, -h),
                c + new Vector3(h, 0f, -h),
                c + new Vector3(h, 0f, h),
                c + new Vector3(-h, 0f, h)
            };
            Handles.DrawSolidRectangleWithOutline(verts, color, new Color(color.r, color.g, color.b, 0.9f));
        }

        private void SaveToSO(MapAuthoring m)
        {
            if (m.path.Count < 2)
            {
                EditorUtility.DisplayDialog("맵 저장", "경로 셀이 최소 2개는 있어야 합니다.", "확인");
                return;
            }

            const string dir = "Assets/_Project/Data/Maps";
            EnsureFolder(dir);
            string path = dir + "/Map_authored_" + m.gameObject.name + ".asset";

            MapSO so = AssetDatabase.LoadAssetAtPath<MapSO>(path);
            bool created = false;
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<MapSO>();
                created = true;
            }

            so.gridWidth = m.gridWidth;
            so.gridHeight = m.gridHeight;
            so.waypointX = new int[m.path.Count];
            so.waypointY = new int[m.path.Count];
            for (int i = 0; i < m.path.Count; ++i)
            {
                so.waypointX[i] = m.path[i].x;
                so.waypointY[i] = m.path[i].y;
            }
            so.spawnIndices = m.spawnIndices.ToArray();
            so.statueX = new int[0];
            so.statueY = new int[0];

            MapGenParams p = RuntimeDataLoader.LoadMapGenParams();
            so.statueHpRaw = Fixed.FromInt(p.statueHp).raw;
            so.coverageRadius = p.coverageRadius;

            if (created) AssetDatabase.CreateAsset(so, path);
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MapAuthoring] 저장: " + path + " (경로 " + m.path.Count + " / 스폰 " + m.spawnIndices.Count + ")");
            EditorGUIUtility.PingObject(so);
        }

        private void LoadFromSO(MapAuthoring m, MapSO so)
        {
            Undo.RecordObject(m, "Load Map");
            m.gridWidth = so.gridWidth;
            m.gridHeight = so.gridHeight;
            m.path.Clear();
            int n = so.waypointX != null ? so.waypointX.Length : 0;
            for (int i = 0; i < n; ++i) m.path.Add(new Vector2Int(so.waypointX[i], so.waypointY[i]));
            m.spawnIndices = new List<int>(so.spawnIndices ?? new int[0]);
            EditorUtility.SetDirty(m);
            SceneView.RepaintAll();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Directory.CreateDirectory(Path.GetFullPath(path));
            AssetDatabase.Refresh();
        }
    }
}
