using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Core.Text
{
    // STEP 3. 기반 도구 - 문자열 테이블. Data/strings.csv 한 벌을 읽어 키로 조회한다.
    // Core 에 두는 이유: 린터가 키 누락과 치환자 오타를 빌드 전에 검사할 수 있어야 한다(CLAUDE.md 4-7).
    // UnityEngine 을 참조하지 않으므로 헤드리스 테스트에서도 그대로 돈다.
    public enum Language
    {
        Ko,
        En
    }

    public sealed class StringEntry
    {
        public string key;
        public string ko;
        public string en;

        // 요청한 언어의 값. 비어 있으면 ko 로 폴백한다(번역 전 항목이 화면에서 사라지지 않게).
        public string GetText(Language language)
        {
            if (language == Language.En && !string.IsNullOrEmpty(en)) return en;
            return ko;
        }
    }

    public sealed class StringTable
    {
        private readonly Dictionary<string, StringEntry> entryByKey = new Dictionary<string, StringEntry>();
        private readonly List<StringEntry> entryList = new List<StringEntry>();

        public int Count => entryList.Count;
        public IReadOnlyList<StringEntry> EntryList => entryList;

        public void Add(StringEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key)) return;
            if (entryByKey.ContainsKey(entry.key)) return; // 먼저 온 항목을 유지한다(중복은 린터가 잡는다)

            entryByKey[entry.key] = entry;
            entryList.Add(entry);
        }

        public bool Contains(string key)
        {
            return !string.IsNullOrEmpty(key) && entryByKey.ContainsKey(key);
        }

        public bool TryGet(string key, out StringEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(key)) return false;
            return entryByKey.TryGetValue(key, out entry);
        }

        // 키에 해당하는 문자열. 없으면 키 자체를 돌려준다. 크래시 대신 화면에 키가 보여 누락이 드러난다.
        public string Get(string key, Language language)
        {
            StringEntry entry;
            if (!TryGet(key, out entry)) return key;
            return entry.GetText(language);
        }

        // strings.csv: key,ko,en (값에 쉼표가 들어갈 수 있어 따옴표 인식 분해를 쓴다)
        public static StringTable Load(string fileText)
        {
            StringTable table = new StringTable();
            foreach (var line in CsvUtil.CsvToDataLines(fileText))
            {
                var split = CsvUtil.SplitCsvFields(line);
                if (split.Count < 2)
                {
                    continue;
                }

                StringEntry entry = new StringEntry();
                entry.key = split[0].Trim();
                entry.ko = split[1];
                entry.en = split.Count > 2 ? split[2] : "";
                table.Add(entry);
            }
            return table;
        }
    }
}
