using UnityEngine;
using TMPro;
using Synthesis.Core.Text;

namespace Synthesis.Presentation
{
    // STEP 3. 뷰 - 텍스트 하나에 문자열 키를 물린다.
    //   인스펙터에 키를 등록해 두면 실행 시 현재 언어의 값이 들어간다. 프리팹에 한국어를 박지 않는다.
    //   키는 에디터의 Synthesis/String Table 창에서 찾아 넣는다(값으로 유사 키 검색도 된다).
    //   TMP_Text 는 추상 타입이라 RequireComponent 로 강제하지 못한다. 없으면 조용히 아무것도 하지 않는다.
    public sealed class StringParser : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private TMP_Text target;

        private IStringValues values; // 치환자가 있는 문자열용. 없으면 키 값을 그대로 넣는다

        public string Key => key;

        private void Reset()
        {
            target = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (target == null) target = GetComponent<TMP_Text>();
            StringManager.Register(this);
        }

        private void OnDisable()
        {
            StringManager.Unregister(this);
        }

        // 런타임에 키를 갈아끼운다. 같은 텍스트가 상황에 따라 다른 문자열을 쓸 때.
        public void SetKey(string value)
        {
            key = value;
            Apply();
        }

        // 치환자 값을 넣는다. 수치가 바뀔 때마다 다시 부른다.
        public void SetValues(IStringValues value)
        {
            values = value;
            Apply();
        }

        public void Apply()
        {
            // 에디터에서 컴포넌트를 막 붙였거나 프리팹이 옛날 것이면 target 이 비어 있을 수 있다.
            if (target == null) target = GetComponent<TMP_Text>();
            if (target == null || string.IsNullOrEmpty(key)) return;

            target.text = values != null ? StringManager.Format(key, values) : StringManager.Get(key);
        }

        public TMP_Text Target => target;
    }
}
