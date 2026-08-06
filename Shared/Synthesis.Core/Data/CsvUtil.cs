using System;
using System.Collections.Generic;
using System.Globalization;

namespace Synthesis.Core.Data
{
    // STEP 1. 기반 도구 - CSV 저수준 파싱 헬퍼.
    // 규칙: '#' 로 시작하는 줄은 주석, 빈 줄은 무시, 첫 데이터 줄은 헤더로 보고 버린다.
    public static class CsvUtil
    {
        // 파일 전체 텍스트를 데이터 줄 목록으로 변환한다. 주석/빈 줄 제거 후 첫 줄(헤더)을 버린다.
        // 각 줄의 필드 분해는 CsvToXxx 파서가 담당한다 (var split = line.Split(',')).
        public static List<string> CsvToDataLines(string text)
        {
            List<string> lineList = new List<string>();
            if (string.IsNullOrEmpty(text)) return lineList;

            var lineSplit = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            bool headerSkipped = false;
            for (int i = 0; i < lineSplit.Length; ++i)
            {
                var line = lineSplit[i].Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                if (line[0] == '#')
                {
                    continue;
                }
                if (!headerSkipped)
                {
                    headerSkipped = true;
                    continue;
                }
                lineList.Add(line);
            }

            return lineList;
        }

        public static int StringToInt(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        public static bool StringToBool(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var lower = value.Trim().ToLowerInvariant();
            return lower == "true" || lower == "1" || lower == "yes";
        }

        // "1.5" 같은 십진 문자열을 스케일 1000 고정소수점 raw 로 변환한다.
        // 부동소수점을 거치지 않고 정수 파싱만으로 처리해 재현성을 지킨다.
        public static Fixed StringToFixed(string value)
        {
            if (string.IsNullOrEmpty(value)) return Fixed.Zero;

            var text = value.Trim();
            bool negative = false;
            int start = 0;
            if (text[0] == '-')
            {
                negative = true;
                start = 1;
            }
            else if (text[0] == '+')
            {
                start = 1;
            }

            long whole = 0;
            long frac = 0;
            long fracDiv = 1;
            bool afterDot = false;

            for (int i = start; i < text.Length; ++i)
            {
                var c = text[i];
                if (c == '.')
                {
                    afterDot = true;
                    continue;
                }
                if (c < '0' || c > '9')
                {
                    throw new FormatException("StringToFixed invalid char in '" + value + "'");
                }

                int digit = c - '0';
                if (!afterDot)
                {
                    whole = whole * 10 + digit;
                }
                else if (fracDiv < Fixed.Scale)
                {
                    frac = frac * 10 + digit;
                    fracDiv *= 10;
                }
                // 스케일 1000 을 넘어가는 소수 자리는 버린다(반올림 없음, 결정적).
            }

            long fracRaw = frac * (Fixed.Scale / fracDiv);
            long raw = whole * Fixed.Scale + fracRaw;
            if (negative) raw = -raw;
            return Fixed.FromRaw(raw);
        }
    }
}
