using UnityEngine;
using UnityEngine.InputSystem;
using UISystem;

namespace Synthesis.Presentation
{
    // ESC(뒤로) 입력을 UI 스택으로 넘긴다. UISystem 은 입력을 직접 읽지 않으므로 씬에 이 컴포넌트를 하나 둔다.
    // 닫을 수 있는 최상단 UI 하나가 닫히고, 못 닫는 모달(BlockClose)을 만나면 거기서 소비된다.
    public sealed class UIBackInput : MonoBehaviour
    {
        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (UIManager.Instance == null) return;

            UIManager.Instance.OnBackPressed();
        }
    }
}
