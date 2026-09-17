using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Text;

namespace Synthesis.Core.Tests
{
    // STEP 3. 검증 - 선택 패널의 버프/감속 표시 문자열(SPEC 5장).
    //   화면 조립은 Unity 쪽이라 자동 검증이 안 되지만, 키 존재와 치환자는 여기서 막을 수 있다.
    public class SelectionInfoStringTests
    {
        private static StringTable LoadTable()
        {
            return StringTable.Load(TestPaths.ReadData("strings.csv"));
        }

        // 유닛의 적용 중 버프 절은 머리말과 없음 문구가 짝이어야 한다. 하나만 있으면 한쪽 상태가 빈 줄로 나간다.
        //   문구에 출처(오라)를 적지 않는다. 버프 수단이 늘어도 같은 키를 쓸 수 있어야 한다.
        [Fact]
        public void UnitBuffKeys_Exist()
        {
            StringTable table = LoadTable();
            Assert.True(table.Contains("str.unit.buff.header"), "버프 머리말 키가 없다");
            Assert.True(table.Contains("str.unit.buff.none"), "버프 없음 키가 없다");
        }

        // 감속 줄은 코드가 채우는 이름만 써야 한다. 모르는 치환자가 있으면 화면에 중괄호가 그대로 나온다.
        [Fact]
        public void MonsterSlowLines_UseKnownPlaceholders()
        {
            StringTable table = LoadTable();
            Assert.True(table.Contains("str.monster.slow.header"), "감속 머리말 키가 없다");
            Assert.True(table.Contains("str.monster.slow.none"), "감속 없음 키가 없다");

            var aura = StringFormatter.CollectPlaceholders(table.Get("str.monster.slow.aura", Language.Ko));
            Assert.Equal(new List<string> { "name", "percent" }, aura);

            // 온힛 감속만 남은 시간이 있다. 오라 감속은 반경 안에 있는 동안 계속이라 시간이 없다.
            var onHit = StringFormatter.CollectPlaceholders(table.Get("str.monster.slow.onhit", Language.Ko));
            Assert.Equal(new List<string> { "name", "percent", "sec" }, onHit);

            var capped = StringFormatter.CollectPlaceholders(table.Get("str.monster.slow.capped", Language.Ko));
            Assert.Equal(new List<string> { "percent" }, capped);
        }

        // 감속 줄이 실제 값으로 조립되고 치환자가 남지 않는다. HUD 와 같은 순서로 재현한다.
        [Fact]
        public void MonsterSlowLines_FillWithoutLeftovers()
        {
            StringTable table = LoadTable();

            StringValues values = new StringValues()
                .Set("name", "냉기")
                .Set("percent", "35")
                .Set("sec", "1.8");

            string aura = StringFormatter.Format(table.Get("str.monster.slow.aura", Language.Ko), values);
            Assert.Contains("냉기", aura);
            Assert.Contains("35", aura);
            Assert.DoesNotContain("{", aura);

            string onHit = StringFormatter.Format(table.Get("str.monster.slow.onhit", Language.Ko), values);
            Assert.Contains("1.8", onHit);
            Assert.DoesNotContain("{", onHit);
        }

        // ---- 서식 감싸개(TextDecor) ----

        // 감싸개 키가 없으면 Get 이 키 문자열을 돌려주고, 감싼 값이 통째로 사라진다.
        [Fact]
        public void DecorKeys_Exist()
        {
            StringTable table = LoadTable();
            Assert.True(table.Contains(TextDecor.ValueUpKey), "올라가는 수치 감싸개 키가 없다");
            Assert.True(table.Contains(TextDecor.ValueDownKey), "깎는 수치 감싸개 키가 없다");
            Assert.True(table.Contains(TextDecor.SectionHeaderKey), "문단 제목 감싸개 키가 없다");
            Assert.True(table.Contains(TextDecor.SectionEmptyKey), "빈 문단 감싸개 키가 없다");
        }

        // 감싼 뒤에도 원래 값이 그대로 남아야 한다. 올라가는 값과 깎는 값은 서식이 갈린다.
        [Fact]
        public void FormatValue_KeepsTextAndSplitsByDirection()
        {
            StringTable table = LoadTable();

            string up = TextDecor.FormatValue(table, Language.Ko, "15", false);
            string down = TextDecor.FormatValue(table, Language.Ko, "15", true);

            Assert.Contains("15", up);
            Assert.Contains("15", down);
            Assert.NotEqual(up, down);
            Assert.DoesNotContain("{", up);
            Assert.DoesNotContain("{", down);

            // 테이블이 없으면 감싸지 않고 값만 낸다. 헤드리스 경로가 마크업에 물리면 안 된다.
            Assert.Equal("15", TextDecor.FormatValue(null, Language.Ko, "15", false));
        }

        // 문단 제목과 빈 문단은 서식이 갈려야 한다. 같으면 절이 비었는지 안 보인다.
        [Fact]
        public void FormatSection_SplitsHeaderFromEmpty()
        {
            StringTable table = LoadTable();

            string header = TextDecor.FormatSection(table, Language.Ko, "스킬", false);
            string empty = TextDecor.FormatSection(table, Language.Ko, "스킬 없음", true);

            Assert.Contains("스킬", header);
            Assert.Contains("스킬 없음", empty);
            Assert.NotEqual(header, empty);
            Assert.DoesNotContain("{", header);
            Assert.DoesNotContain("{", empty);
        }

        // 스킬 설명의 세기에만 색이 붙는다. 감속과 방깎은 깎는 수치라 올라가는 것과 서식이 달라야 한다.
        [Theory]
        [InlineData("WARCRY1", false)] // 아군 공격력 상승
        [InlineData("HEAVY3", false)]  // 추가 피해
        [InlineData("MULTI3", false)]  // 대상 수가 곧 세기다
        [InlineData("CHILL2", true)]   // 감속
        [InlineData("FROST1", true)]   // 감속 오라
        [InlineData("SUNDER2", true)]  // 방어력 감소
        public void SkillDesc_ColorsMagnitudeByDirection(string skillId, bool reduction)
        {
            StringTable table = LoadTable();
            SkillData skill = SkillOf(skillId);

            string desc = StringFormatter.Format(
                table.Get("str.skill." + skillId + ".desc", Language.Ko),
                new SkillStringValues().Bind(skill, table, Language.Ko));

            string marker = table.Get(reduction ? TextDecor.ValueDownKey : TextDecor.ValueUpKey, Language.Ko);
            string opposite = table.Get(reduction ? TextDecor.ValueUpKey : TextDecor.ValueDownKey, Language.Ko);

            // 감싸개의 여는 부분("<color=#...>")이 들어 있는지로 방향을 본다.
            Assert.Contains(OpenTagOf(marker), desc);
            Assert.DoesNotContain(OpenTagOf(opposite), desc);
            Assert.DoesNotContain("{", desc);
        }

        // 퍼센트 기호가 색 안에 들어가야 한다. 문장에 두면 숫자만 칠해지고 "%" 가 회색으로 남는다.
        //   비율이 아닌 효과(장판 dps, 방깎)는 기호 자체가 없어야 한다.
        [Theory]
        [InlineData("WARCRY1", "15%")]
        [InlineData("HEAVY3", "200%")]
        [InlineData("CRIT1", "200%")]
        [InlineData("AREA4", "90%")]
        [InlineData("CHILL2", "35%")]
        public void PercentSkills_KeepSignInsideTheColoredValue(string skillId, string expected)
        {
            StringTable table = LoadTable();
            SkillData skill = SkillOf(skillId);
            bool reduction = skill.effect == SkillEffect.Slow || skill.effect == SkillEffect.ArmorReduction;

            string wrapper = table.Get(reduction ? TextDecor.ValueDownKey : TextDecor.ValueUpKey, Language.Ko);
            string desc = StringFormatter.Format(
                table.Get("str.skill." + skillId + ".desc", Language.Ko),
                new SkillStringValues().Bind(skill, table, Language.Ko));

            Assert.Contains(wrapper.Replace("{value}", expected), desc);
        }

        [Theory]
        [InlineData("POISON1")]
        [InlineData("SUNDER2")]
        public void AbsoluteSkills_CarryNoPercentSign(string skillId)
        {
            StringTable table = LoadTable();
            string desc = StringFormatter.Format(
                table.Get("str.skill." + skillId + ".desc", Language.Ko),
                new SkillStringValues().Bind(SkillOf(skillId), table, Language.Ko));

            Assert.DoesNotContain("%", desc);
        }

        // 조건 수치(반경, 지속시간, 발동 주기)에는 색을 주지 않는다. 전부 칠하면 어디가 세기인지 다시 안 보인다.
        [Fact]
        public void SkillDesc_LeavesConditionNumbersPlain()
        {
            StringTable table = LoadTable();
            string upOpen = OpenTagOf(table.Get(TextDecor.ValueUpKey, Language.Ko));

            // 반경 3 안 아군 공격력 15% 상승. 색이 붙는 것은 15 하나뿐이다.
            string desc = StringFormatter.Format(
                table.Get("str.skill.WARCRY1.desc", Language.Ko),
                new SkillStringValues().Bind(SkillOf("WARCRY1"), table, Language.Ko));

            int first = desc.IndexOf(upOpen, System.StringComparison.Ordinal);
            Assert.True(first >= 0, "세기에 색이 안 붙었다");
            Assert.Equal(-1, desc.IndexOf(upOpen, first + upOpen.Length, System.StringComparison.Ordinal));
        }

        // 테이블을 안 넘긴 바인딩은 예전처럼 값만 낸다. 수치 규칙만 보는 테스트가 마크업에 물리면 안 된다.
        [Fact]
        public void PlainBind_StaysFreeOfMarkup()
        {
            StringTable table = LoadTable();
            string desc = StringFormatter.Format(
                table.Get("str.skill.WARCRY1.desc", Language.Ko),
                new SkillStringValues().Bind(SkillOf("WARCRY1")));

            Assert.DoesNotContain("<color", desc);
            Assert.Contains("15", desc);
        }

        // 감싸개의 여는 태그. "<color=#4ADE80>{value}</color>" 에서 "{value}" 앞부분을 뗀다.
        private static string OpenTagOf(string wrapper)
        {
            int at = wrapper.IndexOf("{value}", System.StringComparison.Ordinal);
            return at < 0 ? wrapper : wrapper.Substring(0, at);
        }

        private static SkillData SkillOf(string skillId)
        {
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));
            for (int i = 0; i < skills.Count; ++i)
            {
                if (skills[i].id == skillId) return skills[i];
            }
            throw new KeyNotFoundException("skills.csv 에 " + skillId + " 가 없다");
        }

        // 감속 스킬 전부가 이름 키를 갖는다. 목록에 키 문자열이 그대로 나가면 안 된다.
        [Fact]
        public void EverySlowSkill_HasNameKey()
        {
            StringTable table = LoadTable();
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));

            int slowCount = 0;
            for (int i = 0; i < skills.Count; ++i)
            {
                SkillData s = skills[i];
                if (s.effect != SkillEffect.Slow)
                {
                    continue;
                }
                ++slowCount;
                Assert.True(table.Contains("str.skill." + s.id + ".name"), s.id + " 의 이름 키가 없다");
            }
            Assert.True(slowCount > 0, "skills.csv 에 감속 스킬이 없다");
        }
    }
}
