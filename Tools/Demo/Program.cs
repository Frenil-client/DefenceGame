using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Simulation;

namespace Synthesis.Demo
{
    // STEP 3. 검증 - 데모 러너. 뽑기로 유닛이 생성되고 배치되어 웨이브를 막는 전체 파이프라인을 보여준다.
    // 사용: synthesis-demo [dataDir] [seed] [waveCount]
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            string dataDir = args.Length >= 1 ? args[0] : FindDataDir(Directory.GetCurrentDirectory());
            long seed = args.Length >= 2 ? long.Parse(args[1]) : 1;
            int waveCount = args.Length >= 3 ? int.Parse(args[2]) : 24;

            if (string.IsNullOrEmpty(dataDir) || !Directory.Exists(dataDir))
            {
                Console.Error.WriteLine("[demo] Data 디렉터리를 찾지 못했습니다. 인자로 경로를 주세요.");
                return 2;
            }

            GameDatabase db = LoadDatabase(dataDir);
            MapData map = LoadMap(dataDir, "map01");

            Console.WriteLine("SYNTHESIS 데모 - map01, 시드 " + seed + " (뽑기로 유닛 생성 -> 자동 배치 -> 방어)");
            Console.WriteLine("------------------------------------------------------------");

            RunController run = new RunController(db, map, seed);
            for (int i = 1; i <= waveCount; ++i)
            {
                WaveOutcome o = run.RunWave(i);
                Console.WriteLine(
                    "wave " + o.waveIndex.ToString().PadLeft(2)
                    + "  뽑기=" + (o.grantedUnitId ?? "-").PadRight(4)
                    + "  배치+" + o.placedThisWave
                    + "  " + (o.isBoss ? ("BOSS " + o.enemyLabel) : ("적 " + o.enemyLabel)).PadRight(10)
                    + "  처치=" + o.killedThisWave.ToString().PadLeft(2)
                    + "  누출=" + o.leakedThisWave.ToString().PadLeft(2)
                    + "  인벤=" + run.inventory.Count.ToString().PadLeft(2)
                    + "  코스트=" + run.sim.state.cost);
            }

            ulong first = run.sim.ComputeStateHash();
            RunController run2 = new RunController(db, map, seed);
            run2.RunWaves(waveCount);
            ulong second = run2.sim.ComputeStateHash();

            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("결정성: 재실행 해시 " + (first == second ? "일치 (OK)" : "불일치 (FAIL)"));
            Console.WriteLine("final hash: " + first.ToString("x16"));
            return first == second ? 0 : 1;
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

        private static MapData LoadMap(string dataDir, string mapId)
        {
            string grid = ReadIfExists(Path.Combine(dataDir, "maps"), mapId + "_grid.csv");
            string path = ReadIfExists(Path.Combine(dataDir, "maps"), mapId + "_path.csv");
            return MapParser.CsvToMap(grid, path);
        }

        private static string FindDataDir(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Data");
                if (File.Exists(Path.Combine(candidate, "units.csv")))
                {
                    return candidate;
                }
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
