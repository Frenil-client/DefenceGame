using System.Collections.Generic;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Combination;
using Synthesis.Core.Units;
using Synthesis.Core.Simulation;
using Synthesis.Core.Waves;
using Synthesis.Data;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 조립 루트 - 한 런에 필요한 Core 객체를 한 곳에 모은다(Bootstrap 계층).
    // 매니저/뷰는 이 컨텍스트를 주입받아 공유한다. Core 는 여전히 헤드리스다.
    public sealed class RunContext
    {
        public LoopMap map;
        public LoopSimulator sim;
        public GachaEngine gacha;
        public Inventory inventory;
        public CombinationEngine combination;
        public GameDatabase db;

        public Dictionary<string, UnitData> unitById;
        public Dictionary<string, EnemyData> enemyById;
        public Dictionary<string, BossData> bossById;
        public Dictionary<int, WaveData> waveByIndex;
        public Dictionary<string, SkillData> skillById;

        // 선택권: 석상 파괴와 보스 격파로 얻는 재화. 상점에서 원하는 1성 1기로 교환한다(SPEC 2-2).
        public int selectionTokens;
        public int statueTokenReward = 1; // [TEMP] 석상 1기 파괴 보상. 시뮬로 재확정
        public int selectionCost = 3;     // [TEMP] 1성 1기 구매에 드는 선택권 수. 시뮬로 재확정

        public static RunContext Build(long seed, bool useDefaultMap = false, MapSO mapAsset = null)
        {
            MapGenParams p = RuntimeDataLoader.LoadMapGenParams();

            RunContext ctx = new RunContext();
            // 맵 소스 우선순위: 저장된 맵(MapSO) > 기본 직사각형 > 시드 변주.
            // MapSO 를 쓰면 저장된 경로를 그대로 로드하므로 생성/베이크 불일치가 없다.
            if (mapAsset != null) ctx.map = mapAsset.ToLoopMap();
            else if (useDefaultMap) ctx.map = LoopMapGenerator.GenerateRectangular(p, seed);
            else ctx.map = LoopMapGenerator.Generate(p, seed);
            ctx.db = RuntimeDataLoader.LoadDatabase();
            ctx.sim = new LoopSimulator(ctx.map, seed);
            // 시작 코스트를 상한만큼 쥐고 시작한다(초기 지급 유닛을 바로 배치 가능).
            ctx.sim.state.cost = Fixed.FromInt(ctx.sim.state.costCap);
            ctx.gacha = new GachaEngine(ctx.db.unitList, seed);
            ctx.inventory = new Inventory();
            ctx.combination = new CombinationEngine(ctx.db.recipeList);

            ctx.unitById = new Dictionary<string, UnitData>();
            foreach (var u in ctx.db.unitList)
            {
                if (u != null && !string.IsNullOrEmpty(u.id)) ctx.unitById[u.id] = u;
            }
            ctx.enemyById = WaveResolver.BuildEnemyLookup(ctx.db.enemyList);
            ctx.bossById = WaveResolver.BuildBossLookup(ctx.db.bossList);
            ctx.waveByIndex = WaveResolver.BuildWaveLookup(ctx.db.waveList);

            ctx.skillById = new Dictionary<string, SkillData>();
            foreach (var skill in ctx.db.skillList)
            {
                if (skill != null && !string.IsNullOrEmpty(skill.id)) ctx.skillById[skill.id] = skill;
            }
            return ctx;
        }

        public bool IsValid()
        {
            return map != null && db != null && db.unitList.Count > 0;
        }

        // 구매 가능한 1성 목록(계열당 1종). 상점 UI 가 버튼으로 나열한다.
        public List<UnitData> SelectableTier1List()
        {
            List<UnitData> list = new List<UnitData>();
            for (int i = 0; i < db.unitList.Count; ++i)
            {
                UnitData u = db.unitList[i];
                if (u != null && u.tier == 1) list.Add(u);
            }
            return list;
        }

        public bool CanBuySelected()
        {
            return selectionTokens >= selectionCost;
        }

        // 선택권으로 원하는 1성을 구매해 인벤토리에 넣는다. 상점/히어로가 공유하는 로직(상점에 종속시키지 않음).
        public bool BuySelectedUnit(string tier1Id)
        {
            if (selectionTokens < selectionCost) return false;
            UnitData data;
            if (!unitById.TryGetValue(tier1Id, out data)) return false;
            if (data.tier != 1) return false;
            selectionTokens -= selectionCost;
            inventory.Add(tier1Id);
            return true;
        }

        // 인벤토리 + 필드 배치 유닛의 보유 개수 합산(조합 재료 판정에 쓴다. SPEC 3-2 필드 유닛도 재료).
        public Dictionary<string, int> MergedCounts()
        {
            Dictionary<string, int> counts = inventory.CountsByUnit();
            for (int i = 0; i < sim.state.unitList.Count; ++i)
            {
                string id = sim.state.unitList[i].data.id;
                if (!counts.ContainsKey(id)) counts[id] = 0;
                counts[id] += 1;
            }
            return counts;
        }

        public bool CanCraftMerged(string resultId)
        {
            RecipeData recipe;
            if (!combination.TryGetRecipe(resultId, out recipe)) return false;
            return combination.CanCraft(recipe, MergedCounts());
        }

        // 인벤토리 우선, 부족분은 필드에서 소모해 결과를 인벤토리에 넣는다(수동 조합 한 건). 결과는 다음 프레임 자동 배치된다.
        public bool TryCraftFromField(string resultId)
        {
            RecipeData recipe;
            if (!combination.TryGetRecipe(resultId, out recipe)) return false;
            if (!combination.CanCraft(recipe, MergedCounts())) return false;

            Dictionary<string, int> need = CombinationEngine.Needs(recipe);
            foreach (var pair in need)
            {
                int remaining = pair.Value;
                while (remaining > 0 && RemoveOneFromInventory(pair.Key)) --remaining;
                while (remaining > 0 && RemoveOneFromField(pair.Key)) --remaining;
                if (remaining > 0) return false; // CanCraft 통과 후엔 도달하지 않음
            }
            inventory.Add(resultId);
            return true;
        }

        private bool RemoveOneFromInventory(string unitId)
        {
            for (int i = 0; i < inventory.ownedList.Count; ++i)
            {
                if (inventory.ownedList[i].unitId == unitId)
                {
                    inventory.RemoveByInstance(inventory.ownedList[i].instanceId);
                    return true;
                }
            }
            return false;
        }

        private bool RemoveOneFromField(string unitId)
        {
            for (int i = 0; i < sim.state.unitList.Count; ++i)
            {
                if (sim.state.unitList[i].data.id == unitId)
                {
                    sim.state.unitList.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }
}
