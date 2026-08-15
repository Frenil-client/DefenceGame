using System.Collections.Generic;
using UnityEngine;

namespace Synthesis.Presentation
{
    // UI 허브. 씬에 미리 올려둔 기본 Canvas 위에 팝업 프리팹을 띄운다.
    // 런타임 생성/탐색을 하지 않는다: 기본 Canvas 와 팝업 프리팹은 인스펙터에 직접 등록한다.
    //   - 상시 HUD(HudView/Inventory/MonsterHealthBar): 씬의 Canvas 아래에 프리팹 인스턴스로 미리 배치.
    //   - 독립 UI(팝업/화면): popupPrefabs 에 등록하고 Open(panelId) 로 띄운다(동적이라 인스턴스화만 함).
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private Canvas baseCanvas;
        [Tooltip("팝업 프리팹 목록. 각 프리팹의 panelId 로 Open 한다")]
        [SerializeField] private List<UIPanel> popupPrefabs = new List<UIPanel>();

        private readonly List<UIPanel> stack = new List<UIPanel>();

        public static UIManager Instance { get; private set; }
        public Canvas BaseCanvas => baseCanvas;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 등록된 팝업 프리팹을 기본 Canvas 에 띄운다. 이미 같은 id 가 열려 있으면 앞으로 가져온다.
        public UIPanel Open(string id)
        {
            UIPanel existing = Find(id);
            if (existing != null)
            {
                existing.transform.SetAsLastSibling();
                return existing;
            }

            UIPanel prefab = FindPrefab(id);
            if (prefab == null)
            {
                Debug.LogError("[UIManager] 팝업 프리팹이 등록되지 않았습니다: " + id);
                return null;
            }
            if (baseCanvas == null)
            {
                Debug.LogError("[UIManager] baseCanvas 가 할당되지 않았습니다(인스펙터에서 등록).");
                return null;
            }

            UIPanel panel = Instantiate(prefab, baseCanvas.transform);
            if (string.IsNullOrEmpty(panel.panelId)) panel.panelId = id;
            panel.transform.SetAsLastSibling();
            stack.Add(panel);
            panel.OnOpen(this);
            return panel;
        }

        public void Close(UIPanel panel)
        {
            if (panel == null) return;
            stack.Remove(panel);
            panel.OnClose();
            Destroy(panel.gameObject);
        }

        public void CloseTop()
        {
            if (stack.Count == 0) return;
            Close(stack[stack.Count - 1]);
        }

        public bool IsOpen(string id)
        {
            return Find(id) != null;
        }

        private UIPanel Find(string id)
        {
            for (int i = 0; i < stack.Count; ++i)
            {
                if (stack[i] != null && stack[i].panelId == id) return stack[i];
            }
            return null;
        }

        private UIPanel FindPrefab(string id)
        {
            for (int i = 0; i < popupPrefabs.Count; ++i)
            {
                if (popupPrefabs[i] != null && popupPrefabs[i].panelId == id) return popupPrefabs[i];
            }
            return null;
        }
    }
}
