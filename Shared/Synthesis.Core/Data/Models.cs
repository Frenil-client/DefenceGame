using System.Collections.Generic;

namespace Synthesis.Core.Data
{
    // STEP 1. 기반 도구 - 데이터 모델. 스키마는 BALANCE_SPEC.md 10.
    // 전투 수치(hp, atk, range 등)는 재현성을 위해 Fixed(long 고정소수점)로 보관한다.

    public sealed class UnitData
    {
        public string id;
        public string name;
        public Grade grade;
        public Element element;
        public Role role;
        public Placement placement;
        public int cost;
        public Fixed hp;
        public Fixed atk;
        public Fixed atkSpeed;   // 초당 공격 횟수
        public Fixed range;
        public int blockCount;
        public int redeployCd;         // 재배치 쿨타임(틱). 12초 = 240틱 (ARCHITECTURE.md 4-2)
        public bool isAdvance;
        public string note;
    }

    public sealed class RecipeData
    {
        public string resultId;
        public string mat1;
        public string mat2;
        public ConditionType conditionType;
        public bool isHidden;
        public bool unlockedByDefault;
    }

    public sealed class WaveData
    {
        public int waveIndex;
        public string enemySetId;
        public int spawnCount;
        public int spawnInterval;      // 틱
        public bool isBoss;
        public string bossId;
        public Fixed difficultyScale;
    }

    public sealed class BossData
    {
        public string id;
        public string name;
        public Element element;
        public Fixed hp;
        public Fixed armor;
        public Fixed moveSpeed;
        public bool isFlying;
        public bool ignorePath;
        public string summonId;
        public int summonInterval;     // 틱
        public Fixed preDamageCapRatio;
    }

    // STEP 2. 뼈대 - 적 데이터. enemies.csv 스키마는 STEP 2 에서 새로 도입(TEMP).
    public sealed class EnemyData
    {
        public string id;
        public string name;
        public Fixed hp;
        public Fixed atk;        // 저지 중인 근접 유닛에게 주는 초당 피해
        public Fixed moveSpeed;  // 초당 이동 셀 수
    }

    public sealed class RelicData
    {
        public string id;
        public string name;
        public string ruleType;
        public string targetGrade;
        public string targetElement;
        public string param1;
        public string param2;
        public string rarity;
    }

    // 로드된 전체 데이터 묶음. 순회 순서 의존을 막기 위해 List 로 보관한다 (ARCHITECTURE.md 4-4).
    public sealed class GameDatabase
    {
        public List<UnitData> unitList = new List<UnitData>();
        public List<RecipeData> recipeList = new List<RecipeData>();
        public List<WaveData> waveList = new List<WaveData>();
        public List<BossData> bossList = new List<BossData>();
        public List<RelicData> relicList = new List<RelicData>();
        public List<EnemyData> enemyList = new List<EnemyData>();
    }
}
