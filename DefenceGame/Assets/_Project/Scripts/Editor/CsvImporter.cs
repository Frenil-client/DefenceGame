using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Synthesis.Core.Data;
using Synthesis.Data;

namespace Synthesis.Editor
{
    // STEP 1. 기반 도구 - Data/*.csv 를 ScriptableObject 캐시로 변환하는 에디터 임포터.
    // Core 파서를 그대로 쓴다. Sim/Editor 가 같은 파서를 공유하는 것이 결정성의 전제다 (ARCHITECTURE.md 5-1).
    public static class CsvImporter
    {
        private const string GeneratedAssetPath = "Assets/_Project/Data/Generated/SynthesisDatabase.asset";

        [MenuItem("Synthesis/Import CSV to ScriptableObjects")]
        public static void ImportAll()
        {
            string dataDir = FindDataDir();
            if (dataDir == null)
            {
                Debug.LogError("[CsvImporter] Data 디렉터리를 찾지 못했습니다.");
                return;
            }

            var units   = CsvParsers.LoadUnits(ReadIfExists(dataDir, "units.csv"));
            var recipes = CsvParsers.LoadRecipes(ReadIfExists(dataDir, "recipes.csv"));

            SynthesisDatabaseSO db = LoadOrCreateDatabase();
            db.unitList.Clear();
            db.recipeList.Clear();

            foreach (var unit in units)
            {
                db.unitList.Add(UnitRow.FromModel(unit));
            }
            foreach (var recipe in recipes)
            {
                db.recipeList.Add(RecipeRow.FromModel(recipe));
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 파서 일치 자기검증: SO 로 캐시한 뒤 되돌린 모델이 CSV 직접 파싱과 동일해야 한다 (ARCHITECTURE.md 5-1).
            int mismatch = VerifyParserMatch(units, db);
            if (mismatch > 0)
            {
                Debug.LogError("[CsvImporter] 파서 일치 검증 실패: 유닛 " + mismatch + "건 불일치");
            }
            else
            {
                Debug.Log("[CsvImporter] 완료: 유닛 " + units.Count + " / 레시피 " + recipes.Count + " (파서 일치 검증 통과)");
            }
        }

        private static int VerifyParserMatch(List<UnitData> direct, SynthesisDatabaseSO db)
        {
            var roundTrip = db.BuildUnitModels();
            if (roundTrip.Count != direct.Count) return System.Math.Abs(roundTrip.Count - direct.Count);

            int mismatch = 0;
            for (int i = 0; i < direct.Count; ++i)
            {
                UnitData a = direct[i];
                UnitData b = roundTrip[i];
                bool same = a.id == b.id
                    && a.grade == b.grade
                    && a.element == b.element
                    && a.role == b.role
                    && a.placement == b.placement
                    && a.cost == b.cost
                    && a.hp.raw == b.hp.raw
                    && a.atk.raw == b.atk.raw
                    && a.atkSpeed.raw == b.atkSpeed.raw
                    && a.range.raw == b.range.raw
                    && a.blockCount == b.blockCount
                    && a.redeployCd == b.redeployCd
                    && a.isAdvance == b.isAdvance;
                if (!same) ++mismatch;
            }
            return mismatch;
        }

        private static SynthesisDatabaseSO LoadOrCreateDatabase()
        {
            SynthesisDatabaseSO db = AssetDatabase.LoadAssetAtPath<SynthesisDatabaseSO>(GeneratedAssetPath);
            if (db != null) return db;

            var dir = Path.GetDirectoryName(GeneratedAssetPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(Path.GetFullPath(dir));
                AssetDatabase.Refresh();
            }

            db = ScriptableObject.CreateInstance<SynthesisDatabaseSO>();
            AssetDatabase.CreateAsset(db, GeneratedAssetPath);
            return db;
        }

        // Assets 상위(저장소 루트)에서 Data/units.csv 를 찾는다.
        private static string FindDataDir()
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

        private static string ReadIfExists(string dataDir, string fileName)
        {
            var path = Path.Combine(dataDir, fileName);
            if (!File.Exists(path)) return string.Empty;
            return File.ReadAllText(path);
        }
    }
}
