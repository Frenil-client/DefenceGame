using System.IO;
using Synthesis.Core.Data;

namespace Synthesis.Linter
{
    // STEP 1. 기반 도구 - Data/*.csv 를 읽어 GameDatabase 로 로드한다.
    // Core 의 파서를 그대로 쓴다. Sim/Editor 와 파서가 갈라지면 안 된다 (ARCHITECTURE.md 5-1).
    public static class DataLoader
    {
        // 시작 디렉터리에서 위로 올라가며 Data/units.csv 를 가진 폴더를 찾는다.
        public static string FindDataDir(string startDir)
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

        public static GameDatabase Load(string dataDir)
        {
            GameDatabase db = new GameDatabase();

            db.unitList   = CsvParsers.LoadUnits(ReadIfExists(dataDir, "units.csv"));
            db.recipeList = CsvParsers.LoadRecipes(ReadIfExists(dataDir, "recipes.csv"));
            db.waveList   = CsvParsers.LoadWaves(ReadIfExists(dataDir, "waves.csv"));
            db.bossList   = CsvParsers.LoadBosses(ReadIfExists(dataDir, "bosses.csv"));

            return db;
        }

        private static string ReadIfExists(string dataDir, string fileName)
        {
            var path = Path.Combine(dataDir, fileName);
            if (!File.Exists(path)) return string.Empty;
            return File.ReadAllText(path);
        }
    }
}
