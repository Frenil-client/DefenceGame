using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Synthesis.Core.Text;
using Synthesis.Presentation;

namespace Synthesis.Editor
{
    // 문자열 테이블 검색 창. 키를 넣을 때 두 방향으로 찾는다.
    //   키로 찾기: "str.stat" 을 넣으면 그 접두사를 가진 항목이 나온다. 값이 준비돼 있는지 확인용.
    //   값으로 찾기: "공격력" 을 넣으면 그 말이 들어간 항목의 키가 나온다. 이미 있는 키를 재사용하려고.
    // 코드에 한국어를 직접 쓰지 않으려면 "이 문장에 쓸 키가 이미 있나" 를 빠르게 확인할 수 있어야 한다.
    public sealed class StringTableWindow : EditorWindow
    {
        private string query = "";
        private Vector2 scroll;
        private StringParser pickTarget; // 인스펙터에서 열었으면 고른 키를 여기에 넣는다

        private readonly List<StringEntry> keyHitList = new List<StringEntry>();
        private readonly List<StringEntry> valueHitList = new List<StringEntry>();

        [MenuItem("Synthesis/String Table")]
        public static void Open()
        {
            GetWindow<StringTableWindow>("String Table").Show();
        }

        // StringParser 인스펙터의 검색 버튼에서 연다. 항목을 고르면 그 컴포넌트의 키가 채워진다.
        public static void OpenForPicker(StringParser target)
        {
            StringTableWindow window = GetWindow<StringTableWindow>("String Table");
            window.pickTarget = target;
            window.Show();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("검색", GUILayout.Width(40f));
                string next = EditorGUILayout.TextField(query);
                if (next != query)
                {
                    query = next;
                    Search();
                }
                if (GUILayout.Button("다시 읽기", GUILayout.Width(80f)))
                {
                    StringManager.Reload();
                    Search();
                }
            }

            EditorGUILayout.LabelField("전체 " + StringManager.Table.Count + "개  |  키로 " + keyHitList.Count + "건, 값으로 " + valueHitList.Count + "건");

            // 적용 대상은 한 번 고른 뒤에도 유지한다. 잘못 눌렀을 때 다시 고를 수 있어야 한다.
            if (pickTarget != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox("적용 대상: " + pickTarget.name + "  (다른 항목을 눌러 바꿀 수 있습니다)", MessageType.Info);
                    if (GUILayout.Button("대상 해제", GUILayout.Width(80f), GUILayout.Height(38f))) pickTarget = null;
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSection("키로 찾은 결과", keyHitList);
            DrawSection("값으로 찾은 결과", valueHitList);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSection(string title, List<StringEntry> list)
        {
            if (list.Count == 0) return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            for (int i = 0; i < list.Count; ++i)
            {
                StringEntry entry = list[i];
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(entry.key, EditorStyles.miniBoldLabel);
                        EditorGUILayout.LabelField("ko  " + entry.ko, EditorStyles.miniLabel);
                        if (!string.IsNullOrEmpty(entry.en)) EditorGUILayout.LabelField("en  " + entry.en, EditorStyles.miniLabel);
                    }
                    if (GUILayout.Button("복사", GUILayout.Width(50f))) EditorGUIUtility.systemCopyBuffer = entry.key;
                    if (pickTarget != null && GUILayout.Button("적용", GUILayout.Width(50f))) ApplyPick(entry.key);
                }
            }
        }

        // 키를 넣고 TMP 텍스트까지 그 자리에서 갱신한다. 실행하지 않아도 씬에서 결과가 보여야 고를 수 있다.
        //   대상은 유지한다. 잘못 눌러도 다른 항목을 바로 다시 적용할 수 있다.
        private void ApplyPick(string key)
        {
            SerializedObject so = new SerializedObject(pickTarget);
            so.FindProperty("key").stringValue = key;
            so.ApplyModifiedProperties();

            pickTarget.Apply();

            EditorUtility.SetDirty(pickTarget);
            if (pickTarget.Target != null) EditorUtility.SetDirty(pickTarget.Target);
            Repaint();
        }

        // 키 검색과 값 검색을 동시에 돌린다. 어느 쪽으로 물어도 답이 나오게 하기 위해서다.
        private void Search()
        {
            keyHitList.Clear();
            valueHitList.Clear();
            if (string.IsNullOrEmpty(query)) return;

            string lower = query.ToLowerInvariant();
            var entries = StringManager.Table.EntryList;
            for (int i = 0; i < entries.Count; ++i)
            {
                StringEntry entry = entries[i];
                if (entry.key.ToLowerInvariant().Contains(lower))
                {
                    keyHitList.Add(entry);
                    continue;
                }
                if (Matches(entry.ko, lower) || Matches(entry.en, lower)) valueHitList.Add(entry);
            }
        }

        private static bool Matches(string text, string lowerQuery)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.ToLowerInvariant().Contains(lowerQuery);
        }
    }
}
