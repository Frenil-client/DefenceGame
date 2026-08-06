using System;
using System.IO;

namespace Synthesis.Core.Tests
{
    // STEP 1. 기반 도구 - 테스트에서 저장소의 Data 디렉터리를 찾는 헬퍼.
    public static class TestPaths
    {
        public static string FindDataDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Data");
                if (File.Exists(Path.Combine(candidate, "units.csv")))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Data/units.csv 를 상위 경로에서 찾지 못했습니다.");
        }

        public static string ReadData(string fileName)
        {
            return File.ReadAllText(Path.Combine(FindDataDir(), fileName));
        }
    }
}
