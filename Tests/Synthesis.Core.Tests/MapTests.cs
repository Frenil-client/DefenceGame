using System.IO;
using Synthesis.Core.Map;

namespace Synthesis.Core.Tests
{
    // STEP 2. 검증 - 맵 파서.
    public class MapTests
    {
        private static MapData LoadMap01()
        {
            string dataDir = TestPaths.FindDataDir();
            string grid = File.ReadAllText(Path.Combine(dataDir, "maps", "map01_grid.csv"));
            string path = File.ReadAllText(Path.Combine(dataDir, "maps", "map01_path.csv"));
            return MapParser.CsvToMap(grid, path);
        }

        [Fact]
        public void Map01_Dimensions()
        {
            MapData map = LoadMap01();
            Assert.Equal(8, map.width);
            Assert.Equal(5, map.height);
        }

        [Fact]
        public void Map01_SpawnAndExit()
        {
            MapData map = LoadMap01();
            Assert.Equal(CellType.Spawn, map.GetCell(0, 0));
            Assert.Equal(CellType.Exit, map.GetCell(7, 0));
            Assert.Equal(CellType.Path, map.GetCell(3, 0));
        }

        [Fact]
        public void Map01_PlacementCells()
        {
            MapData map = LoadMap01();
            Assert.Equal(CellType.Melee, map.GetCell(1, 1));
            Assert.Equal(CellType.Ranged, map.GetCell(5, 1));
            Assert.Equal(CellType.Empty, map.GetCell(0, 4));
        }

        [Fact]
        public void Map01_PathLength()
        {
            MapData map = LoadMap01();
            Assert.Equal(8, map.pathList.Count);
            Assert.Equal(7, map.GetPathLength());
        }
    }
}
