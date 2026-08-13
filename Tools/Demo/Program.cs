using System;
using System.IO;
using System.Text;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Simulation;

namespace Synthesis.Demo
{
    // STEP 3(v0.4). 헤드리스 데모 - 뽑기->조합->배치->41웨이브->보스를 한 사이클 돌려 결과를 출력한다.
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

            Console.WriteLine("SYNTHESIS v0.4 데모 - 시드 " + seed + " (뽑기->조합->배치->41웨이브)");
            Console.Write("석상 " + map.statueList.Count + "개: ");
            foreach (var s in map.statueList) Console.Write("(" + s.x + "," + s.y + ") ");
            Console.WriteLine("hp=" + map.statueHp.ToIntRounded());
            Console.WriteLine("------------------------------------------------------------");

            LoopRunController run = new LoopRunController(db, map, seed);
            LoopRunResult result = run.RunFullCycle();


            foreach (var line in result.waveLog) Console.WriteLine("  " + line);

            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("도달 웨이브 " + result.wavesReached + " / 41   총 처치 " + result.killedTotal);
            Console.WriteLine("결과: " + (result.cleared ? "클리어" : (result.defeated ? "패배" : "미완")));
            Console.WriteLine("final hash: " + result.finalHash.ToString("x16"));
            return result.cleared ? 0 : 1;
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
