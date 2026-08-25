using System;

namespace Synthesis.Core.Data
{
    // STEP 1. 기반 도구 - CSV 문자열과 enum 변환 (v0.4 계열).
    public static class CsvEnum
    {
        public static Klass StringToKlass(string value)
        {
            switch (value.Trim().ToUpperInvariant())
            {
                case "WAR": return Klass.War;
                case "ARC": return Klass.Arc;
                case "MAG": return Klass.Mag;
                case "PRI": return Klass.Pri;
                case "THI": return Klass.Thi;
                case "SPI": return Klass.Spi;
                default: throw new FormatException("Unknown klass '" + value + "'");
            }
        }

        public static SkillTrigger StringToSkillTrigger(string value)
        {
            switch (value.Trim().ToUpperInvariant())
            {
                case "PASSIVE": return SkillTrigger.Passive;
                case "EVERYNTH": return SkillTrigger.EveryNthAttack;
                case "CHANCE": return SkillTrigger.ChanceOnAttack;
                default: throw new FormatException("Unknown skill trigger '" + value + "'");
            }
        }

        public static SkillEffect StringToSkillEffect(string value)
        {
            switch (value.Trim().ToUpperInvariant())
            {
                case "MULTITARGET": return SkillEffect.MultiTarget;
                case "AREADAMAGE": return SkillEffect.AreaDamage;
                case "PIERCE": return SkillEffect.Pierce;
                case "BONUSDAMAGE": return SkillEffect.BonusDamage;
                case "CRIT": return SkillEffect.Crit;
                case "DOT": return SkillEffect.DamageOverTime;
                case "SLOW": return SkillEffect.Slow;
                case "ALLYBUFF": return SkillEffect.AllyBuff;
                case "ARMORREDUCTION": return SkillEffect.ArmorReduction;
                default: throw new FormatException("Unknown skill effect '" + value + "'");
            }
        }

        public static BuffStat StringToBuffStat(string value)
        {
            switch (value.Trim().ToUpperInvariant())
            {
                case "": case "-": case "NONE": return BuffStat.None;
                case "ATK": return BuffStat.Atk;
                case "ATKSPEED": return BuffStat.AtkSpeed;
                case "RANGE": return BuffStat.Range;
                default: throw new FormatException("Unknown buff stat '" + value + "'");
            }
        }
    }
}
