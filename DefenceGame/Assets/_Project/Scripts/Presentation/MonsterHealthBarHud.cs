using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // HUD - 몬스터 HP 게이지를 화면공간에 그린다(3D 아님). 바는 프리팹(MonsterHpBarView)을 인스턴스화/풀링한다.
    // 매 프레임 각 몬스터의 월드 위치를 스크린으로 투영해 바를 배치한다.
    public sealed class MonsterHealthBarHud : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private Camera cam;               // 게임 카메라. 인스펙터 등록
        [SerializeField] private Canvas baseCanvas;        // 상위 UI Canvas(스케일 기준). 인스펙터 등록
        [SerializeField] private EntityView entityView;    // 보간된 몬스터 몸체 위치 출처. 인스펙터 등록
        [SerializeField] private MonsterHpBarView barPrefab; // HP 바 아이템 프리팹
        [SerializeField] private float barWidth = 44f;     // 바 폭(프리팹과 일치, 중앙 정렬 보정용)
        [SerializeField] private float screenYOffset = 30f;

        private readonly List<MonsterHpBarView> bars = new List<MonsterHpBarView>();
        private readonly Dictionary<LoopMonster, long> maxHp = new Dictionary<LoopMonster, long>();

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            if (cam == null || barPrefab == null || entityView == null) return;

            LoopSimulator sim = game.Context.sim;
            float sf = baseCanvas != null && baseCanvas.scaleFactor > 0f ? baseCanvas.scaleFactor : 1f;

            int used = 0;
            var list = sim.state.monsterList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopMonster m = list[i];
                if (!m.alive) { maxHp.Remove(m); continue; }

                long max;
                if (!maxHp.TryGetValue(m, out max)) { max = m.hp.raw > 0 ? m.hp.raw : 1; maxHp[m] = max; }

                // 시뮬 위치가 아니라 보간된 몸체 위치를 투영해 몬스터와 정확히 정렬(함께 부드럽게).
                Vector3 world;
                if (!entityView.TryGetMonsterWorld(m, out world)) continue;
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z < 0f) continue; // 카메라 뒤

                float ratio = Mathf.Clamp01((float)m.hp.raw / max);

                MonsterHpBarView bar = GetBar(used);
                ++used;
                bar.SetActive(ratio < 1f); // 가득 차면 숨김
                bar.SetScreenPosition(new Vector2(screen.x / sf - barWidth * 0.5f, screen.y / sf + screenYOffset));
                bar.SetRatio(ratio);
            }

            for (int j = used; j < bars.Count; ++j) bars[j].SetActive(false);
        }

        private MonsterHpBarView GetBar(int index)
        {
            while (bars.Count <= index) bars.Add(Instantiate(barPrefab, transform));
            return bars[index];
        }
    }
}
