using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Synthesis.Presentation
{
    // 하단 유닛 바의 버튼 아이템. 프리팹에 미리 만들어 두고, InventoryView 가 인스턴스화해 내용만 채운다.
    public sealed class UnitButtonView : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Button button;

        public void Set(string text, UnityAction onClick)
        {
            if (label != null) label.text = text;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(onClick);
            }
        }
    }
}
