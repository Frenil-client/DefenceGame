using System.IO;
using UnityEngine;
using Synthesis.Core.Data;
using Synthesis.Core.Map;

namespace Synthesis.Presentation
{
    // STEP 2/3. 최소 View - 저장소의 Data/*.csv 를 에디터 Play 에서 읽는 공용 로더.
    // 빌드 배포는 이후 Addressables/SO 로 대체한다(ARCHITECTURE 5-2). 지금은 에디터 확인 전용이다.
    public static class RuntimeDataLoader
    {
        public static string FindDataDir()
        {
            var dir = new DirectoryInfo(Application.dataPath);
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

        public static GameDatabase LoadDatabase()
        {
            string dataDir = FindDataDir();
            GameDatabase db = new GameDatabase();
            if (dataDir == null) return db;

            db.unitList   = CsvParsers.LoadUnits(ReadIfExists(dataDir, "units.csv"));
            db.recipeList = CsvParsers.LoadRecipes(ReadIfExists(dataDir, "recipes.csv"));
            db.enemyList  = CsvParsers.LoadEnemies(ReadIfExists(dataDir, "enemies.csv"));
            db.bossList   = CsvParsers.LoadBosses(ReadIfExists(dataDir, "bosses.csv"));
            db.waveList   = CsvParsers.LoadWaves(ReadIfExists(dataDir, "waves.csv"));
            db.skillList  = CsvParsers.LoadSkills(ReadIfExists(dataDir, "skills.csv"));
            return db;
        }

        public static MapGenParams LoadMapGenParams()
        {
            string dataDir = FindDataDir();
            if (dataDir == null) return MapGenParams.Defaults();
            string text = ReadIfExists(dataDir, "mapgen.csv");
            if (string.IsNullOrEmpty(text)) return MapGenParams.Defaults();
            return MapGenParser.Load(text);
        }

        private static string ReadIfExists(string dir, string fileName)
        {
            var path = Path.Combine(dir, fileName);
            if (!File.Exists(path)) return string.Empty;
            return File.ReadAllText(path);
        }
    }
}
