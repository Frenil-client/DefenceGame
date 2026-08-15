using System;
using System.IO;
using System.Text;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Simulation;

namespace Synthesis.Demo
{
    // 헤드리스 데모(축소판). 전투/유닛 이동은 시뮬에서 제거됐으므로 데이터 로드/맵 생성/몬스터 스폰만 확인한다.
    // 사용: synthesis-demo [dataDir] [seed]
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            string dataDir = args.Length >= 1 ? args[0] : FindDataDir(Directory.GetCurrentDirectory());
            long seed = args.Length >= 2 ? long.Parse(args[1]) : 1;
            if (string.IsNullOrEmpty(dataDir) || !Directory.Exists(dataDir))
            {
                Console.Error.WriteLine("[demo] Data 디렉터리를 찾지 못했습니다.");
                return 2;
            }

            GameDatabase db = LoadDatabase(dataDir);
            MapGenParams p = MapGenParser.Load(ReadIfExists(dataDir, "mapgen.csv"));
            LoopMap map = LoopMapGenerator.Generate(p, seed);

            Console.WriteLine("SYNTHESIS 데모 - 시드 " + seed);
            Console.WriteLine("데이터: 유닛 " + db.unitList.Count + " / 레시피 " + db.recipeList.Count
                + " / 적 " + db.enemyList.Count + " / 보스 " + db.bossList.Count + " / 웨이브 " + db.waveList.Count);
            Console.WriteLine("맵: 둘레 " + map.perimeter + " / 배치칸 " + map.buildArea
                + " / 스폰 " + map.spawnIndexList.Count + " / 석상 " + map.statueList.Count
                + (map.isFallback ? " (폴백)" : ""));

            // 첫 웨이브를 스폰해 몬스터가 순회하는지만 확인(전투 없음).
            LoopSimulator sim = new LoopSimulator(map, seed);
            EnemyData e = db.enemyList.Count > 0 ? db.enemyList[0] : null;
            sim.StartWave(e, 10, 8);
            for (int i = 0; i < 200; ++i) sim.Tick();
            Console.WriteLine("200틱 후 필드 몬스터 " + sim.state.aliveCount + " (스폰만, 처치 없음)");
            Console.WriteLine("final hash: " + sim.ComputeStateHash().ToString("x16"));
            return 0;
        }

        private static GameDatabase LoadDatabase(string dataDir)
        {
            GameDatabase db = new GameDatabase();
            db.unitList   = CsvParsers.LoadUnits(ReadIfExists(dataDir, "units.csv"));
            db.recipeList = CsvParsers.LoadRecipes(ReadIfExists(dataDir, "recipes.csv"));
            db.enemyList  = CsvParsers.LoadEnemies(ReadIfExists(dataDir, "enemies.csv"));
            db.bossList   = CsvParsers.LoadBosses(ReadIfExists(dataDir, "bosses.csv"));
            db.waveList   = CsvParsers.LoadWaves(ReadIfExists(dataDir, "waves.csv"));
            return db;
        }

        private static string FindDataDir(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Data");
                if (File.Exists(Path.Combine(candidate, "units.csv"))) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        private static string ReadIfExists(string dir, string fileName)
        {
            var path = Path.Combine(dir, fileName);
            if (!File.Exists(path)) return string.Empty;
            return File.ReadAllText(path);
        }
    }
}
