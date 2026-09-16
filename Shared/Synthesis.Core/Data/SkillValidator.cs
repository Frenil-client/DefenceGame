using System.Collections.Generic;

namespace Synthesis.Core.Data
{
    // STEP 3. 기반 도구 - 스킬 정의 적합성 검사 SKILL-01 부터 SKILL-05.
    //   파서는 필드 변환만 하므로 "파싱은 되는데 실행되지 않는" 조합이 그대로 통과했다(점검 보고서 4-C).
    //   예: EVERYNTH(3) + ALLYBUFF 는 CSV 로 만들 수 있지만 전투가 오라 경로에서 Passive 만 읽어 아무 일도 안 일어난다.
    //   린터, 테스트, 에디터가 이 한 벌을 공유한다(CLAUDE.md 4-7). 실행부와 지원 조합이 갈라지면 안 된다.
    public static class SkillValidator
    {
        // 오라 경로(CollectAuras)에서만 실행되는 효과. 반드시 상시이고 반경이 있어야 한다.
        public static bool IsAuraOnlyEffect(SkillEffect effect)
        {
            return effect == SkillEffect.AllyBuff
                || effect == SkillEffect.ArmorReduction
                || effect == SkillEffect.DamageZone;
        }

        // 평타 경로(AttackMonster)에서 실행되는 효과. 트리거는 상시/N회/확률 전부 가능하다.
        public static bool IsOnHitEffect(SkillEffect effect)
        {
            return effect == SkillEffect.BonusDamage
                || effect == SkillEffect.Crit
                || effect == SkillEffect.MultiTarget
                || effect == SkillEffect.Pierce
                || effect == SkillEffect.AreaDamage;
        }

        // 스킬 목록 전체를 검사한다. 위반이 없으면 true. failuresOut 에는 스킬 id 와 이유를 남긴다.
        public static bool Validate(List<SkillData> skillList, List<string> failuresOut)
        {
            if (skillList == null) return true;

            bool ok = true;

            // SKILL-01 id 가 비었거나 중복인가
            Dictionary<string, int> seenCount = new Dictionary<string, int>();
            for (int i = 0; i < skillList.Count; ++i)
            {
                SkillData s = skillList[i];
                if (s == null) continue;
                if (string.IsNullOrEmpty(s.id))
                {
                    ok = false;
                    Add(failuresOut, "SKILL-01 " + i + "번째 행의 id 가 비었다");
                    continue;
                }

                int already;
                if (seenCount.TryGetValue(s.id, out already))
                {
                    ok = false;
                    Add(failuresOut, "SKILL-01 " + s.id + " id 중복");
                    seenCount[s.id] = already + 1;
                    continue;
                }
                seenCount[s.id] = 1;
            }

            for (int i = 0; i < skillList.Count; ++i)
            {
                SkillData s = skillList[i];
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                if (!ValidateOne(s, failuresOut)) ok = false;
            }

            return ok;
        }

        // 스킬 한 줄을 검사한다. 트리거와 효과의 지원 조합이 핵심이고 나머지는 수치 범위다.
        public static bool ValidateOne(SkillData s, List<string> failuresOut)
        {
            if (s == null) return true;

            bool ok = true;
            string tag = s.id + " ";

            // SKILL-02 트리거 인자
            if (s.trigger == SkillTrigger.EveryNthAttack)
            {
                if (s.triggerN.raw <= 0 || s.triggerN.raw % Fixed.Scale != 0)
                {
                    ok = false;
                    Add(failuresOut, "SKILL-02 " + tag + "EVERYNTH 의 triggerN 은 양의 정수여야 한다 (현재 " + s.triggerN + ")");
                }
            }
            else if (s.trigger == SkillTrigger.ChanceOnAttack)
            {
                if (s.triggerN.raw <= 0 || s.triggerN.raw > Fixed.One.raw)
                {
                    ok = false;
                    Add(failuresOut, "SKILL-02 " + tag + "CHANCE 의 triggerN 은 0 초과 1 이하여야 한다 (현재 " + s.triggerN + ")");
                }
            }

            // SKILL-03 트리거와 효과의 지원 조합. 실행부가 읽지 않는 조합은 여기서 막는다.
            if (IsAuraOnlyEffect(s.effect))
            {
                if (s.trigger != SkillTrigger.Passive)
                {
                    ok = false;
                    Add(failuresOut, "SKILL-03 " + tag + s.effect + " 는 오라 효과라 PASSIVE 만 실행된다 (현재 " + s.trigger + ")");
                }
                if (s.radius.raw <= 0)
                {
                    ok = false;
                    Add(failuresOut, "SKILL-03 " + tag + s.effect + " 는 반경이 있어야 오라로 수집된다 (현재 " + s.radius + ")");
                }
            }
            else if (s.effect == SkillEffect.Slow)
            {
                // 반경이 있으면 오라 감속, 없으면 평타 명중 감속이다. 오라 쪽만 PASSIVE 를 요구한다.
                if (s.radius.raw > 0 && s.trigger != SkillTrigger.Passive)
                {
                    ok = false;
                    Add(failuresOut, "SKILL-03 " + tag + "반경이 있는 SLOW 는 오라라 PASSIVE 만 실행된다 (현재 " + s.trigger + ")");
                }
                if (s.radius.raw <= 0 && s.duration.raw <= 0)
                {
                    ok = false;
                    Add(failuresOut, "SKILL-03 " + tag + "온힛 SLOW 는 duration 이 있어야 적용된다");
                }
            }
            else if (!IsOnHitEffect(s.effect))
            {
                ok = false;
                Add(failuresOut, "SKILL-03 " + tag + s.effect + " 를 실행하는 경로가 없다");
            }

            // SKILL-04 효과별 필수 수치
            if (s.effect == SkillEffect.MultiTarget || s.effect == SkillEffect.Pierce)
            {
                if (s.count < 2)
                {
                    ok = false;
                    Add(failuresOut, "SKILL-04 " + tag + s.effect + " 의 count 는 2 이상이어야 한다 (현재 " + s.count + ")");
                }
            }
            else if (s.effect == SkillEffect.AreaDamage)
            {
                if (s.radius.raw <= 0)
                {
                    ok = false;
                    Add(failuresOut, "SKILL-04 " + tag + "AREADAMAGE 는 반경이 있어야 한다");
                }
                if (s.magnitude.raw <= 0)
                {
                    ok = false;
                    Add(failuresOut, "SKILL-04 " + tag + "AREADAMAGE 의 magnitude(피해 비율)가 0 이하다");
                }
            }
            else if (s.effect == SkillEffect.Crit)
            {
                if (s.magnitude.raw <= Fixed.One.raw)
                {
                    ok = false;
                    Add(failuresOut, "SKILL-04 " + tag + "CRIT 의 magnitude 는 1 을 넘어야 피해가 늘어난다 (현재 " + s.magnitude + ")");
                }
            }
            else if (s.magnitude.raw <= 0)
            {
                ok = false;
                Add(failuresOut, "SKILL-04 " + tag + s.effect + " 의 magnitude 가 0 이하다");
            }

            // SKILL-05 비율로 쓰는 효과의 상한과 buffStat 필수 여부
            if (s.effect == SkillEffect.Slow && s.magnitude.raw > Fixed.One.raw)
            {
                ok = false;
                Add(failuresOut, "SKILL-05 " + tag + "SLOW 의 magnitude 는 0~1 비율이다 (현재 " + s.magnitude + ")");
            }
            if (s.effect == SkillEffect.AllyBuff && s.buffStat == BuffStat.None)
            {
                ok = false;
                Add(failuresOut, "SKILL-05 " + tag + "ALLYBUFF 는 buffStat 이 필요하다");
            }
            if (s.effect != SkillEffect.AllyBuff && s.buffStat != BuffStat.None)
            {
                ok = false;
                Add(failuresOut, "SKILL-05 " + tag + s.effect + " 는 buffStat 을 쓰지 않는다 (현재 " + s.buffStat + ")");
            }

            return ok;
        }

        private static void Add(List<string> list, string message)
        {
            if (list == null) return;
            list.Add(message);
        }
    }
}
