using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Map;
using Synthesis.Core.Units;
using Synthesis.Core.Waves;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 매니저 - 웨이브 스케줄 + 유닛 자동 배치.
    // 유닛은 플레이어가 위치를 고르지 않고 맵 중앙에서 바깥으로 퍼지며 자동 배치된다(중앙 우선).
    // 필드 몬스터가 0이 되면 즉시 다음 웨이브(BALANCE v0.4 12). 없으면 대기시간 후 진행.
    public sealed class WaveManager : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private float prepSeconds = 1.5f;
        [SerializeField] private float waveHoldSeconds = 6f;
        // [TEMP] 게임 시작 시 미리 지급하는 1성 유닛 수(최초 지급, BALANCE 6-1). 시뮬로 재확정.
        [SerializeField] private int startUnitCount = 5;

        public int NextWave { get; private set; } = 1;
        public string LastGranted { get; private set; } = "-";

        private float waveCountdown;
        private float placeTimer;
        private List<GridPos> centerTiles;
        private bool startGranted;

        private void Awake()
        {
            // game 은 인스펙터에 등록한다(씬에 미리 배치).
            waveCountdown = prepSeconds;
        }

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            var ctx = game.Context;
            if (ctx.sim.state.defeated) return;

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

            if (NextWave > game.MaxWave || !ctx.sim.IsSpawningDone()) return;

            waveCountdown -= Time.deltaTime * game.Speed;
            bool timeUp = waveCountdown <= 0f;
            bool fieldClear = NextWave > 1 && ctx.sim.IsFieldClear();

            if (timeUp || fieldClear)
            {
                BeginWave(ctx, NextWave);
                ++NextWave;
                waveCountdown = waveHoldSeconds;
            }
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
