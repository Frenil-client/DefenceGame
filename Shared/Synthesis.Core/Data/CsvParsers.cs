using System.Collections.Generic;

namespace Synthesis.Core.Data
{
    // STEP 1. 기반 도구 - CSV 한 줄을 데이터 모델로 변환 (v0.4). 파서는 Core 에 한 벌만 둔다.
    public static class CsvParsers
    {
        // units.csv: id,name,tier,klass,cost,hp,atk,atkSpeed,range,note
        public static UnitData CsvToUnitData(string line)
        {
            var split = line.Split(',');
            if (split.Length < 10) return null;

            UnitData unitData = new UnitData();
            unitData.id       = split[0].Trim();
            unitData.name     = split[1].Trim();
            unitData.tier     = CsvUtil.StringToInt(split[2]);
            unitData.klass    = CsvEnum.StringToKlass(split[3]);
            unitData.cost     = CsvUtil.StringToInt(split[4]);
            unitData.hp       = CsvUtil.StringToFixed(split[5]);
            unitData.atk      = CsvUtil.StringToFixed(split[6]);
            unitData.atkSpeed = CsvUtil.StringToFixed(split[7]);
            unitData.range    = CsvUtil.StringToFixed(split[8]);
            unitData.note     = RejoinFrom(split, 9);
            if (unitData.cost < 0) unitData.cost = 0;
            return unitData;
        }

        // recipes.csv: resultId,mat1,mat2,mat3,mat4 (mat3/mat4 는 비어 있을 수 있음)
        public static RecipeData CsvToRecipeData(string line)
        {
            var split = line.Split(',');
            if (split.Length < 3) return null;

            RecipeData recipeData = new RecipeData();
            recipeData.resultId = split[0].Trim();
            for (int i = 1; i < split.Length; ++i)
            {
                var mat = split[i].Trim();
                if (string.IsNullOrEmpty(mat))
                {
                    continue;
                }
                recipeData.materials.Add(mat);
            }
            if (recipeData.materials.Count < 2) return null;
            return recipeData;
        }

        // enemies.csv: id,name,hp,atk,moveSpeed
        public static EnemyData CsvToEnemyData(string line)
        {
            var split = line.Split(',');
            if (split.Length < 5) return null;

            EnemyData enemyData = new EnemyData();
            enemyData.id        = split[0].Trim();
            enemyData.name      = split[1].Trim();
            enemyData.hp        = CsvUtil.StringToFixed(split[2]);
            enemyData.atk       = CsvUtil.StringToFixed(split[3]);
            enemyData.moveSpeed = CsvUtil.StringToFixed(split[4]);
            return enemyData;
        }

        // bosses.csv: id,name,hp,armor,moveSpeed,timeLimitSec,selectionReward,note
        public static BossData CsvToBossData(string line)
        {
            var split = line.Split(',');
            if (split.Length < 6) return null;

            BossData bossData = new BossData();
            bossData.id            = split[0].Trim();
            bossData.name          = split[1].Trim();
            bossData.hp            = CsvUtil.StringToFixed(split[2]);
            bossData.armor         = CsvUtil.StringToFixed(split[3]);
            bossData.moveSpeed     = CsvUtil.StringToFixed(split[4]);
            bossData.timeLimitTicks = CsvUtil.StringToInt(split[5]) * 20; // 초 -> 틱
            bossData.selectionReward = split.Length > 6 ? CsvUtil.StringToInt(split[6]) : 0;
            bossData.note          = split.Length > 7 ? RejoinFrom(split, 7) : "";
            return bossData;
        }

        // waves.csv: waveIndex,enemySetId,spawnCount,spawnInterval,isBoss,bossId,difficultyScale
        public static WaveData CsvToWaveData(string line)
        {
            var split = line.Split(',');
            if (split.Length < 7) return null;

            WaveData waveData = new WaveData();
            waveData.waveIndex       = CsvUtil.StringToInt(split[0]);
            waveData.enemySetId      = split[1].Trim();
            waveData.spawnCount      = CsvUtil.StringToInt(split[2]);
            waveData.spawnInterval   = CsvUtil.StringToInt(split[3]);
            waveData.isBoss          = CsvUtil.StringToBool(split[4]);
            waveData.bossId          = split[5].Trim();
            waveData.difficultyScale = CsvUtil.StringToFixed(split[6]);
            return waveData;
        }

        // skills.csv: id,trigger,triggerN,effect,radius,magnitude,duration,count,buffStat,note
        public static SkillData CsvToSkillData(string line)
        {
            var split = line.Split(',');
            if (split.Length < 8) return null;

            SkillData s = new SkillData();
            s.id        = split[0].Trim();
            s.trigger   = CsvEnum.StringToSkillTrigger(split[1]);
            s.triggerN  = CsvUtil.StringToFixed(split[2]);
            s.effect    = CsvEnum.StringToSkillEffect(split[3]);
            s.radius    = CsvUtil.StringToFixed(split[4]);
            s.magnitude = CsvUtil.StringToFixed(split[5]);
            s.duration  = CsvUtil.StringToFixed(split[6]);
            s.count     = CsvUtil.StringToInt(split[7]);
            s.buffStat  = split.Length > 8 ? CsvEnum.StringToBuffStat(split[8]) : BuffStat.None;
            s.note      = split.Length > 9 ? RejoinFrom(split, 9) : "";
            return s;
        }

        // ---- 로더 ----

        public static List<UnitData> LoadUnits(string fileText)
        {
            List<UnitData> result = new List<UnitData>();
            foreach (var line in CsvUtil.CsvToDataLines(fileText))
            {
                var d = CsvToUnitData(line);
                if (d != null) result.Add(d);
            }
            return result;
        }

        public static List<RecipeData> LoadRecipes(string fileText)
        {
            List<RecipeData> result = new List<RecipeData>();
            foreach (var line in CsvUtil.CsvToDataLines(fileText))
            {
                var d = CsvToRecipeData(line);
                if (d != null) result.Add(d);
            }
            return result;
        }

        public static List<EnemyData> LoadEnemies(string fileText)
        {
            List<EnemyData> result = new List<EnemyData>();
            foreach (var line in CsvUtil.CsvToDataLines(fileText))
            {
                var d = CsvToEnemyData(line);
                if (d != null) result.Add(d);
            }
            return result;
        }

        public static List<BossData> LoadBosses(string fileText)
        {
            List<BossData> result = new List<BossData>();
            foreach (var line in CsvUtil.CsvToDataLines(fileText))
            {
                var d = CsvToBossData(line);
                if (d != null) result.Add(d);
            }
            return result;
        }

        public static List<WaveData> LoadWaves(string fileText)
        {
            List<WaveData> result = new List<WaveData>();
            foreach (var line in CsvUtil.CsvToDataLines(fileText))
            {
                var d = CsvToWaveData(line);
                if (d != null) result.Add(d);
            }
            return result;
        }

        public static List<SkillData> LoadSkills(string fileText)
        {
            List<SkillData> result = new List<SkillData>();
            foreach (var line in CsvUtil.CsvToDataLines(fileText))
            {
                var d = CsvToSkillData(line);
                if (d != null) result.Add(d);
            }
            return result;
        }

        private static string RejoinFrom(string[] split, int startIndex)
        {
            if (startIndex >= split.Length) return string.Empty;
            List<string> parts = new List<string>();
            for (int i = startIndex; i < split.Length; ++i)
            {
                parts.Add(split[i]);
            }
            return string.Join(",", parts).Trim();
        }
    }
}
