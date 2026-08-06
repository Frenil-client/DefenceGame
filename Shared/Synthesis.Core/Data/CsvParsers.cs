using System.Collections.Generic;

namespace Synthesis.Core.Data
{
    // STEP 1. 기반 도구 - CSV 한 줄을 데이터 모델로 변환하는 파서.
    // 파서는 Core 에 한 벌만 둔다. Sim 콘솔과 Unity 임포터가 같은 파서를 쓴다 (ARCHITECTURE.md 5-1).
    public static class CsvParsers
    {
        // units.csv: id,name,grade,element,role,placement,cost,hp,atk,atkSpeed,range,blockCount,redeployCd,isAdvance,note
        public static UnitData CsvToUnitData(string line)
        {
            var split = line.Split(',');
            if (split.Length < 15) return null;

            UnitData unitData = new UnitData();
            unitData.id         = split[0].Trim();
            unitData.name       = split[1].Trim();
            unitData.grade      = CsvEnum.StringToGrade(split[2]);
            unitData.element    = CsvEnum.StringToElement(split[3]);
            unitData.role       = CsvEnum.StringToRole(split[4]);
            unitData.placement  = CsvEnum.StringToPlacement(split[5]);
            unitData.cost       = CsvUtil.StringToInt(split[6]);
            unitData.hp         = CsvUtil.StringToFixed(split[7]);
            unitData.atk        = CsvUtil.StringToFixed(split[8]);
            unitData.atkSpeed   = CsvUtil.StringToFixed(split[9]);
            unitData.range      = CsvUtil.StringToFixed(split[10]);
            unitData.blockCount = CsvUtil.StringToInt(split[11]);
            unitData.redeployCd = CsvUtil.StringToInt(split[12]);
            unitData.isAdvance  = CsvUtil.StringToBool(split[13]);

            // note 안에 쉼표가 있어도 깨지지 않도록 14번 이후를 다시 이어붙인다.
            unitData.note = RejoinFrom(split, 14);

            if (unitData.cost < 0) unitData.cost = 0;
            return unitData;
        }

        // recipes.csv: resultId,mat1,mat2,conditionType,isHidden,unlockedByDefault
        public static RecipeData CsvToRecipeData(string line)
        {
            var split = line.Split(',');
            if (split.Length < 6) return null;

            RecipeData recipeData = new RecipeData();
            recipeData.resultId          = split[0].Trim();
            recipeData.mat1              = split[1].Trim();
            recipeData.mat2              = split[2].Trim();
            recipeData.conditionType     = CsvEnum.StringToConditionType(split[3]);
            recipeData.isHidden          = CsvUtil.StringToBool(split[4]);
            recipeData.unlockedByDefault = CsvUtil.StringToBool(split[5]);
            return recipeData;
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

        // bosses.csv: id,name,element,hp,armor,moveSpeed,isFlying,ignorePath,summonId,summonInterval,preDamageCapRatio
        public static BossData CsvToBossData(string line)
        {
            var split = line.Split(',');
            if (split.Length < 11) return null;

            BossData bossData = new BossData();
            bossData.id                = split[0].Trim();
            bossData.name              = split[1].Trim();
            bossData.element           = CsvEnum.StringToElement(split[2]);
            bossData.hp                = CsvUtil.StringToFixed(split[3]);
            bossData.armor             = CsvUtil.StringToFixed(split[4]);
            bossData.moveSpeed         = CsvUtil.StringToFixed(split[5]);
            bossData.isFlying          = CsvUtil.StringToBool(split[6]);
            bossData.ignorePath        = CsvUtil.StringToBool(split[7]);
            bossData.summonId          = split[8].Trim();
            bossData.summonInterval    = CsvUtil.StringToInt(split[9]);
            bossData.preDamageCapRatio = CsvUtil.StringToFixed(split[10]);
            return bossData;
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

        // relics.csv: id,name,ruleType,targetGrade,targetElement,param1,param2,rarity
        public static RelicData CsvToRelicData(string line)
        {
            var split = line.Split(',');
            if (split.Length < 8) return null;

            RelicData relicData = new RelicData();
            relicData.id            = split[0].Trim();
            relicData.name          = split[1].Trim();
            relicData.ruleType      = split[2].Trim();
            relicData.targetGrade   = split[3].Trim();
            relicData.targetElement = split[4].Trim();
            relicData.param1        = split[5].Trim();
            relicData.param2        = split[6].Trim();
            relicData.rarity        = split[7].Trim();
            return relicData;
        }

        // ---- 파일 텍스트 -> 리스트 로더 ----

        public static List<UnitData> LoadUnits(string fileText)
        {
            List<UnitData> resultList = new List<UnitData>();
            var lineList = CsvUtil.CsvToDataLines(fileText);
            foreach (var line in lineList)
            {
                var data = CsvToUnitData(line);
                if (data == null)
                {
                    continue;
                }
                resultList.Add(data);
            }
            return resultList;
        }

        public static List<RecipeData> LoadRecipes(string fileText)
        {
            List<RecipeData> resultList = new List<RecipeData>();
            var lineList = CsvUtil.CsvToDataLines(fileText);
            foreach (var line in lineList)
            {
                var data = CsvToRecipeData(line);
                if (data == null)
                {
                    continue;
                }
                resultList.Add(data);
            }
            return resultList;
        }

        public static List<WaveData> LoadWaves(string fileText)
        {
            List<WaveData> resultList = new List<WaveData>();
            var lineList = CsvUtil.CsvToDataLines(fileText);
            foreach (var line in lineList)
            {
                var data = CsvToWaveData(line);
                if (data == null)
                {
                    continue;
                }
                resultList.Add(data);
            }
            return resultList;
        }

        public static List<BossData> LoadBosses(string fileText)
        {
            List<BossData> resultList = new List<BossData>();
            var lineList = CsvUtil.CsvToDataLines(fileText);
            foreach (var line in lineList)
            {
                var data = CsvToBossData(line);
                if (data == null)
                {
                    continue;
                }
                resultList.Add(data);
            }
            return resultList;
        }

        public static List<RelicData> LoadRelics(string fileText)
        {
            List<RelicData> resultList = new List<RelicData>();
            var lineList = CsvUtil.CsvToDataLines(fileText);
            foreach (var line in lineList)
            {
                var data = CsvToRelicData(line);
                if (data == null)
                {
                    continue;
                }
                resultList.Add(data);
            }
            return resultList;
        }

        public static List<EnemyData> LoadEnemies(string fileText)
        {
            List<EnemyData> resultList = new List<EnemyData>();
            var lineList = CsvUtil.CsvToDataLines(fileText);
            foreach (var line in lineList)
            {
                var data = CsvToEnemyData(line);
                if (data == null)
                {
                    continue;
                }
                resultList.Add(data);
            }
            return resultList;
        }

        private static string RejoinFrom(string[] split, int startIndex)
        {
            if (startIndex >= split.Length) return string.Empty;
            List<string> partList = new List<string>();
            for (int i = startIndex; i < split.Length; ++i)
            {
                partList.Add(split[i]);
            }
            return string.Join(",", partList).Trim();
        }
    }
}
