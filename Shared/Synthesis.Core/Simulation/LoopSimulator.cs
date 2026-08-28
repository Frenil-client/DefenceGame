using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Random;

namespace Synthesis.Core.Simulation
{
    // 루프형 시뮬(축소판). 몬스터 스폰과 폐곡선 순회, 유닛 배치(칸 점유)만 다룬다.
    // 유닛 이동/전투/투사체는 시뮬에서 제거됐다(Unity 실시간에서 처리). 배치/경제/스폰만 결정적으로 유지.
    public sealed class LoopMonster
    {
        public string enemyId;
        public Fixed hp;
        public Fixed armor;         // 피해에서 차감(보스 고방어). 방깎(ArmorReduction)은 전투에서 처리
        public Fixed moveSpeed;     // 초당 셀 수(감속 적용된 현재 값). 전투가 실시간 수정
        public Fixed baseMoveSpeed; // 감속 복원용 기준 속도(스폰 시 설정)
        public int waypointIndex;   // 현재 웨이포인트
        public Fixed progress;      // 구간 진행도 [0,1)
        public bool alive;
    }

    // 배치된 유닛(칸 점유 기록). cellX/cellY 는 홈(배치) 셀이다.
    public sealed class LoopUnit
    {
        public UnitData data;
        public int cellX;
        public int cellY;

        // 집중 명령 대상(실시간 전투 전용, Unity 가 설정/해제). 둘 중 하나만 설정된다. 둘 다 null 이면 홈 셀로 복귀한다.
        // 결정적 시뮬/상태 해시와 무관한 transient 상태다.
        public LoopMonster focusMonster;
        public LoopStatue focusStatue;
    }

    // 석상(맵 오브젝트). 유닛이 자동 공격으로 파괴하며(선택권 드랍), 살아있는 동안 배치를 막는다.
    public sealed class LoopStatue
    {
        public int cellX;
        public int cellY;
        public Fixed hp;
        public Fixed maxHp;
        public bool alive = true;
    }

    public sealed class LoopState
    {
        public LoopMap map;
        public DeterministicRandom rng;

        public int tick;
        public Fixed cost;
        public int costCap = 40;
        public int statueHp = 400; // [TEMP] 석상 체력. 유닛 자동 공격으로 파괴. 시뮬로 재확정

        public List<LoopMonster> monsterList = new List<LoopMonster>();
        public List<LoopUnit> unitList = new List<LoopUnit>();
        public List<LoopStatue> statueList = new List<LoopStatue>();
        public int aliveCount;
        public bool defeated;

        // 스폰 스케줄
        public EnemyData spawnEnemy;
        public Fixed spawnArmor;    // 이번 웨이브 스폰 몬스터의 방어력(원형별 기본값, 보스는 보스 방어력)
        public int pendingSpawns;
        public int spawnIntervalTicks;
        public int nextSpawnTick;
        public int spawnCursor;     // 스폰 지점 순환용
    }

    public sealed class LoopSimulator
    {
        public const int TicksPerSecond = 20;

        private static readonly Fixed costPerTick = Fixed.One / Fixed.FromInt(TicksPerSecond);

        public LoopState state { get; private set; }

        public LoopSimulator(LoopMap map, long seed)
        {
            state = new LoopState();
            state.map = map;
            state.rng = new DeterministicRandom(seed);

            Fixed statueHp = Fixed.FromInt(state.statueHp);
            for (int i = 0; i < map.statueList.Count; ++i)
            {
                LoopStatue statue = new LoopStatue();
                statue.cellX = map.statueList[i].x;
                statue.cellY = map.statueList[i].y;
                statue.hp = statueHp;
                statue.maxHp = statueHp;
                statue.alive = true;
                state.statueList.Add(statue);
            }
        }

        // 살아있는 석상이 해당 칸에 있으면 true. 파괴된 석상은 칸을 막지 않는다(배치 가능해짐).
        private bool IsStatueAt(int x, int y)
        {
            for (int i = 0; i < state.statueList.Count; ++i)
            {
                LoopStatue s = state.statueList[i];
                if (s.alive && s.cellX == x && s.cellY == y) return true;
            }
            return false;
        }

        public void StartWave(EnemyData enemy, int spawnCount, int spawnInterval, Fixed armor = default)
        {
            state.spawnEnemy = enemy;
            state.spawnArmor = armor;
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
            // 승패 판정(누적 상한/보스 제한시간)은 시뮬이 하지 않는다. 게임 레이어(WaveManager)가 상태를 읽어 판정한다.
        }

        // 스폰이 끝났고 살아있는 몬스터가 없으면 true. (몬스터 처치는 시뮬 밖에서 처리한다)
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
                m.armor = state.spawnArmor;
                m.moveSpeed = state.spawnEnemy.moveSpeed;
                m.baseMoveSpeed = state.spawnEnemy.moveSpeed;
                m.waypointIndex = spawnIdx;
                m.progress = Fixed.Zero;
                m.alive = true;
                state.monsterList.Add(m);
                ++state.aliveCount;

                --state.pendingSpawns;
                state.nextSpawnTick += state.spawnIntervalTicks;
            }
        }

        // 몬스터는 루프를 계속 돈다. 이동은 거리(셀) 기준(구간 길이에 맞춰 스케일).
        private void MoveMonsters()
        {
            int n = state.map.loopWaypointList.Count;
            if (n < 2) return;

            Fixed tps = Fixed.FromInt(TicksPerSecond);
            for (int i = 0; i < state.monsterList.Count; ++i)
            {
                LoopMonster m = state.monsterList[i];
                if (!m.alive) continue;

                Fixed step = m.moveSpeed / tps;
                AdvanceAlongPath(m, step, n);
            }
        }

        private void AdvanceAlongPath(LoopMonster m, Fixed step, int n)
        {
            int guard = 0;
            while (step.raw > 0 && guard <= n)
            {
                ++guard;

                Fixed segLen = SegmentLength(m.waypointIndex, n);
                if (segLen.raw <= 0)
                {
                    m.waypointIndex = (m.waypointIndex + 1) % n;
                    m.progress = Fixed.Zero;
                    continue;
                }

                Fixed remainingCells = (Fixed.One - m.progress) * segLen;
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

        // ---- 배치 (BUILD 타일, 한 칸 1기, 석상 칸 금지) ----

        public bool PlaceUnit(UnitData unitData, int x, int y)
        {
            if (unitData == null) return false;
            if (state.map.GetTile(x, y) != LoopTile.Build) return false;
            if (GetUnitAt(x, y) != null) return false;
            if (IsStatueAt(x, y)) return false;

            Fixed price = Fixed.FromInt(unitData.cost);
            if (state.cost < price) return false;

            state.cost = state.cost - price;

            LoopUnit unit = new LoopUnit();
            unit.data = unitData;
            unit.cellX = x;
            unit.cellY = y;
            state.unitList.Add(unit);
            return true;
        }

        // 목표 칸에서 이 유닛이 놓일 수 있는(BUILD, 석상/경로/타 유닛 아님) 가장 가까운 칸을 찾는다. 자기 칸은 허용.
        // 목표 자체가 유효하면 그대로 반환한다. 유효 칸이 하나도 없으면 false. 드래그 프리뷰와 실제 재배치가 같은 판정을 공유한다.
        public bool FindPlacementCell(LoopUnit unit, int destX, int destY, out int resultx, out int resulty)
        {
            resultx = 0;
            resulty = 0;
            if (unit == null) return false;

            if (CanOccupy(unit, destX, destY))
            {
                resultx = destX;
                resulty = destY;
                return true;
            }

            var tiles = state.map.buildTileList;
            int bestIndex = -1;
            long bestSq = long.MaxValue;
            for (int i = 0; i < tiles.Count; ++i)
            {
                GridPos c = tiles[i];
                if (!CanOccupy(unit, c.x, c.y))
                {
                    continue;
                }
                long dx = c.x - destX;
                long dy = c.y - destY;
                long d2 = dx * dx + dy * dy;
                if (d2 < bestSq) { bestSq = d2; bestIndex = i; }
            }
            if (bestIndex < 0) return false;

            resultx = tiles[bestIndex].x;
            resulty = tiles[bestIndex].y;
            return true;
        }

        // 유닛을 목표 칸으로 재배치한다. 즉시 칸을 재할당하고(가장 가까운 유효 칸으로 보정), 걷는 연출은 Unity 실시간이 맡는다.
        public bool RelocateUnit(LoopUnit unit, int destX, int destY)
        {
            int cellx, celly;
            if (!FindPlacementCell(unit, destX, destY, out cellx, out celly)) return false;
            unit.cellX = cellx;
            unit.cellY = celly;
            return true;
        }

        // 이 유닛을 해당 칸에 놓을 수 있는지 공개 판정(드래그 프리뷰의 최근접 타일 탐색에 쓴다).
        public bool CanPlaceUnitAt(LoopUnit unit, int x, int y)
        {
            if (unit == null) return false;
            return CanOccupy(unit, x, y);
        }

        // 해당 칸에 이 유닛을 놓을 수 있나. BUILD 타일, 석상 아님, 다른 유닛 점유 아님(자기 칸은 허용).
        private bool CanOccupy(LoopUnit self, int x, int y)
        {
            if (state.map.GetTile(x, y) != LoopTile.Build) return false;
            if (IsStatueAt(x, y)) return false;
            LoopUnit at = GetUnitAt(x, y);
            if (at != null && at != self) return false;
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

        // 몬스터가 죽었을 때 로스터에서 뺀다(스폰은 시뮬, 처치는 전투 스크립트가 이 메서드로 로스터만 갱신).
        // 데미지 계산/hp 처리는 전투 스크립트(CombatController)가 소유한다. 시뮬은 스폰/순회/배치/경제만 다룬다.
        public void OnMonsterKilled()
        {
            if (state.aliveCount > 0) --state.aliveCount;
        }

        // 렌더링용 몬스터 실수 좌표(구간 보간).
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
            hash = Mix(hash, (ulong)state.aliveCount);
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
