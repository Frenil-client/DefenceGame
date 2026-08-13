using System.Collections.Generic;
using Synthesis.Core.Combination;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Units;
using Synthesis.Core.Waves;

namespace Synthesis.Core.Simulation
{
    // STEP 3(v0.4). 핵심 - 한 런의 생성/처리 파이프라인을 잇는다(헤드리스).
    // 웨이브마다: 뽑기(1성) -> 자동 조합(가능한 상위 유닛) -> 자동 배치(중앙 우선) -> 전투.
    // 보스 웨이브는 제한시간 안에 처치해야 한다. 41웨이브 보스 격파 시 클리어.
    public sealed class LoopRunResult
    {
        public int wavesReached;
        public bool cleared;
        public bool defeated;
        public int killedTotal;
        public ulong finalHash;
        public List<string> waveLog = new List<string>();
    }

    public sealed class LoopRunController
    {
        private readonly GameDatabase db;
        private readonly LoopSimulator sim;
        private readonly GachaEngine gacha;
        private readonly Inventory inventory;
        private readonly CombinationEngine combination;
        private readonly Dictionary<string, UnitData> unitById;
        private readonly Dictionary<string, EnemyData> enemyById;
        private readonly Dictionary<string, BossData> bossById;
        private readonly Dictionary<int, WaveData> waveByIndex;
        private readonly List<RecipeData> recipesByTierDesc;
        private readonly List<GridPos> placeTiles;
        private readonly List<GridPos> statueFarmTiles;   // 석상마다 사거리로 커버할 인접 배치칸
        private readonly int maxWave;

        private int doppelAvailable;        // 사용 가능한 도플갱어 수 (석상 + 보스 보상 - 사용)
        private int doppelSeenFromStatues;  // 시뮬 석상 드랍 누계 반영분
        private int currentWave;

        // [TEMP] 게임 시작 시 미리 지급하는 1성 유닛 수. 최초 지급(BALANCE 6-1)의 초기값이며 시뮬로 재확정한다.
        public const int DefaultStartUnitCount = 5;

        private readonly int startUnitCount;

        public LoopSimulator Sim => sim;
        public Inventory InventoryRef => inventory;

        public LoopRunController(GameDatabase db, LoopMap map, long seed, int maxWave = 41, int startUnitCount = DefaultStartUnitCount)
        {
            this.db = db;
            this.maxWave = maxWave;
            this.startUnitCount = startUnitCount;
            sim = new LoopSimulator(map, seed);
            gacha = new GachaEngine(db.unitList, seed);
            inventory = new Inventory();
            combination = new CombinationEngine(db.recipeList);

            unitById = new Dictionary<string, UnitData>();
            foreach (var u in db.unitList)
            {
                if (u != null && !string.IsNullOrEmpty(u.id)) unitById[u.id] = u;
            }
            enemyById = WaveResolver.BuildEnemyLookup(db.enemyList);
            bossById = WaveResolver.BuildBossLookup(db.bossList);
            waveByIndex = WaveResolver.BuildWaveLookup(db.waveList);

            recipesByTierDesc = new List<RecipeData>(db.recipeList);
            recipesByTierDesc.Sort((a, b) => TierOf(b.resultId).CompareTo(TierOf(a.resultId)));

            // 헤드리스 오토플레이는 시작 코스트를 상한만큼 쥐고 시작한다(첫 배치 가능).
            sim.state.cost = Fixed.FromInt(sim.state.costCap);

            placeTiles = BuildPlaceOrder(map);
            statueFarmTiles = BuildStatueFarmTiles(map);
        }

        // 석상마다, 석상을 사거리에 두면서 배치 가능한 가장 가까운 칸을 하나씩 고른다.
        // 오토플레이가 여기에 유닛을 놓아 석상을 farming(도플갱어 확보)한다.
        private static List<GridPos> BuildStatueFarmTiles(LoopMap map)
        {
            HashSet<int> statueKey = new HashSet<int>();
            foreach (var s in map.statueList) statueKey.Add(s.y * map.gridWidth + s.x);

            List<GridPos> farm = new List<GridPos>();
            foreach (var statue in map.statueList)
            {
                int pick = -1;
                int best = int.MaxValue;
                for (int i = 0; i < map.buildTileList.Count; ++i)
                {
                    GridPos b = map.buildTileList[i];
                    if (statueKey.Contains(b.y * map.gridWidth + b.x)) continue; // 석상 칸 제외
                    int dx = b.x - statue.x;
                    int dy = b.y - statue.y;
                    int d = dx * dx + dy * dy;
                    if (d < best)
                    {
                        best = d;
                        pick = i;
                    }
                }
                farm.Add(pick >= 0 ? map.buildTileList[pick] : statue);
            }
            return farm;
        }

        // 배치 순서를 루프 전체에 고르게 분산시킨다. 보스는 느려서(0.5셀/초) 제한시간 안에
        // 루프를 다 못 돌기 때문에, 유닛이 한 곳에 뭉치면 보스가 사거리에 안 들어온다.
        // 루프 웨이포인트를 순서대로 돌며 각 지점에 가장 가까운 미사용 배치칸을 배정한다.
        private static List<GridPos> BuildPlaceOrder(LoopMap map)
        {
            List<GridPos> ordered = new List<GridPos>();
            List<GridPos> pool = new List<GridPos>(map.buildTileList);
            bool[] used = new bool[pool.Count];

            var wp = map.loopWaypointList;
            for (int w = 0; w < wp.Count && ordered.Count < pool.Count; ++w)
            {
                int pick = -1;
                int best = int.MaxValue;
                for (int i = 0; i < pool.Count; ++i)
                {
                    if (used[i]) continue;
                    int dx = pool[i].x - wp[w].x;
                    int dy = pool[i].y - wp[w].y;
                    int d = dx * dx + dy * dy;
                    if (d < best)
                    {
                        best = d;
                        pick = i;
                    }
                }
                if (pick < 0) continue;
                used[pick] = true;
                ordered.Add(pool[pick]);
            }

            // 혹시 남은 칸이 있으면 뒤에 이어붙인다.
            for (int i = 0; i < pool.Count; ++i)
            {
                if (!used[i]) ordered.Add(pool[i]);
            }
            return ordered;
        }

        public LoopRunResult RunFullCycle(int maxTicksPerWave = 8000)
        {
            LoopRunResult result = new LoopRunResult();

            // 게임 시작 시 1성 유닛을 미리 지급한다(최초 지급). 이후 웨이브마다 1기씩 추가된다.
            for (int i = 0; i < startUnitCount; ++i)
            {
                string startUnit = gacha.Grant();
                if (startUnit != null) inventory.Add(startUnit);
            }

            for (int wave = 1; wave <= maxWave; ++wave)
            {
                WaveData wd;
                if (!waveByIndex.TryGetValue(wave, out wd)) continue;

                currentWave = wave;

                // 뽑기 -> 조합 -> 배치
                string granted = gacha.GrantForWave(wave);
                if (granted != null) inventory.Add(granted);
                HarvestStatueDoppel();
                AutoCombine();
                AutoPlace();

                // 스폰
                EnemyData enemy = ResolveScaledEnemy(wd);
                int spawnCount = enemy != null ? wd.spawnCount : 0;
                sim.StartWave(enemy, spawnCount, wd.spawnInterval);

                int limit = wd.isBoss ? BossTimeLimit(wd) : maxTicksPerWave;
                int guard = 0;
                while (!sim.IsFieldClear() && guard < limit)
                {
                    sim.Tick();
                    ++guard;

                    // 코스트가 회복되고 석상이 파괴되는 동안 계속 조합/배치를 시도한다(1초마다).
                    if (guard % LoopSimulator.TicksPerSecond == 0)
                    {
                        HarvestStatueDoppel();
                        AutoCombine();
                        AutoPlace();
                    }
                }

                bool cleared = sim.IsFieldClear();

                // 보스 격파 보상 도플갱어 (SPEC 3-6).
                if (cleared && wd.isBoss)
                {
                    BossData boss;
                    if (bossById.TryGetValue(wd.bossId, out boss)) doppelAvailable += boss.doppelReward;
                }

                result.wavesReached = wave;
                result.waveLog.Add("wave " + wave + (wd.isBoss ? " BOSS " + wd.bossId : " " + wd.enemySetId)
                    + " units=" + sim.state.unitList.Count + " [" + FieldTierSummary() + "]"
                    + " killed=" + sim.state.killedCount + " dopp=" + doppelAvailable
                    + " field=" + sim.state.aliveCount + " bossHp=" + BossHpLeft(wd)
                    + (cleared ? " CLEAR" : " TIMEOUT"));

                if (!cleared)
                {
                    result.defeated = true;
                    break;
                }
            }

            result.cleared = !result.defeated && result.wavesReached >= maxWave;
            result.killedTotal = sim.state.killedCount;
            result.finalHash = sim.ComputeStateHash();
            return result;
        }

        // 조합 목표를 향해 재귀적으로 만든다. 매 스텝, 만들 수 있는 가장 높은 등급을 하나 만든다.
        // 조합 재료는 인벤토리 + 필드 배치 유닛을 합친 풀에서 끌어오고(SPEC 3-2 창고 규칙), 부족한 1성은 도플갱어로 채운다(SPEC 2-2).
        // 깊은 트리(T5 = 1성 31기 상당)에서도 목표를 정해 중간 재료까지 만들어 올라간다.
        // 조합 시도는 원자적이다. 중간에 실패하면 소모한 재료/도플갱어를 그대로 되돌린다.
        private void AutoCombine()
        {
            bool progressed = true;
            int guard = 0;
            while (progressed && guard < 500)
            {
                ++guard;
                progressed = false;

                for (int i = 0; i < recipesByTierDesc.Count; ++i)
                {
                    string target = recipesByTierDesc[i].resultId;

                    // 도플갱어(희소 자원)는 3성 이상 조합에만 쓴다. 2성에 새어 나가면 상위 조합이 영영 막힌다.
                    bool allowDoppel = TierOf(target) >= 3;

                    if (TryCraftAtomic(target, allowDoppel))
                    {
                        progressed = true;
                        break;
                    }
                }
            }
        }

        // target 하나를 조합한다. 성공하면 확정, 실패하면 소모분을 전부 되돌린다(원자적).
        private bool TryCraftAtomic(string target, bool allowDoppel)
        {
            List<OwnedUnit> invSnap = new List<OwnedUnit>(inventory.ownedList);
            List<LoopUnit> fieldSnap = new List<LoopUnit>(sim.state.unitList);
            int doppelSnap = doppelAvailable;

            if (CraftOne(target, allowDoppel)) return true;

            inventory.ownedList.Clear();
            inventory.ownedList.AddRange(invSnap);
            sim.state.unitList.Clear();
            sim.state.unitList.AddRange(fieldSnap);
            doppelAvailable = doppelSnap;
            return false;
        }

        // id 하나를 실제로 만들어 인벤토리에 넣는다(재료는 소모).
        private bool CraftOne(string id, bool allowDoppel)
        {
            RecipeData recipe;
            if (!combination.TryGetRecipe(id, out recipe)) return false;

            Dictionary<string, int> need = CombinationEngine.Needs(recipe);
            foreach (var pair in need)
            {
                for (int k = 0; k < pair.Value; ++k)
                {
                    if (!AcquireMaterial(pair.Key, allowDoppel)) return false;
                }
            }
            inventory.Add(id);
            return true;
        }

        // 재료 하나를 확보(소모)한다. 풀에 있으면 소모, 없는 1성은 도플갱어(허용 시), 없는 상위는 재귀 조합해 즉시 소모.
        private bool AcquireMaterial(string id, bool allowDoppel)
        {
            if (RemoveOneFromInventory(id)) return true;
            if (RemoveOneFromField(id)) return true;

            UnitData u;
            unitById.TryGetValue(id, out u);

            RecipeData recipe;
            if (!combination.TryGetRecipe(id, out recipe))
            {
                if (u != null && u.tier == 1 && allowDoppel && doppelAvailable > 0)
                {
                    --doppelAvailable;
                    return true;
                }
                return false;
            }

            Dictionary<string, int> need = CombinationEngine.Needs(recipe);
            foreach (var pair in need)
            {
                for (int k = 0; k < pair.Value; ++k)
                {
                    if (!AcquireMaterial(pair.Key, allowDoppel)) return false;
                }
            }
            return true; // 한 기 조합해 즉시 소모(상위 재료로 쓰임)
        }

        // 시뮬이 석상을 파괴해 드랍한 도플갱어를 가용 풀에 반영한다.
        private void HarvestStatueDoppel()
        {
            int dropped = sim.state.doppelDropped;
            if (dropped > doppelSeenFromStatues)
            {
                doppelAvailable += dropped - doppelSeenFromStatues;
                doppelSeenFromStatues = dropped;
            }
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

        private void AutoPlace()
        {
            List<OwnedUnit> snapshot = new List<OwnedUnit>(inventory.ownedList);
            foreach (var owned in snapshot)
            {
                UnitData data;
                if (!unitById.TryGetValue(owned.unitId, out data)) continue;
                if (data.isDoppel) continue;
                if (sim.state.cost < Fixed.FromInt(data.cost)) continue;

                if (TryPlace(data))
                {
                    inventory.RemoveByInstance(owned.instanceId);
                }
            }
        }

        // 배치 우선순위: 2웨이브부터, 아직 살아있는 석상을 커버하는 farming 칸을 먼저 채운다(도플갱어 확보).
        // 그 칸이 이미 찼으면 루프 커버용 배치칸으로 넘어간다.
        private bool TryPlace(UnitData data)
        {
            if (currentWave >= 2)
            {
                for (int s = 0; s < statueFarmTiles.Count && s < sim.state.statueList.Count; ++s)
                {
                    if (!sim.state.statueList[s].alive) continue;
                    GridPos t = statueFarmTiles[s];
                    if (sim.PlaceUnit(data, t.x, t.y)) return true;
                }
            }

            for (int i = 0; i < placeTiles.Count; ++i)
            {
                if (sim.PlaceUnit(data, placeTiles[i].x, placeTiles[i].y)) return true;
            }
            return false;
        }

        // 일반 적은 웨이브 난이도 스케일을 hp 에 곱한다. 보스는 hp 를 bosses.csv 에서 직접 정의하므로 스케일을 적용하지 않는다.
        private EnemyData ResolveScaledEnemy(WaveData wd)
        {
            EnemyData baseEnemy = WaveResolver.ResolveEnemy(wd, enemyById, bossById);
            if (baseEnemy == null) return null;
            if (wd.isBoss) return baseEnemy;

            EnemyData scaled = new EnemyData();
            scaled.id = baseEnemy.id;
            scaled.name = baseEnemy.name;
            scaled.hp = baseEnemy.hp * wd.difficultyScale;
            scaled.atk = baseEnemy.atk;
            scaled.moveSpeed = baseEnemy.moveSpeed;
            return scaled;
        }

        private int BossTimeLimit(WaveData wd)
        {
            BossData boss;
            if (bossById.TryGetValue(wd.bossId, out boss) && boss.timeLimitTicks > 0) return boss.timeLimitTicks;
            return 1200;
        }

        private string FieldTierSummary()
        {
            int[] byTier = new int[6];
            for (int i = 0; i < sim.state.unitList.Count; ++i)
            {
                int t = sim.state.unitList[i].data.tier;
                if (t >= 1 && t <= 5) byTier[t] += 1;
            }
            return "T1:" + byTier[1] + " T2:" + byTier[2] + " T3:" + byTier[3] + " T4:" + byTier[4] + " T5:" + byTier[5];
        }

        private string BossHpLeft(WaveData wd)
        {
            if (!wd.isBoss) return "-";
            for (int i = 0; i < sim.state.monsterList.Count; ++i)
            {
                if (sim.state.monsterList[i].alive) return sim.state.monsterList[i].hp.ToIntRounded().ToString();
            }
            return "0";
        }

        private int TierOf(string unitId)
        {
            UnitData u;
            return unitById.TryGetValue(unitId, out u) ? u.tier : 0;
        }
    }
}
