using System.Collections.Generic;
using System.IO;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Simulation;

namespace Synthesis.Core.Tests
{
    // STEP 2. 검증 - 배치/저지/자동공격/사망/재배치 쿨타임.
    public class CombatTests
    {
        private static MapData LoadMap01()
        {
            string dataDir = TestPaths.FindDataDir();
            string grid = File.ReadAllText(Path.Combine(dataDir, "maps", "map01_grid.csv"));
            string path = File.ReadAllText(Path.Combine(dataDir, "maps", "map01_path.csv"));
            return MapParser.CsvToMap(grid, path);
        }

        private static List<UnitData> LoadUnits()
        {
            return CsvParsers.LoadUnits(TestPaths.ReadData("units.csv"));
        }

        private static EnemyData LoadE01()
        {
            var enemies = CsvParsers.LoadEnemies(TestPaths.ReadData("enemies.csv"));
            foreach (var enemy in enemies)
            {
                if (enemy.id == "E01") return enemy;
            }
            return null;
        }

        private static UnitData FindUnit(List<UnitData> list, string id)
        {
            foreach (var unit in list)
            {
                if (unit.id == id) return unit;
            }
            return null;
        }

        private static void RunWave(Simulator sim, EnemyData enemy, int count, int interval, int maxTicks = 2000)
        {
            sim.StartWave(enemy, count, interval);
            int guard = 0;
            while (!sim.IsWaveComplete() && guard < maxTicks)
            {
                sim.Tick();
                ++guard;
            }
        }

        [Fact]
        public void RangedUnits_KillEnemies()
        {
            var units = LoadUnits();
            UnitData bowman = FindUnit(units, "C08"); // 물리 단일 원거리
            Simulator sim = new Simulator(LoadMap01(), 1);
            sim.state.cost = Fixed.FromInt(40);

            Assert.True(sim.PlaceUnit(bowman, 5, 1));
            Assert.True(sim.PlaceUnit(bowman, 6, 1));
            Assert.True(sim.PlaceUnit(bowman, 5, 2));

            RunWave(sim, LoadE01(), 8, 30);
            Assert.True(sim.state.killedCount > 0);
        }

        [Fact]
        public void PlaceUnit_EnforcesRules()
        {
            var units = LoadUnits();
            UnitData bowman = FindUnit(units, "C08"); // ranged
            UnitData shield = FindUnit(units, "C07"); // melee
            Simulator sim = new Simulator(LoadMap01(), 1);

            // 코스트 부족
            sim.state.cost = Fixed.Zero;
            Assert.False(sim.PlaceUnit(bowman, 5, 1));

            sim.state.cost = Fixed.FromInt(40);
            // 원거리 유닛을 근접칸에 배치 불가
            Assert.False(sim.PlaceUnit(bowman, 1, 1));
            // 근접 유닛을 원거리칸에 배치 불가
            Assert.False(sim.PlaceUnit(shield, 5, 1));
            // 정상 배치 -> 코스트 차감(석궁병 5)
            Assert.True(sim.PlaceUnit(bowman, 5, 1));
            Assert.Equal(Fixed.FromInt(35).raw, sim.state.cost.raw);
            // 같은 칸 중복 배치 불가
            Assert.False(sim.PlaceUnit(bowman, 5, 1));
        }

        [Fact]
        public void RecallUnit_Refunds50Percent()
        {
            var units = LoadUnits();
            UnitData bowman = FindUnit(units, "C08"); // cost 5
            Simulator sim = new Simulator(LoadMap01(), 1);
            sim.state.cost = Fixed.FromInt(40);

            Assert.True(sim.PlaceUnit(bowman, 5, 1)); // 40 - 5 = 35
            Assert.True(sim.RecallUnit(5, 1));        // + 2.5 -> 37.5
            Assert.Equal(37500L, sim.state.cost.raw);
            // 회수 후 다시 배치 가능
            Assert.True(sim.PlaceUnit(bowman, 5, 1));
        }

        [Fact]
        public void MeleeUnit_BlocksAndTakesDamage()
        {
            var units = LoadUnits();
            UnitData shield = FindUnit(units, "C07"); // 근접 저지2
            Simulator sim = new Simulator(LoadMap01(), 1);
            sim.state.cost = Fixed.FromInt(40);

            Assert.True(sim.PlaceUnit(shield, 1, 1)); // 경로칸 (1,0) 에 인접
            long fullHp = sim.state.unitList[0].hp.raw;

            RunWave(sim, LoadE01(), 8, 30);

            // 저지 중 피격으로 유닛 hp 가 줄었고, 공격으로 적을 처치했다.
            Assert.True(sim.state.unitList[0].hp.raw < fullHp);
            Assert.True(sim.state.killedCount > 0);
        }

        [Fact]
        public void Determinism_WithUnits_Holds()
        {
            var units = LoadUnits();
            UnitData bowman = FindUnit(units, "C08");

            Simulator a = new Simulator(LoadMap01(), 7);
            a.state.cost = Fixed.FromInt(40);
            a.PlaceUnit(bowman, 5, 1);
            a.PlaceUnit(bowman, 6, 2);

            Simulator b = new Simulator(LoadMap01(), 7);
            b.state.cost = Fixed.FromInt(40);
            b.PlaceUnit(bowman, 5, 1);
            b.PlaceUnit(bowman, 6, 2);

            EnemyData e01 = LoadE01();
            for (int w = 0; w < 3; ++w)
            {
                RunWave(a, e01, 6, 20);
                RunWave(b, e01, 6, 20);
            }
            Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        }
    }
}
