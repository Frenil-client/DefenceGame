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
}
