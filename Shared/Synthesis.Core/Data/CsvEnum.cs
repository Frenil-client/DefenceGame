using System;

namespace Synthesis.Core.Data
{
    // STEP 1. 기반 도구 - CSV 문자열 값과 enum 사이의 변환.
    // 문자열 표기는 BALANCE_SPEC.md 10 스키마를 따른다.
    public static class CsvEnum
    {
        public static Grade StringToGrade(string value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "common": return Grade.Common;
                case "rare":   return Grade.Rare;
                case "unique": return Grade.Unique;
                case "hidden": return Grade.Hidden;
                default: throw new FormatException("Unknown grade '" + value + "'");
            }
        }

        public static Element StringToElement(string value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "fire":     return Element.Fire;
                case "ice":      return Element.Ice;
                case "thunder":  return Element.Thunder;
                case "physical": return Element.Physical;
                case "holy":     return Element.Holy;
                default: throw new FormatException("Unknown element '" + value + "'");
            }
        }

        public static Role StringToRole(string value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "single":  return Role.Single;
                case "splash":  return Role.Splash;
                case "pierce":  return Role.Pierce;
                case "dot":     return Role.Dot;
                case "support": return Role.Support;
                default: throw new FormatException("Unknown role '" + value + "'");
            }
        }

        public static Placement StringToPlacement(string value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "melee":  return Placement.Melee;
                case "ranged": return Placement.Ranged;
                default: throw new FormatException("Unknown placement '" + value + "'");
            }
        }

        public static ConditionType StringToConditionType(string value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "sameelement": return ConditionType.SameElement;
                case "samerole":    return ConditionType.SameRole;
                case "fixed":       return ConditionType.Fixed;
                default: throw new FormatException("Unknown conditionType '" + value + "'");
            }
        }
    }
}
