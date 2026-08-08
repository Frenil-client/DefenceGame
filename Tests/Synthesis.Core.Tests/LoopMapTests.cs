using System.Collections.Generic;
using Synthesis.Core.Map;

namespace Synthesis.Core.Tests
{
    // STEP 1. 검증 - 루프 맵 생성기와 검증기.
    public class LoopMapTests
    {
        private static MapGenParams Params()
        {
            return MapGenParser.Load(TestPaths.ReadData("mapgen.csv"));
        }

        private static ulong HashMap(LoopMap map)
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, (ulong)map.perimeter);
            hash = Mix(hash, (ulong)map.buildArea);
            hash = Mix(hash, (ulong)map.cornerIndexList.Count);
            foreach (var c in map.loopWaypointList)
            {
                hash = Mix(hash, (ulong)((c.y << 8) + c.x));
            }
            return hash;
        }

        private static ulong Mix(ulong h, ulong v)
        {
            h ^= v;
            h *= 1099511628211UL;
            return h;
        }

        [Fact]
        public void Fallback_PassesAllConstraints()
        {
            var p = Params();
            LoopMap map = FallbackMap.Create(p, 0);
            List<string> failures = new List<string>();
            bool ok = LoopMapValidator.Validate(map, p, failures);
            Assert.True(ok, "폴백 위반: " + string.Join(" / ", failures));
        }

        [Fact]
        public void SameSeed_ProducesIdenticalMap()
        {
            var p = Params();
            LoopMap a = LoopMapGenerator.Generate(p, 12345);
            LoopMap b = LoopMapGenerator.Generate(p, 12345);
            Assert.Equal(HashMap(a), HashMap(b));
        }

        [Fact]
        public void Generate_AlwaysReturnsValidMap()
        {
            var p = Params();
            for (long seed = 1; seed <= 100; ++seed)
            {
                LoopMap map = LoopMapGenerator.Generate(p, seed);
                List<string> failures = new List<string>();
                bool ok = LoopMapValidator.Validate(map, p, failures);
                Assert.True(ok, "시드 " + seed + " 위반: " + string.Join(" / ", failures));
            }
        }

        [Fact]
        public void Generate_ClosedLoopIsWellFormed()
        {
            var p = Params();
            LoopMap map = LoopMapGenerator.Generate(p, 7);

            // 연속 웨이포인트는 4-인접, 마지막은 0으로 순환
            var wp = map.loopWaypointList;
            Assert.True(wp.Count >= 8);
            for (int i = 0; i < wp.Count; ++i)
            {
                GridPos cur = wp[i];
                GridPos next = wp[(i + 1) % wp.Count];
                int dx = cur.x - next.x; if (dx < 0) dx = -dx;
                int dy = cur.y - next.y; if (dy < 0) dy = -dy;
                Assert.Equal(1, dx + dy);
            }
        }

        [Fact]
        public void DifferentSeeds_ProduceDifferentMaps()
        {
            var p = Params();
            HashSet<ulong> hashes = new HashSet<ulong>();
            int distinct = 0;
            for (long seed = 1; seed <= 20; ++seed)
            {
                if (hashes.Add(HashMap(LoopMapGenerator.Generate(p, seed)))) ++distinct;
            }
            // 폴백만 반환되면 전부 같아진다. 최소한 여러 형태가 나와야 랜덤화 가치가 있다.
            Assert.True(distinct >= 5, "서로 다른 맵 " + distinct + "개 (>=5 기대)");
        }
    }
}
