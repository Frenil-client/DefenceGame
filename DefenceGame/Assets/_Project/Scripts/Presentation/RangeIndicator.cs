using UnityEngine;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 뷰 - 유닛 사거리 원. 선택/집는 중에만 표시한다.
    // ring 은 지면에 눕힌 원형 메시(quad/disk). 스케일로 반지름을 나타낸다. 프리팹에서 연결한다.
    public sealed class RangeIndicator : MonoBehaviour
    {
        [SerializeField] private Transform ring;

        private void Awake()
        {
            Hide();
        }

        public void Show(float radius)
        {
            if (ring == null) return;
            ring.gameObject.SetActive(true);
            ring.localScale = new Vector3(radius * 2f, ring.localScale.y, radius * 2f);
        }

        public void Hide()
        {
            if (ring != null) ring.gameObject.SetActive(false);
        }
    }
}
