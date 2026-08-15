using UnityEngine;

namespace Synthesis.Presentation
{
    // 독립 UI(기본 화면/팝업) 프리팹의 베이스. Resources/UI/<panelId> 에 두고 UIManager 가 인스턴스화한다.
    // 상시 재사용되는 HUD 류는 이 대상이 아니다(각자 코드로 그린다).
    public class UIPanel : MonoBehaviour
    {
        [Tooltip("Resources/UI 아래 프리팹 이름과 동일하게. 비우면 프리팹 이름으로 채운다")]
        public string panelId;

        [Tooltip("팝업처럼 아래 UI 입력을 막는 모달인지")]
        public bool modal = true;

        protected UIManager manager;

        // UIManager 가 연다. 파생 클래스는 여기서 초기화한다.
        public virtual void OnOpen(UIManager mgr)
        {
            manager = mgr;
        }

        public virtual void OnClose()
        {
        }

        // 닫기 버튼 등에서 호출. 매니저를 통해 스택에서 빠지고 파괴된다.
        public void Close()
        {
            if (manager != null) manager.Close(this);
            else Destroy(gameObject);
        }
    }
}
