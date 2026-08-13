using System.Collections.Generic;

namespace Synthesis.Core.Data
{
    // STEP 1. 기반 도구 - 데이터 모델 (v0.4). 스키마는 BALANCE_SPEC.md 17.
    // 전투 수치는 재현성을 위해 Fixed(long 고정소수점)로 보관한다.

    public sealed class UnitData
    {
        public string id;
        public string name;
        public int tier;           // 1..5, 도플갱어는 0
        public Klass klass;
        public int cost;
        public Fixed hp;
        public Fixed atk;
        public Fixed atkSpeed;     // 초당 공격 횟수
        public Fixed range;
        public bool isDoppel;      // 도플갱어(변환 전 공격 불가)
        public string note;
    }

    // v0.4 조합식: 재료 2~4기(고정 레시피). 같은 재료가 반복될 수 있다(예: 전사+전사).
    public sealed class RecipeData
    {
        public string resultId;
        public List<string> materials = new List<string>();
    }

    public sealed class EnemyData
    {
        public string id;
        public string name;
        public Fixed hp;
        public Fixed atk;
        public Fixed moveSpeed;
    }

    public sealed class BossData
    {
        public string id;
        public string name;
        public Fixed hp;
        public Fixed armor;
        public Fixed moveSpeed;
        public int timeLimitTicks;   // 제한시간(틱). 넘기면 패배 (BALANCE 13)
        public int doppelReward;     // 격파 보상 도플갱어 수 (SPEC 3-6)
        public string note;
    }

    public sealed class WaveData
    {
        public int waveIndex;
        public string enemySetId;
        public int spawnCount;
        public int spawnInterval;    // 틱
        public bool isBoss;
        public string bossId;
        public Fixed difficultyScale;
    }

    public sealed class GameDatabase
    {
        public List<UnitData> unitList = new List<UnitData>();
        public List<RecipeData> recipeList = new List<RecipeData>();
        public List<WaveData> waveList = new List<WaveData>();
        public List<BossData> bossList = new List<BossData>();
        public List<EnemyData> enemyList = new List<EnemyData>();
    }
}
