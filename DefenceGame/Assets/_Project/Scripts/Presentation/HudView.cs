using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Synthesis.Core.Data;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // 뷰 - HUD. UI 계층은 프리팹에 미리 만들어 두고, 여기서는 참조(statsText)만 갱신한다.
    // 배속 버튼의 onClick 은 프리팹에서 SetSpeed(float) 로 연결한다.
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private WaveManager waves;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private Button skipButton; // 웨이브 스킵 버튼(스폰 완료 시 활성)

        // 배속 버튼(프리팹의 onClick 에서 호출).
        public void SetSpeed(float value)
        {
            if (game != null) game.Speed = value;
        }

        // 웨이브 스킵 버튼(프리팹의 onClick 에서 호출). 생성 완료된 일반 웨이브에서 다음 웨이브로 넘긴다.
        public void OnSkipClicked()
        {
            if (waves != null) waves.RequestSkip();
        }

        // 상점 버튼(프리팹의 onClick 에서 호출). 선택권으로 원하는 1성을 구매한다.
        public void OpenShop()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            if (UIManager.Instance == null) return;
            ShopPopup popup = UIManager.Instance.Open("ShopPopup") as ShopPopup;
            if (popup != null) popup.Setup(game.Context);
        }

        // 보스 웨이브면 화면 상단에 목표(보스 이름/HP/방어/남은시간)를 표시한다(SPEC 3-6). 아니면 빈 문자열.
        private string BossObjectiveLine(int activeWave, float remain)
        {
            WaveData wave;
            if (!game.Context.waveByIndex.TryGetValue(activeWave, out wave) || !wave.isBoss || string.IsNullOrEmpty(wave.bossId)) return "";
            BossData boss;
            if (!game.Context.bossById.TryGetValue(wave.bossId, out boss)) return "";

            long curHp = 0;
            var list = game.Context.sim.state.monsterList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopMonster m = list[i];
                if (m.alive && m.enemyId == boss.id) { curHp = m.hp.ToIntTruncated(); break; }
            }
            return "[보스] " + boss.name + "  HP " + curHp + " / " + boss.hp.ToIntTruncated()
                + "  방어 " + boss.armor.ToIntTruncated() + "  남은 " + remain.ToString("F1") + "s\n";
        }

        private void Update()
        {
            // 스킵 버튼 활성화: 생성이 완료된 일반 웨이브에서만 누를 수 있다.
            if (skipButton != null) skipButton.interactable = waves != null && waves.CanSkip;

            if (statsText == null || game == null || game.Context == null || !game.Context.IsValid()) return;

            var s = game.Context.sim.state;
            int next = waves != null ? waves.NextWave : 1;
            string granted = waves != null ? waves.LastGranted : "-";
            bool cleared = waves != null && waves.Cleared;
            int shownWave = Mathf.Clamp(next - 1, 0, game.MaxWave);
            string phaseLabel = s.defeated ? "패배"
                : cleared ? "클리어"
                : (s.pendingSpawns > 0 ? "전투" : "대기");
            float remain = waves != null ? Mathf.Max(0f, waves.WaveTimer) : 0f;

            statsText.text =
                BossObjectiveLine(next - 1, remain)
                + "웨이브 " + shownWave + " / " + game.MaxWave + "  [" + phaseLabel + "]  x" + game.Speed + "\n"
                + "제한시간 " + remain.ToString("F1") + "s\n"
                + "코스트 " + s.cost + " / " + s.costCap + "\n"
                + "필드 몬스터 " + s.aliveCount + " / " + (waves != null ? waves.AccumCap : 0) + "\n"
                + "인벤토리 " + game.Context.inventory.Count + "   최근 뽑기 " + granted + "\n"
                + "선택권 " + game.Context.selectionTokens;
        }
    }
}
