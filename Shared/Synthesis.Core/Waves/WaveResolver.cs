using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Core.Waves
{
    // STEP 2. 뼈대 - 웨이브 스폰 모델의 조회/해석 유틸. Driver, RunController, Demo 가 공유한다.
    public static class WaveResolver
    {
        public static Dictionary<string, EnemyData> BuildEnemyLookup(List<EnemyData> enemyList)
        {
            Dictionary<string, EnemyData> map = new Dictionary<string, EnemyData>();
            foreach (var enemy in enemyList)
            {
                if (enemy == null || string.IsNullOrEmpty(enemy.id)) continue;
                map[enemy.id] = enemy;
            }
            return map;
        }

        public static Dictionary<string, BossData> BuildBossLookup(List<BossData> bossList)
        {
            Dictionary<string, BossData> map = new Dictionary<string, BossData>();
            foreach (var boss in bossList)
            {
                if (boss == null || string.IsNullOrEmpty(boss.id)) continue;
                map[boss.id] = boss;
            }
            return map;
        }

        public static Dictionary<int, WaveData> BuildWaveLookup(List<WaveData> waveList)
        {
            Dictionary<int, WaveData> map = new Dictionary<int, WaveData>();
            foreach (var wave in waveList)
            {
                if (wave == null) continue;
                map[wave.waveIndex] = wave;
            }
            return map;
        }

        // 웨이브의 적을 EnemyData 로 해석한다.
        // 보스는 STEP 2 스켈레톤에서 체력 큰 보행 적으로만 취급한다(각성/사전타격/소환은 STEP 4).
        public static EnemyData ResolveEnemy(WaveData wave, Dictionary<string, EnemyData> enemyById, Dictionary<string, BossData> bossById)
        {
            if (wave.isBoss)
            {
                BossData boss;
                if (!bossById.TryGetValue(wave.bossId, out boss)) return null;

                EnemyData asEnemy = new EnemyData();
                asEnemy.id = boss.id;
                asEnemy.name = boss.name;
                asEnemy.hp = boss.hp;
                asEnemy.atk = Fixed.Zero;
                asEnemy.moveSpeed = boss.moveSpeed;
                return asEnemy;
            }

            EnemyData enemy;
            if (enemyById.TryGetValue(wave.enemySetId, out enemy)) return enemy;
            return null;
        }
    }
}
