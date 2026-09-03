using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Text;

namespace Synthesis.Core.Tests
{
    // STEP 3. 검증 - 문자열 테이블과 치환 엔진.
    public class StringTableTests
    {
        private static StringTable LoadTable()
        {
            return StringTable.Load(TestPaths.ReadData("strings.csv"));
        }

        [Fact]
        public void Table_LoadsAndFallsBackToKo()
        {
            StringTable table = LoadTable();
            Assert.True(table.Count > 0, "문자열 테이블이 비어 있다");

            Assert.Equal("공격력", table.Get("str.stat.atk", Language.Ko));
            Assert.Equal("Attack", table.Get("str.stat.atk", Language.En));

            // 없는 키는 키 자체가 나와야 누락이 화면에서 드러난다.
            Assert.Equal("str.nope", table.Get("str.nope", Language.Ko));
        }

        // 값에 쉼표가 들어가는 문자열이 한 필드로 읽혀야 한다.
        [Fact]
        public void SplitCsvFields_HandlesQuotes()
        {
            var split = CsvUtil.SplitCsvFields("key,\"쉼표, 포함\",\"quoted \"\"word\"\"\"");
            Assert.Equal(3, split.Count);
            Assert.Equal("key", split[0]);
            Assert.Equal("쉼표, 포함", split[1]);
            Assert.Equal("quoted \"word\"", split[2]);
        }

        [Fact]
        public void Formatter_ReplacesPlaceholders()
        {
            StringValues values = new StringValues();
            values.Set("label", "공격력").Set("value", "450").Set("delta", "90");

            Assert.Equal("공격력 450", StringFormatter.Format("{label} {value}", values));

            // 값이 없는 치환자는 그대로 남는다.
            Assert.Equal("{missing}", StringFormatter.Format("{missing}", values));

            // 닫히지 않은 중괄호는 리터럴이다.
            Assert.Equal("{broken", StringFormatter.Format("{broken", values));
        }

        [Fact]
        public void Formatter_CollectsPlaceholders()
        {
            var names = StringFormatter.CollectPlaceholders("{label} {value} (+{delta})");
            Assert.Equal(3, names.Count);
            Assert.Equal("label", names[0]);
            Assert.Equal("delta", names[2]);
        }

        // 키 규약: 스킬 32종은 name 과 desc 키를 모두 갖는다. 규약이라 skills.csv 에 키 컬럼이 없다.
        [Fact]
        public void EverySkillHasNameAndDescKey()
        {
            StringTable table = LoadTable();
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));

            foreach (var skill in skills)
            {
                Assert.True(table.Contains("str.skill." + skill.id + ".name"), skill.id + " 의 이름 키가 없다");
                Assert.True(table.Contains("str.skill." + skill.id + ".desc"), skill.id + " 의 설명 키가 없다");
            }
        }

        // 두 언어를 모두 채운다. en 이 비면 ko 로 폴백되지만, 폴백에 기대면 누락을 못 알아챈다.
        [Fact]
        public void EveryEntryHasBothLanguages()
        {
            StringTable table = LoadTable();
            foreach (var entry in table.EntryList)
            {
                Assert.False(string.IsNullOrEmpty(entry.ko), entry.key + " 의 ko 가 비었다");
                Assert.False(string.IsNullOrEmpty(entry.en), entry.key + " 의 en 이 비었다");
            }
        }

        // 같은 키의 두 언어가 같은 치환자를 써야 한다. 번역하다 치환자를 빠뜨리면 값이 사라진다.
        [Fact]
        public void LanguagesShareSamePlaceholders()
        {
            StringTable table = LoadTable();
            foreach (var entry in table.EntryList)
            {
                var ko = StringFormatter.CollectPlaceholders(entry.ko);
                var en = StringFormatter.CollectPlaceholders(entry.en);
                Assert.Equal(ko.Count, en.Count);

                foreach (var name in ko)
                {
                    Assert.True(en.Contains(name), entry.key + " 의 en 에 치환자 " + name + " 이 없다");
                }
            }
        }

        // 스탯 표기 형식은 코드가 채우는 이름에 의존한다. 이름이 바뀌면 화면에 중괄호가 그대로 나온다.
        [Fact]
        public void StatFormatsUseKnownPlaceholders()
        {
            StringTable table = LoadTable();

            Assert.Equal(new List<string> { "label", "value" }, StringFormatter.CollectPlaceholders(table.Get("str.stat.plain", Language.Ko)));
            Assert.Equal(new List<string> { "label", "value", "delta" }, StringFormatter.CollectPlaceholders(table.Get("str.stat.buffed", Language.Ko)));
            Assert.Equal(new List<string> { "label", "value", "delta" }, StringFormatter.CollectPlaceholders(table.Get("str.stat.debuffed", Language.Ko)));
        }

        // 스킬 설명의 치환자는 SkillData 가 실제로 채울 수 있는 이름만 써야 한다.
        [Fact]
        public void SkillDescPlaceholdersAreKnown()
        {
            StringTable table = LoadTable();
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));

            HashSet<string> allowed = new HashSet<string>
            {
                "triggerN", "radius", "magnitude", "duration", "count"
            };

            foreach (var skill in skills)
            {
                StringEntry entry;
                if (!table.TryGet("str.skill." + skill.id + ".desc", out entry))
                {
                    continue;
                }
                foreach (var name in StringFormatter.CollectPlaceholders(entry.ko))
                {
                    Assert.True(allowed.Contains(name), skill.id + " 설명에 모르는 치환자 " + name);
                }
            }
        }
    }
}
