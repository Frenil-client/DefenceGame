using UnityEngine;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 뷰 - 유닛 사거리 원. 선택 중에만 표시한다.
    // ring 은 지면에 눕힌 쿼드이며 원 모양은 텍스처가 만든다(반투명 베이스 + 밝은 가장자리).
    //   실린더 메시로 그리면 분할 수 때문에 각져 보이고 반경이 커질수록 심해진다. 텍스처는 그 문제가 없다.
    //   쿼드는 XY 평면 1x1 이고 -90도 회전으로 눕혀 두었으므로, 지름을 x 와 y 스케일에 그대로 넣는다.
    //
    // 높이는 부모(유닛)를 따라가지 않고 지면에 고정한다.
    //   유닛 오브젝트는 y=0.3 에 놓이는데, 원이 그 높이를 상속하면 몬스터 허리(y=0.35)를 지나는 것처럼 보인다.
    //   원은 바닥 표시이므로 x/z 만 유닛을 따라가고 y 는 지면에 붙어 있어야 한다.
    public sealed class RangeIndicator : MonoBehaviour
    {
        [SerializeField] private Transform ring;
        // [TEMP] 지면 표시 높이. 배치 타일 윗면(0.10) 바로 위다. 바닥 구성이 바뀌면 함께 조정한다.
        [SerializeField] private float groundY = 0.11f;

        private void Awake()
        {
            Hide();
        }

        // radius 는 셀 단위 반경이다. 맵 셀 크기가 1 이므로 월드 단위와 같다.
        public void Show(float radius)
        {
            if (ring == null) return;
            if (radius <= 0f) { Hide(); return; }

            float diameter = radius * 2f;
            ring.localScale = new Vector3(diameter, diameter, 1f);
            ring.gameObject.SetActive(true);
            SnapToGround();
        }

        public void Hide()
        {
            if (ring != null) ring.gameObject.SetActive(false);
        }

        // 유닛이 움직이면 부모를 따라 높이도 딸려 올라가므로 매 프레임 되돌린다.
        // 표시 중인 유닛은 한 번에 하나뿐이라 비용이 문제되지 않는다.
        private void LateUpdate()
        {
            if (ring == null || !ring.gameObject.activeSelf) return;
            SnapToGround();
        }

        private void SnapToGround()
        {
            Vector3 world = ring.position;
            if (world.y == groundY) return;

            world.y = groundY;
            ring.position = world;
        }
    }
}
