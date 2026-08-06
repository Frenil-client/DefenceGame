using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Random;

namespace Synthesis.Core.Simulation
{
    // STEP 2. 뼈대 - 고정 20틱 시뮬레이션. 1틱 = 50ms (ARCHITECTURE.md 4-2).
    // 이 슬라이스는 척추만 세운다: 웨이브 스폰과 경로 이동, 코스트 회복, 결정성 해시.
    // 배치/저지/자동공격/사망은 다음 슬라이스에서 얹는다.

    public sealed class EnemyInstance
    {
        public string enemyId;
        public Fixed distanceTraveled;  // 경로상 이동 거리(셀 단위)
        public Fixed hp;
        public bool alive;
        public bool leaked;             // 출구 통과 여부
    }

    public sealed class GameState
    {
        public MapData map;
        public DeterministicRandom rng;

        public int tick;
        public Fixed cost;              // 현재 코스트(누적). 배치에서 소모한다.
        public int costCap = 40;

        public List<EnemyInstance> enemyList = new List<EnemyInstance>();
        public int killedCount;
        public int leakedCount;

        // 현재 웨이브 스폰 스케줄
        public EnemyData spawnEnemy;
        public int pendingSpawns;
        public int spawnIntervalTicks;
        public int nextSpawnTick;

        public int pathLength;
    }

    public sealed class Simulation
    {
        public const int TicksPerSecond = 20;

        private static readonly Fixed costPerTick = Fixed.One / Fixed.FromInt(TicksPerSecond); // 초당 1 회복

        public GameState state { get; private set; }

        public Simulation(MapData map, long seed)
        {
            state = new GameState();
            state.map = map;
            state.rng = new DeterministicRandom(seed);
            state.pathLength = map.GetPathLength();
        }

        // 웨이브 스폰 스케줄을 건다. enemy 는 호출자가 enemySetId/bossId 를 해석해 넘긴다.
        public void StartWave(EnemyData enemy, int spawnCount, int spawnInterval)
        {
            state.spawnEnemy = enemy;
            state.pendingSpawns = spawnCount;
            state.spawnIntervalTicks = spawnInterval > 0 ? spawnInterval : 1;
            state.nextSpawnTick = state.tick; // 첫 적은 즉시
        }

        public bool IsWaveComplete()
        {
            if (state.pendingSpawns > 0) return false;
            for (int i = 0; i < state.enemyList.Count; ++i)
            {
                if (state.enemyList[i].alive) return false;
            }
            return true;
        }

        public void Tick()
        {
            ++state.tick;
            RecoverCost();
            ProcessSpawns();
            MoveEnemies();
        }

        private void RecoverCost()
        {
            state.cost = state.cost + costPerTick;
            Fixed cap = Fixed.FromInt(state.costCap);
            if (state.cost > cap) state.cost = cap;
        }

        private void ProcessSpawns()
        {
            if (state.spawnEnemy == null) return;

            while (state.pendingSpawns > 0 && state.tick >= state.nextSpawnTick)
            {
                EnemyInstance enemy = new EnemyInstance();
                enemy.enemyId = state.spawnEnemy.id;
                enemy.distanceTraveled = Fixed.Zero;
                enemy.hp = state.spawnEnemy.hp;
                enemy.alive = true;
                enemy.leaked = false;
                state.enemyList.Add(enemy);

                --state.pendingSpawns;
                state.nextSpawnTick += state.spawnIntervalTicks;
            }
        }

        private void MoveEnemies()
        {
            Fixed perTick = state.spawnEnemy != null
                ? state.spawnEnemy.moveSpeed / Fixed.FromInt(TicksPerSecond)
                : Fixed.Zero;
            Fixed pathLen = Fixed.FromInt(state.pathLength);

            for (int i = 0; i < state.enemyList.Count; ++i)
            {
                EnemyInstance enemy = state.enemyList[i];
                if (!enemy.alive) continue;

                enemy.distanceTraveled = enemy.distanceTraveled + perTick;
                if (enemy.distanceTraveled >= pathLen)
                {
                    enemy.alive = false;
                    enemy.leaked = true;
                    ++state.leakedCount;
                }
            }
        }

        // 결정성 검증용 상태 해시. 순서 고정 직렬화 후 FNV-1a (SIM_SPEC.md 10).
        // 비트 연산 사용 사유: FNV 해시는 XOR 와 곱이 알고리즘의 본질이라 불가피하다 (CLAUDE.md 3-3).
        public ulong ComputeStateHash()
        {
            ulong hash = 1469598103934665603UL; // FNV offset basis
            hash = FnvMix(hash, (ulong)state.tick);
            hash = FnvMix(hash, (ulong)state.cost.raw);
            hash = FnvMix(hash, (ulong)state.killedCount);
            hash = FnvMix(hash, (ulong)state.leakedCount);

            for (int i = 0; i < state.enemyList.Count; ++i)
            {
                EnemyInstance enemy = state.enemyList[i];
                hash = FnvMix(hash, StringHash(enemy.enemyId));
                hash = FnvMix(hash, (ulong)enemy.distanceTraveled.raw);
                hash = FnvMix(hash, (ulong)enemy.hp.raw);
                hash = FnvMix(hash, enemy.alive ? 1UL : 0UL);
            }
            return hash;
        }

        private static ulong FnvMix(ulong hash, ulong value)
        {
            hash = hash ^ value;
            hash = hash * 1099511628211UL; // FNV prime
            return hash;
        }

        // 문자열을 결정적 정수로. System 문자열 해시는 런타임마다 달라 쓰지 않는다.
        private static ulong StringHash(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0UL;
            ulong hash = 1469598103934665603UL;
            for (int i = 0; i < text.Length; ++i)
            {
                hash = FnvMix(hash, text[i]);
            }
            return hash;
        }
    }
}
