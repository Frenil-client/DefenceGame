using System.Collections.Generic;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Simulation;

namespace Synthesis.Core.Tests
{
    // STEP 1(v0.4). 검증 - 저장된 경로 셀로부터 LoopMap 복원(MapSO 로드 경로의 Core 부분).
    public class MapFromCellsTests
    {
        private static LoopMap Generate(long seed)
        {
            return LoopMapGenerator.Generate(MapGenParser.Load(TestPaths.ReadData("mapgen.csv")), seed);
        }

        [Fact]
        public void FromCells_PreservesPathAndSpawnAndStatues()
        {
            LoopMap src = Generate(7);

            LoopMap restored = LoopMapGenerator.FromCells(
                src.gridWidth, src.gridHeight, src.loopWaypointList,
                src.spawnIndexList, src.statueList, src.statueHp, 4);

            // 경로(웨이포인트)가 그대로다
            Assert.Equal(src.loopWaypointList.Count, restored.loopWaypointList.Count);
            for (int i = 0; i < src.loopWaypointList.Count; ++i)
            {
                Assert.Equal(src.loopWaypointList[i].x, restored.loopWaypointList[i].x);
                Assert.Equal(src.loopWaypointList[i].y, restored.loopWaypointList[i].y);
            }

            // 스폰 인덱스 보존
            Assert.Equal(src.spawnIndexList, restored.spawnIndexList);

            // 석상 보존
            Assert.Equal(src.statueList.Count, restored.statueList.Count);
            Assert.Equal(src.statueHp.raw, restored.statueHp.raw);

            // 배치칸은 재계산되어 존재한다
            Assert.True(restored.buildTileList.Count > 0);
        }

        [Fact]
        public void FromCells_MonstersFollowStoredPath()
        {
            LoopMap src = Generate(3);
            LoopMap map = LoopMapGenerator.FromCells(
                src.gridWidth, src.gridHeight, src.loopWaypointList,
                src.spawnIndexList, src.statueList, src.statueHp, 4);

            EnemyData e = null;
            foreach (var ed in CsvParsers.LoadEnemies(TestPaths.ReadData("enemies.csv")))
            {
                if (ed.id == "E01") e = ed;
            }

            LoopSimulator sim = new LoopSimulator(map, 1);
            sim.StartWave(e, 1, 1);
            for (int i = 0; i < 300; ++i) sim.Tick();

            LoopMonster m = sim.state.monsterList[0];
            Assert.True(m.alive);
            Assert.InRange(m.waypointIndex, 0, map.loopWaypointList.Count - 1);
        }
    }
}
