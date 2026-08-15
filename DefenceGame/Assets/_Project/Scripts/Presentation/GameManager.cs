using UnityEngine;
using Synthesis.Core.Simulation;
using Synthesis.Data;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 매니저 - 런 상태 소유 + 20틱 고정 구동. 조립은 RunContext, 스케줄은 WaveManager.
    // 다른 매니저/뷰는 GameManager.Context 를 읽어 공유한다(Core 는 헤드리스 유지).
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private LoopMapView mapView;
        [Tooltip("지정하면 이 저장된 맵을 로드한다(경로/스폰/석상 고정). 비우면 LoopMapView 의 시드/기본 모드로 생성")]
        [SerializeField] private MapSO mapAsset;
        [SerializeField] private int maxWave = 30;

        public const float TickInterval = 1f / 20f;

        public RunContext Context { get; private set; }
        public LoopMapView MapView => mapView;
        public int MaxWave => maxWave;
        public float Speed = 1f;

        private float tickAccum;

        private void Awake()
        {
            // mapView 는 인스펙터에 등록한다(씬에 미리 배치). LoopMapRuntimeRenderer/UIManager 도 씬에 미리 둔다.
            if (mapView == null)
            {
                Debug.LogError("[GameManager] mapView 가 할당되지 않았습니다(인스펙터에서 등록).");
                enabled = false;
                return;
            }
            Context = RunContext.Build(mapView.seed, mapView.useDefaultMap, mapAsset);
            if (!Context.IsValid())
            {
                Debug.LogError("[GameManager] Data 를 읽지 못했습니다.");
                enabled = false;
                return;
            }

            // 로드한 맵 크기를 뷰에 반영해 원점 중심 좌표가 맞도록 한다(카메라도 원점을 봄).
            mapView.gridWidth = Context.map.gridWidth;
            mapView.gridHeight = Context.map.gridHeight;
        }

        private void Update()
        {
            if (Context == null || !Context.IsValid()) return;
            var sim = Context.sim;
            if (sim.state.defeated) return;

            float dt = Time.deltaTime * Speed;
            tickAccum += dt;
            if (tickAccum > TickInterval * 4) tickAccum = TickInterval * 4;

            int ticksThisFrame = 0;
            while (tickAccum >= TickInterval && ticksThisFrame < 20)
            {
                tickAccum -= TickInterval;
                ++ticksThisFrame;
                sim.Tick();
                if (sim.state.defeated) break;
            }
        }
    }
}
