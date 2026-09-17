using System.Collections.Generic;
using Synthesis.Core.Combat;
using Synthesis.Core.Data;

namespace Synthesis.Core.Tests
{
    // STEP 3. 검증 - 평타 스킬 실행 경로. HUD 에 안 보이는 20종(HEAVY/CRIT/MULTI/PIERCE/AREA/POISON)을 여기서 덮는다.
    //   실제 skills.csv 정의를 그대로 읽어 쓴다. 테스트용 수치를 따로 적으면 데이터와 갈라진다.
    public class SkillExecutionTests
    {
        // ---- HEAVY 4종: 평타 N회마다 가산 배율 ----

        // 정의한 N 회째에만 발동하고 그 사이에는 기본 피해다.
        [Theory]
        [InlineData("HEAVY1", 4)]
        [InlineData("HEAVY2", 3)]
        [InlineData("HEAVY3", 3)]
        [InlineData("HEAVY4", 2)]
        public void Heavy_FiresOnlyOnNthAttack(string skillId, int expectedN)
        {
            SkillData s = Skill(skillId);
            Assert.Equal(SkillTrigger.EveryNthAttack, s.trigger);
            Assert.Equal(expectedN, (int)s.triggerN.ToIntRounded());

            for (int attack = 1; attack <= expectedN * 2; ++attack)
            {
                bool fired = SkillPlanner.TriggerFires(s, attack, Fixed.Zero);
                Assert.Equal(attack % expectedN == 0, fired);
            }
        }

        // 발동한 턴의 배수는 1 + magnitude 다. HEAVY3 이면 3배.
        [Fact]
        public void Heavy3_MultipliesByThreeOnFire()
        {
            SkillPlan plan = PlanOf(Only("HEAVY3"), NoRolls(1), 3);
            Assert.Equal(Fixed.FromInt(3).raw, plan.Multiplier().raw);

            SkillPlan idle = PlanOf(Only("HEAVY3"), NoRolls(1), 2);
            Assert.Equal(Fixed.One.raw, idle.Multiplier().raw);
        }

        // ---- CRIT 4종: 확률 발동 배율 ----

        // 굴린 값이 확률 미만이면 발동한다. 경계값(확률과 같은 값)은 미발동이다.
        [Theory]
        [InlineData("CRIT1", 200)]
        [InlineData("CRIT2", 300)]
        [InlineData("CRIT3", 350)]
        [InlineData("CRIT4", 400)]
        public void Crit_FiresBelowChance(string skillId, long chanceMilli)
        {
            SkillData s = Skill(skillId);
            Assert.Equal(SkillTrigger.ChanceOnAttack, s.trigger);
            Assert.Equal(chanceMilli, s.triggerN.raw);

            Assert.True(SkillPlanner.TriggerFires(s, 1, Fixed.FromMilli(chanceMilli - 1)));
            Assert.False(SkillPlanner.TriggerFires(s, 1, Fixed.FromMilli(chanceMilli)));
            Assert.False(SkillPlanner.TriggerFires(s, 1, Fixed.One));
        }

        // 발동하면 정의한 배율이 그대로 곱해진다.
        [Fact]
        public void Crit4_MultipliesByDefinedMagnitude()
        {
            List<SkillData> skills = Only("CRIT4");
            SkillPlan fired = PlanOf(skills, Rolls(Fixed.Zero), 1);
            Assert.Equal(Skill("CRIT4").magnitude.raw, fired.Multiplier().raw); // 2.5배

            SkillPlan missed = PlanOf(skills, Rolls(Fixed.One), 1);
            Assert.Equal(Fixed.One.raw, missed.Multiplier().raw);
        }

        // 데스나이트 조합. 두 스킬이 같은 평타에 발동하면 가산 뒤 치명이다.
        [Fact]
        public void Heavy3AndCrit2_CombineTo600Percent()
        {
            List<SkillData> skills = new List<SkillData>();
            skills.Add(Skill("HEAVY3"));
            skills.Add(Skill("CRIT2"));

            SkillPlan plan = PlanOf(skills, Rolls(Fixed.Zero, Fixed.Zero), 3); // 3회째 + 치명 발동
            Assert.Equal(Fixed.FromInt(6).raw, plan.Multiplier().raw);

            // 순서를 뒤집어도 같다.
            skills.Reverse();
            SkillPlan reversed = PlanOf(skills, Rolls(Fixed.Zero, Fixed.Zero), 3);
            Assert.Equal(plan.Multiplier().raw, reversed.Multiplier().raw);
        }

        // ---- MULTI 2종 / PIERCE 2종: 추가 대상 수 ----

        // count 는 주 대상을 포함한 총 타격 수다. 추가 대상은 하나 적다.
        [Theory]
        [InlineData("MULTI2", 1)]
        [InlineData("MULTI3", 2)]
        [InlineData("PIERCE2", 1)]
        [InlineData("PIERCE3", 2)]
        public void MultiAndPierce_ProduceExtraTargets(string skillId, int expectedExtra)
        {
            SkillPlan plan = PlanOf(Only(skillId), NoRolls(1), 1);
            Assert.Equal(expectedExtra, plan.extraTargets);
        }

        // 스킬 id 가 다르면 추가 대상이 합산된다(UNIT_SKILLS 3장의 중첩 규칙).
        [Fact]
        public void Multi2AndMulti3_AddUp()
        {
            List<SkillData> skills = new List<SkillData>();
            skills.Add(Skill("MULTI2"));
            skills.Add(Skill("MULTI3"));

            SkillPlan plan = PlanOf(skills, NoRolls(2), 1);
            Assert.Equal(3, plan.extraTargets); // 1 + 2
        }

        // 추가 대상은 주 대상을 빼고 가까운 순으로 고른다. 같은 대상을 두 번 고르지 않는다.
        [Fact]
        public void SelectNearest_PicksClosestExcludingPrimary()
        {
            List<TargetPoint> candidates = Points(
                0, 0, 0,    // 주 대상
                1, 5, 0,    // 가장 멈
                2, 1, 0,    // 가장 가까움
                3, 2, 0);

            List<int> result = new List<int>();
            TargetSelector.SelectNearest(candidates, Fixed.Zero, Fixed.Zero, 2, 0, result);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0]);
            Assert.Equal(3, result[1]);
        }

        // 후보가 모자라면 있는 만큼만 때린다. 무한정 고르지 않는다.
        [Fact]
        public void SelectNearest_StopsWhenCandidatesRunOut()
        {
            List<TargetPoint> candidates = Points(0, 0, 0, 1, 1, 0);

            List<int> result = new List<int>();
            TargetSelector.SelectNearest(candidates, Fixed.Zero, Fixed.Zero, 5, 0, result);

            Assert.Single(result);
            Assert.Equal(1, result[0]);
        }

        // 후보가 주 대상뿐이면 아무도 고르지 않는다.
        [Fact]
        public void SelectNearest_EmptyWhenOnlyPrimary()
        {
            List<TargetPoint> candidates = Points(0, 0, 0);

            List<int> result = new List<int>();
            TargetSelector.SelectNearest(candidates, Fixed.Zero, Fixed.Zero, 3, 0, result);

            Assert.Empty(result);
        }

        // ---- AREA 4종: 반경 안 전부 ----

        // 정의한 반경 안의 대상만 고른다. 경계는 포함이다.
        [Theory]
        [InlineData("AREA1")]
        [InlineData("AREA2")]
        [InlineData("AREA3")]
        [InlineData("AREA4")]
        public void Area_SelectsWithinDefinedRadius(string skillId)
        {
            SkillData s = Skill(skillId);
            Assert.True(s.radius.raw > 0);

            // 반경 정확히 위, 반경 바로 밖, 원점 근처를 둔다.
            List<TargetPoint> candidates = new List<TargetPoint>();
            candidates.Add(Point(0, Fixed.Zero, Fixed.Zero));                       // 주 대상
            candidates.Add(Point(1, s.radius, Fixed.Zero));                         // 경계 위 - 포함
            candidates.Add(Point(2, s.radius + Fixed.FromMilli(1), Fixed.Zero));    // 경계 밖 - 제외
            candidates.Add(Point(3, Fixed.FromMilli(10), Fixed.Zero));              // 코앞 - 포함

            List<int> result = new List<int>();
            TargetSelector.SelectWithinRadius(candidates, Fixed.Zero, Fixed.Zero, s.radius, 0, result);

            Assert.Contains(1, result);
            Assert.Contains(3, result);
            Assert.DoesNotContain(2, result);
            Assert.DoesNotContain(0, result);
        }

        // 광역 피해는 평타 피해에 비율을 곱한 값이다. AREA4 는 90%.
        [Fact]
        public void Area4_DealsNinetyPercentOfHit()
        {
            SkillData s = Skill("AREA4");
            Fixed hit = Fixed.FromInt(200);
            Fixed splash = hit * s.magnitude;
            Assert.Equal(Fixed.FromInt(180).raw, splash.raw);
        }

        // 광역 스킬은 계획의 배수에 끼어들지 않고 별도 목록으로 빠진다.
        [Fact]
        public void Area_DoesNotChangeAttackMultiplier()
        {
            List<SkillData> areaOut = new List<SkillData>();
            SkillPlan plan = SkillPlanner.BuildAttackPlan(Only("AREA3"), NoRolls(1), 1, null, areaOut);

            Assert.Equal(Fixed.One.raw, plan.Multiplier().raw);
            Assert.Single(areaOut);
            Assert.Equal("AREA3", areaOut[0].id);
        }

        // ---- POISON 4종: 장판 ----

        // 장판은 반경 안에 있을 때만 dps 가 나온다. 밖이면 0이다.
        [Theory]
        [InlineData("POISON1")]
        [InlineData("POISON2")]
        [InlineData("POISON3")]
        [InlineData("POISON4")]
        public void Poison_AppliesOnlyInsideRadius(string skillId)
        {
            SkillData s = Skill(skillId);
            AuraField field = new AuraField();
            field.Add(SampleOf(s, Fixed.Zero, Fixed.Zero));

            Fixed inside = field.SumAt(SkillEffect.DamageZone, BuffStat.None, Fixed.Zero, Fixed.Zero);
            Assert.Equal(s.magnitude.raw, inside.raw);

            Fixed outside = field.SumAt(SkillEffect.DamageZone, BuffStat.None,
                s.radius + Fixed.FromInt(1), Fixed.Zero);
            Assert.Equal(0, outside.raw);
        }

        // 1초 동안 머무르면 정의한 dps 만큼 들어간다. 프레임률이 달라도 같다.
        [Theory]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(120)]
        public void Poison1_DealsDefinedDpsOverOneSecond(int fps)
        {
            SkillData s = Skill("POISON1");
            AuraField field = new AuraField();
            field.Add(SampleOf(s, Fixed.Zero, Fixed.Zero));

            TickAccumulator acc = new TickAccumulator();
            long perFrame = 1000000L / fps;
            long leftover = 1000000L - perFrame * fps;

            Fixed total = Fixed.Zero;
            for (int i = 0; i < fps; ++i)
            {
                long micros = perFrame;
                if (i == fps - 1) micros += leftover;
                Fixed dt = acc.Consume(micros);
                if (dt.raw <= 0) continue;

                Fixed dps = field.SumAt(SkillEffect.DamageZone, BuffStat.None, Fixed.Zero, Fixed.Zero);
                total = total + dps * dt;
            }

            Assert.Equal(s.magnitude.raw, total.raw); // POISON1 은 dps 5
        }

        // 같은 장판을 두 유닛이 깔아도 1회만 센다. 다른 장판이면 합산된다.
        [Fact]
        public void Poison_StacksBySkillIdOnly()
        {
            SkillData p1 = Skill("POISON1");
            SkillData p2 = Skill("POISON2");

            AuraField same = new AuraField();
            same.Add(SampleOf(p1, Fixed.Zero, Fixed.Zero));
            same.Add(SampleOf(p1, Fixed.Zero, Fixed.Zero));
            Assert.Equal(p1.magnitude.raw,
                same.SumAt(SkillEffect.DamageZone, BuffStat.None, Fixed.Zero, Fixed.Zero).raw);

            AuraField mixed = new AuraField();
            mixed.Add(SampleOf(p1, Fixed.Zero, Fixed.Zero));
            mixed.Add(SampleOf(p2, Fixed.Zero, Fixed.Zero));
            Assert.Equal((p1.magnitude + p2.magnitude).raw,
                mixed.SumAt(SkillEffect.DamageZone, BuffStat.None, Fixed.Zero, Fixed.Zero).raw);
        }

        // ---- 오라 12종의 회귀 ----

        // 아군 버프는 스탯별로 따로 물어야 한다. 공격력 버프가 사거리로 새면 안 된다.
        [Fact]
        public void AllyBuff_IsQueriedPerStat()
        {
            AuraField field = new AuraField();
            field.Add(SampleOf(Skill("WARCRY1"), Fixed.Zero, Fixed.Zero)); // Atk
            field.Add(SampleOf(Skill("SIGHT1"), Fixed.Zero, Fixed.Zero));  // Range

            Fixed atk = field.SumAt(SkillEffect.AllyBuff, BuffStat.Atk, Fixed.Zero, Fixed.Zero);
            Fixed range = field.SumAt(SkillEffect.AllyBuff, BuffStat.Range, Fixed.Zero, Fixed.Zero);
            Fixed aps = field.SumAt(SkillEffect.AllyBuff, BuffStat.AtkSpeed, Fixed.Zero, Fixed.Zero);

            Assert.Equal(Skill("WARCRY1").magnitude.raw, atk.raw);
            Assert.Equal(Skill("SIGHT1").magnitude.raw, range.raw);
            Assert.Equal(0, aps.raw);
        }

        // UNIT_SKILLS 3장의 표. 프리스트 4기는 +15%, 프리스트 + 크루세이더는 +40% 다.
        [Fact]
        public void Warcry_MatchesDocumentedStackingTable()
        {
            AuraField four = new AuraField();
            for (int i = 0; i < 4; ++i) four.Add(SampleOf(Skill("WARCRY1"), Fixed.Zero, Fixed.Zero));
            Assert.Equal(Fixed.FromRatio(15, 100).raw,
                four.SumAt(SkillEffect.AllyBuff, BuffStat.Atk, Fixed.Zero, Fixed.Zero).raw);

            AuraField mixed = new AuraField();
            mixed.Add(SampleOf(Skill("WARCRY1"), Fixed.Zero, Fixed.Zero));
            mixed.Add(SampleOf(Skill("WARCRY2"), Fixed.Zero, Fixed.Zero));
            Assert.Equal(Fixed.FromRatio(40, 100).raw,
                mixed.SumAt(SkillEffect.AllyBuff, BuffStat.Atk, Fixed.Zero, Fixed.Zero).raw);
        }

        // 방깎은 절대값이고 반경 밖에는 걸리지 않는다.
        [Fact]
        public void Sunder_AppliesAbsoluteCutInsideRadius()
        {
            SkillData s = Skill("SUNDER2");
            AuraField field = new AuraField();
            field.Add(SampleOf(s, Fixed.Zero, Fixed.Zero));

            Assert.Equal(s.magnitude.raw,
                field.SumAt(SkillEffect.ArmorReduction, BuffStat.None, Fixed.Zero, Fixed.Zero).raw);
            Assert.Equal(0,
                field.SumAt(SkillEffect.ArmorReduction, BuffStat.None, s.radius + Fixed.One, Fixed.Zero).raw);
        }

        // ---- 오라 목록 조회(선택 패널 표시용) ----
        //   합이 아니라 어느 스킬이 걸렸는지를 낸다. 중복 제외와 반경 판정이 SumAt 과 갈라지면 안 된다.

        // 같은 스킬을 여러 기가 깔아도 목록에는 한 번만 들어간다. 다른 스킬이면 둘 다 들어간다.
        [Fact]
        public void AuraList_ListsEachSkillIdOnce()
        {
            AuraField same = new AuraField();
            same.Add(SampleOf(Skill("WARCRY1"), Fixed.Zero, Fixed.Zero));
            same.Add(SampleOf(Skill("WARCRY1"), Fixed.Zero, Fixed.Zero));
            same.Add(SampleOf(Skill("WARCRY1"), Fixed.Zero, Fixed.Zero));

            List<string> idList = new List<string>();
            same.GetSkillIdsAt(SkillEffect.AllyBuff, Fixed.Zero, Fixed.Zero, idList);
            Assert.Single(idList);
            Assert.Equal("WARCRY1", idList[0]);

            AuraField mixed = new AuraField();
            mixed.Add(SampleOf(Skill("WARCRY1"), Fixed.Zero, Fixed.Zero));
            mixed.Add(SampleOf(Skill("WARCRY2"), Fixed.Zero, Fixed.Zero));
            mixed.GetSkillIdsAt(SkillEffect.AllyBuff, Fixed.Zero, Fixed.Zero, idList);
            Assert.Equal(2, idList.Count);
            Assert.Equal("WARCRY1", idList[0]);
            Assert.Equal("WARCRY2", idList[1]);
        }

        // 목록은 스탯을 가리지 않는다. 화면에는 걸린 오라가 스탯과 무관하게 다 나와야 한다.
        //   합(SumAt)은 스탯별로 갈라지므로 둘의 기준이 다르다는 것도 함께 확인한다.
        [Fact]
        public void AuraList_IncludesEveryBuffStat()
        {
            AuraField field = new AuraField();
            field.Add(SampleOf(Skill("WARCRY1"), Fixed.Zero, Fixed.Zero)); // Atk
            field.Add(SampleOf(Skill("HASTE1"), Fixed.Zero, Fixed.Zero));  // AtkSpeed
            field.Add(SampleOf(Skill("SIGHT1"), Fixed.Zero, Fixed.Zero));  // Range

            List<string> idList = new List<string>();
            field.GetSkillIdsAt(SkillEffect.AllyBuff, Fixed.Zero, Fixed.Zero, idList);
            Assert.Equal(3, idList.Count);

            Assert.Equal(Skill("WARCRY1").magnitude.raw,
                field.SumAt(SkillEffect.AllyBuff, BuffStat.Atk, Fixed.Zero, Fixed.Zero).raw);
        }

        // 반경 밖은 목록에도 안 들어간다. 경계 위는 합과 마찬가지로 포함이다.
        [Fact]
        public void AuraList_FollowsTheSameRadiusRuleAsSum()
        {
            SkillData s = Skill("FROST1");
            AuraField field = new AuraField();
            field.Add(SampleOf(s, Fixed.Zero, Fixed.Zero));

            List<string> idList = new List<string>();

            field.GetSkillIdsAt(SkillEffect.Slow, s.radius, Fixed.Zero, idList);
            Assert.Single(idList);

            field.GetSkillIdsAt(SkillEffect.Slow, s.radius + Fixed.One, Fixed.Zero, idList);
            Assert.Empty(idList);
        }

        // 물어본 효과만 나온다. 방깎을 물었는데 감속이 섞여 나오면 화면이 거짓말을 한다.
        [Fact]
        public void AuraList_FiltersByEffect()
        {
            AuraField field = new AuraField();
            field.Add(SampleOf(Skill("FROST1"), Fixed.Zero, Fixed.Zero));  // Slow
            field.Add(SampleOf(Skill("SUNDER1"), Fixed.Zero, Fixed.Zero)); // ArmorReduction

            List<string> idList = new List<string>();
            field.GetSkillIdsAt(SkillEffect.Slow, Fixed.Zero, Fixed.Zero, idList);
            Assert.Single(idList);
            Assert.Equal("FROST1", idList[0]);

            field.GetSkillIdsAt(SkillEffect.ArmorReduction, Fixed.Zero, Fixed.Zero, idList);
            Assert.Single(idList);
            Assert.Equal("SUNDER1", idList[0]);
        }

        // 오라 감속과 온힛 감속은 실행 경로가 다르다. FROST1 은 반경이 있어 오라, CHILL 은 반경 0 이라 온힛이다.
        [Fact]
        public void Slow_SplitsByRadius()
        {
            Assert.True(AuraField.IsAuraSource(Skill("FROST1")));
            Assert.False(AuraField.IsAuraSource(Skill("CHILL1")));
            Assert.False(AuraField.IsAuraSource(Skill("CHILL2")));

            List<SkillData> onHitOut = new List<SkillData>();
            SkillPlanner.BuildAttackPlan(Only("CHILL2"), NoRolls(1), 1, onHitOut, null);
            Assert.Single(onHitOut);
            Assert.Equal("CHILL2", onHitOut[0].id);

            // 오라 감속은 평타 계획에 들어가지 않는다.
            List<SkillData> auraOnHit = new List<SkillData>();
            SkillPlanner.BuildAttackPlan(Only("FROST1"), NoRolls(1), 1, auraOnHit, null);
            Assert.Empty(auraOnHit);
        }

        // 감속이 아무리 쌓여도 기본 속도의 30% 밑으로 내려가지 않는다.
        [Fact]
        public void Slow_RespectsSpeedFloor()
        {
            Fixed heavy = Fixed.FromRatio(90, 100);
            Assert.Equal(CombatRules.MinSpeedRatio.raw, CombatRules.SpeedRatioAfterSlow(heavy).raw);
        }

        // ---- 32종 전수 ----

        // skills.csv 의 모든 정의가 실제로 무언가를 한다. 개별 테스트가 빠뜨린 스킬을 여기서 잡는다.
        //   데이터에 새 스킬을 추가했는데 실행부가 안 읽으면 이 테스트가 먼저 깨진다.
        [Fact]
        public void EverySkill_ProducesAnObservableEffect()
        {
            var skills = AllSkills();
            Assert.Equal(32, skills.Count);

            List<string> inertList = new List<string>();
            for (int i = 0; i < skills.Count; ++i)
            {
                SkillData s = skills[i];
                if (!HasObservableEffect(s)) inertList.Add(s.id + " (" + s.trigger + " x " + s.effect + ")");
            }

            Assert.True(inertList.Count == 0, "아무 효과도 내지 않는 스킬:\n" + string.Join("\n", inertList));
        }

        // 이 스킬이 실행 경로에서 관측 가능한 결과를 내는가.
        private static bool HasObservableEffect(SkillData s)
        {
            // 오라 경로: 중심에 선 대상이 정의한 세기를 받는다.
            if (AuraField.IsAuraSource(s))
            {
                AuraField field = new AuraField();
                field.Add(SampleOf(s, Fixed.Zero, Fixed.Zero));
                Fixed atCenter = field.SumAt(s.effect, s.buffStat, Fixed.Zero, Fixed.Zero);
                if (atCenter.raw != s.magnitude.raw) return false;

                // 반경 밖에서는 안 걸려야 오라라고 할 수 있다.
                Fixed outside = field.SumAt(s.effect, s.buffStat, s.radius + Fixed.One, Fixed.Zero);
                return outside.raw == 0;
            }

            // 평타 경로: 발동시켰을 때 배수/추가대상/광역/감속 중 하나가 움직인다.
            int attackCount = s.trigger == SkillTrigger.EveryNthAttack ? (int)s.triggerN.ToIntRounded() : 1;
            if (attackCount <= 0) return false;

            List<SkillData> onHitOut = new List<SkillData>();
            List<SkillData> areaOut = new List<SkillData>();
            SkillPlan plan = SkillPlanner.BuildAttackPlan(Only(s.id), NoRolls(1), attackCount, onHitOut, areaOut);

            if (plan.Multiplier().raw != Fixed.One.raw) return true;
            if (plan.extraTargets > 0) return true;
            if (areaOut.Count > 0) return true;
            if (onHitOut.Count > 0) return true;
            return false;
        }

        // 평타 스킬은 발동하지 않은 턴에는 아무 일도 없어야 한다. 상시 발동이면 건너뛴다.
        [Fact]
        public void EveryNthSkill_IsInertBetweenFires()
        {
            var skills = AllSkills();
            for (int i = 0; i < skills.Count; ++i)
            {
                SkillData s = skills[i];
                if (s.trigger != SkillTrigger.EveryNthAttack) continue;

                int n = (int)s.triggerN.ToIntRounded();
                if (n <= 1) continue;

                List<SkillData> onHitOut = new List<SkillData>();
                List<SkillData> areaOut = new List<SkillData>();
                SkillPlan idle = SkillPlanner.BuildAttackPlan(Only(s.id), NoRolls(1), n - 1, onHitOut, areaOut);

                Assert.Equal(Fixed.One.raw, idle.Multiplier().raw);
                Assert.Equal(0, idle.extraTargets);
                Assert.Empty(areaOut);
                Assert.Empty(onHitOut);
            }
        }

        // ---- 헬퍼 ----

        private static List<SkillData> AllSkills()
        {
            if (cachedSkills == null) cachedSkills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));
            return cachedSkills;
        }

        private static List<SkillData> cachedSkills;

        private static SkillData Skill(string id)
        {
            if (cachedSkills == null) cachedSkills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));
            for (int i = 0; i < cachedSkills.Count; ++i)
            {
                if (cachedSkills[i].id == id) return cachedSkills[i];
            }
            throw new KeyNotFoundException("skills.csv 에 " + id + " 가 없다");
        }

        private static List<SkillData> Only(string id)
        {
            List<SkillData> list = new List<SkillData>();
            list.Add(Skill(id));
            return list;
        }

        private static SkillPlan PlanOf(List<SkillData> skills, List<Fixed> rollList, int attackCount)
        {
            return SkillPlanner.BuildAttackPlan(skills, rollList, attackCount,
                new List<SkillData>(), new List<SkillData>());
        }

        // 확률 굴림 0 은 확률 트리거를 반드시 발동시킨다.
        private static List<Fixed> NoRolls(int count)
        {
            List<Fixed> list = new List<Fixed>();
            for (int i = 0; i < count; ++i) list.Add(Fixed.Zero);
            return list;
        }

        private static List<Fixed> Rolls(params Fixed[] values)
        {
            List<Fixed> list = new List<Fixed>();
            for (int i = 0; i < values.Length; ++i) list.Add(values[i]);
            return list;
        }

        private static AuraSample SampleOf(SkillData s, Fixed x, Fixed y)
        {
            AuraSample sample;
            sample.skillId   = s.id;
            sample.x         = x;
            sample.y         = y;
            sample.radius    = s.radius;
            sample.magnitude = s.magnitude;
            sample.effect    = s.effect;
            sample.stat      = s.buffStat;
            return sample;
        }

        private static TargetPoint Point(int index, Fixed x, Fixed y)
        {
            TargetPoint t;
            t.index = index;
            t.x = x;
            t.y = y;
            return t;
        }

        // (index, x, y) 를 셋씩 묶어 후보 목록을 만든다. 좌표는 정수 셀이다.
        private static List<TargetPoint> Points(params int[] triples)
        {
            List<TargetPoint> list = new List<TargetPoint>();
            for (int i = 0; i + 2 < triples.Length; i += 3)
            {
                list.Add(Point(triples[i], Fixed.FromInt(triples[i + 1]), Fixed.FromInt(triples[i + 2])));
            }
            return list;
        }
    }
}
