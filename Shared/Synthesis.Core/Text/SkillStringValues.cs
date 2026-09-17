using Synthesis.Core.Data;

namespace Synthesis.Core.Text
{
    // STEP 3. 기반 도구 - 스킬 설명의 치환자를 SkillData 에서 채운다.
    //   수치 단위가 효과마다 다르다. 비율(0~1)로 저장된 값은 퍼센트로 바꿔야 문장이 자연스럽다.
    //   변환을 문자열 쪽에 두면 번역마다 반복되므로 여기서 한 번에 처리한다.
    public sealed class SkillStringValues : IStringValues
    {
        private SkillData skill;
        private StringTable colorTable; // null 이면 색 없이 값만 낸다(수치 규칙만 보는 테스트용)
        private Language colorLanguage;

        public SkillStringValues Bind(SkillData value)
        {
            skill = value;
            colorTable = null;
            return this;
        }

        // 색을 입혀 낸다. 효과 세기가 어느 방향인지 숫자만 봐서는 안 읽혀서 서식으로 갈라 준다.
        //   감싸개는 문자열 테이블이 갖고 있다. 여기서 색을 정하지 않는다(TextDecor).
        public SkillStringValues Bind(SkillData value, StringTable table, Language language)
        {
            skill = value;
            colorTable = table;
            colorLanguage = language;
            return this;
        }

        public bool TryGetValue(string name, out string value)
        {
            value = null;
            if (skill == null) return false;

            switch (name)
            {
                case "triggerN":
                    // 확률 발동은 0~1 로 저장돼 있어 퍼센트로 바꾼다. 평타 N회는 그대로 정수다.
                    if (skill.trigger == SkillTrigger.ChanceOnAttack) value = Percent(skill.triggerN);
                    else value = Number(skill.triggerN);
                    return true;
                case "radius":
                    value = Number(skill.radius);
                    return true;
                case "magnitude":
                    value = Colored(MagnitudeText());
                    return true;
                case "duration":
                    value = Number(skill.duration);
                    return true;
                case "count":
                    // 다중 타격과 관통은 대상 수가 곧 효과의 세기다. 다른 스킬의 magnitude 자리에 해당한다.
                    value = Colored(skill.count.ToString());
                    return true;
                default:
                    return false;
            }
        }

        // 효과별로 magnitude 의 뜻이 다르다. 비율인 것만 퍼센트로 바꾼다.
        //   퍼센트 기호까지 값에 포함한다. 기호를 문장에 두면 색을 입힐 때 숫자만 칠해지고 "%" 가 밖에 남는다.
        //   피해 배수도 퍼센트로 낸다. "1배 추가" 는 총 2배라는 뜻인데 화면에서 1배로 읽혀 헷갈린다.
        //   BonusDamage 는 더해지는 몫이라 "200% 추가", Crit 은 곱해지는 총량이라 "피해 200%" 로 문장이 갈린다.
        //   두 표기를 곱하면 실제 피해가 나온다. 가산 300% x 치명 200% = 600% (결정 D01 의 계산 단계와 같다).
        private string MagnitudeText()
        {
            switch (skill.effect)
            {
                case SkillEffect.AreaDamage:
                case SkillEffect.Slow:
                case SkillEffect.AllyBuff:
                case SkillEffect.BonusDamage:
                case SkillEffect.Crit:
                    return Percent(skill.magnitude) + "%";
                default:
                    // 장판 dps 와 방어력 감소량은 비율이 아니라 절대값이라 저장값 그대로가 표시값이다.
                    return Number(skill.magnitude);
            }
        }

        // 효과 세기에만 색을 준다. 반경과 지속시간, 발동 조건은 세기가 아니라 조건이라 그대로 둔다.
        //   숫자를 전부 칠하면 어디가 세기인지 다시 안 보인다.
        private string Colored(string text)
        {
            return TextDecor.FormatValue(colorTable, colorLanguage, text, IsReduction());
        }

        // 대상의 수치를 깎는 효과인가. 감속과 방깎만 내려가는 숫자다.
        //   피해 계열은 우리 쪽 출력이 올라가는 것이라 올라가는 수치로 본다.
        private bool IsReduction()
        {
            return skill.effect == SkillEffect.Slow || skill.effect == SkillEffect.ArmorReduction;
        }

        private static string Number(Fixed value)
        {
            return value.ToDoubleForDisplay().ToString("0.##");
        }

        private static string Percent(Fixed value)
        {
            return (value.ToDoubleForDisplay() * 100.0).ToString("0.##");
        }
    }
}
