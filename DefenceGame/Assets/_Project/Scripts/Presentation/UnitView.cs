using UnityEngine;
using Synthesis.Core.Data;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 뷰 컴포넌트 - 배치 유닛 GameObject 에 붙어 자기 모델(LoopUnit)을 참조한다.
    // 인스펙터 확인, 클릭 선택, 사거리 표시의 앵커. 상태는 Core(LoopUnit)가 소유한다.
    public sealed class UnitView : MonoBehaviour, IPoolable
    {
        [Header("프리팹 연결(선택)")]
        [SerializeField] private Transform model;
        [SerializeField] private RangeIndicator rangeIndicator;

        [Header("모델 참조(런타임)")]
        public LoopUnit unit;
        public string unitId;
        public int cellX;
        public int cellY;

        public void Bind(LoopUnit u)
        {
            unit = u;
            UnitData data = u != null ? u.data : null;
            unitId = data != null ? data.id : "";
            cellX = u != null ? u.cellX : 0;
            cellY = u != null ? u.cellY : 0;
        }

        public void Unbind()
        {
            unit = null;
            unitId = "";
        }

        public Renderer GetModelRenderer()
        {
            return model != null ? model.GetComponent<Renderer>() : null;
        }

        // 선택 표시 - 일반 공격 사거리 링. 반경은 호출자가 준다(오라 반경은 그리지 않는다).
        public void ShowRange(float radius)
        {
            if (rangeIndicator == null) return;
            if (radius <= 0f) { rangeIndicator.Hide(); return; }
            rangeIndicator.Show(radius);
        }

        public void HideRange()
        {
            if (rangeIndicator != null) rangeIndicator.Hide();
        }

        // ---- IPoolable ----
        public void OnSpawn()
        {
            if (model != null) model.gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            HideRange();
            Unbind();
        }
    }
}
