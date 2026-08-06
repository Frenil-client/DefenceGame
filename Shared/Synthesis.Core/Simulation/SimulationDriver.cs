using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Map;

namespace Synthesis.Core.Simulation
{
    // STEP 2. 뼈대 - 웨이브를 순서대로 실행하는 드라이버. 테스트와 Sim 콘솔이 공유한다.
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
            Dictionary<string, EnemyData> enemyById = new Dictionary<string, EnemyData>();
            foreach (var enemy in db.enemyList)
            {
                if (enemy == null || string.IsNullOrEmpty(enemy.id)) continue;
                enemyById[enemy.id] = enemy;
            }

            Dictionary<string, BossData> bossById = new Dictionary<string, BossData>();
            foreach (var boss in db.bossList)
            {
                if (boss == null || string.IsNullOrEmpty(boss.id)) continue;
                bossById[boss.id] = boss;
            }

            Dictionary<int, WaveData> waveByIndex = new Dictionary<int, WaveData>();
            foreach (var wave in db.waveList)
            {
                if (wave == null) continue;
                waveByIndex[wave.waveIndex] = wave;
            }

            Simulator sim = new Simulator(map, seed);
            WaveRunResult result = new WaveRunResult();

            for (int i = 1; i <= waveCount; ++i)
            {
                WaveData wave;
                if (!waveByIndex.TryGetValue(i, out wave))
                {
                    continue;
                }

                EnemyData enemy = ResolveEnemy(wave, enemyById, bossById);
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

        // 웨이브의 적을 EnemyData 로 해석한다.
        // 보스 웨이브는 STEP 2 스켈레톤에서 보스를 '체력 큰 보행 적'으로만 취급한다(TEMP).
        // 각성/사전 타격/부하 소환 등 실제 보스 거동은 STEP 4 에서 얹는다.
        private static EnemyData ResolveEnemy(WaveData wave, Dictionary<string, EnemyData> enemyById, Dictionary<string, BossData> bossById)
        {
            if (wave.isBoss)
            {
                BossData boss;
                if (!bossById.TryGetValue(wave.bossId, out boss)) return null;

                EnemyData asEnemy = new EnemyData();
                asEnemy.id = boss.id;
                asEnemy.name = boss.name;
                asEnemy.hp = boss.hp;
                asEnemy.atk = Fixed.Zero;             // TEMP: 보스 공격 거동은 STEP 4
                asEnemy.moveSpeed = boss.moveSpeed;
                return asEnemy;
            }

            EnemyData enemy;
            if (enemyById.TryGetValue(wave.enemySetId, out enemy)) return enemy;
            return null;
        }
    }
}
