using UnityEngine;

namespace Synthesis.Presentation
{
    // 몬스터 HP 바 아이템. 프리팹에 미리 만들어 두고, MonsterHealthBarHud 가 인스턴스화/풀링한다.
    public sealed class MonsterHpBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform rect;
        [SerializeField] private RectTransform fill;

        public void SetActive(bool on)
        {
            gameObject.SetActive(on);
        }

        public void SetScreenPosition(Vector2 pos)
        {
            if (rect != null) rect.anchoredPosition = pos;
        }

        public void SetRatio(float ratio)
        {
            if (fill != null) fill.localScale = new Vector3(Mathf.Clamp01(ratio), 1f, 1f);
        }
    }
}
