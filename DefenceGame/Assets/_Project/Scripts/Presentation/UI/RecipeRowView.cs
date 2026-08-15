using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Synthesis.Presentation
{
    // 조합 팝업의 조합식 행 아이템. 프리팹에 미리 만들어 두고, CombinePopup 이 인스턴스화해 내용만 채운다.
    public sealed class RecipeRowView : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Button button;
        [SerializeField] private Image background;

        private static readonly Color Craftable = new Color(0.30f, 0.46f, 0.30f, 0.95f);
        private static readonly Color Locked = new Color(0.24f, 0.24f, 0.28f, 0.9f);

        public void Set(string text, bool craftable, UnityAction onClick)
        {
            if (label != null) label.text = text;
            if (background != null) background.color = craftable ? Craftable : Locked;
            if (button != null)
            {
                button.interactable = craftable;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(onClick);
            }
        }
    }
}
