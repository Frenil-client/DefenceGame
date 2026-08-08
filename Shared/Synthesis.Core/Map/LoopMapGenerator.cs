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

            int zeroCover;
            map.coverageIndex = CoverageIndex.Compute(map, p.coverageRadius, out zeroCover);
            return map;
        }
    }
}
