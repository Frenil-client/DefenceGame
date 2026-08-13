using UnityEngine;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 뷰 - 몬스터 HP바. fill 의 x 스케일로 비율을 표현한다(피벗 왼쪽 가정).
    // fill 은 프리팹에서 연결한다. 미연결이어도(프로토 프리미티브) 널 안전.
    public sealed class HpBar : MonoBehaviour
    {
        [SerializeField] private Transform fill;
        [SerializeField] private GameObject root; // 바 전체(가득 찼을 때 숨기고 싶으면 사용)

        public void SetRatio(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (fill != null)
            {
                Vector3 s = fill.localScale;
                s.x = ratio;
                fill.localScale = s;
            }
            if (root != null) root.SetActive(ratio < 1f);
        }

        public void ResetFull()
        {
            SetRatio(1f);
        }
    }
}
