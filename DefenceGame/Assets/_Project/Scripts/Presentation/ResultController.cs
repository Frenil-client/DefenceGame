using UnityEngine;

namespace Synthesis.Presentation
{
    // 런 종료(승리/패배)를 감지해 결과 팝업을 한 번 띄우고, 재시작 버튼을 GameManager.Restart 에 연결한다.
    // game 은 인스펙터에 등록한다(씬에 미리 배치). 승/패는 GameManager 가 Restart 시 동기 리셋하는 플래그로 판정해 경쟁을 피한다.
    public sealed class ResultController : MonoBehaviour
    {
        [SerializeField] private GameManager game;

        private int shownRunId = -1;

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            if (shownRunId == game.RunId) return; // 이 런의 결과는 이미 띄웠다

            bool win = game.Won;
            bool lose = game.Context.sim.state.defeated;
            if (!win && !lose) return;

            shownRunId = game.RunId;
            if (UIManager.Instance == null) return;

            // 결과가 나오면 열려 있던 상점/조합 팝업을 전부 닫아 잔류/미갱신을 막는다.
            UIManager.Instance.CloseAll();

            ResultPopup popup = UIManager.Instance.Open("ResultPopup") as ResultPopup;
            if (popup != null) popup.Setup(StringManager.Get(win ? "str.popup.result.win" : "str.popup.result.lose"), game.Restart);
        }
    }
}
