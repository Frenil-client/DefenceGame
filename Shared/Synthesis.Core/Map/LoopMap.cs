using System.Collections.Generic;

namespace Synthesis.Core.Map
{
    // STEP 1. 기반 도구 - 루프 맵 데이터 (MAP_SPEC.md 2).
    // 몬스터 위치는 웨이포인트 인덱스 + 구간 진행도로 표현한다. 마지막 다음은 0으로 돌아간다.

    public enum LoopTile
    {
        Empty,
        Path,
        Build
    }

    public sealed class LoopMap
    {
        public long seed;
        public int gridWidth;
        public int gridHeight;
        public LoopTile[] tileGrid;                              // index = y * gridWidth + x

        public List<GridPos> loopWaypointList = new List<GridPos>(); // 닫힌 순환. 마지막 다음은 0
        public List<GridPos> buildTileList = new List<GridPos>();    // 배치 가능 = 경로 아닌 모든 셀 (루프 안팎)
        public List<GridPos> interiorTileList = new List<GridPos>(); // 루프 내부 셀 (형태 지표/커버 기준)
        public List<int> spawnIndexList = new List<int>();       // loopWaypointList 인덱스
        public List<int> cornerIndexList = new List<int>();
        public List<GridPos> statueList = new List<GridPos>();   // 석상 위치 (맵 외곽, 파괴 시 도플갱어 드랍)
        public Fixed statueHp;                                   // 석상 체력 (TEMP)

        public int perimeter;                                     // 둘레 타일 수 = loopWaypointList.Count
        public int buildArea;                                     // 배치 가능 타일 수 (경로 외 전체)
        public int interiorArea;                                  // 루프 내부 면적 (MAP-03 지표)
        public Fixed coverageIndex;                               // 커버 효율 지수 (5장)
        public bool isFallback;                                   // 폴백 템플릿 여부

        public LoopTile GetTile(int x, int y)
        {
            if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight) return LoopTile.Empty;
            return tileGrid[y * gridWidth + x];
        }
    }

    // MAP_SPEC.md 6 - 생성 파라미터.
    public sealed class MapGenParams
    {
        public int gridWidth;
        public int gridHeight;
        public int baseRingInset;
        public int perimeterMin;
        public int perimeterMax;
        public int cornerMin;
        public int cornerMax;
        public int areaMin;
        public int areaMax;
        public int minLaneGap;
        public int coverageRadius;
        public int coverageMin;
        public int coverageMax;
        public int bumpCountMin;
        public int bumpCountMax;
        public int bumpDistMax;
        public int spawnCount;
        public int maxRetry;
        public int statueCountMin;
        public int statueCountMax;
        public int statueHp;

        public static MapGenParams Defaults()
        {
            MapGenParams p = new MapGenParams();
            p.gridWidth = 16; p.gridHeight = 12; p.baseRingInset = 2;
            p.perimeterMin = 44; p.perimeterMax = 56;
            p.cornerMin = 8; p.cornerMax = 16;
            p.areaMin = 30; p.areaMax = 50;
            p.minLaneGap = 2; p.coverageRadius = 4; p.coverageMin = 0; p.coverageMax = 0;
            p.bumpCountMin = 3; p.bumpCountMax = 6; p.bumpDistMax = 2;
            p.spawnCount = 3; p.maxRetry = 30;
            p.statueCountMin = 1; p.statueCountMax = 2; p.statueHp = 1200;
            return p;
        }
    }
}
