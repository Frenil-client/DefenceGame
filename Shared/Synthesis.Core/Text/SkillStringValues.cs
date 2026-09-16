using Synthesis.Core.Data;

namespace Synthesis.Core.Text
{
    // STEP 3. 기반 도구 - 스킬 설명의 치환자를 SkillData 에서 채운다.
    //   수치 단위가 효과마다 다르다. 비율(0~1)로 저장된 값은 퍼센트로 바꿔야 문장이 자연스럽다.
    //   변환을 문자열 쪽에 두면 번역마다 반복되므로 여기서 한 번에 처리한다.
    public sealed class SkillStringValues : IStringValues
    {
        private SkillData skill;

        public SkillStringValues Bind(SkillData value)
        {
            skill = value;
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
                    value = MagnitudeText();
                    return true;
                case "duration":
                    value = Number(skill.duration);
                    return true;
                case "count":
                    value = skill.count.ToString();
                    return true;
                default:
                    return false;
            }
        }

        // 효과별로 magnitude 의 뜻이 다르다. 비율인 것만 퍼센트로 바꾼다.
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
                    return Percent(skill.magnitude);
                default:
                    // 장판 dps 와 방어력 감소량은 비율이 아니라 절대값이라 저장값 그대로가 표시값이다.
                    return Number(skill.magnitude);
            }
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
