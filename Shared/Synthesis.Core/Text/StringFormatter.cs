using System.Collections.Generic;
using System.Text;

namespace Synthesis.Core.Text
{
    // STEP 3. 기반 도구 - 치환자 해석. "{name}" 을 값으로 바꾼다.
    //   문자열 안에 치환자를 두면 번역자가 어순을 통제하고 코드는 숫자만 공급한다.
    //   정규식을 쓰지 않고 직접 훑는다. HUD 가 매 프레임 부르므로 Regex 객체 할당이 없어야 한다.
    //   중첩이나 파이프 구문은 두지 않는다. 우리 스킬은 효과 하나에 파라미터 몇 개라 한 단계면 충분하다.
    public interface IStringValues
    {
        bool TryGetValue(string name, out string value);
    }

    public static class StringFormatter
    {
        // 값을 못 찾은 치환자는 그대로 남긴다. 화면에 "{radius}" 가 보여 누락이 드러난다.
        public static string Format(string text, IStringValues values)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.IndexOf('{') < 0) return text;

            StringBuilder builder = new StringBuilder(text.Length + 16);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c != '{')
                {
                    builder.Append(c);
                    ++i;
                    continue;
                }

                int close = text.IndexOf('}', i + 1);
                if (close < 0)
                {
                    // 닫히지 않은 중괄호는 리터럴로 본다.
                    builder.Append(text, i, text.Length - i);
                    break;
                }

                var name = text.Substring(i + 1, close - i - 1).Trim();
                string value;
                if (values != null && values.TryGetValue(name, out value)) builder.Append(value);
                else builder.Append('{').Append(name).Append('}');

                i = close + 1;
            }
            return builder.ToString();
        }

        // 문자열이 요구하는 치환자 이름 목록. 린터가 정의되지 않은 이름을 잡는 데 쓴다.
        public static List<string> CollectPlaceholders(string text)
        {
            List<string> nameList = new List<string>();
            if (string.IsNullOrEmpty(text)) return nameList;

            int i = 0;
            while (i < text.Length)
            {
                if (text[i] != '{')
                {
                    ++i;
                    continue;
                }
                int close = text.IndexOf('}', i + 1);
                if (close < 0)
                {
                    break;
                }
                nameList.Add(text.Substring(i + 1, close - i - 1).Trim());
                i = close + 1;
            }
            return nameList;
        }
    }

    // 이름 하나에 값 하나를 담는 기본 공급자. 표시 코드가 몇 개만 채워 넘길 때 쓴다.
    public sealed class StringValues : IStringValues
    {
        private readonly Dictionary<string, string> valueByName = new Dictionary<string, string>();

        public StringValues Set(string name, string value)
        {
            valueByName[name] = value;
            return this;
        }

        public StringValues Clear()
        {
            valueByName.Clear();
            return this;
        }

        public bool TryGetValue(string name, out string value)
        {
            return valueByName.TryGetValue(name, out value);
        }
    }
}
