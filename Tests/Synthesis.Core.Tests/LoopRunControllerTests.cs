using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Simulation;

namespace Synthesis.Core.Tests
{
    // STEP 3(v0.4). 검증 - 헤드리스 풀사이클. 뽑기->조합->배치->41웨이브 한 사이클이 결정적으로 돈다.
    public class LoopRunControllerTests
    {
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

        private static LoopMap MakeMap(long seed)
        {
            return LoopMapGenerator.Generate(MapGenParser.Load(TestPaths.ReadData("mapgen.csv")), seed);
        }

        [Fact]
        public void FullCycle_IsDeterministic()
        {
            GameDatabase db = LoadDb();
            LoopMap map = MakeMap(7);

            LoopRunResult a = new LoopRunController(db, map, 7).RunFullCycle();
            LoopRunResult b = new LoopRunController(db, map, 7).RunFullCycle();

            Assert.Equal(a.wavesReached, b.wavesReached);
            Assert.Equal(a.cleared, b.cleared);
            Assert.Equal(a.finalHash, b.finalHash);
        }

        [Fact]
        public void FullCycle_ReachesFinalWaveAndClears()
        {
            GameDatabase db = LoadDb();
            LoopMap map = MakeMap(7);

            LoopRunResult result = new LoopRunController(db, map, 7).RunFullCycle();

            Assert.Equal(41, result.wavesReached);
            Assert.True(result.cleared, "사이클 미클리어: 마지막 로그 " + LastLog(result));
        }

        private static string LastLog(LoopRunResult r)
        {
            if (r.waveLog.Count == 0) return "(로그 없음)";
            return r.waveLog[r.waveLog.Count - 1];
        }
    }
}
