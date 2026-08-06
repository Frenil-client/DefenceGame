using System;
using System.Collections.Generic;
using System.IO;
using Synthesis.Core.Data;

namespace Synthesis.Linter
{
    // STEP 1. 기반 도구 - 린터 진입점.
    // 사용: synthesis-linter [dataDir]
    //   dataDir 생략 시 현재 위치에서 위로 올라가며 Data/units.csv 를 찾는다.
    // 종료 코드: Authoritative 불변식 위반이 하나라도 있으면 1, 아니면 0.
    public static class Program
    {
        public static int Main(string[] args)
        {
            string dataDir = args.Length >= 1 ? args[0] : DataLoader.FindDataDir(Directory.GetCurrentDirectory());
            if (string.IsNullOrEmpty(dataDir) || !Directory.Exists(dataDir))
            {
                Console.Error.WriteLine("[linter] Data 디렉터리를 찾지 못했습니다. 인자로 경로를 주세요.");
                return 2;
            }

            Console.WriteLine("[linter] Data 경로: " + dataDir);
            GameDatabase db = DataLoader.Load(dataDir);
            Console.WriteLine("[linter] 로드: 유닛 " + db.unitList.Count + " / 레시피 " + db.recipeList.Count
                + " / 웨이브 " + db.waveList.Count + " / 보스 " + db.bossList.Count + " / 유물 " + db.relicList.Count);
            Console.WriteLine("");

            List<InvResult> resultList = Invariants.RunAll(db);

            int authoritativeFail = 0;
            int provisionalFail = 0;
            foreach (var result in resultList)
            {
                string tag;
                if (result.passed) tag = "PASS";
                else if (result.severity == Severity.Authoritative) tag = "FAIL";
                else tag = "WARN";

                Console.WriteLine("[" + tag + "] " + result.id + " (" + result.severity + ")");
                foreach (var message in result.messageList)
                {
                    Console.WriteLine("        " + message);
                }

                if (!result.passed && result.severity == Severity.Authoritative) ++authoritativeFail;
                if (!result.passed && result.severity == Severity.Provisional) ++provisionalFail;
            }

            Console.WriteLine("");
            Console.WriteLine("[linter] 요약: Authoritative 실패 " + authoritativeFail + " / Provisional 경고 " + provisionalFail);

            if (authoritativeFail > 0)
            {
                Console.WriteLine("[linter] 결과: FAIL (구조 불변식 위반 - BALANCE_SPEC 수정 필요)");
                return 1;
            }
            Console.WriteLine("[linter] 결과: PASS (구조 불변식 통과. Provisional 경고는 TEMP 데이터 기준)");
            return 0;
        }
    }
}
