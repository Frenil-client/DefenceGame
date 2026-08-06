using System.IO;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Simulation;

namespace Synthesis.Core.Tests
{
    // STEP 2. 검증 - 시뮬레이션 뼈대. 결정성(DoD 핵심)과 8웨이브 완주.
    public class SimulationTests
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
            db.enemyList = CsvParsers.LoadEnemies(TestPaths.ReadData("enemies.csv"));
            db.bossList  = CsvParsers.LoadBosses(TestPaths.ReadData("bosses.csv"));
            db.waveList  = CsvParsers.LoadWaves(TestPaths.ReadData("waves.csv"));
            return db;
        }

        [Fact]
        public void SameSeed_ProducesIdenticalWaveHashes()
        {
            GameDatabase db = LoadDb();
            MapData map = LoadMap01();

            WaveRunResult a = SimulationDriver.RunWaves(db, map, 42, 8);
            WaveRunResult b = SimulationDriver.RunWaves(db, map, 42, 8);

            Assert.Equal(a.finalHash, b.finalHash);
            Assert.Equal(a.waveHashList.Count, b.waveHashList.Count);
            for (int i = 0; i < a.waveHashList.Count; ++i)
            {
                Assert.Equal(a.waveHashList[i], b.waveHashList[i]);
            }
        }

        [Fact]
        public void EightWaves_Complete()
        {
            GameDatabase db = LoadDb();
            MapData map = LoadMap01();

            WaveRunResult result = SimulationDriver.RunWaves(db, map, 1, 8);
            Assert.Equal(8, result.wavesCompleted);
            Assert.Equal(8, result.waveHashList.Count);
        }

        [Fact]
        public void NoDefense_AllEnemiesLeak()
        {
            // 아직 배치/전투가 없으므로 모든 적이 출구를 통과한다. 처치는 0 이어야 한다.
            GameDatabase db = LoadDb();
            MapData map = LoadMap01();

            WaveRunResult result = SimulationDriver.RunWaves(db, map, 1, 8);
            Assert.True(result.leakedCount > 0);
            Assert.Equal(0, result.killedCount);
        }
    }
}
