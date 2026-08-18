using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Simulation;

namespace Synthesis.Core.Tests
{
    // STEP 2(v0.4). 검증 - 루프 시뮬레이션: 순회, 누적 무패, 배치, 결정성.
    public class LoopSimulationTests
    {
        private static LoopMap MakeMap(long seed)
        {
            return LoopMapGenerator.Generate(MapGenParser.Load(TestPaths.ReadData("mapgen.csv")), seed);
        }

        private static EnemyData E01()
        {
            foreach (var e in CsvParsers.LoadEnemies(TestPaths.ReadData("enemies.csv")))
            {
                if (e.id == "E01") return e;
            }
            return null;
        }

        private static GridPos FirstNonStatueBuildTile(LoopMap map)
        {
            foreach (var b in map.buildTileList)
            {
                bool isStatue = false;
                foreach (var s in map.statueList)
                {
                    if (s.x == b.x && s.y == b.y) { isStatue = true; break; }
                }
                if (!isStatue) return b;
            }
            return map.buildTileList[0];
        }

        private static UnitData FindUnit(string id)
        {
            foreach (var u in CsvParsers.LoadUnits(TestPaths.ReadData("units.csv")))
            {
                if (u.id == id) return u;
            }
            return null;
        }

        [Fact]
        public void Accumulation_SimDoesNotJudgeDefeat()
        {
            // 승패 판정(누적 상한 포함)은 게임 레이어(WaveManager)가 한다. 시뮬은 몬스터가 쌓여도 스스로 패배하지 않는다.
            LoopSimulator sim = new LoopSimulator(MakeMap(1), 1);
            sim.StartWave(E01(), 80, 4);

            for (int i = 0; i < 2000; ++i) sim.Tick();

            Assert.False(sim.state.defeated);
            Assert.True(sim.state.aliveCount > 60);
        }

        [Fact]
        public void Monster_LoopsWithoutExiting()
        {
            LoopSimulator sim = new LoopSimulator(MakeMap(1), 1);
            sim.StartWave(E01(), 1, 1);

            for (int i = 0; i < 400; ++i) sim.Tick();

            LoopMonster m = sim.state.monsterList[0];
            Assert.True(m.alive);
            Assert.InRange(m.waypointIndex, 0, sim.state.map.loopWaypointList.Count - 1);
        }

        [Fact]
        public void PlaceUnit_OnlyOnBuildTile()
        {
            UnitData mag = FindUnit("T1-MAG");

            LoopMap map = MakeMap(1);
            LoopSimulator sim = new LoopSimulator(map, 1);
            sim.state.cost = Fixed.FromInt(40);

            GridPos pathCell = map.loopWaypointList[0];
            GridPos buildCell = FirstNonStatueBuildTile(map);

            Assert.False(sim.PlaceUnit(mag, pathCell.x, pathCell.y)); // 경로칸 불가
            Assert.True(sim.PlaceUnit(mag, buildCell.x, buildCell.y)); // 배치칸 가능
            Assert.False(sim.PlaceUnit(mag, buildCell.x, buildCell.y)); // 중복 불가
        }


        [Fact]
        public void Determinism_Holds()
        {
            LoopMap map = MakeMap(5);
            LoopSimulator a = new LoopSimulator(map, 9);
            LoopSimulator b = new LoopSimulator(map, 9);

            a.StartWave(E01(), 40, 6);
            b.StartWave(E01(), 40, 6);
            for (int i = 0; i < 500; ++i) { a.Tick(); b.Tick(); }

            Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        }
    }
}
