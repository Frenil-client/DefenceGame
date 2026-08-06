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

    public enum Role
    {
        Single,
        Splash,
        Pierce,
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
