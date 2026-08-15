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

        private bool painting;
        private Vector2Int lastPaintCell;

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
                "Scene 뷰에서 타일 클릭/드래그로 편집(드래그로 연속 칠하기).\n" +
                "경로: 좌클릭/드래그로 순서대로 추가(단일 클릭 시 마지막 재클릭=취소)\n" +
                "스폰: 경로 셀 좌클릭 토글(드래그로 여러 셀 지정)\n" +
                "지우기: 경로 셀 좌클릭/드래그로 제거\n" +
                "저장 시 닫힌 루프/끊김/스폰 유무를 검사합니다.", MessageType.Info);

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

            // alt 는 카메라 조작이므로 건드리지 않는다. 좌클릭=클릭 편집, 좌드래그=칠하기.
            if (!e.alt && e.button == 0)
            {
                if (e.type == EventType.MouseDown && hasCell)
                {
                    Undo.RecordObject(m, "Edit Map");
                    ApplyClick(m, cell);
                    painting = true;
                    lastPaintCell = cell;
                    EditorUtility.SetDirty(m);
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && painting && hasCell && cell != lastPaintCell)
                {
                    Undo.RecordObject(m, "Paint Map");
                    Paint(m, cell);
                    lastPaintCell = cell;
                    EditorUtility.SetDirty(m);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp)
                {
                    painting = false;
                }
            }

            if (e.type == EventType.MouseMove) SceneView.RepaintAll();
        }

        // 드래그로 칠할 때: 경로는 직선 보간으로 빈 셀까지 이어붙이고(빠른 드래그 대비), 지우기는 제거, 스폰은 설정.
        private void Paint(MapAuthoring m, Vector2Int cell)
        {
            if (mode == Mode.Path)
            {
                foreach (Vector2Int step in LineCells(lastPaintCell, cell))
                {
                    if (m.InBounds(step) && m.IndexOfCell(step) < 0) m.path.Add(step);
                }
            }
            else if (mode == Mode.Spawn)
            {
                int idx = m.IndexOfCell(cell);
                if (idx >= 0 && !m.spawnIndices.Contains(idx)) m.spawnIndices.Add(idx);
            }
            else // Erase
            {
                int idx = m.IndexOfCell(cell);
                if (idx >= 0) RemoveAt(m, idx);
            }
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

        // a(제외)에서 b(포함)까지 격자 직선 위의 셀들(Bresenham). 드래그 보간용.
        private static List<Vector2Int> LineCells(Vector2Int a, Vector2Int b)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            int dx = Mathf.Abs(b.x - a.x);
            int dy = Mathf.Abs(b.y - a.y);
            int sx = a.x < b.x ? 1 : -1;
            int sy = a.y < b.y ? 1 : -1;
            int err = dx - dy;
            int x = a.x, y = a.y;
            int guard = 0;
            while (guard++ < 4096)
            {
                if (!(x == a.x && y == a.y)) cells.Add(new Vector2Int(x, y));
                if (x == b.x && y == b.y) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }
            }
            return cells;
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
            cell = m.WorldToCell(ray.GetPoint(enter));
            return m.InBounds(cell);
        }

        // 그리드는 CellToWorld(원점 중심) 기준으로 그린다.
        private void DrawGrid(MapAuthoring m)
        {
            Handles.color = GridColor;
            float half = m.cellSize * 0.5f;
            Vector3 origin = m.CellToWorld(0, 0);
            float minX = origin.x - half;
            float topZ = origin.z + half;

            for (int x = 0; x <= m.gridWidth; ++x)
            {
                float px = minX + x * m.cellSize;
                Handles.DrawLine(new Vector3(px, 0f, topZ), new Vector3(px, 0f, topZ - m.gridHeight * m.cellSize));
            }
            for (int y = 0; y <= m.gridHeight; ++y)
            {
                float pz = topZ - y * m.cellSize;
                Handles.DrawLine(new Vector3(minX, 0f, pz), new Vector3(minX + m.gridWidth * m.cellSize, 0f, pz));
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

        // 저장 전 경로 완전성 검사: 닫힌 루프인지, 끊긴 구간/중복 없는지, 스폰이 있는지.
        private bool ValidatePath(MapAuthoring m, out string issues)
        {
            List<string> problems = new List<string>();

            if (m.path.Count < 3)
            {
                problems.Add("경로가 3셀 미만입니다 (루프가 되려면 최소 3셀).");
            }

            HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
            foreach (var c in m.path)
            {
                if (!seen.Add(c)) { problems.Add("같은 셀이 두 번 이상 들어갔습니다: " + c.x + "," + c.y); break; }
            }

            // 이웃 연결성(닫힘 포함): 이웃한 웨이포인트는 상하좌우/대각으로 인접해야 한다.
            if (m.path.Count >= 2)
            {
                for (int i = 0; i < m.path.Count; ++i)
                {
                    Vector2Int a = m.path[i];
                    Vector2Int b = m.path[(i + 1) % m.path.Count];
                    int cheb = Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
                    if (cheb != 1)
                    {
                        bool closing = (i == m.path.Count - 1);
                        problems.Add(closing
                            ? "루프가 닫히지 않았습니다 (마지막 셀과 첫 셀이 떨어져 있음)."
                            : "끊긴 구간이 있습니다 (" + i + "번과 " + (i + 1) + "번 셀이 인접하지 않음).");
                        break;
                    }
                }
            }

            if (m.spawnIndices.Count == 0)
            {
                problems.Add("스폰 지점이 없습니다 (최소 1곳 필요).");
            }

            issues = string.Join("\n", problems);
            return problems.Count == 0;
        }

        private void SaveToSO(MapAuthoring m)
        {
            string issues;
            if (!ValidatePath(m, out issues))
            {
                EditorUtility.DisplayDialog("맵 저장 불가", "경로가 완전하지 않습니다:\n\n" + issues, "확인");
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
