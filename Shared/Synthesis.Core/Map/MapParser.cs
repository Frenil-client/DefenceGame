using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Core.Map
{
    // STEP 2. 뼈대 - 맵 CSV 파서. grid.csv 와 path.csv 를 MapData 로 합친다.
    // 파서는 Core 에 둔다. Sim 과 Unity 가 같은 파서를 공유한다 (ARCHITECTURE.md 5-1).
    public static class MapParser
    {
        public static MapData CsvToMap(string gridText, string pathText)
        {
            MapData mapData = ParseGrid(gridText);
            ParsePath(pathText, mapData);
            return mapData;
        }

        // 격자는 헤더가 없다. '#' 로 시작하는 줄은 주석, 빈 줄은 무시. 나머지 각 줄이 한 행이다.
        private static MapData ParseGrid(string gridText)
        {
            MapData mapData = new MapData();
            List<CellType[]> rowList = new List<CellType[]>();
            int width = 0;

            var lineSplit = gridText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            for (int i = 0; i < lineSplit.Length; ++i)
            {
                var line = lineSplit[i].Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                if (line[0] == '#')
                {
                    continue;
                }

                var split = line.Split(',');
                CellType[] row = new CellType[split.Length];
                for (int j = 0; j < split.Length; ++j)
                {
                    row[j] = CodeToCell(split[j].Trim());
                }
                if (split.Length > width) width = split.Length;
                rowList.Add(row);
            }

            mapData.width = width;
            mapData.height = rowList.Count;
            mapData.cellList = new CellType[width * rowList.Count];
            for (int y = 0; y < rowList.Count; ++y)
            {
                CellType[] row = rowList[y];
                for (int x = 0; x < width; ++x)
                {
                    mapData.cellList[y * width + x] = x < row.Length ? row[x] : CellType.Empty;
                }
            }
            return mapData;
        }

        // path.csv: x,y (헤더 있음, 순서 있음)
        private static void ParsePath(string pathText, MapData mapData)
        {
            var lineList = CsvUtil.CsvToDataLines(pathText);
            foreach (var line in lineList)
            {
                var split = line.Split(',');
                if (split.Length < 2)
                {
                    continue;
                }
                int x = CsvUtil.StringToInt(split[0]);
                int y = CsvUtil.StringToInt(split[1]);
                mapData.pathList.Add(new GridPos(x, y));
            }
        }

        private static CellType CodeToCell(string code)
        {
            if (string.IsNullOrEmpty(code)) return CellType.Empty;
            switch (code[0])
            {
                case '#': return CellType.Obstacle;
                case 'p': case 'P': return CellType.Path;
                case 'm': case 'M': return CellType.Melee;
                case 'r': case 'R': return CellType.Ranged;
                case 'S': case 's': return CellType.Spawn;
                case 'X': case 'x': return CellType.Exit;
                default:  return CellType.Empty;
            }
        }
    }
}
