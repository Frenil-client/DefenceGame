using System.Collections.Generic;

namespace Synthesis.Core.Data
{
    // STEP 1. 기반 도구 - 데이터 모델 (v0.4). 스키마는 BALANCE_SPEC.md 17.
    // 전투 수치는 재현성을 위해 Fixed(long 고정소수점)로 보관한다.

    public sealed class UnitData
    {
        public string id;
        public string name;
        public int tier;           // 1..5
        public Klass klass;
        // 유닛에 hp 는 없다. 몬스터가 유닛을 공격하지 않으므로 유닛 체력이라는 개념 자체가 없다.
        public Fixed atk;
        public Fixed atkSpeed;     // 초당 공격 횟수
        public Fixed range;
        public List<string> skillIds = new List<string>(); // 이 유닛이 가진 스킬 id(0개 이상). 매핑은 추후 데이터로 채운다
        public string note;
    }

    // 패시브 스킬 정의(트리거 + 효과 + 수치). 유닛 스킬은 배치만으로 작동한다(액티브 없음).
    // 효과별로 쓰는 파라미터가 다르다(주석 참고). 효과 로직은 전투 레이어가, 정의/파싱은 Core 가 소유한다.
    public sealed class SkillData
    {
        public string id;
        public SkillTrigger trigger;
        public Fixed triggerN;   // EveryNthAttack: N(정수), ChanceOnAttack: 확률(0~1), Passive: 무시
        public SkillEffect effect;
        public Fixed radius;     // 광역/오라 반경(셀)
        public Fixed magnitude;  // 효과 세기(배수/비율/dps/방어감소량 등, 효과별 의미)
        public Fixed duration;   // 지속시간(초). 도트/감속에만. 오라/즉발은 무시
        public int count;        // 대상 수(다중타격/관통)
        public BuffStat buffStat;// 아군 버프 대상 스탯(AllyBuff 에서만)
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
        // 몬스터에 atk 는 없다. 유닛을 공격하는 경우가 없어 줄 피해가 없다.
        public Fixed armor;   // 곱연산 방어력. 보스 전용이 아니라 일반 몬스터도 원형별로 갖는다
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
        public int selectionReward;  // 격파 보상 선택권 수 (SPEC 3-6)
        public string note;
    }

    public sealed class WaveData
    {
        public int waveIndex;
        public string enemySetId;
        public int spawnCount;
        public bool isBoss;
        public string bossId;
        public Fixed hpScale;   // 원형 체력에 곱한다. 보스는 1.0(bosses.csv 절대값을 쓴다)
        public int armorAdd;    // 원형 방어력에 더한다. 방깎이 절대값이라 증가도 절대값이다
    }

    public sealed class GameDatabase
    {
        public List<UnitData> unitList = new List<UnitData>();
        public List<RecipeData> recipeList = new List<RecipeData>();
        public List<WaveData> waveList = new List<WaveData>();
        public List<BossData> bossList = new List<BossData>();
        public List<EnemyData> enemyList = new List<EnemyData>();
        public List<SkillData> skillList = new List<SkillData>();
    }
}
