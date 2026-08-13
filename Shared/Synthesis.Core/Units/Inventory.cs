using System.Collections.Generic;
using Synthesis.Core.Combination;
using Synthesis.Core.Data;

namespace Synthesis.Core.Units
{
    // STEP 3. 핵심 - 유닛 소유 모델 (v0.4). 뽑기로 1성이 쌓이고, 재료 2~4기를 소모해 상위로 조합한다.
    public sealed class OwnedUnit
    {
        public int instanceId;
        public string unitId;
    }

    public sealed class Inventory
    {
        private int nextInstanceId = 1;
        public List<OwnedUnit> ownedList = new List<OwnedUnit>();

        public int Count => ownedList.Count;

        public OwnedUnit Add(string unitId)
        {
            OwnedUnit owned = new OwnedUnit();
            owned.instanceId = nextInstanceId;
            owned.unitId = unitId;
            ++nextInstanceId;
            ownedList.Add(owned);
            return owned;
        }

        public bool RemoveByInstance(int instanceId)
        {
            for (int i = 0; i < ownedList.Count; ++i)
            {
                if (ownedList[i].instanceId == instanceId)
                {
                    ownedList.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        // 보유 유닛 개수 집계 (창고 포함 판정은 호출자가 합쳐 넘긴다).
        public Dictionary<string, int> CountsByUnit()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (var owned in ownedList)
            {
                if (!counts.ContainsKey(owned.unitId)) counts[owned.unitId] = 0;
                counts[owned.unitId] += 1;
            }
            return counts;
        }

        // resultId 를 조합한다. 재료가 인벤토리에 충분하면 소모하고 결과를 추가한다.
        public bool TryCraft(CombinationEngine engine, string resultId, out OwnedUnit result)
        {
            result = null;
            RecipeData recipe;
            if (!engine.TryGetRecipe(resultId, out recipe)) return false;

            // 소모할 인스턴스를 재료별 필요 개수만큼 찾는다.
            Dictionary<string, int> need = CombinationEngine.Needs(recipe);
            List<int> toRemove = new List<int>();
            Dictionary<string, int> taken = new Dictionary<string, int>();

            foreach (var owned in ownedList)
            {
                int required;
                if (!need.TryGetValue(owned.unitId, out required)) continue;
                int already;
                taken.TryGetValue(owned.unitId, out already);
                if (already >= required) continue;
                toRemove.Add(owned.instanceId);
                taken[owned.unitId] = already + 1;
            }

            // 전부 확보됐는지 확인
            foreach (var pair in need)
            {
                int got;
                taken.TryGetValue(pair.Key, out got);
                if (got < pair.Value) return false;
            }

            foreach (int id in toRemove) RemoveByInstance(id);
            result = Add(resultId);
            return true;
        }
    }
}
