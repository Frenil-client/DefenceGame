using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core.Text;

namespace Synthesis.Presentation
{
    // STEP 3. 핵심 - 문자열 표시의 유일한 창구.
    //   화면에 나가는 텍스트는 반드시 여기를 거친다. 코드에 한국어를 직접 쓰지 않는다.
    //   테이블 자체는 Core(StringTable)가 소유하고, 이 클래스는 로딩과 언어 전환, 등록 갱신만 맡는다.
    //
    // MonoBehaviour 가 아니라 static 이다. 에디터 검색 창에서도 씬 없이 테이블을 읽어야 하기 때문이다.
    public static class StringManager
    {
        private static StringTable table;
        private static Language language = Language.Ko;

        // 언어가 바뀌면 다시 칠해야 하는 표시자들. 등록/해제는 StringParser 가 한다.
        private static readonly List<StringParser> parserList = new List<StringParser>();

        public static Language CurrentLanguage => language;

        public static StringTable Table
        {
            get
            {
                if (table == null) Reload();
                return table;
            }
        }

        // Data/strings.csv 를 다시 읽는다. 에디터에서 CSV 를 고친 뒤 즉시 반영할 때도 쓴다.
        public static void Reload()
        {
            table = StringTable.Load(RuntimeDataLoader.LoadStringsText());
            RefreshAll();
        }

        public static void SetLanguage(Language value)
        {
            if (language == value) return;
            language = value;
            RefreshAll();
        }

        // 키에 해당하는 문자열. 키가 없으면 키 자체가 나와 누락이 화면에서 드러난다.
        public static string Get(string key)
        {
            return Table.Get(key, language);
        }

        // 치환자를 채운 문자열. values 가 없으면 치환자가 그대로 남는다.
        public static string Format(string key, IStringValues values)
        {
            return StringFormatter.Format(Table.Get(key, language), values);
        }

        // ---- 스탯 표기: "총합 (증감)". 증감은 긍정 초록, 부정 빨강으로 갈린다. ----
        //   버프가 걸린 상태에서 기본값을 주 숫자로 두면 실제 수치를 알려고 덧셈을 해야 한다.
        //   그래서 총합을 앞에 두고 괄호에 차이만 적는다.
        public static string FormatStat(string labelKey, float baseValue, float effectiveValue, string numberFormat = "0.##")
        {
            float delta = effectiveValue - baseValue;

            statValues.Clear();
            statValues.Set("label", Get(labelKey));
            statValues.Set("value", effectiveValue.ToString(numberFormat));

            if (Mathf.Abs(delta) < 0.005f) return Format("str.stat.plain", statValues);

            statValues.Set("delta", Mathf.Abs(delta).ToString(numberFormat));
            return Format(delta > 0f ? "str.stat.buffed" : "str.stat.debuffed", statValues);
        }

        private static readonly StringValues statValues = new StringValues();

        // ---- 표시자 등록 ----

        public static void Register(StringParser parser)
        {
            if (parser == null || parserList.Contains(parser)) return;
            parserList.Add(parser);
            parser.Apply();
        }

        public static void Unregister(StringParser parser)
        {
            if (parser == null) return;
            parserList.Remove(parser);
        }

        private static void RefreshAll()
        {
            for (int i = parserList.Count - 1; i >= 0; --i)
            {
                StringParser parser = parserList[i];
                if (parser == null)
                {
                    parserList.RemoveAt(i);
                    continue;
                }
                parser.Apply();
            }
        }
    }
}
