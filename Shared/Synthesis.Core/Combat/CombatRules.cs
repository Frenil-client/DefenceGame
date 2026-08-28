namespace Synthesis.Core.Combat
{
    // STEP 3. 기반 도구 - 전투 상한 규칙. 전투와 UI 가 같은 한 벌을 쓴다(CLAUDE.md 4-7).
    public static class CombatRules
    {
        // [TEMP] 감속이 아무리 쌓여도 기본 이동 속도의 30% 밑으로는 내려가지 않는다.
        // 상한이 없으면 감속 스킬을 모으는 것만으로 몬스터가 사실상 정지해 루프 디펜스가 성립하지 않는다.
        // 방어력의 하한 0(ArmorFormula 쪽)과 짝을 이루는 규칙이다. 값은 시뮬로 재확정한다.
        public static readonly Fixed MinSpeedRatio = Fixed.FromRatio(30, 100);

        // 누적 감속 비율(0~1)을 실제 속도 배수로 바꾼다. 하한을 넘겨 느려지지 않는다.
        public static Fixed SpeedRatioAfterSlow(Fixed slowRatio)
        {
            Fixed remain = Fixed.One - slowRatio;
            if (remain.raw < MinSpeedRatio.raw) return MinSpeedRatio;
            if (remain.raw > Fixed.One.raw) return Fixed.One;
            return remain;
        }
    }
}
