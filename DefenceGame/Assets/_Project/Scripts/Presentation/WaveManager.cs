using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Units;
using Synthesis.Core.Waves;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 매니저 - 웨이브 스케줄 + 유닛 자동 배치.
    // 각 웨이브에는 제한시간이 있다. 제한시간은 기본적으로 다음 웨이브가 시작될 때까지 남은 시간이다(SPEC 2-3).
    // 일반 웨이브: 제한시간이 끝나거나 필드 몬스터가 0이 되면 다음 웨이브로 진행한다.
    // 보스 웨이브: 제한시간 안에 보스를 처치하면 진행(마지막 라운드면 클리어), 못 처치하면 패배(SPEC 2-4).
    // 유닛은 플레이어가 위치를 고르지 않고 맵 중앙에서 바깥으로 퍼지며 자동 배치된다(중앙 우선).
    public sealed class WaveManager : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private float prepSeconds = 1.5f;
        // [TEMP] 웨이브 제한시간(초). 8x12 둘레(36셀)를 몬스터가 약 1.5바퀴 도는 시간(BALANCE 12). 시뮬로 재확정.
        [SerializeField] private float waveTimeLimit = 35f;
        // [TEMP] 게임 시작 시 미리 지급하는 1성 유닛 수(최초 지급, BALANCE 6-1). 시뮬로 재확정.
        [SerializeField] private int startUnitCount = 5;

        public int NextWave { get; private set; } = 1;
        public string LastGranted { get; private set; } = "-";
        public bool Cleared { get; private set; }
        public float WaveTimer => waveTimer;

        private float waveTimer;
        private float placeTimer;
        private List<GridPos> centerTiles;
        private bool startGranted;

        private void Awake()
        {
            // game 은 인스펙터에 등록한다(씬에 미리 배치). 첫 웨이브 전 준비시간.
            waveTimer = prepSeconds;
        }

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            var ctx = game.Context;
            if (ctx.sim.state.defeated || Cleared) return;

            // 게임 시작 시 1성 유닛을 미리 지급한다(최초 지급). 컨텍스트가 준비된 첫 프레임에 1회.
            if (!startGranted)
            {
                startGranted = true;
                for (int i = 0; i < startUnitCount; ++i)
                {
                    string startUnit = ctx.gacha.Grant();
                    if (startUnit != null) ctx.inventory.Add(startUnit);
                }
            }

            // 상시 자동 배치: 코스트가 되는 대로 인벤토리 유닛을 중앙에서 바깥으로 배치한다(0.25초마다).
            placeTimer -= Time.deltaTime * game.Speed;
            if (placeTimer <= 0f)
            {
                AutoPlaceCenterOut(ctx);
                placeTimer = 0.25f;
            }

            waveTimer -= Time.deltaTime * game.Speed;

            int activeWave = NextWave - 1; // 현재 진행 중 웨이브(0 = 첫 웨이브 전 준비 단계)

            // 준비 단계: 제한시간이 끝나면 첫 웨이브를 시작한다.
            if (activeWave <= 0)
            {
                if (waveTimer <= 0f) StartNextWave(ctx);
                return;
            }

            WaveData active;
            bool isBoss = ctx.waveByIndex.TryGetValue(activeWave, out active) && active.isBoss;

            if (isBoss)
            {
                // 보스 처치 = 스폰이 끝났고(보스 1기 등장 완료) 살아있는 보스가 없다.
                bool bossDefeated = ctx.sim.IsSpawningDone() && !AnyBossAlive(ctx);
                if (bossDefeated)
                {
                    // 마지막 라운드는 보스 처치가 곧 클리어. 잔여 몬스터가 남아도 즉시 클리어.
                    if (activeWave >= game.MaxWave) { Cleared = true; return; }
                    StartNextWave(ctx);
                    return;
                }
                // 제한시간 안에 보스를 못 잡으면 패배(다음 웨이브로 넘어가지 않는다).
                if (waveTimer <= 0f) ctx.sim.state.defeated = true;
                return;
            }

            // 일반 웨이브: 제한시간 만료 또는 필드 클리어 시 다음 웨이브로.
            if (waveTimer <= 0f || ctx.sim.IsFieldClear())
            {
                if (activeWave >= game.MaxWave) { Cleared = true; return; } // 방어적(마지막은 보스지만)
                StartNextWave(ctx);
            }
        }

        private void StartNextWave(RunContext ctx)
        {
            BeginWave(ctx, NextWave);
            ++NextWave;
            waveTimer = waveTimeLimit;
        }

        // 살아있는 보스 몬스터가 하나라도 있으면 true. 보스 id 는 bossById 로 판별한다.
        private bool AnyBossAlive(RunContext ctx)
        {
            var list = ctx.sim.state.monsterList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopMonster m = list[i];
                if (m.alive && ctx.bossById.ContainsKey(m.enemyId)) return true;
            }
            return false;
        }

        private void BeginWave(RunContext ctx, int idx)
        {
            LastGranted = ctx.gacha.GrantForWave(idx);
            ctx.inventory.Add(LastGranted);
            AutoPlaceCenterOut(ctx);

            WaveData wave;
            if (ctx.waveByIndex.TryGetValue(idx, out wave))
            {
                EnemyData enemy = WaveResolver.ResolveEnemy(wave, ctx.enemyById, ctx.bossById);
                int count = enemy != null ? wave.spawnCount : 0;
                ctx.sim.StartWave(enemy, count, wave.spawnInterval);
            }
        }

        // 맵 중앙에 가까운 BUILD 타일부터 감당 가능한 인벤토리 유닛을 채운다.
        private void AutoPlaceCenterOut(RunContext ctx)
        {
            EnsureCenterTiles(ctx);

            List<OwnedUnit> snapshot = new List<OwnedUnit>(ctx.inventory.ownedList);
            foreach (var owned in snapshot)
            {
                UnitData data;
                if (!ctx.unitById.TryGetValue(owned.unitId, out data)) continue;
                if (ctx.sim.state.cost < Fixed.FromInt(data.cost)) continue;

                for (int i = 0; i < centerTiles.Count; ++i)
                {
                    if (ctx.sim.PlaceUnit(data, centerTiles[i].x, centerTiles[i].y))
                    {
                        ctx.inventory.RemoveByInstance(owned.instanceId);
                        break;
                    }
                }
            }
        }

        private void EnsureCenterTiles(RunContext ctx)
        {
            if (centerTiles != null) return;

            int cx = ctx.map.gridWidth / 2;
            int cy = ctx.map.gridHeight / 2;
            centerTiles = new List<GridPos>(ctx.map.buildTileList);
            centerTiles.Sort((a, b) =>
            {
                int da = (a.x - cx) * (a.x - cx) + (a.y - cy) * (a.y - cy);
                int db = (b.x - cx) * (b.x - cx) + (b.y - cy) * (b.y - cy);
                return da.CompareTo(db);
            });
        }
    }
}
