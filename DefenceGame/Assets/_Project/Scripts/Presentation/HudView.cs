using UnityEngine;
using UnityEngine.UI;

namespace Synthesis.Presentation
{
    // 뷰 - HUD. UI 계층은 프리팹에 미리 만들어 두고, 여기서는 참조(statsText)만 갱신한다.
    // 배속 버튼의 onClick 은 프리팹에서 SetSpeed(float) 로 연결한다.
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private WaveManager waves;
        [SerializeField] private Text statsText;

        // 배속 버튼(프리팹의 onClick 에서 호출).
        public void SetSpeed(float value)
        {
            if (game != null) game.Speed = value;
        }

        private void Update()
        {
            if (statsText == null || game == null || game.Context == null || !game.Context.IsValid()) return;

            var s = game.Context.sim.state;
            int next = waves != null ? waves.NextWave : 1;
            string granted = waves != null ? waves.LastGranted : "-";
            int shownWave = Mathf.Clamp(next - 1, 0, game.MaxWave);
            string phaseLabel = s.defeated ? "패배"
                : (next > game.MaxWave && s.aliveCount == 0 ? "클리어"
                : (s.pendingSpawns > 0 ? "전투" : "대기"));

            statsText.text =
                "웨이브 " + shownWave + " / " + game.MaxWave + "  [" + phaseLabel + "]  x" + game.Speed + "\n"
                + "코스트 " + s.cost + " / " + s.costCap + "\n"
                + "필드 몬스터 " + s.aliveCount + "\n"
                + "인벤토리 " + game.Context.inventory.Count + "   최근 뽑기 " + granted;
        }
    }
}
