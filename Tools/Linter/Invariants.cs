using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Linter
{
    // STEP 1(v0.4). 불변식 검증 BALANCE_SPEC.md 17. 등급 간 재료 커버리지가 핵심이다.
    public enum Severity { Authoritative, Provisional }

    public sealed class InvResult
    {
        public string id;
        public bool passed;
        public Severity severity;
        public List<string> messageList = new List<string>();
    }

    public static class Invariants
    {
        public static List<InvResult> RunAll(GameDatabase db)
        {
            Dictionary<string, UnitData> unitById = new Dictionary<string, UnitData>();
            foreach (var u in db.unitList)
            {
                if (u == null || string.IsNullOrEmpty(u.id)) continue;
                unitById[u.id] = u;
            }

            List<InvResult> list = new List<InvResult>();
            list.Add(CheckOccurrence(db, unitById, "INV-01", 1, 2, 4, 4));   // 1성: 2성에 정확히 4회
            list.Add(CheckOccurrence(db, unitById, "INV-02", 2, 3, 1, int.MaxValue)); // 2성: 3성에 1회+
            list.Add(CheckTierUpward(db, unitById, "INV-03", 3, new int[] { 4, 5 })); // 3성: 4성/5성에 1회+
            list.Add(CheckTierUpward(db, unitById, "INV-04", 4, new int[] { 5 }));     // 4성: 5성에 1회+
            list.Add(CheckCostMonotonic(db, unitById));                                 // INV-06 코스트 단조
            return list;
        }

        // fromTier 유닛이 toTier 레시피에 등장하는 총 횟수가 [minCount, maxCount] 인가.
        private static InvResult CheckOccurrence(GameDatabase db, Dictionary<string, UnitData> unitById,
            string id, int fromTier, int toTier, int minCount, int maxCount)
        {
            InvResult r = new InvResult { id = id, severity = Severity.Authoritative, passed = true };
            foreach (var unit in db.unitList)
            {
                if (unit.tier != fromTier || unit.isDoppel) continue;
                int count = CountOccurrences(db, unitById, unit.id, toTier);
                if (count < minCount || count > maxCount)
                {
                    r.passed = false;
                    r.messageList.Add(unit.id + " (" + unit.name + ") 는 " + toTier + "성 레시피에 " + count + "회 등장");
                }
            }
            return r;
        }

        private static InvResult CheckTierUpward(GameDatabase db, Dictionary<string, UnitData> unitById,
            string id, int fromTier, int[] toTiers)
        {
            InvResult r = new InvResult { id = id, severity = Severity.Authoritative, passed = true };
            foreach (var unit in db.unitList)
            {
                if (unit.tier != fromTier || unit.isDoppel) continue;
                int count = 0;
                foreach (int t in toTiers) count += CountOccurrences(db, unitById, unit.id, t);
                if (count < 1)
                {
                    r.passed = false;
                    r.messageList.Add(unit.id + " (" + unit.name + ") 는 상위 레시피에 등장하지 않음 (고아)");
                }
            }
            return r;
        }

        private static InvResult CheckCostMonotonic(GameDatabase db, Dictionary<string, UnitData> unitById)
        {
            InvResult r = new InvResult { id = "INV-06", severity = Severity.Authoritative, passed = true };
            int[] tierCost = new int[6];
            bool[] seen = new bool[6];
            foreach (var unit in db.unitList)
            {
                if (unit.isDoppel || unit.tier < 1 || unit.tier > 5) continue;
                if (!seen[unit.tier]) { tierCost[unit.tier] = unit.cost; seen[unit.tier] = true; }
            }
            for (int t = 2; t <= 5; ++t)
            {
                if (seen[t] && seen[t - 1] && tierCost[t] <= tierCost[t - 1])
                {
                    r.passed = false;
                    r.messageList.Add(t + "성 코스트 " + tierCost[t] + " <= " + (t - 1) + "성 " + tierCost[t - 1]);
                }
            }
            return r;
        }

        private static int CountOccurrences(GameDatabase db, Dictionary<string, UnitData> unitById, string unitId, int resultTier)
        {
            int count = 0;
            foreach (var recipe in db.recipeList)
            {
                UnitData result;
                if (!unitById.TryGetValue(recipe.resultId, out result)) continue;
                if (result.tier != resultTier) continue;
                foreach (var mat in recipe.materials)
                {
                    if (mat == unitId) ++count;
                }
            }
            return count;
        }
    }
}
