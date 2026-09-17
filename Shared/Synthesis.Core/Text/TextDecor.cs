namespace Synthesis.Core.Text
{
    // STEP 3. 기반 도구 - 수치와 문단 제목의 서식 감싸개.
    //   색과 굵기를 코드가 아니라 문자열 테이블이 정한다. 팔레트를 바꿀 때 한 곳만 고치면 되고,
    //   언어마다 강조 방식이 다를 여지도 남는다. 스킬 설명 64행에 색을 박는 것을 피하려는 것이 목적이다.
    //
    // Core 와 Presentation 이 같은 한 벌을 쓴다(CLAUDE.md 4-7). 표시 코드가 따로 감싸면 색이 갈라진다.
    public static class TextDecor
    {
        public const string ValueUpKey = "str.value.up";
        public const string ValueDownKey = "str.value.down";
        public const string SectionHeaderKey = "str.section.header";
        public const string SectionEmptyKey = "str.section.empty";

        // 효과 세기 한 값. 올라가는 수치는 up, 깎는 수치(감속/방깎)는 down 이다.
        //   기준은 스탯 표기와 같다. 대상의 수치가 오르면 초록, 내려가면 빨강이다.
        public static string FormatValue(StringTable table, Language language, string text, bool reduction)
        {
            if (table == null) return text;

            StringValues wrapValues = new StringValues();
            wrapValues.Set("value", text);
            return StringFormatter.Format(table.Get(reduction ? ValueDownKey : ValueUpKey, language), wrapValues);
        }

        // 선택 패널의 문단 제목. empty 면 그 절에 내용이 없다는 뜻이라 흐리게 낸다.
        public static string FormatSection(StringTable table, Language language, string label, bool empty)
        {
            if (table == null) return label;

            StringValues wrapValues = new StringValues();
            wrapValues.Set("label", label);
            return StringFormatter.Format(table.Get(empty ? SectionEmptyKey : SectionHeaderKey, language), wrapValues);
        }

        // STEP 3. 기반 도구 - 선택 패널과 다른 표시자가 같은 스탯 증감 서식을 사용한다.
        public static string FormatStat(StringTable table, Language language, string labelKey,
            float baseValue, float effectiveValue, string numberFormat = "0.##")
        {
            var delta = effectiveValue - baseValue;
            StringValues values = new StringValues();
            values.Set("label", table.Get(labelKey, language));
            values.Set("value", effectiveValue.ToString(numberFormat));
            var key = "str.stat.plain";
            if (System.Math.Abs(delta) >= 0.005f)
            {
                values.Set("delta", System.Math.Abs(delta).ToString(numberFormat));
                key = delta > 0f ? "str.stat.buffed" : "str.stat.debuffed";
            }
            return StringFormatter.Format(table.Get(key, language), values);
        }
    }
}
