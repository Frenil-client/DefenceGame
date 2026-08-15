using UnityEngine;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 뷰 컴포넌트 - 순회 몬스터 GameObject 에 붙어 자기 모델(LoopMonster)을 참조한다.
    // 인스펙터 확인용. HP 표시는 HUD(MonsterHealthBarHud)가 담당한다. 상태는 Core(LoopMonster)가 소유한다.
    public sealed class MonsterView : MonoBehaviour, IPoolable
    {
        [Header("프리팹 연결(선택)")]
        [SerializeField] private Transform model;

        [Header("모델 참조(런타임)")]
        public LoopMonster monster;
        public string enemyId;
        public int waypointIndex;

        public void Bind(LoopMonster m)
        {
            monster = m;
            enemyId = m != null ? m.enemyId : "";
        }

        // 매 프레임 라이브 값 갱신(인스펙터 표시용).
        public void Refresh()
        {
            if (monster == null) return;
            waypointIndex = monster.waypointIndex;
        }

        public Renderer GetModelRenderer()
        {
            return model != null ? model.GetComponent<Renderer>() : null;
        }

        public void Unbind()
        {
            monster = null;
            enemyId = "";
        }

        // ---- IPoolable ----
        public void OnSpawn()
        {
            if (model != null) model.gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            Unbind();
        }
    }
}
