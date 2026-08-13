using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core.Map;

namespace Synthesis.Presentation
{
    // 런타임에 GameManager.Context 의 맵에서 타일을 직접 그린다.
    // 에디터에서 구운 타일에 의존하지 않으므로, 시뮬이 쓰는 맵과 화면 타일이 항상 일치한다(생성/베이크 불일치 제거).
    public sealed class LoopMapRuntimeRenderer : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private LoopMapView mapView;

        private bool drawn;
        private Shader tileShader;
        private readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();

        private static readonly Color StatueColor = new Color(0.55f, 0.35f, 0.75f);

        private void Start()
        {
            if (game == null) game = Object.FindFirstObjectByType<GameManager>();
            if (mapView == null) mapView = Object.FindFirstObjectByType<LoopMapView>();
        }

        private void Update()
        {
            if (drawn) return;
            if (game == null || game.Context == null || !game.Context.IsValid() || mapView == null) return;
            Draw(game.Context.map);
            drawn = true;
        }

        private void Draw(LoopMap map)
        {
            ClearChildren(mapView.transform); // 에디터 베이크 타일 제거(중복 방지)

            HashSet<int> spawnKeys = new HashSet<int>();
            foreach (int idx in map.spawnIndexList)
            {
                if (idx < 0 || idx >= map.loopWaypointList.Count) continue;
                GridPos s = map.loopWaypointList[idx];
                spawnKeys.Add(s.y * map.gridWidth + s.x);
            }
            HashSet<int> statueKeys = new HashSet<int>();
            foreach (var s in map.statueList) statueKeys.Add(s.y * map.gridWidth + s.x);

            // 배치(BUILD) 타일: 조금 올림. 석상 칸은 보라.
            foreach (var c in map.buildTileList)
            {
                bool isStatue = statueKeys.Contains(c.y * map.gridWidth + c.x);
                Color color = isStatue ? StatueColor : LoopMapView.TileColor(LoopTile.Build);
                MakeTile(c.x, c.y, 0.0f, 0.2f, color);
            }

            // 경로(PATH) 타일: 평평. 스폰 칸은 빨강.
            foreach (var c in map.loopWaypointList)
            {
                bool isSpawn = spawnKeys.Contains(c.y * map.gridWidth + c.x);
                Color color = isSpawn ? LoopMapView.SpawnColor() : LoopMapView.TileColor(LoopTile.Path);
                MakeTile(c.x, c.y, -0.05f, 0.1f, color);
            }
        }

        private void MakeTile(int x, int y, float yOffset, float height, Color color)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "tile_" + x + "_" + y;
            tile.transform.SetParent(mapView.transform, false);
            tile.transform.localPosition = mapView.CellToWorld(x, y) + new Vector3(0f, yOffset, 0f);
            tile.transform.localScale = new Vector3(mapView.cellSize * 0.92f, height, mapView.cellSize * 0.92f);

            Collider col = tile.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = tile.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = GetMaterial(color);
        }

        private Material GetMaterial(Color color)
        {
            Material cached;
            if (matCache.TryGetValue(color, out cached)) return cached;

            if (tileShader == null)
            {
                tileShader = Shader.Find("Universal Render Pipeline/Lit");
                if (tileShader == null) tileShader = Shader.Find("Standard");
            }
            Material mat = new Material(tileShader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            matCache[color] = mat;
            return mat;
        }

        private static void ClearChildren(Transform root)
        {
            List<GameObject> toDelete = new List<GameObject>();
            foreach (Transform child in root) toDelete.Add(child.gameObject);
            foreach (var go in toDelete) Destroy(go);
        }
    }
}
