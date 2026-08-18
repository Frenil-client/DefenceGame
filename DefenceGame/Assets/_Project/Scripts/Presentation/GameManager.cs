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
        [SerializeField] private int maxWave = 40;

        public const float TickInterval = 1f / 20f;

        public RunContext Context { get; private set; }
        public LoopMapView MapView => mapView;
        public int MaxWave => maxWave;
        public float Speed = 1f;

        // 런 세대 번호. 재시작할 때마다 증가한다. 상태를 캐시하는 뷰/매니저가 이 값 변화를 보고 스스로 리셋한다.
        public int RunId { get; private set; }
        public bool Won { get; private set; }
        public bool IsOver => Won || (Context != null && Context.IsValid() && Context.sim.state.defeated);

        private float tickAccum;
        private long runSeed;

        private void Awake()
        {
            // mapView 는 인스펙터에 등록한다(씬에 미리 배치). LoopMapRuntimeRenderer/UIManager 도 씬에 미리 둔다.
            if (mapView == null)
            {
                Debug.LogError("[GameManager] mapView 가 할당되지 않았습니다(인스펙터에서 등록).");
                enabled = false;
                return;
            }
            runSeed = mapView.seed;
            BuildRun();
        }

        // 런을 (재)빌드한다. Context 를 새로 만들고 승리 플래그와 틱 누적을 초기화한다.
        private void BuildRun()
        {
            Won = false;
            tickAccum = 0f;
            Context = RunContext.Build(runSeed, mapView.useDefaultMap, mapAsset);
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

        // 클리어(WaveManager)가 호출. 승리 시 틱을 멈춘다.
        public void MarkWon()
        {
            Won = true;
        }

        // 결과 화면의 재시작 버튼이 호출. 같은 시드로 런을 다시 시작한다(뷰/매니저는 RunId 변화로 리셋).
        public void Restart()
        {
            BuildRun();
            ++RunId;
        }

        private void Update()
        {
            if (Context == null || !Context.IsValid()) return;
            var sim = Context.sim;
            if (Won || sim.state.defeated) return; // 런 종료 시 틱 정지

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
