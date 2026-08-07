namespace Synthesis.Core.Data
{
    // STEP 1. 기반 도구 - 용어집(SPEC.md 6)과 데이터 스키마(BALANCE_SPEC.md 10)의 축 정의.
    // CSV 문자열 값과의 매핑은 CsvEnum.cs 가 담당한다.

    public enum Grade
    {
        Common,
        Rare,
        Unique,
        Hidden
    }

    public enum Element
    {
        Fire,
        Ice,
        Thunder,
        Physical,
        Holy
    }

    // 역할 4종. 관통(pierce)은 역할에서 제외하고 전격 속성 효과로 옮겼다 (BALANCE_SPEC.md 1-2, 개정).
    public enum Role
    {
        Single,
        Splash,
        Dot,
        Support
    }

    public enum Placement
    {
        Melee,
        Ranged
    }

    // 흔함 -> 레어 성립 조건 (BALANCE_SPEC.md 4-1). 레어 이상은 Fixed(고정 레시피).
    public enum ConditionType
    {
        SameElement,
        SameRole,
        Fixed
    }
}
