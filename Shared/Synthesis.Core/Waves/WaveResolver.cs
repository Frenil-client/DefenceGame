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

        // 웨이브의 적을 EnemyData 로 해석한다. 웨이브 난이도(hpScale, armorAdd)를 여기서 적용한다.
        //   난이도가 오르는 수단은 능력치뿐이다. 스폰 수와 간격은 전 웨이브 동일하다(BALANCE 12).
        //   원형 원본을 그대로 돌려주면 스케일이 원본을 오염시키므로 항상 복사본을 만든다.
        // 보스는 STEP 2 스켈레톤에서 체력 큰 보행 적으로만 취급한다(각성/사전타격/소환은 STEP 4).
        public static EnemyData ResolveEnemy(WaveData wave, Dictionary<string, EnemyData> enemyById, Dictionary<string, BossData> bossById)
        {
            if (wave.isBoss)
            {
                BossData boss;
                if (!bossById.TryGetValue(wave.bossId, out boss)) return null;

                // 보스 수치는 bosses.csv 의 절대값이 전부다. 웨이브 스케일을 얹지 않는다.
                EnemyData asEnemy = new EnemyData();
                asEnemy.id = boss.id;
                asEnemy.name = boss.name;
                asEnemy.hp = boss.hp;
                asEnemy.armor = boss.armor;
                asEnemy.moveSpeed = boss.moveSpeed;
                return asEnemy;
            }

            EnemyData baseEnemy;
            if (!enemyById.TryGetValue(wave.enemySetId, out baseEnemy)) return null;

            EnemyData scaled = new EnemyData();
            scaled.id        = baseEnemy.id;
            scaled.name      = baseEnemy.name;
            scaled.hp        = ScaledHp(baseEnemy.hp, wave.hpScale);
            scaled.armor     = baseEnemy.armor + Fixed.FromInt(wave.armorAdd);
            scaled.moveSpeed = baseEnemy.moveSpeed;
            return scaled;
        }

        // 체력은 곱연산으로 오른다. 스케일이 비어 있으면(0 이하) 원본을 그대로 쓴다.
        private static Fixed ScaledHp(Fixed baseHp, Fixed hpScale)
        {
            if (hpScale.raw <= 0) return baseHp;
            return baseHp * hpScale;
        }
    }
}
