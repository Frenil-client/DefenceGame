using Synthesis.Core.Data;
using Synthesis.Core.Text;

namespace Synthesis.Core.Tests
{
    // STEP 3. 검증 - 치트(무료 상점) 문자열. 값에 쉼표가 들어 있어 따옴표 처리가 깨지면 바로 티가 난다.
    public class CheatShopStringTests
    {
        [Fact]
        public void CheatShopKeys_ExistAndParse()
        {
            StringTable table = StringTable.Load(TestPaths.ReadData("strings.csv"));

            Assert.True(table.Contains("str.popup.shop.title.cheat"), "치트 상점 제목 키가 없다");
            Assert.True(table.Contains("str.popup.shop.buy.free"), "치트 상점 구매 문구 키가 없다");

            // 쉼표가 따옴표 안에서 잘리지 않아야 한다.
            string title = table.Get("str.popup.shop.title.cheat", Language.Ko);
            Assert.Contains("전 등급", title);
            Assert.Contains("선택권 소모 없음", title);
        }

        // 치트 행 문구는 등급과 이름을 채운다. 치환자가 남아 있으면 안 된다.
        [Fact]
        public void CheatBuyLabel_FillsTierAndName()
        {
            StringTable table = StringTable.Load(TestPaths.ReadData("strings.csv"));
            string template = table.Get("str.popup.shop.buy.free", Language.Ko);

            StringValues values = new StringValues().Set("tier", "5").Set("name", "검성 라그나로크");
            string label = StringFormatter.Format(template, values);

            Assert.Contains("5성", label);
            Assert.Contains("검성 라그나로크", label);
            Assert.DoesNotContain("{", label);
        }
    }

    // STEP 3. 검증 - 유닛 정보의 스킬 한 줄 서식. "[이름] 설명" 으로 나가는지 본다.
    public class SkillLineStringTests
    {
        // 서식 키가 두 언어에 있고 name 과 desc 를 모두 쓴다.
        [Fact]
        public void SkillLineKey_UsesNameAndDesc()
        {
            StringTable table = StringTable.Load(TestPaths.ReadData("strings.csv"));
            Assert.True(table.Contains("str.unit.skill.line"), "스킬 줄 서식 키가 없다");

            var ko = StringFormatter.CollectPlaceholders(table.Get("str.unit.skill.line", Language.Ko));
            Assert.Contains("name", ko);
            Assert.Contains("desc", ko);
        }

        // 32종 전부가 "[이름] 설명" 으로 조립되고 치환자가 남지 않는다.
        //   설명의 수치를 먼저 채운 뒤 줄 서식에 끼우는 두 단계를 HUD 와 같은 순서로 재현한다.
        [Fact]
        public void EverySkill_RendersAsBracketedLine()
        {
            StringTable table = StringTable.Load(TestPaths.ReadData("strings.csv"));
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));
            string lineFormat = table.Get("str.unit.skill.line", Language.Ko);

            foreach (var skill in skills)
            {
                string name = table.Get("str.skill." + skill.id + ".name", Language.Ko);
                string desc = StringFormatter.Format(
                    table.Get("str.skill." + skill.id + ".desc", Language.Ko), SkillValuesOf(skill));

                StringValues values = new StringValues().Set("name", name).Set("desc", desc);
                string line = StringFormatter.Format(lineFormat, values).TrimEnd();

                Assert.StartsWith("[" + name + "]", line);
                Assert.Contains(desc, line);
                Assert.DoesNotContain("{", line); // 못 채운 치환자가 화면에 나가면 안 된다
            }
        }

        // 피해 관련 설명은 배수가 아니라 퍼센트로 쓴다. "1배 추가" 가 총 2배라는 뜻이라 화면에서 헷갈렸다.
        //   HEAVY 는 더해지는 몫("200% 추가"), CRIT 은 곱해지는 총량("피해 200%")이라 문장이 갈린다.
        [Theory]
        [InlineData("HEAVY1", "100% 추가")]
        [InlineData("HEAVY2", "150% 추가")]
        [InlineData("HEAVY3", "200% 추가")]
        [InlineData("HEAVY4", "210% 추가")]
        [InlineData("CRIT1", "피해 200%")]
        [InlineData("CRIT3", "피해 220%")]
        [InlineData("CRIT4", "피해 250%")]
        public void DamageSkills_ReadAsPercentNotMultiplier(string skillId, string expected)
        {
            StringTable table = StringTable.Load(TestPaths.ReadData("strings.csv"));
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));

            SkillData skill = null;
            foreach (var s in skills)
            {
                if (s.id == skillId) skill = s;
            }
            Assert.NotNull(skill);

            string desc = StringFormatter.Format(
                table.Get("str.skill." + skillId + ".desc", Language.Ko), SkillValuesOf(skill));

            Assert.Contains(expected, desc);
            Assert.DoesNotContain("배", desc);
        }

        // 절대값인 것까지 퍼센트로 바꾸면 안 된다. 장판 dps 와 방깎은 수치 그대로다.
        [Theory]
        [InlineData("POISON1", "초당 5 피해")]
        [InlineData("SUNDER2", "방어력 8 감소")]
        public void AbsoluteSkills_StayAsRawNumbers(string skillId, string expected)
        {
            StringTable table = StringTable.Load(TestPaths.ReadData("strings.csv"));
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));

            SkillData skill = null;
            foreach (var s in skills)
            {
                if (s.id == skillId) skill = s;
            }
            Assert.NotNull(skill);

            string desc = StringFormatter.Format(
                table.Get("str.skill." + skillId + ".desc", Language.Ko), SkillValuesOf(skill));

            Assert.Contains(expected, desc);
        }

        // 설명 치환자를 채우는 값. 표시 규칙은 Core 에 한 벌이라 테스트도 그것을 그대로 쓴다(CLAUDE.md 4-7).
        private static IStringValues SkillValuesOf(SkillData s)
        {
            return new SkillStringValues().Bind(s);
        }
    }
}
