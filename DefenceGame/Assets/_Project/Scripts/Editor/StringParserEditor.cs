using UnityEditor;
using UnityEngine;
using Synthesis.Presentation;

namespace Synthesis.Editor
{
    // StringParser 인스펙터. 등록한 키가 실제로 테이블에 있는지 그 자리에서 보여준다.
    //   키 오타는 실행해 봐야 드러나는 종류의 실수라, 인스펙터에서 미리 잡는다.
    [CustomEditor(typeof(StringParser))]
    public sealed class StringParserEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();

            StringParser parser = (StringParser)target;

            // 인스펙터에서 키를 직접 고쳐도 TMP 에 바로 반영한다. 실행하지 않고 결과를 보려고.
            if (EditorGUI.EndChangeCheck())
            {
                parser.Apply();
                if (parser.Target != null) EditorUtility.SetDirty(parser.Target);
            }

            string key = parser.Key;

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("문자열 테이블에서 찾기")) StringTableWindow.OpenForPicker(parser);

            if (string.IsNullOrEmpty(key))
            {
                EditorGUILayout.HelpBox("키가 비어 있습니다.", MessageType.Warning);
                return;
            }

            if (!StringManager.Table.Contains(key))
            {
                EditorGUILayout.HelpBox("테이블에 없는 키입니다: " + key, MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("미리보기", StringManager.Get(key));
        }
    }
}
