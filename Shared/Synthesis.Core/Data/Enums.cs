namespace Synthesis.Core.Data
{
    // STEP 1. 기반 도구 - v0.4 계열 축 (BALANCE_SPEC.md 1). 유닛의 유일한 분류 축이다.
    // 속성/역할/배치 축은 v0.4 에서 폐기했다.
    public enum Klass
    {
        War,   // 전사 - 근거리 단일 고피해, 이동 빠름
        Arc,   // 궁수 - 원거리 단일, 사거리 최장
        Mag,   // 법사 - 광역, 사거리 김
        Pri,   // 사제 - 아군 강화/적 약화
        Thi,   // 도적 - 고속 공격, 치명타
        Spi    // 정령 - 지속 피해/상태이상
    }

    // 스킬 발동 조건. 유닛 스킬은 전부 패시브(배치만으로 작동)지만 발동 시점이 다르다.
    public enum SkillTrigger
    {
        Passive,        // 상시 (오라/평타 속성)
        EveryNthAttack, // 평타 N회째마다 (triggerN = N)
        ChanceOnAttack  // 평타마다 확률 발동 (triggerN = 확률 0~1)
    }

    // 스킬 효과(원시 기능). 트리거와 조합해 데이터로 스킬을 만든다. 고정값 단계(B). 동적/시너지는 이후 확장.
    public enum SkillEffect
    {
        MultiTarget,    // 평타가 가까운 count 명을 동시 타격
        AreaDamage,     // 명중 지점 반경(radius)에 magnitude 비율 광역 피해
        Pierce,         // 진행 방향 직선으로 count 체 관통
        BonusDamage,    // magnitude 배 추가 피해
        Crit,           // magnitude 배 피해 (보통 ChanceOnAttack 과 함께)
        DamageOverTime, // 대상에 magnitude(dps)로 duration 초 지속 피해
        Slow,           // 대상 이동속도 magnitude(0~1) 만큼 감속, duration 초 (상시면 지속 무시)
        AllyBuff,       // 반경(radius) 내 아군 buffStat 를 magnitude(0~1) 비율 상향
        ArmorReduction  // 반경(radius) 내 적 방어력을 magnitude 만큼 절대 감소(% 아님)
    }

    // 아군 버프 대상 스탯(AllyBuff 효과에서 사용).
    public enum BuffStat
    {
        None,
        Atk,
        AtkSpeed,
        Range
    }
}
