using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Random;

namespace Synthesis.Core.Simulation
{
    // STEP 2. 뼈대 - 고정 20틱 시뮬레이션. 1틱 = 50ms (ARCHITECTURE.md 4-2).
    // 슬라이스 2: 유닛 배치/회수, 저지 판정, 자동 공격/사거리, 유닛/적 사망, 재배치 쿨타임.
    //
    // 문서 미정의라 STEP 2 에서 임시로 정한 규칙(전부 TEMP, 이후 확정):
    //   - 타게팅: 사거리 내에서 경로 진행이 가장 앞선(출구에 가까운) 적을 노린다.
    //   - 저지: 근접칸 유닛이 인접한 경로칸의 적을 blockCount 만큼 잡는다. 잡힌 적은 정지하고 유닛을 때린다.
    //   - 원거리칸 유닛은 저지하지 않고 공격만 한다.

    public sealed class EnemyInstance
    {
        public string enemyId;
        public Fixed distanceTraveled;  // 경로상 이동 거리(셀 단위)
        public Fixed hp;
        public bool alive;
        public bool leaked;             // 출구 통과 여부
        public int blockerIndex;        // 자신을 저지 중인 유닛 인덱스(-1 없음)
    }

    public sealed class UnitInstance
    {
        public UnitData data;
        public int cellX;
        public int cellY;
        public Fixed hp;
        public int attackCdTicks;       // 다음 공격까지 남은 틱(0 = 준비)
        public bool alive;
        public int redeployCdTicks;     // 사망 후 재배치까지 남은 틱
    }

    public sealed class GameState
    {
        public MapData map;
        public DeterministicRandom rng;

        public int tick;
        public Fixed cost;              // 현재 코스트(누적)
        public int costCap = 40;

        public List<EnemyInstance> enemyList = new List<EnemyInstance>();
        public List<UnitInstance> unitList = new List<UnitInstance>();
        public int killedCount;
        public int leakedCount;

        // 현재 웨이브 스폰 스케줄
        public EnemyData spawnEnemy;
        public int pendingSpawns;
        public int spawnIntervalTicks;
        public int nextSpawnTick;

        public int pathLength;
    }

    public sealed class Simulator
    {
        public const int TicksPerSecond = 20;

        private static readonly Fixed costPerTick = Fixed.One / Fixed.FromInt(TicksPerSecond); // 초당 1 회복

        public GameState state { get; private set; }

        public Simulator(MapData map, long seed)
        {
            state = new GameState();
            state.map = map;
            state.rng = new DeterministicRandom(seed);
            state.pathLength = map.GetPathLength();
        }

        // ---- 웨이브 ----

        public void StartWave(EnemyData enemy, int spawnCount, int spawnInterval)
        {
            state.spawnEnemy = enemy;
            state.pendingSpawns = spawnCount;
            state.spawnIntervalTicks = spawnInterval > 0 ? spawnInterval : 1;
            state.nextSpawnTick = state.tick;
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

        // ---- 배치와 회수 (SIM_SPEC 의 ActionBuffer 추상화는 정책이 붙는 STEP 3 이후) ----

        // 배치 성공 시 true. 칸 종류/점유/코스트를 검사한다 (BALANCE_SPEC 5).
        public bool PlaceUnit(UnitData unitData, int x, int y)
        {
            if (unitData == null) return false;

            CellType cell = state.map.GetCell(x, y);
            bool cellOk = (unitData.placement == Placement.Melee && cell == CellType.Melee)
                       || (unitData.placement == Placement.Ranged && cell == CellType.Ranged);
            if (!cellOk) return false;

            if (GetLiveUnitAt(x, y) != null) return false;

            Fixed price = Fixed.FromInt(unitData.cost);
            if (state.cost < price) return false;

            state.cost = state.cost - price;

            UnitInstance unit = new UnitInstance();
            unit.data = unitData;
            unit.cellX = x;
            unit.cellY = y;
            unit.hp = unitData.hp;
            unit.attackCdTicks = 0;
            unit.alive = true;
            unit.redeployCdTicks = 0;
            state.unitList.Add(unit);
            return true;
        }

        // 회수 시 배치 코스트의 50% 환불 (BALANCE_SPEC 5-1).
        public bool RecallUnit(int x, int y)
        {
            UnitInstance unit = GetLiveUnitAt(x, y);
            if (unit == null) return false;

            state.cost = state.cost + Fixed.FromRatio(unit.data.cost, 2);
            unit.alive = false;
            unit.redeployCdTicks = unit.data.redeployCd;
            return true;
        }

        // ---- 틱 ----

        public void Tick()
        {
            ++state.tick;
            RecoverCost();
            ProcessSpawns();
            ResolveBlocking();
            MoveEnemies();
            UnitsAttack();
            EnemiesAttackBlockers();
            AdvanceCooldowns();
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
                enemy.blockerIndex = -1;
                state.enemyList.Add(enemy);

                --state.pendingSpawns;
                state.nextSpawnTick += state.spawnIntervalTicks;
            }
        }

        // 근접칸 유닛이 인접 경로칸의 적을 blockCount 만큼 저지한다. 진행이 앞선 적을 우선.
        private void ResolveBlocking()
        {
            for (int i = 0; i < state.enemyList.Count; ++i)
            {
                state.enemyList[i].blockerIndex = -1;
            }

            for (int u = 0; u < state.unitList.Count; ++u)
            {
                UnitInstance unit = state.unitList[u];
                if (!unit.alive) continue;
                if (unit.data.placement != Placement.Melee) continue;
                if (unit.data.blockCount <= 0) continue;

                int slots = unit.data.blockCount;
                // 진행이 앞선 적부터 잡도록 후보를 정렬 대신 반복 선택(결정적, 소규모라 비용 무시).
                for (int s = 0; s < slots; ++s)
                {
                    int pick = -1;
                    Fixed best = Fixed.Zero;
                    for (int e = 0; e < state.enemyList.Count; ++e)
                    {
                        EnemyInstance enemy = state.enemyList[e];
                        if (!enemy.alive || enemy.blockerIndex >= 0) continue;
                        GridPos cell = GetEnemyCell(enemy);
                        if (!IsAdjacent(cell.x, cell.y, unit.cellX, unit.cellY)) continue;
                        if (pick < 0 || enemy.distanceTraveled > best)
                        {
                            pick = e;
                            best = enemy.distanceTraveled;
                        }
                    }
                    if (pick < 0) break;
                    state.enemyList[pick].blockerIndex = u;
                }
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
                if (enemy.blockerIndex >= 0) continue; // 저지 중이면 정지

                enemy.distanceTraveled = enemy.distanceTraveled + perTick;
                if (enemy.distanceTraveled >= pathLen)
                {
                    enemy.alive = false;
                    enemy.leaked = true;
                    ++state.leakedCount;
                }
            }
        }

        // 준비된 유닛이 사거리 내 가장 앞선 적을 공격한다.
        private void UnitsAttack()
        {
            for (int u = 0; u < state.unitList.Count; ++u)
            {
                UnitInstance unit = state.unitList[u];
                if (!unit.alive) continue;
                if (unit.attackCdTicks > 0) continue;

                int target = FindTarget(unit);
                if (target < 0) continue;

                EnemyInstance enemy = state.enemyList[target];
                enemy.hp = enemy.hp - unit.data.atk;
                if (enemy.hp.raw <= 0)
                {
                    enemy.alive = false;
                    ++state.killedCount;
                }

                unit.attackCdTicks = GetAttackCooldownTicks(unit.data);
            }
        }

        // 저지당한 적이 저지 유닛을 때린다(초당 atk 를 틱 분할).
        private void EnemiesAttackBlockers()
        {
            for (int e = 0; e < state.enemyList.Count; ++e)
            {
                EnemyInstance enemy = state.enemyList[e];
                if (!enemy.alive || enemy.blockerIndex < 0) continue;

                UnitInstance blocker = state.unitList[enemy.blockerIndex];
                if (!blocker.alive) continue;

                EnemyData def = state.spawnEnemy;
                Fixed dmgPerTick = def != null ? def.atk / Fixed.FromInt(TicksPerSecond) : Fixed.Zero;
                blocker.hp = blocker.hp - dmgPerTick;
                if (blocker.hp.raw <= 0)
                {
                    blocker.alive = false;
                    blocker.redeployCdTicks = blocker.data.redeployCd;
                }
            }
        }

        private void AdvanceCooldowns()
        {
            for (int u = 0; u < state.unitList.Count; ++u)
            {
                UnitInstance unit = state.unitList[u];
                if (unit.attackCdTicks > 0) --unit.attackCdTicks;
                if (!unit.alive && unit.redeployCdTicks > 0) --unit.redeployCdTicks;
            }
        }

        // ---- 조회 헬퍼 ----

        private UnitInstance GetLiveUnitAt(int x, int y)
        {
            for (int u = 0; u < state.unitList.Count; ++u)
            {
                UnitInstance unit = state.unitList[u];
                if (unit.alive && unit.cellX == x && unit.cellY == y) return unit;
            }
            return null;
        }

        private int FindTarget(UnitInstance unit)
        {
            Fixed range = unit.data.range;
            Fixed rangeSq = range * range;
            Fixed ux = Fixed.FromInt(unit.cellX);
            Fixed uy = Fixed.FromInt(unit.cellY);

            int pick = -1;
            Fixed best = Fixed.Zero;
            for (int e = 0; e < state.enemyList.Count; ++e)
            {
                EnemyInstance enemy = state.enemyList[e];
                if (!enemy.alive) continue;

                Fixed ex, ey;
                GetEnemyPos(enemy, out ex, out ey);
                Fixed dx = ex - ux;
                Fixed dy = ey - uy;
                Fixed distSq = dx * dx + dy * dy;
                if (distSq > rangeSq) continue;

                if (pick < 0 || enemy.distanceTraveled > best)
                {
                    pick = e;
                    best = enemy.distanceTraveled;
                }
            }
            return pick;
        }

        private static int GetAttackCooldownTicks(UnitData data)
        {
            if (data.atkSpeed.raw <= 0) return TicksPerSecond;
            long ticks = (Fixed.FromInt(TicksPerSecond) / data.atkSpeed).ToIntRounded();
            if (ticks < 1) ticks = 1;
            return (int)ticks;
        }

        // 렌더링용: 경로상 적의 실수 좌표를 공개한다(Presentation 이 위치 보간에 쓴다).
        public void GetEnemyPosition(EnemyInstance enemy, out Fixed x, out Fixed y)
        {
            GetEnemyPos(enemy, out x, out y);
        }

        private GridPos GetEnemyCell(EnemyInstance enemy)
        {
            Fixed x, y;
            GetEnemyPos(enemy, out x, out y);
            return new GridPos((int)x.ToIntRounded(), (int)y.ToIntRounded());
        }

        // 경로상 distanceTraveled 위치를 (x,y) 실수 좌표로 보간한다.
        private void GetEnemyPos(EnemyInstance enemy, out Fixed x, out Fixed y)
        {
            List<GridPos> path = state.map.pathList;
            if (path.Count == 0)
            {
                x = Fixed.Zero;
                y = Fixed.Zero;
                return;
            }

            Fixed acc = Fixed.Zero;
            for (int i = 1; i < path.Count; ++i)
            {
                int dxi = path[i].x - path[i - 1].x;
                int dyi = path[i].y - path[i - 1].y;
                int segLen = (dxi < 0 ? -dxi : dxi) + (dyi < 0 ? -dyi : dyi);
                if (segLen <= 0) continue;

                Fixed segLenF = Fixed.FromInt(segLen);
                Fixed segEnd = acc + segLenF;
                if (enemy.distanceTraveled <= segEnd)
                {
                    Fixed t = (enemy.distanceTraveled - acc) / segLenF;
                    x = Fixed.FromInt(path[i - 1].x) + (Fixed.FromInt(path[i].x) - Fixed.FromInt(path[i - 1].x)) * t;
                    y = Fixed.FromInt(path[i - 1].y) + (Fixed.FromInt(path[i].y) - Fixed.FromInt(path[i - 1].y)) * t;
                    return;
                }
                acc = segEnd;
            }

            GridPos last = path[path.Count - 1];
            x = Fixed.FromInt(last.x);
            y = Fixed.FromInt(last.y);
        }

        private static bool IsAdjacent(int ax, int ay, int bx, int by)
        {
            int dx = ax - bx;
            int dy = ay - by;
            if (dx < 0) dx = -dx;
            if (dy < 0) dy = -dy;
            return dx + dy <= 1;
        }

        // ---- 결정성 해시 (SIM_SPEC.md 10) ----
        // 비트 연산 사용 사유: FNV 해시는 XOR 와 곱이 알고리즘의 본질이라 불가피하다 (CLAUDE.md 3-3).
        public ulong ComputeStateHash()
        {
            ulong hash = 1469598103934665603UL;
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

            for (int i = 0; i < state.unitList.Count; ++i)
            {
                UnitInstance unit = state.unitList[i];
                hash = FnvMix(hash, StringHash(unit.data.id));
                hash = FnvMix(hash, (ulong)((unit.cellY << 16) + unit.cellX));
                hash = FnvMix(hash, (ulong)unit.hp.raw);
                hash = FnvMix(hash, unit.alive ? 1UL : 0UL);
            }
            return hash;
        }

        private static ulong FnvMix(ulong hash, ulong value)
        {
            hash = hash ^ value;
            hash = hash * 1099511628211UL;
            return hash;
        }

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
