using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Waves;

namespace Synthesis.Core.Simulation
{
    // STEP 2. 뼈대 - 웨이브를 순서대로 실행하는 드라이버(방어 없음, 결정성 검증용). 테스트가 쓴다.
    // 뽑기/인벤토리/배치까지 포함한 전체 런은 RunController 가 담당한다.
    public sealed class WaveRunResult
    {
        public int wavesCompleted;
        public int killedCount;
        public int leakedCount;
        public ulong finalHash;
        public List<ulong> waveHashList = new List<ulong>();
    }

    public static class SimulationDriver
    {
        // waveCount 만큼 웨이브를 완주시키고 웨이브별 상태 해시를 기록한다.
        public static WaveRunResult RunWaves(GameDatabase db, MapData map, long seed, int waveCount, int maxTicksPerWave = 2000)
        {
            var enemyById = WaveResolver.BuildEnemyLookup(db.enemyList);
            var bossById = WaveResolver.BuildBossLookup(db.bossList);
            var waveByIndex = WaveResolver.BuildWaveLookup(db.waveList);

            Simulator sim = new Simulator(map, seed);
            WaveRunResult result = new WaveRunResult();

            for (int i = 1; i <= waveCount; ++i)
            {
                WaveData wave;
                if (!waveByIndex.TryGetValue(i, out wave))
                {
                    continue;
                }

                EnemyData enemy = WaveResolver.ResolveEnemy(wave, enemyById, bossById);
                int spawnCount = enemy != null ? wave.spawnCount : 0;
                sim.StartWave(enemy, spawnCount, wave.spawnInterval);

                int guard = 0;
                while (!sim.IsWaveComplete() && guard < maxTicksPerWave)
                {
                    sim.Tick();
                    ++guard;
                }

                result.waveHashList.Add(sim.ComputeStateHash());
                ++result.wavesCompleted;
            }

            result.killedCount = sim.state.killedCount;
            result.leakedCount = sim.state.leakedCount;
            result.finalHash = sim.ComputeStateHash();
            return result;
        }
    }
}
