using System.Collections.Generic;
using Synthesis.Core.Combat;
using Synthesis.Core.Data;

namespace Synthesis.Core.Tests
{
    // STEP 3. 검증 - 평타 피해 단계(T01), 장판 시간 누산(T02), 스킬 정의 적합성(T03).
    public class SkillRulesTests
    {
        // ---- T01. 피해 계산 순서 독립 ----

        // 가산과 치명을 어느 순서로 모아도 같은 배수가 나와야 한다. 예전에는 skillIds 나열 순서가 곧 산술 순서였다.
        [Fact]
        public void AttackMultiplier_IgnoresCollectionOrder()
        {
            Fixed heavy = Fixed.FromInt(2); // HEAVY3 가산 2배
            Fixed crit  = Fixed.FromInt(2); // CRIT2 치명 2배

            Fixed forward  = ApplyInOrder(new bool[] { true, false }, heavy, crit);
            Fixed reversed = ApplyInOrder(new bool[] { false, true }, heavy, crit);

            Assert.Equal(forward.raw, reversed.raw);
            Assert.Equal(Fixed.FromInt(6).raw, forward.raw); // (1 + 2) * 2
        }

        // 확정 규칙(D01) 자체의 값. 공격력 100 기준으로 점검 보고서가 기록한 600 을 유지한다.
        [Fact]
        public void AttackMultiplier_HeavyPlusCrit_Keeps600()
        {
            Fixed mult = CombatRules.AttackMultiplier(Fixed.FromInt(2), Fixed.FromInt(2));
            Fixed hit  = Fixed.FromInt(100) * mult;
            Assert.Equal(Fixed.FromInt(600).raw, hit.raw);
        }

        // 각 효과 단독과 미발동. 가산만이면 곱이 1, 치명만이면 가산이 0이다.
        [Fact]
        public void AttackMultiplier_SingleEffectsAndNone()
        {
            Assert.Equal(Fixed.FromInt(1).raw, CombatRules.AttackMultiplier(Fixed.Zero, Fixed.One).raw);
            Assert.Equal(Fixed.FromInt(3).raw, CombatRules.AttackMultiplier(Fixed.FromInt(2), Fixed.One).raw);
            Assert.Equal(Fixed.FromInt(2).raw, CombatRules.AttackMultiplier(Fixed.Zero, Fixed.FromInt(2)).raw);
        }

        // 가산이 여러 개여도 전부 더한 뒤 한 번만 곱한다.
        [Fact]
        public void AttackMultiplier_MultipleBonusesAddBeforeCrit()
        {
            Fixed bonusSum = Fixed.FromRatio(5, 10) + Fixed.FromRatio(25, 10); // 0.5 + 2.5
            Fixed mult = CombatRules.AttackMultiplier(bonusSum, Fixed.FromInt(2));
            Assert.Equal(Fixed.FromInt(8).raw, mult.raw); // (1 + 3) * 2
        }

        // 효과 목록을 순서대로 적용하는 실행부 재현. true 는 가산, false 는 치명이다.
        private static Fixed ApplyInOrder(bool[] isBonusList, Fixed bonus, Fixed crit)
        {
            Fixed bonusSum = Fixed.Zero;
            Fixed critMult = Fixed.One;
            for (int i = 0; i < isBonusList.Length; ++i)
            {
                if (isBonusList[i]) bonusSum = bonusSum + bonus;
                else critMult = critMult * crit;
            }
            return CombatRules.AttackMultiplier(bonusSum, critMult);
        }

        // ---- T02. 장판 시간 누산 ----

        // 명목상 같은 1초라면 프레임률이 달라도 소비한 시간의 합이 같아야 한다.
        [Theory]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(120)]
        [InlineData(144)]
        public void TickAccumulator_OneSecondIsFrameRateIndependent(int fps)
        {
            TickAccumulator acc = new TickAccumulator();
            long perFrame = 1000000L / fps;
            long leftover = 1000000L - perFrame * fps; // 나누어떨어지지 않는 몫은 마지막 프레임에 얹는다

            Fixed total = Fixed.Zero;
            for (int i = 0; i < fps; ++i)
            {
                long micros = perFrame;
                if (i == fps - 1) micros += leftover;
                total = total + acc.Consume(micros);
            }

            Assert.Equal(Fixed.One.raw, total.raw);
        }

        // 1밀리초를 못 채운 프레임은 0을 돌려주되 잔여분을 버리지 않는다.
        [Fact]
        public void TickAccumulator_KeepsSubMilliRemainder()
        {
            TickAccumulator acc = new TickAccumulator();

            Assert.Equal(0, acc.Consume(400).raw);
            Assert.Equal(0, acc.Consume(400).raw);
            Assert.Equal(Fixed.FromMilli(1).raw, acc.Consume(400).raw); // 1200 마이크로 -> 1밀리 소비, 200 보존
            Assert.Equal(0, acc.Consume(400).raw);                      // 600 마이크로 누적
            Assert.Equal(Fixed.FromMilli(1).raw, acc.Consume(400).raw); // 1000 마이크로 -> 1밀리
        }

        // 런 재시작 시 이전 런의 잔여 시간이 새 런으로 넘어가면 안 된다.
        [Fact]
        public void TickAccumulator_ResetDropsRemainder()
        {
            TickAccumulator acc = new TickAccumulator();
            acc.Consume(900);
            acc.Reset();
            Assert.Equal(0, acc.Consume(500).raw);
            Assert.Equal(Fixed.FromMilli(1).raw, acc.Consume(500).raw);
        }

        // 배속으로 dt 가 커져도 한 번에 소비한다. 음수와 0 입력은 아무것도 소비하지 않는다.
        [Fact]
        public void TickAccumulator_HandlesLargeAndNonPositiveDelta()
        {
            TickAccumulator acc = new TickAccumulator();
            Assert.Equal(Fixed.FromMilli(50).raw, acc.Consume(50000).raw);
            Assert.Equal(0, acc.Consume(0).raw);
            Assert.Equal(0, acc.Consume(-1000).raw);
        }

        // ---- T03. 스킬 정의 적합성 ----

        // 현재 제공 CSV 는 전부 통과해야 한다. 여기서 실패하면 데이터가 실행되지 않는 조합을 쓰고 있다는 뜻이다.
        [Fact]
        public void SkillValidator_ShippedDataPasses()
        {
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));
            Assert.True(skills.Count > 0);

            List<string> failureList = new List<string>();
            bool ok = SkillValidator.Validate(skills, failureList);
            Assert.True(ok, string.Join("\n", failureList));
        }

        // 점검 보고서 4-C 의 재현 조합. 파싱은 되지만 전투가 실행하지 않으므로 검증에서 걸러야 한다.
        [Fact]
        public void SkillValidator_RejectsEveryNthAllyBuff()
        {
            SkillData s = Make("BADBUFF", SkillTrigger.EveryNthAttack, SkillEffect.AllyBuff);
            s.triggerN  = Fixed.FromInt(3);
            s.radius    = Fixed.FromInt(3);
            s.magnitude = Fixed.FromRatio(5, 10);
            s.duration  = Fixed.FromInt(5);
            s.buffStat  = BuffStat.Atk;

            List<string> failureList = new List<string>();
            Assert.False(SkillValidator.ValidateOne(s, failureList));

            bool found = false;
            for (int i = 0; i < failureList.Count; ++i)
            {
                if (failureList[i].Contains("SKILL-03") && failureList[i].Contains("BADBUFF")) found = true;
            }
            Assert.True(found, string.Join("\n", failureList));
        }

        // 반경이 있는 SLOW 는 오라 경로라 상시여야 하고, 반경 0 인 온힛 SLOW 는 어떤 트리거든 된다.
        [Fact]
        public void SkillValidator_SlowDependsOnRadius()
        {
            SkillData aura = Make("AURASLOW", SkillTrigger.ChanceOnAttack, SkillEffect.Slow);
            aura.triggerN  = Fixed.FromRatio(3, 10);
            aura.radius    = Fixed.FromInt(2);
            aura.magnitude = Fixed.FromRatio(2, 10);
            Assert.False(SkillValidator.ValidateOne(aura, null));

            SkillData onHit = Make("HITSLOW", SkillTrigger.ChanceOnAttack, SkillEffect.Slow);
            onHit.triggerN  = Fixed.FromRatio(3, 10);
            onHit.magnitude = Fixed.FromRatio(2, 10);
            onHit.duration  = Fixed.FromInt(2);
            Assert.True(SkillValidator.ValidateOne(onHit, null));
        }

        // 오라 전용 효과에 반경이 없으면 수집 자체가 안 된다.
        [Fact]
        public void SkillValidator_RejectsAuraWithoutRadius()
        {
            SkillData s = Make("NOZONE", SkillTrigger.Passive, SkillEffect.DamageZone);
            s.magnitude = Fixed.FromInt(5);
            Assert.False(SkillValidator.ValidateOne(s, null));
        }

        // 트리거 인자 범위. EVERYNTH 는 양의 정수, CHANCE 는 0 초과 1 이하다.
        [Fact]
        public void SkillValidator_ChecksTriggerArgument()
        {
            SkillData everyNth = Make("BADN", SkillTrigger.EveryNthAttack, SkillEffect.BonusDamage);
            everyNth.triggerN  = Fixed.FromRatio(15, 10); // 1.5 회째는 없다
            everyNth.magnitude = Fixed.FromInt(2);
            Assert.False(SkillValidator.ValidateOne(everyNth, null));

            SkillData chance = Make("BADP", SkillTrigger.ChanceOnAttack, SkillEffect.Crit);
            chance.triggerN  = Fixed.FromRatio(15, 10); // 150% 확률
            chance.magnitude = Fixed.FromInt(2);
            Assert.False(SkillValidator.ValidateOne(chance, null));
        }

        // 효과별 필수 수치. 대상 수 1인 다중타격과 1배 이하 치명은 아무 일도 하지 않는다.
        [Fact]
        public void SkillValidator_ChecksEffectMagnitudes()
        {
            SkillData multi = Make("BADMULTI", SkillTrigger.Passive, SkillEffect.MultiTarget);
            multi.count = 1;
            multi.magnitude = Fixed.One;
            Assert.False(SkillValidator.ValidateOne(multi, null));

            SkillData crit = Make("BADCRIT", SkillTrigger.ChanceOnAttack, SkillEffect.Crit);
            crit.triggerN  = Fixed.FromRatio(3, 10);
            crit.magnitude = Fixed.One;
            Assert.False(SkillValidator.ValidateOne(crit, null));
        }

        // ALLYBUFF 는 buffStat 이 필수고, 나머지 효과는 buffStat 을 쓰지 않는다.
        [Fact]
        public void SkillValidator_ChecksBuffStat()
        {
            SkillData missing = Make("NOSTAT", SkillTrigger.Passive, SkillEffect.AllyBuff);
            missing.radius    = Fixed.FromInt(3);
            missing.magnitude = Fixed.FromRatio(2, 10);
            Assert.False(SkillValidator.ValidateOne(missing, null));

            SkillData stray = Make("STRAYSTAT", SkillTrigger.Passive, SkillEffect.BonusDamage);
            stray.magnitude = Fixed.FromInt(2);
            stray.buffStat  = BuffStat.Range;
            Assert.False(SkillValidator.ValidateOne(stray, null));
        }

        // id 중복은 RunContext 의 skillById 조회에서 조용히 한쪽을 덮으므로 로드 단계에서 잡는다.
        [Fact]
        public void SkillValidator_RejectsDuplicateId()
        {
            SkillData a = Make("DUP", SkillTrigger.Passive, SkillEffect.BonusDamage);
            a.magnitude = Fixed.FromInt(2);
            SkillData b = Make("DUP", SkillTrigger.Passive, SkillEffect.BonusDamage);
            b.magnitude = Fixed.FromInt(3);

            List<SkillData> skillList = new List<SkillData>();
            skillList.Add(a);
            skillList.Add(b);

            List<string> failureList = new List<string>();
            Assert.False(SkillValidator.Validate(skillList, failureList));

            bool found = false;
            for (int i = 0; i < failureList.Count; ++i)
            {
                if (failureList[i].Contains("SKILL-01")) found = true;
            }
            Assert.True(found, string.Join("\n", failureList));
        }

        private static SkillData Make(string id, SkillTrigger trigger, SkillEffect effect)
        {
            SkillData s = new SkillData();
            s.id      = id;
            s.trigger = trigger;
            s.effect  = effect;
            s.note    = "";
            return s;
        }
    }
}
