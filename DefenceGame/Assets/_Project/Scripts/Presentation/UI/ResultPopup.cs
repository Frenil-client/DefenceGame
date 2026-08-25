using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Synthesis.Presentation
{
    // 독립 UI(팝업) - 런 결과(승리/패배)를 보여주고 재시작 버튼을 제공한다.
    // UI 틀은 프리팹에 미리 만들어 두고, 여기서는 메시지 텍스트와 재시작 콜백만 받는다.
    public sealed class ResultPopup : UIPanel
    {
        [SerializeField] private TMP_Text messageText;

        private System.Action onRestart;

        public void Setup(string message, System.Action restart)
        {
            onRestart = restart;
            if (messageText != null) messageText.text = message;
        }

        // 재시작 버튼(프리팹의 onClick 에서 호출). 팝업을 닫고 재시작한다.
        public void OnRestartClicked()
        {
            System.Action cb = onRestart;
            Close();
            if (cb != null) cb();
        }
    }
}
