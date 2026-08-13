using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Random;

namespace Synthesis.Core.Simulation
{
    // STEP 2(재작업). 뼈대 - 루프형 시뮬레이션. 몬스터가 폐곡선을 계속 순회하고,
    // 필드에 60기가 쌓이면 패배한다 (SPEC v0.3, BALANCE 9-2). 저지/누출/근접-원거리 구분은 없다.
    // 배치는 BUILD 타일(루프 안팎)에 하며 사거리로 루프를 커버한다.
    // 이 슬라이스는 순회/누적/배치까지. 전투는 다음 슬라이스에서 얹는다.

    public sealed class LoopMonster
    {
        public string enemyId;
        public Fixed hp;
        public Fixed moveSpeed;     // 초당 셀 수
        public int waypointIndex;   // 현재 웨이포인트
        public Fixed progress;      // 구간 진행도 [0,1)
        public bool alive;
    }

    public sealed class LoopUnit
    {
        public UnitData data;
        public int cellX;
        public int cellY;
        public int attackCdTicks;

        // 표현용 공격 이벤트. 마지막으로 공격한 틱과 그때 대상 위치(뷰가 공격 빔을 그리는 데 쓴다).
        public int lastAttackTick = -100000;
        public Fixed lastTargetX;
        public Fixed lastTargetY;
    }

    // 석상 (SPEC 3-9). 반격하지 않는다. 파괴하면 도플갱어를 드랍한다.
    public sealed class LoopStatue
    {
        public int cellX;
        public int cellY;
        public Fixed hp;
        public bool alive;
    }

    public sealed class LoopState
    {
        public LoopMap map;
        public DeterministicRandom rng;

        public int tick;
        public Fixed cost;
        public int costCap = 40;

        public List<LoopMonster> monsterList = new List<LoopMonster>();
        public List<LoopUnit> unitList = new List<LoopUnit>();
        public List<LoopStatue> statueList = new List<LoopStatue>();
        public int killedCount;
        public int aliveCount;
        public int doppelDropped;   // 석상 파괴로 누적 드랍한 도플갱어 수
        public bool defeated;

        // 스폰 스케줄
        public EnemyData spawnEnemy;
        public int pendingSpawns;
        public int spawnIntervalTicks;
        public int nextSpawnTick;
        public int spawnCursor;     // 스폰 지점 순환용
    }

    public sealed class LoopSimulator
    {
        public const int TicksPerSecond = 20;
        // BALANCE v0.4: 필드 누적 상한(패배 조건) 폐기. 패배는 보스 제한시간으로 이관(STEP 4 보스).

        private static readonly Fixed costPerTick = Fixed.One / Fixed.FromInt(TicksPerSecond);

        public LoopState state { get; private set; }

        public LoopSimulator(LoopMap map, long seed)
        {
            state = new LoopState();
            state.map = map;
            state.rng = new DeterministicRandom(seed);

            // 맵의 석상 위치로 석상을 초기화한다.
            for (int i = 0; i < map.statueList.Count; ++i)
            {
                LoopStatue statue = new LoopStatue();
                statue.cellX = map.statueList[i].x;
                statue.cellY = map.statueList[i].y;
                statue.hp = map.statueHp;
                statue.alive = true;
                state.statueList.Add(statue);
            }
        }

        // (x,y) 에 살아있는 석상이 있으면 반환.
        private LoopStatue GetStatueAt(int x, int y)
        {
            for (int i = 0; i < state.statueList.Count; ++i)
            {
                LoopStatue statue = state.statueList[i];
                if (statue.alive && statue.cellX == x && statue.cellY == y) return statue;
            }
            return null;
        }

        public void StartWave(EnemyData enemy, int spawnCount, int spawnInterval)
        {
            state.spawnEnemy = enemy;
            state.pendingSpawns = spawnCount;
            state.spawnIntervalTicks = spawnInterval > 0 ? spawnInterval : 1;
            state.nextSpawnTick = state.tick;
        }

        public bool IsSpawningDone()
        {
            return state.pendingSpawns <= 0;
        }

        public void Tick()
        {
            if (state.defeated) return;

            ++state.tick;
            RecoverCost();
            ProcessSpawns();
            MoveMonsters();
            UnitsAttack();
            AdvanceCooldowns();
        }

        // 필드 클리어(다음 웨이브 조건). 스폰이 끝났고 살아있는 몬스터가 없으면 true (BALANCE v0.4 12).
        public bool IsFieldClear()
        {
            return state.pendingSpawns <= 0 && state.aliveCount <= 0;
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

            var spawns = state.map.spawnIndexList;
            while (state.pendingSpawns > 0 && state.tick >= state.nextSpawnTick)
            {
                int spawnIdx = spawns.Count > 0 ? spawns[state.spawnCursor % spawns.Count] : 0;
                state.spawnCursor++;

                LoopMonster m = new LoopMonster();
                m.enemyId = state.spawnEnemy.id;
                m.hp = state.spawnEnemy.hp;
                m.moveSpeed = state.spawnEnemy.moveSpeed;
                m.waypointIndex = spawnIdx;
                m.progress = Fixed.Zero;
                m.alive = true;
                state.monsterList.Add(m);
                ++state.aliveCount;

                --state.pendingSpawns;
                state.nextSpawnTick += state.spawnIntervalTicks;
            }
        }

        // 몬스터는 루프를 계속 돈다. 마지막 웨이포인트 다음은 0 (순환). 출구/누출 없음.
        // 이동은 거리(셀) 기준이다. moveSpeed 는 초당 셀 수이며, 구간 길이에 맞춰 진행도를 스케일한다.
        // 그래서 코너만 찍은 긴 구간이든 셀마다 찍은 짧은 구간이든 실제 이동 속도는 같다.
        private void MoveMonsters()
        {
            int n = state.map.loopWaypointList.Count;
            if (n < 2) return;

            Fixed tps = Fixed.FromInt(TicksPerSecond);
            for (int i = 0; i < state.monsterList.Count; ++i)
            {
                LoopMonster m = state.monsterList[i];
                if (!m.alive) continue;

                Fixed step = m.moveSpeed / tps; // 이번 틱 이동 거리(셀)
                AdvanceAlongPath(m, step, n);
            }
        }

        // 이동 거리 step(셀)만큼 경로를 따라 전진한다. 짧은 구간이면 한 틱에 여러 구간을 넘을 수 있다.
        private void AdvanceAlongPath(LoopMonster m, Fixed step, int n)
        {
            int guard = 0;
            while (step.raw > 0 && guard <= n)
            {
                ++guard;

                Fixed segLen = SegmentLength(m.waypointIndex, n);
                if (segLen.raw <= 0) // 길이 0 구간(중복 웨이포인트)은 건너뛴다
                {
                    m.waypointIndex = (m.waypointIndex + 1) % n;
                    m.progress = Fixed.Zero;
                    continue;
                }

                Fixed remainingCells = (Fixed.One - m.progress) * segLen; // 이 구간에 남은 거리(셀)
                if (step < remainingCells)
                {
                    m.progress = m.progress + step / segLen;
                    step = Fixed.Zero;
                }
                else
                {
                    step = step - remainingCells;
                    m.waypointIndex = (m.waypointIndex + 1) % n;
                    m.progress = Fixed.Zero;
                }
            }
        }

        // 웨이포인트 i 에서 i+1 까지의 거리(셀). 축 정렬이면 정확, 대각이면 유클리드.
        private Fixed SegmentLength(int i, int n)
        {
            GridPos a = state.map.loopWaypointList[i];
            GridPos b = state.map.loopWaypointList[(i + 1) % n];
            long dx = b.x - a.x;
            long dy = b.y - a.y;
            if (dx == 0) return Fixed.FromInt(dy >= 0 ? dy : -dy);
            if (dy == 0) return Fixed.FromInt(dx >= 0 ? dx : -dx);
            return Fixed.Sqrt(Fixed.FromInt(dx * dx + dy * dy));
        }

        // 준비된 유닛이 사거리 내 가장 가까운 몬스터를 타격한다. 처치하면 필드에서 제거(누적 감소).
        private void UnitsAttack()
        {
            for (int u = 0; u < state.unitList.Count; ++u)
            {
                LoopUnit unit = state.unitList[u];
                if (unit.attackCdTicks > 0) continue;

                int target = FindTarget(unit);
                if (target >= 0)
                {
                    LoopMonster m = state.monsterList[target];
                    Fixed tx, ty;
                    GetMonsterPosition(m, out tx, out ty);
                    RecordAttack(unit, tx, ty);

                    m.hp = m.hp - unit.data.atk;
                    if (m.hp.raw <= 0)
                    {
                        m.alive = false;
                        --state.aliveCount;
                        ++state.killedCount;
                    }
                    unit.attackCdTicks = GetAttackCooldownTicks(unit.data);
                    continue;
                }

                // 사거리에 몬스터가 없으면 석상을 때린다 (SPEC 3-9: 커버를 석상에 쓰는 비용).
                LoopStatue statue = FindStatueTarget(unit);
                if (statue == null) continue;

                RecordAttack(unit, Fixed.FromInt(statue.cellX), Fixed.FromInt(statue.cellY));
                statue.hp = statue.hp - unit.data.atk;
                if (statue.hp.raw <= 0)
                {
                    statue.alive = false;
                    ++state.doppelDropped;
                }
                unit.attackCdTicks = GetAttackCooldownTicks(unit.data);
            }
        }

        private int FindTarget(LoopUnit unit)
        {
            Fixed range = unit.data.range;
            Fixed rangeSq = range * range;
            Fixed ux = Fixed.FromInt(unit.cellX);
            Fixed uy = Fixed.FromInt(unit.cellY);

            int pick = -1;
            Fixed best = Fixed.Zero;
            for (int i = 0; i < state.monsterList.Count; ++i)
            {
                LoopMonster m = state.monsterList[i];
                if (!m.alive) continue;

                Fixed mx, my;
                GetMonsterPosition(m, out mx, out my);
                Fixed dx = mx - ux;
                Fixed dy = my - uy;
                Fixed distSq = dx * dx + dy * dy;
                if (distSq > rangeSq) continue;

                if (pick < 0 || distSq < best)
                {
                    pick = i;
                    best = distSq;
                }
            }
            return pick;
        }

        // 표현용: 유닛이 공격한 틱과 대상 위치를 기록한다(전투 판정에는 영향 없음).
        private void RecordAttack(LoopUnit unit, Fixed tx, Fixed ty)
        {
            unit.lastAttackTick = state.tick;
            unit.lastTargetX = tx;
            unit.lastTargetY = ty;
        }

        private LoopStatue FindStatueTarget(LoopUnit unit)
        {
            Fixed range = unit.data.range;
            Fixed rangeSq = range * range;
            Fixed ux = Fixed.FromInt(unit.cellX);
            Fixed uy = Fixed.FromInt(unit.cellY);

            LoopStatue pick = null;
            Fixed best = Fixed.Zero;
            for (int i = 0; i < state.statueList.Count; ++i)
            {
                LoopStatue statue = state.statueList[i];
                if (!statue.alive) continue;

                Fixed dx = Fixed.FromInt(statue.cellX) - ux;
                Fixed dy = Fixed.FromInt(statue.cellY) - uy;
                Fixed distSq = dx * dx + dy * dy;
                if (distSq > rangeSq) continue;

                if (pick == null || distSq < best)
                {
                    pick = statue;
                    best = distSq;
                }
            }
            return pick;
        }

        private void AdvanceCooldowns()
        {
            for (int u = 0; u < state.unitList.Count; ++u)
            {
                if (state.unitList[u].attackCdTicks > 0) --state.unitList[u].attackCdTicks;
            }
        }

        private static int GetAttackCooldownTicks(UnitData data)
        {
            if (data.atkSpeed.raw <= 0) return TicksPerSecond;
            long ticks = (Fixed.FromInt(TicksPerSecond) / data.atkSpeed).ToIntRounded();
            if (ticks < 1) ticks = 1;
            return (int)ticks;
        }


        // ---- 배치 (BUILD 타일이면 어디든. 저지/근접-원거리 구분 없음) ----

        public bool PlaceUnit(UnitData unitData, int x, int y)
        {
            if (unitData == null) return false;
            if (state.map.GetTile(x, y) != LoopTile.Build) return false;
            if (GetUnitAt(x, y) != null) return false;
            if (GetStatueAt(x, y) != null) return false; // 석상 칸에는 배치 불가

            Fixed price = Fixed.FromInt(unitData.cost);
            if (state.cost < price) return false;

            state.cost = state.cost - price;

            LoopUnit unit = new LoopUnit();
            unit.data = unitData;
            unit.cellX = x;
            unit.cellY = y;
            unit.attackCdTicks = 0;
            state.unitList.Add(unit);
            return true;
        }

        public bool RecallUnit(int x, int y)
        {
            for (int i = 0; i < state.unitList.Count; ++i)
            {
                LoopUnit unit = state.unitList[i];
                if (unit.cellX == x && unit.cellY == y)
                {
                    state.cost = state.cost + Fixed.FromRatio(unit.data.cost, 2);
                    state.unitList.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        private LoopUnit GetUnitAt(int x, int y)
        {
            for (int i = 0; i < state.unitList.Count; ++i)
            {
                if (state.unitList[i].cellX == x && state.unitList[i].cellY == y) return state.unitList[i];
            }
            return null;
        }

        // 렌더링/타게팅용 몬스터 실수 좌표(구간 보간).
        public void GetMonsterPosition(LoopMonster m, out Fixed x, out Fixed y)
        {
            var wp = state.map.loopWaypointList;
            int n = wp.Count;
            GridPos a = wp[m.waypointIndex];
            GridPos b = wp[(m.waypointIndex + 1) % n];
            x = Fixed.FromInt(a.x) + (Fixed.FromInt(b.x) - Fixed.FromInt(a.x)) * m.progress;
            y = Fixed.FromInt(a.y) + (Fixed.FromInt(b.y) - Fixed.FromInt(a.y)) * m.progress;
        }

        // ---- 결정성 해시 ----
        public ulong ComputeStateHash()
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, (ulong)state.tick);
            hash = Mix(hash, (ulong)state.cost.raw);
            hash = Mix(hash, (ulong)state.killedCount);
            hash = Mix(hash, (ulong)state.aliveCount);
            hash = Mix(hash, (ulong)state.doppelDropped);
            hash = Mix(hash, state.defeated ? 1UL : 0UL);

            for (int i = 0; i < state.monsterList.Count; ++i)
            {
                LoopMonster m = state.monsterList[i];
                hash = Mix(hash, (ulong)m.waypointIndex);
                hash = Mix(hash, (ulong)m.progress.raw);
                hash = Mix(hash, (ulong)m.hp.raw);
                hash = Mix(hash, m.alive ? 1UL : 0UL);
            }
            for (int i = 0; i < state.unitList.Count; ++i)
            {
                LoopUnit u = state.unitList[i];
                hash = Mix(hash, (ulong)((u.cellY << 16) + u.cellX));
            }
            for (int i = 0; i < state.statueList.Count; ++i)
            {
                LoopStatue s = state.statueList[i];
                hash = Mix(hash, (ulong)s.hp.raw);
                hash = Mix(hash, s.alive ? 1UL : 0UL);
            }
            return hash;
        }

        private static ulong Mix(ulong hash, ulong value)
        {
            hash = hash ^ value;
            hash = hash * 1099511628211UL;
            return hash;
        }
    }
}
