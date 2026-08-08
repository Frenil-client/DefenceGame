using System.Collections.Generic;
using Synthesis.Core.Combination;

namespace Synthesis.Core.Units
{
    // STEP 3. 핵심 - 유닛 소유 모델. 뽑기로 얻은 유닛이 인벤토리에 쌓이고, 조합으로 소모/승급된다.
    // 필드 배치는 Simulation 이 담당하며, 배치되면 인벤토리에서 빠진다(생성 파이프라인 모델).
    public sealed class OwnedUnit
    {
        public int instanceId;   // 안정 식별자(결정적 순회/삭제용)
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

        public OwnedUnit GetByInstance(int instanceId)
        {
            for (int i = 0; i < ownedList.Count; ++i)
            {
                if (ownedList[i].instanceId == instanceId) return ownedList[i];
            }
            return null;
        }

        // 두 소유 유닛을 조합한다. 성립하면 둘을 제거하고 결과 유닛을 새로 추가한다.
        public bool TryCombine(CombinationEngine engine, int instanceA, int instanceB, out OwnedUnit result)
        {
            result = null;
            if (instanceA == instanceB) return false;

            OwnedUnit a = GetByInstance(instanceA);
            OwnedUnit b = GetByInstance(instanceB);
            if (a == null || b == null) return false;

            string resultId;
            if (!engine.TryCombine(a.unitId, b.unitId, out resultId)) return false;

            RemoveByInstance(instanceA);
            RemoveByInstance(instanceB);
            result = Add(resultId);
            return true;
        }
    }
}
