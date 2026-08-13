using System.Collections.Generic;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Simulation;

namespace Synthesis.Core.Tests
{
    // STEP 2(v0.4). 검증 - 이동은 거리(셀) 기준. 경로를 코너만 찍든 셀마다 찍든 실제 속도가 같아야 한다.
    public class MovementSpeedTests
    {
        private static EnemyData Runner(int cellsPerSec)
        {
            EnemyData e = new EnemyData();
            e.id = "RUN";
            e.name = "runner";
            e.hp = Fixed.FromInt(1000000); // 안 죽게
            e.atk = Fixed.Zero;
            e.moveSpeed = Fixed.FromInt(cellsPerSec);
            return e;
        }

        // 사각형 둘레를 순서대로 도는 셀 목록(코너 중복 없이).
        private static List<GridPos> RectPerimeterCells(int x0, int y0, int x1, int y1)
        {
            List<GridPos> cells = new List<GridPos>();
            for (int x = x0; x < x1; ++x) cells.Add(new GridPos(x, y0));
            for (int y = y0; y < y1; ++y) cells.Add(new GridPos(x1, y));
            for (int x = x1; x > x0; --x) cells.Add(new GridPos(x, y1));
            for (int y = y1; y > y0; --y) cells.Add(new GridPos(x0, y));
            return cells;
        }

        private static LoopMap MapFrom(List<GridPos> cells)
        {
            var spawns = new List<int> { 0 };
            var statues = new List<GridPos>();
            return LoopMapGenerator.FromCells(16, 12, cells, spawns, statues, Fixed.Zero, 4);
        }

        [Fact]
        public void Speed_IsDistanceBased_RegardlessOfSubdivision()
        {
            // 같은 사각형(둘레 32셀)을 코너 4개 vs 모든 셀로 구성.
            var corners = new List<GridPos>
            {
                new GridPos(0, 0), new GridPos(10, 0), new GridPos(10, 6), new GridPos(0, 6)
            };
            var fine = RectPerimeterCells(0, 0, 10, 6);

            LoopSimulator coarse = new LoopSimulator(MapFrom(corners), 1);
            LoopSimulator dense = new LoopSimulator(MapFrom(fine), 1);

            coarse.StartWave(Runner(4), 1, 1);
            dense.StartWave(Runner(4), 1, 1);

            for (int i = 0; i < 40; ++i) { coarse.Tick(); dense.Tick(); }

            Fixed cx, cy, dx, dy;
            coarse.GetMonsterPosition(coarse.state.monsterList[0], out cx, out cy);
            dense.GetMonsterPosition(dense.state.monsterList[0], out dx, out dy);

            // 두 경로에서 물리 위치가 사실상 같아야 한다(세분화 무관).
            double ddx = System.Math.Abs(cx.ToDoubleForDisplay() - dx.ToDoubleForDisplay());
            double ddy = System.Math.Abs(cy.ToDoubleForDisplay() - dy.ToDoubleForDisplay());
            Assert.True(ddx < 0.05 && ddy < 0.05,
                "코너경로 (" + cx.ToDoubleForDisplay() + "," + cy.ToDoubleForDisplay() + ") vs "
                + "셀경로 (" + dx.ToDoubleForDisplay() + "," + dy.ToDoubleForDisplay() + ")");
        }

        [Fact]
        public void Sqrt_IsDeterministicAndFloor()
        {
            Assert.Equal(Fixed.FromInt(3).raw, Fixed.Sqrt(Fixed.FromInt(9)).raw);
            Assert.Equal(Fixed.FromInt(4).raw, Fixed.Sqrt(Fixed.FromInt(16)).raw);
            Assert.Equal(Fixed.Sqrt(Fixed.FromInt(2)).raw, Fixed.Sqrt(Fixed.FromInt(2)).raw); // 반복 동일
            Assert.True(Fixed.Sqrt(Fixed.Zero).raw == 0);
        }
    }
}
