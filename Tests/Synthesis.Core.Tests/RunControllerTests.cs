using System.Collections.Generic;
using System.IO;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Simulation;

namespace Synthesis.Core.Tests
{
    // STEP 3. 검증 - 생성/처리 파이프라인(뽑기 -> 인벤토리 -> 배치 -> 스폰 -> 전투).
    public class RunControllerTests
    {
        private static MapData LoadMap01()
        {
            string dataDir = TestPaths.FindDataDir();
            string grid = File.ReadAllText(Path.Combine(dataDir, "maps", "map01_grid.csv"));
            string path = File.ReadAllText(Path.Combine(dataDir, "maps", "map01_path.csv"));
            return MapParser.CsvToMap(grid, path);
        }

        private static GameDatabase LoadDb()
        {
            GameDatabase db = new GameDatabase();
            db.unitList   = CsvParsers.LoadUnits(TestPaths.ReadData("units.csv"));
            db.recipeList = CsvParsers.LoadRecipes(TestPaths.ReadData("recipes.csv"));
            db.enemyList  = CsvParsers.LoadEnemies(TestPaths.ReadData("enemies.csv"));
            db.bossList   = CsvParsers.LoadBosses(TestPaths.ReadData("bosses.csv"));
            db.waveList   = CsvParsers.LoadWaves(TestPaths.ReadData("waves.csv"));
            return db;
        }

        [Fact]
        public void FullRun_24Waves_Completes()
        {
            var run = new RunController(LoadDb(), LoadMap01(), 1);
            var outcomes = run.RunWaves(24);
            Assert.Equal(24, outcomes.Count);
        }

        [Fact]
        public void EveryGrantedUnitIsInInventoryOrOnField()
        {
            var run = new RunController(LoadDb(), LoadMap01(), 3);
            var outcomes = run.RunWaves(12);

            int placedTotal = 0;
            foreach (var o in outcomes) placedTotal += o.placedThisWave;

            // 12웨이브면 12기 지급. 조합 자동 실행이 없으므로 지급분은 인벤토리에 있거나 배치됐다.
            Assert.Equal(12, run.inventory.Count + placedTotal);
        }

        [Fact]
        public void FullRun_IsDeterministic()
        {
            var a = new RunController(LoadDb(), LoadMap01(), 7);
            var b = new RunController(LoadDb(), LoadMap01(), 7);

            var oa = a.RunWaves(24);
            var ob = b.RunWaves(24);

            for (int i = 0; i < oa.Count; ++i)
            {
                Assert.Equal(oa[i].grantedUnitId, ob[i].grantedUnitId);
                Assert.Equal(oa[i].stateHash, ob[i].stateHash);
            }
            Assert.Equal(a.sim.ComputeStateHash(), b.sim.ComputeStateHash());
        }
    }
}
