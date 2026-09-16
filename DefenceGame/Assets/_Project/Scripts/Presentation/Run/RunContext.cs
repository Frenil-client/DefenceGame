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
        public int statueTokenReward = 3; // [TEMP] 석상 1기 파괴 보상. 시뮬로 재확정
        public int selectionCost = 1;     // [TEMP] 1성 1기 구매에 드는 선택권 수. 시뮬로 재확정

        // [치트] 테스트 전용. 켜면 선택권 없이 전 등급 유닛을 상점에서 바로 산다.
        //   스킬은 등급이 올라가야 붙으므로(1성은 스킬 없음) 상위 유닛을 조합 없이 꺼내 확인하려고 둔다.
        //   밸런스와 무관한 확인용 통로이며 기본값은 꺼짐이다. 켜고 끄는 것은 GameManager 의 단축키다.
        public bool cheatFreeShop;

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

        // 구매 가능한 목록. 평소에는 1성만(계열당 1종), 치트면 전 등급이다. 상점 UI 가 버튼으로 나열한다.
        //   등급 오름차순으로 낸다. 42종을 나열해도 상점 목록이 스크롤이라 그대로 들어간다.
        public List<UnitData> PurchasableUnitList()
        {
            List<UnitData> list = new List<UnitData>();
            for (int tier = 1; tier <= 5; ++tier)
            {
                for (int i = 0; i < db.unitList.Count; ++i)
                {
                    UnitData u = db.unitList[i];
                    if (u == null || u.tier != tier) continue;
                    if (!cheatFreeShop && u.tier != 1)
                    {
                        continue;
                    }
                    list.Add(u);
                }

                if (!cheatFreeShop) break;
            }
            return list;
        }

        // 이번 구매에 드는 선택권 수. 치트면 공짜다.
        public int GetBuyCost()
        {
            if (cheatFreeShop) return 0;
            return selectionCost;
        }

        public bool CanBuySelected()
        {
            return selectionTokens >= GetBuyCost();
        }

        // 원하는 유닛을 구매해 인벤토리에 넣는다. 상점/히어로가 공유하는 로직(상점에 종속시키지 않음).
        //   평소에는 선택권을 내고 1성만 살 수 있다. 치트면 비용도 등급 제한도 없다.
        public bool BuySelectedUnit(string unitId)
        {
            int cost = GetBuyCost();
            if (selectionTokens < cost) return false;
            UnitData data;
            if (!unitById.TryGetValue(unitId, out data)) return false;
            if (!cheatFreeShop && data.tier != 1) return false;
            selectionTokens -= cost;
            inventory.Add(unitId);
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
