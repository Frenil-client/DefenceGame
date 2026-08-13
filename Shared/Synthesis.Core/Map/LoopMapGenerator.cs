using System.Collections.Generic;
using Synthesis.Core.Random;

namespace Synthesis.Core.Map
{
    // STEP 1. 기반 도구 - 시드 기반 루프 맵 생성기 (MAP_SPEC.md 3).
    // 기본 링에서 시작해 요철을 넣는다. DeterministicRandom 만 쓴다(같은 시드 = 같은 맵, MAP_SPEC 7).
    public static class LoopMapGenerator
    {
        public static LoopMap Generate(MapGenParams p, long baseSeed)
        {
            for (int retry = 0; retry <= p.maxRetry; ++retry)
            {
                long seed = baseSeed + retry;
                DeterministicRandom rng = new DeterministicRandom(seed);

                List<GridPos> verts = RectVertices(p);
                int bumpCount = rng.NextInt(p.bumpCountMin, p.bumpCountMax + 1);

                int applied = 0;
                int attempts = 0;
                int maxAttempts = (bumpCount + 1) * 12;
                while (applied < bumpCount && attempts < maxAttempts)
                {
                    ++attempts;
                    List<GridPos> candidate = TryBump(verts, p, rng);
                    if (candidate != null)
                    {
                        verts = candidate;
                        ++applied;
                    }
                }

                LoopMap map = BuildFromVertices(verts, p, seed, false);
                if (LoopMapValidator.Validate(map, p, null))
                {
                    return map;
                }
            }
            return FallbackMap.Create(p, baseSeed);
        }

        // 요철 없는 기본 직사각형 링 맵. 시드는 석상 배치 등 결정적 요소에만 쓰인다.
        // 맵 에디트/기본 옵션에서 변주 없는 깔끔한 사각 루프가 필요할 때 쓴다.
        public static LoopMap GenerateRectangular(MapGenParams p, long seed)
        {
            List<GridPos> verts = RectVertices(p);
            return BuildFromVertices(verts, p, seed, false);
        }

        public static List<GridPos> RectVertices(MapGenParams p)
        {
            int left = p.baseRingInset;
            int top = p.baseRingInset;
            int right = p.gridWidth - 1 - p.baseRingInset;
            int bottom = p.gridHeight - 1 - p.baseRingInset;

            List<GridPos> verts = new List<GridPos>();
            verts.Add(new GridPos(left, top));
            verts.Add(new GridPos(right, top));
            verts.Add(new GridPos(right, bottom));
            verts.Add(new GridPos(left, bottom));
            return verts;
        }

        // 한 변에 요철을 넣은 새 꼭짓점 목록을 반환. 불가능하면 null.
        private static List<GridPos> TryBump(List<GridPos> verts, MapGenParams p, DeterministicRandom rng)
        {
            int n = verts.Count;
            int i = rng.NextInt(n);
            GridPos a = verts[i];
            GridPos b = verts[(i + 1) % n];
            int d = rng.NextInt(1, p.bumpDistMax + 1);
            bool positive = rng.NextInt(2) == 0;

            List<GridPos> inserted = new List<GridPos>();

            if (a.y == b.y)
            {
                int low = a.x < b.x ? a.x : b.x;
                int high = a.x > b.x ? a.x : b.x;
                if (high - low < 4) return null;

                int ra = low + 1 + rng.NextInt(high - low - 3);
                int rb = ra + 2 + rng.NextInt(high - ra - 2);
                int off = positive ? d : -d;
                int y = a.y;

                int p1x = b.x > a.x ? ra : rb;
                int p2x = b.x > a.x ? rb : ra;
                inserted.Add(new GridPos(p1x, y));
                inserted.Add(new GridPos(p1x, y + off));
                inserted.Add(new GridPos(p2x, y + off));
                inserted.Add(new GridPos(p2x, y));
            }
            else
            {
                int low = a.y < b.y ? a.y : b.y;
                int high = a.y > b.y ? a.y : b.y;
                if (high - low < 4) return null;

                int ra = low + 1 + rng.NextInt(high - low - 3);
                int rb = ra + 2 + rng.NextInt(high - ra - 2);
                int off = positive ? d : -d;
                int x = a.x;

                int p1y = b.y > a.y ? ra : rb;
                int p2y = b.y > a.y ? rb : ra;
                inserted.Add(new GridPos(x, p1y));
                inserted.Add(new GridPos(x + off, p1y));
                inserted.Add(new GridPos(x + off, p2y));
                inserted.Add(new GridPos(x, p2y));
            }

            List<GridPos> candidate = new List<GridPos>(verts);
            candidate.InsertRange(i + 1, inserted);

            List<GridPos> cells = LoopMapGeometry.Rasterize(candidate, null);
            if (!LoopMapGeometry.IsCleanLoop(cells, p.gridWidth, p.gridHeight, p.minLaneGap))
            {
                return null;
            }
            return candidate;
        }

        public static LoopMap BuildFromVertices(List<GridPos> verts, MapGenParams p, long seed, bool isFallback)
        {
            LoopMap map = new LoopMap();
            map.seed = seed;
            map.gridWidth = p.gridWidth;
            map.gridHeight = p.gridHeight;
            map.tileGrid = new LoopTile[p.gridWidth * p.gridHeight];
            map.isFallback = isFallback;

            List<int> corners = new List<int>();
            List<GridPos> cells = LoopMapGeometry.Rasterize(verts, corners);
            map.loopWaypointList = cells;
            map.cornerIndexList = corners;
            map.perimeter = cells.Count;

            foreach (var c in cells)
            {
                map.tileGrid[c.y * p.gridWidth + c.x] = LoopTile.Path;
            }

            // 루프 내부(형태 지표/커버 기준)
            List<GridPos> interior = LoopMapGeometry.FindInterior(cells, p.gridWidth, p.gridHeight);
            map.interiorTileList = interior;
            map.interiorArea = interior.Count;

            // 배치 가능 = 경로가 아닌 모든 셀 (루프 안팎 모두, 몬스터 라인 밖에도 배치 가능)
            for (int y = 0; y < p.gridHeight; ++y)
            {
                for (int x = 0; x < p.gridWidth; ++x)
                {
                    if (map.tileGrid[y * p.gridWidth + x] == LoopTile.Path) continue;
                    map.tileGrid[y * p.gridWidth + x] = LoopTile.Build;
                    map.buildTileList.Add(new GridPos(x, y));
                }
            }
            map.buildArea = map.buildTileList.Count;

            // 스폰: 웨이포인트를 spawnCount 등분한 지점 (MAP_SPEC 3-4)
            int count = p.spawnCount > 0 ? p.spawnCount : 1;
            for (int k = 0; k < count; ++k)
            {
                int idx = (int)((long)k * cells.Count / count);
                map.spawnIndexList.Add(idx);
            }

            // 석상: 맵 외곽(루프 외부 배치칸)에 statueCount 개를 서로 떨어뜨려 놓는다 (SPEC 3-9).
            map.statueHp = Fixed.FromInt(p.statueHp);
            PlaceStatues(map, p, seed);

            int zeroCover;
            map.coverageIndex = CoverageIndex.Compute(map, p.coverageRadius, out zeroCover);
            return map;
        }

        // 저장된 경로 셀들로부터 LoopMap 을 복원한다(생성이 아니라 로드용).
        // SO/파일에 담아둔 authored 맵을 이 메서드로 되살린다. 시드/생성 로직을 타지 않으므로 어긋날 여지가 없다.
        // 경로(cells)와 스폰 인덱스, 석상만 저장하고 배치칸/내부/커버는 여기서 재계산한다(모순 방지).
        public static LoopMap FromCells(int gridWidth, int gridHeight, List<GridPos> cells,
            List<int> spawnIndices, List<GridPos> statues, Fixed statueHp, int coverageRadius)
        {
            LoopMap map = new LoopMap();
            map.seed = 0;
            map.gridWidth = gridWidth;
            map.gridHeight = gridHeight;
            map.tileGrid = new LoopTile[gridWidth * gridHeight];
            map.isFallback = false;

            map.loopWaypointList = cells;
            map.perimeter = cells.Count;

            foreach (var c in cells)
            {
                if (c.x < 0 || c.y < 0 || c.x >= gridWidth || c.y >= gridHeight) continue;
                map.tileGrid[c.y * gridWidth + c.x] = LoopTile.Path;
            }

            List<GridPos> interior = LoopMapGeometry.FindInterior(cells, gridWidth, gridHeight);
            map.interiorTileList = interior;
            map.interiorArea = interior.Count;

            for (int y = 0; y < gridHeight; ++y)
            {
                for (int x = 0; x < gridWidth; ++x)
                {
                    if (map.tileGrid[y * gridWidth + x] == LoopTile.Path) continue;
                    map.tileGrid[y * gridWidth + x] = LoopTile.Build;
                    map.buildTileList.Add(new GridPos(x, y));
                }
            }
            map.buildArea = map.buildTileList.Count;

            // 스폰: 저장된 인덱스를 그대로 쓴다. 없으면 0번 웨이포인트.
            if (spawnIndices != null)
            {
                foreach (int idx in spawnIndices)
                {
                    if (idx >= 0 && idx < cells.Count) map.spawnIndexList.Add(idx);
                }
            }
            if (map.spawnIndexList.Count == 0 && cells.Count > 0) map.spawnIndexList.Add(0);

            // 석상: 저장된 위치를 그대로.
            map.statueHp = statueHp;
            if (statues != null)
            {
                foreach (var s in statues) map.statueList.Add(s);
            }

            int zeroCover;
            map.coverageIndex = CoverageIndex.Compute(map, coverageRadius > 0 ? coverageRadius : 4, out zeroCover);
            return map;
        }

        // 루프 외부 배치칸에서 중심에서 먼 순으로, 서로 떨어지게 석상 위치를 고른다. 결정적이다.
        private static void PlaceStatues(LoopMap map, MapGenParams p, long seed)
        {
            HashSet<int> interiorKey = new HashSet<int>();
            foreach (var c in map.interiorTileList) interiorKey.Add(c.y * map.gridWidth + c.x);

            List<GridPos> exterior = new List<GridPos>();
            foreach (var b in map.buildTileList)
            {
                if (!interiorKey.Contains(b.y * map.gridWidth + b.x)) exterior.Add(b);
            }
            if (exterior.Count == 0) return;

            int cx = map.gridWidth / 2;
            int cy = map.gridHeight / 2;
            exterior.Sort((a, b) =>
            {
                int da = (a.x - cx) * (a.x - cx) + (a.y - cy) * (a.y - cy);
                int db = (b.x - cx) * (b.x - cx) + (b.y - cy) * (b.y - cy);
                if (da != db) return db.CompareTo(da);
                int ka = a.y * map.gridWidth + a.x;
                int kb = b.y * map.gridWidth + b.x;
                return ka.CompareTo(kb);
            });

            DeterministicRandom rng = new DeterministicRandom(seed ^ 0x5713L);
            int count = rng.NextInt(p.statueCountMin, p.statueCountMax + 1);
            if (count < 1) count = 1;

            List<GridPos> picked = new List<GridPos>();
            for (int i = 0; i < exterior.Count && picked.Count < count; ++i)
            {
                GridPos cand = exterior[i];
                bool ok = true;
                for (int j = 0; j < picked.Count; ++j)
                {
                    int dx = cand.x - picked[j].x;
                    int dy = cand.y - picked[j].y;
                    if (dx * dx + dy * dy < 16) { ok = false; break; }
                }
                if (ok) picked.Add(cand);
            }
            // 간격 조건으로 못 채웠으면 남은 외곽칸에서 순서대로 채운다.
            for (int i = 0; i < exterior.Count && picked.Count < count; ++i)
            {
                if (!picked.Contains(exterior[i])) picked.Add(exterior[i]);
            }
            map.statueList = picked;
        }
    }
}
