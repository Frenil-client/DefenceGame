using System.Collections.Generic;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Combination;
using Synthesis.Core.Units;
using Synthesis.Core.Simulation;
using Synthesis.Core.Waves;
using Synthesis.Data;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 조립 루트 - 한 런에 필요한 Core 객체를 한 곳에 모은다(Bootstrap 계층).
    // 매니저/뷰는 이 컨텍스트를 주입받아 공유한다. Core 는 여전히 헤드리스다.
    public sealed class RunContext
    {
        public LoopMap map;
        public LoopSimulator sim;
        public GachaEngine gacha;
        public Inventory inventory;
        public CombinationEngine combination;
        public GameDatabase db;

        public Dictionary<string, UnitData> unitById;
        public Dictionary<string, EnemyData> enemyById;
        public Dictionary<string, BossData> bossById;
        public Dictionary<int, WaveData> waveByIndex;

        public static RunContext Build(long seed, bool useDefaultMap = false, MapSO mapAsset = null)
        {
            MapGenParams p = RuntimeDataLoader.LoadMapGenParams();

            RunContext ctx = new RunContext();
            // 맵 소스 우선순위: 저장된 맵(MapSO) > 기본 직사각형 > 시드 변주.
            // MapSO 를 쓰면 저장된 경로를 그대로 로드하므로 생성/베이크 불일치가 없다.
            if (mapAsset != null) ctx.map = mapAsset.ToLoopMap();
            else if (useDefaultMap) ctx.map = LoopMapGenerator.GenerateRectangular(p, seed);
            else ctx.map = LoopMapGenerator.Generate(p, seed);
            ctx.db = RuntimeDataLoader.LoadDatabase();
            ctx.sim = new LoopSimulator(ctx.map, seed);
            // 시작 코스트를 상한만큼 쥐고 시작한다(초기 지급 유닛을 바로 배치 가능).
            ctx.sim.state.cost = Fixed.FromInt(ctx.sim.state.costCap);
            ctx.gacha = new GachaEngine(ctx.db.unitList, seed);
            ctx.inventory = new Inventory();
            ctx.combination = new CombinationEngine(ctx.db.recipeList);

            ctx.unitById = new Dictionary<string, UnitData>();
            foreach (var u in ctx.db.unitList)
            {
                if (u != null && !string.IsNullOrEmpty(u.id)) ctx.unitById[u.id] = u;
            }
            ctx.enemyById = WaveResolver.BuildEnemyLookup(ctx.db.enemyList);
            ctx.bossById = WaveResolver.BuildBossLookup(ctx.db.bossList);
            ctx.waveByIndex = WaveResolver.BuildWaveLookup(ctx.db.waveList);
            return ctx;
        }

        public bool IsValid()
        {
            return map != null && db != null && db.unitList.Count > 0;
        }
    }
}
