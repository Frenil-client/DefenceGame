using System.Collections.Generic;
using Synthesis.Core.Combination;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Units;
using Synthesis.Core.Waves;

namespace Synthesis.Core.Simulation
{
    // STEP 3. 핵심 - 한 런의 생성/처리 파이프라인을 잇는다.
    // 웨이브마다: 뽑기 -> 인벤토리 적재 -> (임시 정책) 배치 -> 몬스터 스폰 -> 전투 -> 정산.
    // 능력/스탯 처리는 아직 없다(별도 작업). 여기서는 "무엇이 언제 생성되고 어디로 흐르는가"만 모델링한다.
    //
    // 자동 배치는 데모/검증용 임시 정책이다. 정식 플레이어 정책(IPlayerPolicy)은 STEP 7 이다.
    // 사망 유닛의 인벤토리 복귀(재배치 쿨타임 후)는 아직 잇지 않았다. 배치 시 인벤토리에서 제거만 한다.

    public sealed class WaveOutcome
    {
        public int waveIndex;
        public string grantedUnitId;
        public int placedThisWave;
        public int killedThisWave;
        public int leakedThisWave;
        public ulong stateHash;
        public bool isBoss;
        public string enemyLabel;
        public int spawnCount;
    }

    public sealed class RunController
    {
        private readonly GameDatabase db;
        private readonly MapData map;
        private readonly Dictionary<string, UnitData> unitById;
        private readonly Dictionary<string, EnemyData> enemyById;
        private readonly Dictionary<string, BossData> bossById;
        private readonly Dictionary<int, WaveData> waveByIndex;

        public Simulator sim { get; private set; }
        public Inventory inventory { get; private set; }
        public GachaEngine gacha { get; private set; }
        public CombinationEngine combination { get; private set; }

        private WaveOutcome activeOutcome;
        private int activePrevKilled;
        private int activePrevLeaked;

        public RunController(GameDatabase db, MapData map, long seed)
        {
            this.db = db;
            this.map = map;

            unitById = new Dictionary<string, UnitData>();
            foreach (var unit in db.unitList)
            {
                if (unit == null || string.IsNullOrEmpty(unit.id)) continue;
                unitById[unit.id] = unit;
            }
            enemyById = WaveResolver.BuildEnemyLookup(db.enemyList);
            bossById = WaveResolver.BuildBossLookup(db.bossList);
            waveByIndex = WaveResolver.BuildWaveLookup(db.waveList);

            sim = new Simulator(map, seed);
            inventory = new Inventory();
            gacha = new GachaEngine(db.unitList, db.recipeList, seed);
            combination = new CombinationEngine(db.recipeList);
        }

        // 웨이브 시작: 뽑기 -> 인벤토리 -> 배치 -> 스폰 준비. 이후 StepTick 을 반복 호출한다.
        public WaveOutcome BeginWave(int waveIndex)
        {
            WaveOutcome outcome = new WaveOutcome();
            outcome.waveIndex = waveIndex;

            string granted = gacha.GrantForWave(waveIndex);
            inventory.Add(granted);
            outcome.grantedUnitId = granted;

            outcome.placedThisWave = AutoPlaceFromInventory();

            WaveData wave;
            if (waveByIndex.TryGetValue(waveIndex, out wave))
            {
                EnemyData enemy = WaveResolver.ResolveEnemy(wave, enemyById, bossById);
                int spawnCount = enemy != null ? wave.spawnCount : 0;
                outcome.isBoss = wave.isBoss;
                outcome.enemyLabel = wave.isBoss ? wave.bossId : wave.enemySetId;
                outcome.spawnCount = spawnCount;
                sim.StartWave(enemy, spawnCount, wave.spawnInterval);
            }

            activeOutcome = outcome;
            activePrevKilled = sim.state.killedCount;
            activePrevLeaked = sim.state.leakedCount;
            return outcome;
        }

        public void StepTick()
        {
            sim.Tick();
        }

        public bool IsWaveComplete()
        {
            return sim.IsWaveComplete();
        }

        // 진행 중이던 웨이브를 마감하고 결과를 채운다.
        public WaveOutcome EndWave()
        {
            if (activeOutcome == null) return null;
            activeOutcome.killedThisWave = sim.state.killedCount - activePrevKilled;
            activeOutcome.leakedThisWave = sim.state.leakedCount - activePrevLeaked;
            activeOutcome.stateHash = sim.ComputeStateHash();
            WaveOutcome done = activeOutcome;
            activeOutcome = null;
            return done;
        }

        // 배치 방식(한 웨이브를 끝까지 즉시 실행). 테스트/Sim 이 쓴다.
        public WaveOutcome RunWave(int waveIndex, int maxTicks = 4000)
        {
            BeginWave(waveIndex);
            int guard = 0;
            while (!sim.IsWaveComplete() && guard < maxTicks)
            {
                sim.Tick();
                ++guard;
            }
            return EndWave();
        }

        public List<WaveOutcome> RunWaves(int waveCount)
        {
            List<WaveOutcome> resultList = new List<WaveOutcome>();
            for (int i = 1; i <= waveCount; ++i)
            {
                resultList.Add(RunWave(i));
            }
            return resultList;
        }

        // 임시 배치 정책: 인벤토리를 순회하며 감당 가능한 유닛을 배치 종류에 맞는 첫 빈 칸에 놓는다.
        private int AutoPlaceFromInventory()
        {
            int placed = 0;
            List<OwnedUnit> snapshot = new List<OwnedUnit>(inventory.ownedList);
            foreach (var owned in snapshot)
            {
                UnitData data;
                if (!unitById.TryGetValue(owned.unitId, out data)) continue;
                if (IsCostBelow(data.cost)) continue;
                if (TryPlaceOnFreeCell(data))
                {
                    inventory.RemoveByInstance(owned.instanceId);
                    ++placed;
                }
            }
            return placed;
        }

        private bool IsCostBelow(int cost)
        {
            return sim.state.cost < Fixed.FromInt(cost);
        }

        private bool TryPlaceOnFreeCell(UnitData data)
        {
            for (int y = 0; y < map.height; ++y)
            {
                for (int x = 0; x < map.width; ++x)
                {
                    CellType cell = map.GetCell(x, y);
                    bool match = (data.placement == Placement.Melee && cell == CellType.Melee)
                              || (data.placement == Placement.Ranged && cell == CellType.Ranged);
                    if (!match) continue;
                    if (sim.PlaceUnit(data, x, y)) return true;
                }
            }
            return false;
        }
    }
}
