using System.Collections.Generic;
using Synthesis.Core.Combat;
using Synthesis.Core.Data;
using Synthesis.Core.Simulation;

namespace Synthesis.Core.Text
{
    // STEP 3. 핵심 - 선택 정보의 문자열 조립. 전투 조회와 Unity 표시를 포함하지 않는다.
    public sealed class SelectionInfoFormatter
    {
        private StringTable table;
        private Language language;
        private readonly StringValues scratch = new StringValues();
        private readonly SkillStringValues skillScratch = new SkillStringValues();
        private readonly StringValues skillLineScratch = new StringValues();
        private readonly StringValues slowLineScratch = new StringValues();

        public SelectionInfoFormatter Bind(StringTable table, Language language)
        {
            this.table = table;
            this.language = language;
            return this;
        }

        public string FormatUnit(UnitData data, float atk, float aps, float range, Dictionary<string, SkillData> registry, IReadOnlyList<string> buffIdList)
        {
            var baseAtk = (float)data.atk.ToDoubleForDisplay();
            var baseAps = (float)data.atkSpeed.ToDoubleForDisplay();
            var baseRange = (float)data.range.ToDoubleForDisplay();

            scratch.Clear();
            scratch.Set("name", data.name);
            scratch.Set("tier", data.tier.ToString());
            scratch.Set("klass", data.klass.ToString());

            var text = Format("str.unit.header", scratch) + "\n"
                + FormatStat("str.stat.atk", baseAtk, atk, "0") + "\n"
                + FormatStat("str.stat.atkspeed", baseAps, aps) + "\n"
                + FormatStat("str.stat.range", baseRange, range) + "\n"
                + FormatStat("str.stat.dps", atk * aps, atk * aps, "0");
            if (data.skillIds.Count == 0)
            {
                text += "\n" + FormatSection("str.unit.skill.none", true);
            }
            else
            {
                text += "\n" + FormatSection("str.unit.skill.header");
                for (int i = 0; i < data.skillIds.Count; ++i)
                {
                    text += "\n  " + SkillLine(registry, data.skillIds[i]);
                }
            }

            return text + "\n" + BuffSection(buffIdList, registry);
        }

        // 전투가 확정해 전달한 버프를 표시한다. 이 조립기에서는 적용 여부를 다시 판정하지 않는다.
        //   출처는 지금 오라뿐이지만 절 이름에 오라를 적지 않는다.
        //   유닛이 스스로 거는 오라도 반경 안이라 자기 자신에게 걸린다. 그래서 스킬 목록과 겹쳐 보이는 것이 맞다.
        private string BuffSection(IReadOnlyList<string> buffIdList, Dictionary<string, SkillData> registry)
        {
            if (buffIdList == null || buffIdList.Count == 0) return FormatSection("str.unit.buff.none", true);

            var text = FormatSection("str.unit.buff.header");
            for (int i = 0; i < buffIdList.Count; ++i)
            {
                text += "\n  " + SkillLine(registry, buffIdList[i], true);
            }
            return text;
        }

        // 스킬 한 줄. 이름과 설명을 그냥 붙이면 어디까지가 이름인지 안 보여서 서식을 씌운다.
        //   설명의 수치 치환자를 먼저 채운 뒤 줄 서식에 끼운다. 치환은 한 단계씩 두 번이라 중첩이 아니다.
        //   괄호 규칙은 언어마다 다를 수 있으므로 서식을 코드가 아니라 문자열 테이블에 둔다.
        private string SkillLine(Dictionary<string, SkillData> registry, string skillId, bool appliedBuff = false)
        {
            var name = Get("str.skill." + skillId + ".name");

            // 정의를 못 찾으면 설명을 비운다(units.csv 와 skills.csv 가 어긋난 경우. 이름 키는 그대로 드러난다).
            var desc = "";
            SkillData skill;
            if (registry != null && registry.TryGetValue(skillId, out skill))
            {
                // 효과 세기에 색이 붙은 값으로 채운다. 감싸개는 문자열 테이블이 갖고 있다(TextDecor).
                desc = appliedBuff ? BuffDescription(skill)
                    : Format("str.skill." + skillId + ".desc", skillScratch.Bind(skill, table, language));
            }

            skillLineScratch.Clear();
            skillLineScratch.Set("name", name);
            skillLineScratch.Set("desc", desc);
            return Format("str.unit.skill.line", skillLineScratch).TrimEnd();
        }

        // STEP 3. 핵심 - 받은 버프에는 반경과 대상 조건을 빼고 실제 효과만 표시한다.
        private string BuffDescription(SkillData skill)
        {
            string key;
            switch (skill.buffStat)
            {
                case BuffStat.Atk:
                    key = "str.unit.buff.effect.atk";
                    break;
                case BuffStat.AtkSpeed:
                    key = "str.unit.buff.effect.atkspeed";
                    break;
                case BuffStat.Range:
                    key = "str.unit.buff.effect.range";
                    break;
                default:
                    return "";
            }
            return Format(key, skillScratch.Bind(skill, table, language));
        }

        public string FormatMonster(string name, LoopMonster monster, Fixed armorFixed, IMonsterSlowSnapshot slowSnapshot)
        {
            var armor = (float)armorFixed.ToDoubleForDisplay();
            var baseArmor = (float)monster.armor.ToDoubleForDisplay();
            var speed = (float)monster.moveSpeed.ToDoubleForDisplay();
            var baseSpeed = (float)monster.baseMoveSpeed.ToDoubleForDisplay();

            var armorLine = FormatStat("str.stat.armor", baseArmor, armor);
            if (armorFixed.raw > 0)
            {
                scratch.Clear();
                scratch.Set("percent", ArmorLabelPercent(armorFixed).ToString());
                armorLine += "   " + Format("str.monster.armor.reduction", scratch);
            }
            scratch.Clear();
            scratch.Set("value", monster.hp.ToIntTruncated().ToString());
            var hpLine = Format("str.monster.hp", scratch);

            return name + "\n"
                + hpLine + "\n"
                + armorLine + "\n"
                + FormatStat("str.stat.movespeed", baseSpeed, speed) + "\n"
                + SlowSection(slowSnapshot);
        }

        // 지금 이 몬스터에 걸려 있는 감속. 이동 속도가 왜 그 값인지 스킬 단위로 드러낸다(SPEC 5장).
        //   오라 감속과 온힛 감속은 수명이 달라 줄 서식이 갈린다. 온힛만 남은 시간을 적는다.
        private string SlowSection(IMonsterSlowSnapshot slowSnapshot)
        {
            if (slowSnapshot == null) return FormatSection("str.monster.slow.none", true);

            IReadOnlyList<SlowInfo> slowList = slowSnapshot.GetAppliedList();
            if (slowList.Count == 0) return FormatSection("str.monster.slow.none", true);

            var text = FormatSection("str.monster.slow.header");
            for (int i = 0; i < slowList.Count; ++i)
            {
                text += "\n  " + SlowLine(slowList[i]);
            }

            if (slowSnapshot.IsCapped) text += "\n" + SlowCappedLine();
            return text;
        }

        private string SlowLine(SlowInfo info)
        {
            slowLineScratch.Clear();
            slowLineScratch.Set("name", Get("str.skill." + info.skillId + ".name"));
            // 감속은 몬스터의 이동 속도를 깎는다. 스킬 설명의 감속 수치와 같은 색이어야 한다.
            //   퍼센트 기호도 값에 넣는다. 문장에 두면 숫자만 칠해지고 "%" 가 색 밖에 남는다.
            slowLineScratch.Set("percent",
                FormatValue((int)System.Math.Round(info.magnitude.ToDoubleForDisplay() * 100.0) + "%", true));

            if (info.isAura) return Format("str.monster.slow.aura", slowLineScratch);

            slowLineScratch.Set("sec", info.remaining.ToDoubleForDisplay().ToString("F1"));
            return Format("str.monster.slow.onhit", slowLineScratch);
        }

        // 하한 비율은 Core 의 CombatRules 가 갖고 있다. 여기서 30 을 적어 두면 규칙이 바뀔 때 갈라진다.
        private string SlowCappedLine()
        {
            scratch.Clear();
            scratch.Set("percent",
                System.Math.Round(CombatRules.MinSpeedRatio.ToDoubleForDisplay() * 100.0).ToString());
            return Format("str.monster.slow.capped", scratch);
        }

        private string Get(string key)
        {
            return table.Get(key, language);
        }

        private string Format(string key, IStringValues values)
        {
            return StringFormatter.Format(Get(key), values);
        }

        private string FormatSection(string key, bool empty = false)
        {
            return TextDecor.FormatSection(table, language, Get(key), empty);
        }

        private string FormatValue(string value, bool reduction)
        {
            return TextDecor.FormatValue(table, language, value, reduction);
        }

        private string FormatStat(string key, float baseValue, float effectiveValue, string numberFormat = "0.##")
        {
            return TextDecor.FormatStat(table, language, key, baseValue, effectiveValue, numberFormat);
        }

        private static int ArmorLabelPercent(Fixed armor)
        {
            return (int)System.Math.Round((float)(ArmorFormula.ReductionRatio(armor) * 100.0));
        }
    }
}
