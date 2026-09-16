using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Core.Combat
{
    // 필드에 깔린 오라 한 건. 매 프레임 다시 모은다.
    public struct AuraSample
    {
        public string skillId;
        public Fixed x;
        public Fixed y;
        public Fixed radius;
        public Fixed magnitude;
        public SkillEffect effect;
        public BuffStat stat;
    }

    // STEP 3. 핵심 - 오라 표본과 중첩 판정(UNIT_SKILLS.md 3장).
    //   스택 키는 스킬 id 다. 같은 스킬은 필드에 몇 기가 깔려 있어도 1회만 센다.
    //   유닛마다 전체 유닛을 다시 훑지 않도록 한 프레임에 한 번만 모으고 모든 질의가 그 표본을 공유한다.
    public sealed class AuraField
    {
        private readonly List<AuraSample> sampleList = new List<AuraSample>();
        private readonly HashSet<string> stackScratch = new HashSet<string>(); // 순회하지 않고 조회만 한다

        public int Count
        {
            get { return sampleList.Count; }
        }

        public void Clear()
        {
            sampleList.Clear();
        }

        public void Add(AuraSample sample)
        {
            sampleList.Add(sample);
        }

        // 오라 경로(상시 + 반경)로만 실행되는 효과인가. 평타 효과와 갈라지는 기준이다.
        public static bool IsAuraEffect(SkillEffect effect)
        {
            return effect == SkillEffect.AllyBuff
                || effect == SkillEffect.ArmorReduction
                || effect == SkillEffect.Slow
                || effect == SkillEffect.DamageZone;
        }

        // 스킬 정의가 이번 프레임의 오라 표본이 될 자격이 있는가. 반경 0 과 비패시브는 오라가 아니다.
        public static bool IsAuraSource(SkillData s)
        {
            if (s == null) return false;
            if (s.trigger != SkillTrigger.Passive) return false;
            if (s.radius.raw <= 0) return false;
            return IsAuraEffect(s.effect);
        }

        // 대상 위치에 걸리는 오라 세기의 합. 같은 스킬 id 는 1회만 센다.
        //   AllyBuff 는 스탯별로 따로 물어야 해서 stat 을 받는다. 나머지 효과는 BuffStat.None 을 넘긴다.
        public Fixed SumAt(SkillEffect effect, BuffStat stat, Fixed targetx, Fixed targety)
        {
            if (sampleList.Count == 0) return Fixed.Zero;

            stackScratch.Clear();
            Fixed total = Fixed.Zero;
            for (int i = 0; i < sampleList.Count; ++i)
            {
                AuraSample a = sampleList[i];
                if (a.effect != effect) continue;
                if (effect == SkillEffect.AllyBuff && a.stat != stat) continue;
                if (stackScratch.Contains(a.skillId)) continue;

                Fixed d2 = TargetSelector.DistanceSq(a.x, a.y, targetx, targety);
                if (d2.raw > (a.radius * a.radius).raw) continue;

                stackScratch.Add(a.skillId);
                total = total + a.magnitude;
            }
            return total;
        }
    }
}
