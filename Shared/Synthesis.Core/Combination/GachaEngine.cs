using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Random;

namespace Synthesis.Core.Combination
{
    // STEP 3. 핵심 - 뽑기와 보장 규칙 G1 부터 G5 (BALANCE_SPEC.md 6-2).
    // 웨이브 종료 시 흔함 1기를 지급한다. 랜덤 편차가 런의 성패를 가르지 않도록 보장 규칙으로 강제한다.
    //
    // 기본 가중치는 흔함 균등이며, 필터/강제 후 후보 중 균등 추첨한다(분포는 STEP 7 시뮬로 확정, TEMP).
    // 공세에 의한 속성 2배 가중치(6-3)는 공세가 붙는 STEP 5 에서 얹는다.
    public sealed class GachaEngine
    {
        private const int GuardKeyDeadlineWave = 7;   // G2: 8웨이브 이전
        private const int G1DeadlineWave = 5;
        private const int G5DeadlineWave = 11;        // G5: 12웨이브 이전

        private readonly List<UnitData> commonList;
        private readonly Dictionary<string, UnitData> commonById;
        private readonly List<RecipeData> rareRecipeList;
        private readonly DeterministicRandom rng;
        private readonly List<string> grantHistory = new List<string>();

        public IReadOnlyList<string> history => grantHistory;

        public GachaEngine(List<UnitData> allUnits, List<RecipeData> recipes, long seed)
        {
            commonList = new List<UnitData>();
            commonById = new Dictionary<string, UnitData>();
            foreach (var unit in allUnits)
            {
                if (unit.grade != Grade.Common) continue;
                commonList.Add(unit);
                commonById[unit.id] = unit;
            }

            rareRecipeList = new List<RecipeData>();
            foreach (var recipe in recipes)
            {
                if (recipe == null || recipe.isHidden) continue;
                if (recipe.conditionType == ConditionType.Fixed) continue; // 흔함->레어 만
                rareRecipeList.Add(recipe);
            }

            rng = new DeterministicRandom(seed);
        }

        // 해당 웨이브 종료 시 흔함 1기를 지급한다.
        public string GrantForWave(int wave)
        {
            List<UnitData> candidates = BuildCandidates();

            List<UnitData> forced = ComputeForced(wave);
            List<UnitData> pool;
            if (forced != null && forced.Count > 0)
            {
                List<UnitData> intersect = Intersect(forced, candidates);
                pool = intersect.Count > 0 ? intersect : forced; // 보장은 회피 규칙보다 우선
            }
            else
            {
                pool = candidates;
            }
            if (pool.Count == 0) pool = commonList; // 과도 제약 방어

            UnitData pick = pool[rng.NextInt(pool.Count)];
            grantHistory.Add(pick.id);
            return pick.id;
        }

        // ---- 후보 필터 (회피 규칙 G3, G4) ----

        private List<UnitData> BuildCandidates()
        {
            List<UnitData> result = new List<UnitData>();
            foreach (var unit in commonList)
            {
                if (ViolatesG3(unit.id)) continue;
                if (ViolatesG4(unit.element)) continue;
                result.Add(unit);
            }
            return result;
        }

        // G3: 동일 유닛 4연속 금지. 직전 3회가 모두 같으면 그 유닛을 제외.
        private bool ViolatesG3(string id)
        {
            int n = grantHistory.Count;
            if (n < 3) return false;
            return grantHistory[n - 1] == id && grantHistory[n - 2] == id && grantHistory[n - 3] == id;
        }

        // G4: 최근 6회 중 동일 속성 4회 초과 금지. 직전 5회에 이미 4회면 이번에 넣으면 5회가 되어 제외.
        private bool ViolatesG4(Element element)
        {
            int count = 0;
            int n = grantHistory.Count;
            for (int i = n - 1; i >= 0 && i >= n - 5; --i)
            {
                UnitData unit;
                if (commonById.TryGetValue(grantHistory[i], out unit) && unit.element == element) ++count;
            }
            return count >= 4;
        }

        // ---- 보장 강제 (G1, G2, G5) ----

        private List<UnitData> ComputeForced(int wave)
        {
            // 우선순위: G2(가장 치명적) > G1 > G5
            List<UnitData> g2 = ForceG2(wave);
            if (g2 != null) return g2;

            List<UnitData> g1 = ForceG1(wave);
            if (g1 != null) return g1;

            List<UnitData> g5 = ForceG5(wave);
            if (g5 != null) return g5;

            return null;
        }

        // G2: 8웨이브 이전에 C05(관통) 또는 C09(방깎) 최소 1종 보장.
        private List<UnitData> ForceG2(int wave)
        {
            if (HasGuardKey()) return null;
            int remainingIncludingThis = GuardKeyDeadlineWave - wave + 1;
            if (remainingIncludingThis > 1) return null; // 아직 여유

            List<UnitData> forced = new List<UnitData>();
            AddIfExists(forced, "C05");
            AddIfExists(forced, "C09");
            return forced;
        }

        // G1: 5웨이브 이내 근접 최소 2종, 원거리 최소 2종.
        private List<UnitData> ForceG1(int wave)
        {
            if (wave > G1DeadlineWave) return null;

            int distinctMelee = CountDistinctPlacement(Placement.Melee);
            int distinctRanged = CountDistinctPlacement(Placement.Ranged);
            int needMelee = distinctMelee < 2 ? 2 - distinctMelee : 0;
            int needRanged = distinctRanged < 2 ? 2 - distinctRanged : 0;
            int totalNeed = needMelee + needRanged;
            if (totalNeed == 0) return null;

            int remainingIncludingThis = G1DeadlineWave - wave + 1;
            if (totalNeed < remainingIncludingThis) return null; // 아직 여유

            Placement want = needMelee >= needRanged ? Placement.Melee : Placement.Ranged;
            List<UnitData> forced = new List<UnitData>();
            foreach (var unit in commonList)
            {
                if (unit.placement != want) continue;
                if (HistoryContains(unit.id)) continue; // 새 종류로 distinct 를 늘린다
                forced.Add(unit);
            }
            return forced;
        }

        // G5: 12웨이브 이전에 레어 조합이 최소 2회 가능한 재료 구성 보장.
        private List<UnitData> ForceG5(int wave)
        {
            if (wave > G5DeadlineWave) return null;

            int possible = CountPossibleRareCombos();
            int need = possible < 2 ? 2 - possible : 0;
            if (need == 0) return null;

            int remainingIncludingThis = G5DeadlineWave - wave + 1;
            if (need < remainingIncludingThis) return null; // 아직 여유

            // 지금 지급하면 새로운 레어 조합을 성립시키는 흔함들을 강제한다.
            List<UnitData> forced = new List<UnitData>();
            foreach (var unit in commonList)
            {
                if (CompletesNewRareCombo(unit.id)) forced.Add(unit);
            }
            return forced;
        }

        // ---- 조회 헬퍼 ----

        private bool HasGuardKey()
        {
            return HistoryContains("C05") || HistoryContains("C09");
        }

        private int CountDistinctPlacement(Placement placement)
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (var id in grantHistory)
            {
                UnitData unit;
                if (commonById.TryGetValue(id, out unit) && unit.placement == placement) seen.Add(id);
            }
            return seen.Count;
        }

        // 현재 지급 이력으로 재료가 모두 갖춰진 레어 레시피 수(종류 기준).
        private int CountPossibleRareCombos()
        {
            int count = 0;
            foreach (var recipe in rareRecipeList)
            {
                if (HistoryContains(recipe.mat1) && HistoryContains(recipe.mat2)) ++count;
            }
            return count;
        }

        // id 를 지금 지급하면 아직 성립 안 된 레어 레시피가 새로 성립하는가.
        private bool CompletesNewRareCombo(string id)
        {
            foreach (var recipe in rareRecipeList)
            {
                bool has1 = HistoryContains(recipe.mat1);
                bool has2 = HistoryContains(recipe.mat2);
                if (has1 && has2) continue; // 이미 성립
                if (recipe.mat1 == id && has2) return true;
                if (recipe.mat2 == id && has1) return true;
                // 두 재료가 같은 종류를 요구하는 경우는 없다(모두 서로 다른 흔함).
            }
            return false;
        }

        private bool HistoryContains(string id)
        {
            for (int i = 0; i < grantHistory.Count; ++i)
            {
                if (grantHistory[i] == id) return true;
            }
            return false;
        }

        private void AddIfExists(List<UnitData> list, string id)
        {
            UnitData unit;
            if (commonById.TryGetValue(id, out unit)) list.Add(unit);
        }

        private static List<UnitData> Intersect(List<UnitData> a, List<UnitData> b)
        {
            List<UnitData> result = new List<UnitData>();
            foreach (var unit in a)
            {
                if (b.Contains(unit)) result.Add(unit);
            }
            return result;
        }
    }
}
