namespace Synthesis.Core.Combat
{
    // STEP 3. 기반 도구 - 방어력 감소 공식. 전투 판정과 UI 표시가 같은 한 벌을 쓴다(CLAUDE.md 4-7).
    // 따로 구현하면 표시된 감소율과 실제 피해가 갈라진다.
    public static class ArmorFormula
    {
        // [TEMP] 워크래프트3 계열 상수. 실피해 = 원피해 / (1 + K*방어력). 방어력 1당 유효체력 +6%,
        // 감소율은 100%에 점근하므로 완전 차단이 없다(관통 하한 불필요). K와 방어력 값은 시뮬로 재확정한다.
        public static readonly Fixed ArmorK = Fixed.FromRatio(6, 100); // 0.06

        // 방어력을 곱연산으로 감소시킨 실피해. 방어력 0 이면 원 피해 그대로.
        public static Fixed Reduced(Fixed atk, Fixed armor)
        {
            Fixed divisor = Fixed.One + ArmorK * armor;
            return divisor.raw > 0 ? atk / divisor : atk;
        }

        // 표시용 감소율 0~1. 1 - 1/(1 + K*방어력). 화면 표기 전용이라 double 로 낸다.
        public static double ReductionRatio(Fixed armor)
        {
            double divisor = 1.0 + ArmorK.ToDoubleForDisplay() * armor.ToDoubleForDisplay();
            if (divisor <= 0.0) return 0.0;
            return 1.0 - 1.0 / divisor;
        }
    }
}
