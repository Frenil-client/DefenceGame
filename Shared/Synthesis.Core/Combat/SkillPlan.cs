using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Core.Combat
{
    // STEP 3. 핵심 - 한 번의 평타에서 스킬을 해석한 결과.
    //   효과를 만나는 대로 적용하지 않고 전부 모은 뒤 정해진 단계로 계산한다(결정 D01).
    public struct SkillPlan
    {
        public Fixed bonusSum;   // BonusDamage 가산분의 합. 기본 1배는 포함하지 않는다
        public Fixed critMult;   // Crit 배율의 곱. 미발동이면 1
        public int extraTargets; // MultiTarget/Pierce 로 늘어난 추가 대상 수

        // 이번 평타의 최종 피해 배수.
        public Fixed Multiplier()
        {
            return CombatRules.AttackMultiplier(bonusSum, critMult);
        }
    }

    // STEP 3. 핵심 - 평타 스킬 해석. 전투 스크립트와 테스트가 같은 한 벌을 쓴다(CLAUDE.md 4-7).
    //   Unity 의 난수를 직접 읽지 않고 굴린 값을 받는다. 그래야 헤드리스 테스트가 같은 경로를 탈 수 있다.
    public static class SkillPlanner
    {
        // 트리거가 이번 평타에 발동하는가. roll 은 0~1 이며 ChanceOnAttack 에서만 쓴다.
        public static bool TriggerFires(SkillData s, int attackCount, Fixed roll)
        {
            if (s == null) return false;

            switch (s.trigger)
            {
                case SkillTrigger.Passive:
                    return true;
                case SkillTrigger.EveryNthAttack:
                    int n = (int)s.triggerN.ToIntRounded();
                    return n > 0 && attackCount % n == 0;
                case SkillTrigger.ChanceOnAttack:
                    return roll < s.triggerN;
                default:
                    return false;
            }
        }

        // 발동한 스킬을 모아 이번 평타의 계획을 만든다.
        //   rollList 는 skills 와 같은 길이의 난수(0~1)다. 모자라면 0 으로 본다(확정 발동).
        //   onHitSlowOut 과 areaOut 은 호출자가 재사용하는 버퍼다. 여기서 비우고 채운다.
        public static SkillPlan BuildAttackPlan(List<SkillData> skills, List<Fixed> rollList, int attackCount,
            List<SkillData> onHitSlowOut, List<SkillData> areaOut)
        {
            SkillPlan plan;
            plan.bonusSum = Fixed.Zero;
            plan.critMult = Fixed.One;
            plan.extraTargets = 0;

            if (onHitSlowOut != null) onHitSlowOut.Clear();
            if (areaOut != null) areaOut.Clear();
            if (skills == null) return plan;

            for (int i = 0; i < skills.Count; ++i)
            {
                SkillData s = skills[i];
                if (s == null) continue;

                Fixed roll = rollList != null && i < rollList.Count ? rollList[i] : Fixed.Zero;
                if (!TriggerFires(s, attackCount, roll)) continue;

                switch (s.effect)
                {
                    case SkillEffect.BonusDamage:
                        plan.bonusSum = plan.bonusSum + s.magnitude;
                        break;
                    case SkillEffect.Crit:
                        plan.critMult = plan.critMult * s.magnitude;
                        break;
                    case SkillEffect.MultiTarget:
                        plan.extraTargets += ExtraFrom(s.count);
                        break;
                    case SkillEffect.Pierce:
                        plan.extraTargets += ExtraFrom(s.count);
                        break;
                    case SkillEffect.AreaDamage:
                        if (areaOut != null) areaOut.Add(s);
                        break;
                    case SkillEffect.Slow:
                        // 반경이 있으면 오라 감속이라 여기서 처리하지 않는다. 반경 0 만 평타 명중 감속이다.
                        if (s.radius.raw <= 0 && onHitSlowOut != null) onHitSlowOut.Add(s);
                        break;
                    // AllyBuff / ArmorReduction / DamageZone 은 오라 경로(AuraField)가 맡는다.
                }
            }

            return plan;
        }

        // count 는 주 대상을 포함한 총 타격 수다. 추가 대상은 그보다 하나 적다.
        private static int ExtraFrom(int count)
        {
            int extra = count - 1;
            if (extra < 0) return 0;
            return extra;
        }
    }
}
