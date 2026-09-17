using System.Collections.Generic;
using Synthesis.Core.Combat;
using Synthesis.Core.Data;
using Synthesis.Core.Simulation;
using Synthesis.Core.Text;

namespace Synthesis.Core.Tests
{
    // STEP 3. 검증 - HUD 가 사용하는 완성 문자열 조립기를 직접 실행한다.
    public class SelectionInfoFormatterTests
    {
        [Theory]
        [InlineData(Language.Ko)]
        [InlineData(Language.En)]
        public void UnitWithoutSkills_StillShowsItsReceivedBuffs(Language language)
        {
            var table = StringTable.Load(TestPaths.ReadData("strings.csv"));
            var formatter = new SelectionInfoFormatter().Bind(table, language);
            Dictionary<string, SkillData> skillDict = LoadSkills();
            var unit = new UnitData
            {
                name = "Test", tier = 1, atk = Fixed.FromInt(100),
                atkSpeed = Fixed.One, range = Fixed.FromInt(3)
            };
            List<string> buffIdList = new List<string>();
            buffIdList.Add("WARCRY1");

            var text = formatter.FormatUnit(unit, 115f, 1f, 3f, skillDict, buffIdList);
            Assert.Contains(table.Get("str.unit.skill.none", language), text);
            Assert.Contains(table.Get("str.unit.buff.header", language), text);
            Assert.Contains(table.Get("str.skill.WARCRY1.name", language), text);
            Assert.Contains("15%", text);
            Assert.DoesNotContain("{", text);
            var magnitude = TextDecor.FormatValue(table, language, "15%", false);
            var appliedLine = language == Language.Ko
                ? "[전투 함성] 공격력 " + magnitude + " 상승"
                : "[War Cry] Attack increased by " + magnitude;
            Assert.Contains(appliedLine, text);
            Assert.DoesNotContain(language == Language.Ko ? "반경" : "within", text);

            // 자기 스킬과 받은 버프에 함께 나와도 전체 설명과 효과 설명을 구분한다.
            unit.skillIds.Add("WARCRY1");
            text = formatter.FormatUnit(unit, 115f, 1f, 3f, skillDict, buffIdList);
            Assert.Contains(language == Language.Ko ? "반경 3 안 아군의 공격력" : "Allies within 3", text);
            var buffStart = text.IndexOf(table.Get("str.unit.buff.header", language), System.StringComparison.Ordinal);
            var buffText = text.Substring(buffStart);
            Assert.Contains(appliedLine, buffText);
            Assert.DoesNotContain(language == Language.Ko ? "반경" : "within", buffText);
            unit.skillIds.Clear();

            text = formatter.FormatUnit(unit, 100f, 1f, 3f, skillDict, null);
            Assert.Contains(table.Get("str.unit.buff.none", language), text);
            Assert.DoesNotContain(table.Get("str.skill.WARCRY1.name", language), text);
        }

        [Theory]
        [InlineData(Language.Ko)]
        [InlineData(Language.En)]
        public void MonsterExpiry_RemovesFloorAndSlowWhileRestoringDisplayedSpeed(Language language)
        {
            var table = StringTable.Load(TestPaths.ReadData("strings.csv"));
            var formatter = new SelectionInfoFormatter().Bind(table, language);
            var monster = new LoopMonster
            {
                baseMoveSpeed = Fixed.FromInt(10),
                hp = Fixed.FromInt(100)
            };
            var state = new MonsterSlowState();
            var field = new AuraField();
            state.ApplyOnHit("CHILL2", Fixed.FromMilli(700), Fixed.One);
            state.Update(Fixed.Zero, field, Fixed.Zero, Fixed.Zero);
            monster.moveSpeed = monster.baseMoveSpeed * state.SpeedRatio;

            var text = formatter.FormatMonster("Test", monster, Fixed.Zero, state);
            var speedLine = TextDecor.FormatStat(table, language, "str.stat.movespeed", 10f, 3f);
            Assert.Contains(speedLine, text);
            Assert.Contains(table.Get("str.skill.CHILL2.name", language), text);
            Assert.Contains("70%", text);
            var floorLine = StringFormatter.Format(table.Get("str.monster.slow.capped", language),
                new StringValues().Set("percent", "30"));
            Assert.Contains(floorLine, text);
            Assert.DoesNotContain("{", text);

            state.Update(Fixed.One, field, Fixed.Zero, Fixed.Zero);
            monster.moveSpeed = monster.baseMoveSpeed * state.SpeedRatio;
            text = formatter.FormatMonster("Test", monster, Fixed.Zero, state);
            Assert.Contains(TextDecor.FormatStat(table, language, "str.stat.movespeed", 10f, 10f), text);
            Assert.Contains(table.Get("str.monster.slow.none", language), text);
            Assert.DoesNotContain(table.Get("str.skill.CHILL2.name", language), text);
            Assert.DoesNotContain(floorLine, text);
        }

        private static Dictionary<string, SkillData> LoadSkills()
        {
            Dictionary<string, SkillData> skillDict = new Dictionary<string, SkillData>();
            foreach (SkillData skill in CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv")))
            {
                skillDict.Add(skill.id, skill);
            }
            return skillDict;
        }
    }
}
