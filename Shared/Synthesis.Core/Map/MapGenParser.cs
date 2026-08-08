using Synthesis.Core.Data;

namespace Synthesis.Core.Map
{
    // STEP 1. 기반 도구 - mapgen.csv 파서 (MAP_SPEC.md 6).
    public static class MapGenParser
    {
        public static MapGenParams CsvToMapGenParams(string line)
        {
            var split = line.Split(',');
            if (split.Length < 18) return null;

            MapGenParams p = new MapGenParams();
            p.gridWidth     = CsvUtil.StringToInt(split[0]);
            p.gridHeight    = CsvUtil.StringToInt(split[1]);
            p.baseRingInset = CsvUtil.StringToInt(split[2]);
            p.perimeterMin  = CsvUtil.StringToInt(split[3]);
            p.perimeterMax  = CsvUtil.StringToInt(split[4]);
            p.cornerMin     = CsvUtil.StringToInt(split[5]);
            p.cornerMax     = CsvUtil.StringToInt(split[6]);
            p.areaMin       = CsvUtil.StringToInt(split[7]);
            p.areaMax       = CsvUtil.StringToInt(split[8]);
            p.minLaneGap    = CsvUtil.StringToInt(split[9]);
            p.coverageRadius= CsvUtil.StringToInt(split[10]);
            p.coverageMin   = CsvUtil.StringToInt(split[11]);
            p.coverageMax   = CsvUtil.StringToInt(split[12]);
            p.bumpCountMin  = CsvUtil.StringToInt(split[13]);
            p.bumpCountMax  = CsvUtil.StringToInt(split[14]);
            p.bumpDistMax   = CsvUtil.StringToInt(split[15]);
            p.spawnCount    = CsvUtil.StringToInt(split[16]);
            p.maxRetry      = CsvUtil.StringToInt(split[17]);
            return p;
        }

        public static MapGenParams Load(string fileText)
        {
            var lineList = CsvUtil.CsvToDataLines(fileText);
            if (lineList.Count == 0) return MapGenParams.Defaults();
            MapGenParams p = CsvToMapGenParams(lineList[0]);
            return p ?? MapGenParams.Defaults();
        }
    }
}
