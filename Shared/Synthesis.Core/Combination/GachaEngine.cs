using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Random;

namespace Synthesis.Core.Combination
{
    // STEP 3. 핵심 - 뽑기 (v0.4). 웨이브마다 1성 1기를 균등 확률(1/6)로 지급한다.
    // 덱/보장 규칙 없음. 랜덤성 통제는 선택권(석상/보스 보상 + 상점)이 담당한다 (BALANCE 9).
    public sealed class GachaEngine
    {
        private readonly List<string> tier1List = new List<string>();
        private readonly DeterministicRandom rng;

        public GachaEngine(List<UnitData> allUnits, long seed)
        {
            foreach (var unit in allUnits)
            {
                if (unit == null) continue;
                if (unit.tier == 1) tier1List.Add(unit.id);
            }
            rng = new DeterministicRandom(seed);
        }

        public string GrantForWave(int waveIndex)
        {
            return Grant();
        }

        // 1성 1기를 균등 확률로 뽑는다. 웨이브 지급과 게임 시작 시 초기 지급이 같은 스트림을 쓴다.
        public string Grant()
        {
            if (tier1List.Count == 0) return null;
            return tier1List[rng.NextInt(tier1List.Count)];
        }
    }
}
