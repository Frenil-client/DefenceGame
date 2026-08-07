using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Linter
{
    // STEP 1. 기반 도구 - BALANCE_SPEC.md 8장 INV-01 부터 INV-10 검증.
    //
    // 분류:
    //   Authoritative - 확정 데이터(ch3/ch4 구조)에만 의존한다. 위반 시 빌드를 막는다.
    //   Provisional   - TEMP 수치나 미정의 모델에 의존한다. 위반해도 경고만 낸다(데이터가 아직 임시라서).
    public enum Severity
    {
        Authoritative,
        Provisional
    }

    public sealed class InvResult
    {
        public string id;
        public bool passed;
        public Severity severity;
        public List<string> messageList = new List<string>();
    }

    public static class Invariants
    {
        // 등급 순서. INV-06 단조성 판정에 사용한다.
        private static readonly Grade[] gradeOrder =
        {
            Grade.Common, Grade.Rare, Grade.Unique, Grade.Hidden
        };

        public static List<InvResult> RunAll(GameDatabase db)
        {
            // ---- 전처리: 조회용 딕셔너리 (ARCHITECTURE.md 4-4 - 순회 순서에 의존하지 않는다) ----
            Dictionary<string, UnitData> unitById = new Dictionary<string, UnitData>();
            foreach (var unit in db.unitList)
            {
                if (unit == null || string.IsNullOrEmpty(unit.id)) continue;
                unitById[unit.id] = unit;
            }

            Dictionary<string, RecipeData> recipeByResult = new Dictionary<string, RecipeData>();
            foreach (var recipe in db.recipeList)
            {
                if (recipe == null || string.IsNullOrEmpty(recipe.resultId)) continue;
                recipeByResult[recipe.resultId] = recipe;
            }

            List<InvResult> resultList = new List<InvResult>();
            resultList.Add(CheckInv01(db, unitById));
            resultList.Add(CheckInv02(db, unitById));
            resultList.Add(CheckInv03(db, unitById));
            resultList.Add(CheckInv04(db, unitById));
            resultList.Add(CheckInv05(db, unitById, recipeByResult));
            resultList.Add(CheckInv06(db));
            resultList.Add(CheckInv07(db));
            resultList.Add(CheckInv08(db, unitById, recipeByResult));
            resultList.Add(CheckInv09(db));
            resultList.Add(CheckInv10(db));
            return resultList;
        }

        // INV-01: 모든 흔함 유닛은 최소 1개의 레어 레시피에 재료로 등장한다 (죽은 흔함 금지).
        // 완화 사유: 지속(C03)/관통(C05) 같은 의도적 희소 역할은 로스터상 레어 레시피가 1개뿐이라
        //           >=2 는 수학적으로 불가능하고 4장 희소성 장치와도 모순된다 (BALANCE_SPEC.md 8 참조).
        private static InvResult CheckInv01(GameDatabase db, Dictionary<string, UnitData> unitById)
        {
            InvResult r = new InvResult { id = "INV-01", severity = Severity.Authoritative, passed = true };
            foreach (var unit in db.unitList)
            {
                if (unit.grade != Grade.Common) continue;

                int count = 0;
                foreach (var recipe in db.recipeList)
                {
                    if (GradeOfResult(recipe, unitById) != Grade.Rare) continue;
                    if (recipe.mat1 == unit.id || recipe.mat2 == unit.id) ++count;
                }
                if (count < 1)
                {
                    r.passed = false;
                    r.messageList.Add(unit.id + " (" + unit.name + ") 는 어떤 레어 레시피에도 등장하지 않음 (죽은 흔함)");
                }
            }
            return r;
        }

        // INV-02: 모든 레어 유닛은 최소 1개의 유니크 또는 히든 레시피에 등장한다.
        private static InvResult CheckInv02(GameDatabase db, Dictionary<string, UnitData> unitById)
        {
            InvResult r = new InvResult { id = "INV-02", severity = Severity.Authoritative, passed = true };
            foreach (var unit in db.unitList)
            {
                if (unit.grade != Grade.Rare) continue;

                int count = 0;
                foreach (var recipe in db.recipeList)
                {
                    var g = GradeOfResult(recipe, unitById);
                    if (g != Grade.Unique && g != Grade.Hidden) continue;
                    if (recipe.mat1 == unit.id || recipe.mat2 == unit.id) ++count;
                }
                if (count < 1)
                {
                    r.passed = false;
                    r.messageList.Add(unit.id + " (" + unit.name + ") 는 유니크/히든 레시피에 등장하지 않음 (고아 레어)");
                }
            }
            return r;
        }

        // INV-03: 모든 유니크 유닛은 최소 1개의 히든 레시피에 등장한다.
        // 완화 사유: 4-4장 '재료 중복이 깊이를 만든다' 의도 + H06 의 유니크 2기 특수구조 +
        //           냉기 유니크가 U02 하나뿐이라 두 히든(H02,H06)에 냉기를 대려면 중복이 불가피하다.
        //           '정확히 1' 대신 '1 이상'으로 완화한다 (BALANCE_SPEC.md 8 참조).
        private static InvResult CheckInv03(GameDatabase db, Dictionary<string, UnitData> unitById)
        {
            InvResult r = new InvResult { id = "INV-03", severity = Severity.Authoritative, passed = true };
            foreach (var unit in db.unitList)
            {
                if (unit.grade != Grade.Unique) continue;

                int count = 0;
                foreach (var recipe in db.recipeList)
                {
                    if (GradeOfResult(recipe, unitById) != Grade.Hidden) continue;
                    if (recipe.mat1 == unit.id || recipe.mat2 == unit.id) ++count;
                }
                if (count < 1)
                {
                    r.passed = false;
                    r.messageList.Add(unit.id + " (" + unit.name + ") 는 어떤 히든 레시피에도 등장하지 않음 (>=1 필요)");
                }
            }
            return r;
        }

        // INV-04: 어떤 속성도 전체 레시피 등장 횟수가 평균의 1.5배를 넘지 않는다.
        private static InvResult CheckInv04(GameDatabase db, Dictionary<string, UnitData> unitById)
        {
            InvResult r = new InvResult { id = "INV-04", severity = Severity.Provisional, passed = true };

            Dictionary<Element, int> countByElement = new Dictionary<Element, int>();
            int total = 0;
            foreach (var recipe in db.recipeList)
            {
                total += AddElementCount(countByElement, recipe.mat1, unitById);
                total += AddElementCount(countByElement, recipe.mat2, unitById);
            }
            if (total == 0) return r;

            double average = (double)total / 5.0;
            double threshold = average * 1.5;

            foreach (var pair in countByElement)
            {
                if (pair.Value > threshold)
                {
                    r.passed = false;
                    r.messageList.Add(pair.Key + " 등장 " + pair.Value + "회 > 임계 " + threshold.ToString("0.0") + " (평균 " + average.ToString("0.0") + ")");
                }
            }
            r.messageList.Add("[note] 레어 이상 element 는 TEMP 추론값이라 결과는 잠정적이다.");
            return r;
        }

        // INV-05: 보스 1의 요구 조건을 만족하는 조합 경로가 2개 이상 존재한다.
        // 보스 1은 관통(전격, C05 계열) 또는 방깎(신성, C09 계열)을 요구한다 (BALANCE_SPEC.md 7-2).
        // 관통은 역할이 아니라 전격 속성 효과이므로, 두 경로 모두 씨앗 흔함(C05/C09) 계보로 판정한다.
        private static InvResult CheckInv05(GameDatabase db, Dictionary<string, UnitData> unitById, Dictionary<string, RecipeData> recipeByResult)
        {
            InvResult r = new InvResult { id = "INV-05", severity = Severity.Authoritative, passed = true };

            int piercePaths = 0;
            int armorPaths = 0;
            foreach (var recipe in db.recipeList)
            {
                List<string> commons = new List<string>();
                CollectTransitiveCommons(recipe.resultId, recipeByResult, unitById, commons, 0);
                if (ContainsId(commons, "C05")) ++piercePaths;
                if (ContainsId(commons, "C09")) ++armorPaths;
            }

            // 흔함 원본 자체도 하나의 해법이다 (C05 관통, C09 방깎).
            if (unitById.ContainsKey("C05")) ++piercePaths;
            if (unitById.ContainsKey("C09")) ++armorPaths;

            if (piercePaths < 1 || armorPaths < 1)
            {
                r.passed = false;
                r.messageList.Add("관통 경로 " + piercePaths + "개, 방깎 경로 " + armorPaths + "개 (각 >=1, 합 >=2 필요)");
            }
            else
            {
                r.messageList.Add("관통 경로 " + piercePaths + "개, 방깎 경로 " + armorPaths + "개");
            }
            return r;
        }

        // INV-06: 배치 코스트는 등급 상승에 따라 단조 증가한다.
        private static InvResult CheckInv06(GameDatabase db)
        {
            InvResult r = new InvResult { id = "INV-06", severity = Severity.Authoritative, passed = true };

            int previousMax = int.MinValue;
            Grade previousGrade = Grade.Common;
            bool hasPrevious = false;

            for (int i = 0; i < gradeOrder.Length; ++i)
            {
                var grade = gradeOrder[i];
                int gradeMin = int.MaxValue;
                int gradeMax = int.MinValue;
                bool found = false;
                foreach (var unit in db.unitList)
                {
                    if (unit.grade != grade) continue;
                    found = true;
                    if (unit.cost < gradeMin) gradeMin = unit.cost;
                    if (unit.cost > gradeMax) gradeMax = unit.cost;
                }
                if (!found) continue;

                if (hasPrevious && gradeMin <= previousMax)
                {
                    r.passed = false;
                    r.messageList.Add(grade + " 최소 코스트 " + gradeMin + " <= " + previousGrade + " 최대 코스트 " + previousMax + " (단조 증가 위반)");
                }
                previousMax = gradeMax;
                previousGrade = grade;
                hasPrevious = true;
            }
            return r;
        }

        // INV-07: 히든 유닛의 기대 DPS 는 유니크의 1.6배 이상 2.2배 이하.
        private static InvResult CheckInv07(GameDatabase db)
        {
            InvResult r = new InvResult { id = "INV-07", severity = Severity.Provisional, passed = true };

            long sumHidden = 0; int countHidden = 0;
            long sumUnique = 0; int countUnique = 0;
            foreach (var unit in db.unitList)
            {
                long dps = (unit.atk * unit.atkSpeed).raw;
                if (unit.grade == Grade.Hidden) { sumHidden += dps; ++countHidden; }
                else if (unit.grade == Grade.Unique) { sumUnique += dps; ++countUnique; }
            }

            if (countHidden == 0 || countUnique == 0 || sumUnique == 0)
            {
                r.passed = false;
                r.messageList.Add("히든/유니크 DPS 표본 부족");
                return r;
            }

            // avgHidden/avgUnique 를 나눗셈 없이 교차 곱으로 비교한다.
            long lhs = sumHidden * countUnique;
            long unit16 = 16L * sumUnique * countHidden; // 1.6 = 16/10
            long unit22 = 22L * sumUnique * countHidden; // 2.2 = 22/10
            long lhs10 = lhs * 10L;

            if (lhs10 < unit16 || lhs10 > unit22)
            {
                r.passed = false;
                r.messageList.Add("히든/유니크 DPS 비율이 [1.6, 2.2] 밖 (atk 은 TEMP 값)");
            }
            r.messageList.Add("[note] atk/atkSpeed 는 TEMP 값이라 결과는 잠정적이다.");
            return r;
        }

        // INV-08: 모든 히든 레시피의 재료 총량 기준 도달 추정 웨이브가 12 이상 22 이하.
        private static InvResult CheckInv08(GameDatabase db, Dictionary<string, UnitData> unitById, Dictionary<string, RecipeData> recipeByResult)
        {
            InvResult r = new InvResult { id = "INV-08", severity = Severity.Provisional, passed = true };

            foreach (var recipe in db.recipeList)
            {
                UnitData resultUnit;
                if (!unitById.TryGetValue(recipe.resultId, out resultUnit)) continue;
                if (resultUnit.grade != Grade.Hidden) continue;

                List<string> commons = new List<string>();
                CollectTransitiveCommons(recipe.resultId, recipeByResult, unitById, commons, 0);

                // [TEMP 모델] 도달 추정 웨이브 = 소모 흔함 총량 (웨이브당 흔함 1기 지급 기준, BALANCE_SPEC.md 6-1).
                // 실제 추정식은 문서에 정의되어 있지 않다. STEP 3/6 에서 확정한다.
                int estWave = commons.Count;
                if (estWave < 12 || estWave > 22)
                {
                    r.passed = false;
                    r.messageList.Add(recipe.resultId + " (" + resultUnit.name + ") 흔함 총량 " + estWave + " -> 추정 웨이브 범위 [12,22] 밖");
                }
            }
            r.messageList.Add("[note] 도달 추정식이 문서에 미정의다. 흔함 총량 1:1 TEMP 모델로 계산했다.");
            return r;
        }

        // INV-09: 근접칸과 원거리칸 배치 유닛 수의 비율이 등급별로 0.6 에서 1.6 사이.
        private static InvResult CheckInv09(GameDatabase db)
        {
            InvResult r = new InvResult { id = "INV-09", severity = Severity.Provisional, passed = true };

            for (int i = 0; i < gradeOrder.Length; ++i)
            {
                var grade = gradeOrder[i];
                int melee = 0;
                int ranged = 0;
                foreach (var unit in db.unitList)
                {
                    if (unit.grade != grade) continue;
                    if (unit.placement == Placement.Melee) ++melee;
                    else ++ranged;
                }
                if (melee == 0 && ranged == 0) continue;
                if (ranged == 0)
                {
                    r.passed = false;
                    r.messageList.Add(grade + " 원거리 0 (비율 산정 불가)");
                    continue;
                }
                double ratio = (double)melee / ranged;
                if (ratio < 0.6 || ratio > 1.6)
                {
                    r.passed = false;
                    r.messageList.Add(grade + " 근접/원거리 비율 " + ratio.ToString("0.00") + " (근접 " + melee + " 원거리 " + ranged + ") [0.6,1.6] 밖");
                }
            }
            r.messageList.Add("[note] 레어 이상 placement 는 TEMP 추론값이라 결과는 잠정적이다.");
            return r;
        }

        // INV-10: 보장 규칙 G1 에서 G5 는 뽑기 풀 구성으로 실현 가능하다.
        private static InvResult CheckInv10(GameDatabase db)
        {
            InvResult r = new InvResult { id = "INV-10", severity = Severity.Authoritative, passed = true };

            int commonMelee = 0;
            int commonRanged = 0;
            int distinctCommon = 0;
            bool hasPierce = false;   // C05 계열 (관통)
            bool hasArmorBreak = false; // C09 계열 (방깎)
            Dictionary<Element, int> commonByElement = new Dictionary<Element, int>();

            foreach (var unit in db.unitList)
            {
                if (unit.grade != Grade.Common) continue;
                ++distinctCommon;
                if (unit.placement == Placement.Melee) ++commonMelee; else ++commonRanged;
                if (unit.id == "C05") hasPierce = true;
                if (unit.id == "C09") hasArmorBreak = true;
                if (!commonByElement.ContainsKey(unit.element)) commonByElement[unit.element] = 0;
                commonByElement[unit.element] += 1;
            }

            // G1: 근접 최소 2종, 원거리 최소 2종 지급 가능
            if (commonMelee < 2 || commonRanged < 2)
            {
                r.passed = false;
                r.messageList.Add("G1 불가: 흔함 근접 " + commonMelee + " 원거리 " + commonRanged + " (각 >=2 필요)");
            }
            // G2: C05 관통 또는 C09 방깎 중 최소 1종이 풀에 존재해야 보장 가능
            if (!hasPierce && !hasArmorBreak)
            {
                r.passed = false;
                r.messageList.Add("G2 불가: 풀에 C05(관통)도 C09(방깎)도 없음");
            }
            // G3: 4연속 동일 회피 -> 흔함 종수 2 이상
            if (distinctCommon < 2)
            {
                r.passed = false;
                r.messageList.Add("G3 불가: 흔함 종수 " + distinctCommon + " (>=2 필요)");
            }
            // G4: 최근 6회 동일 속성 4 초과 회피 -> 속성 종수 2 이상
            if (commonByElement.Count < 2)
            {
                r.passed = false;
                r.messageList.Add("G4 불가: 흔함 속성 종수 " + commonByElement.Count + " (>=2 필요)");
            }
            // G5: 12웨이브 이전 레어 조합 최소 2회 가능 -> 레어 레시피 2개 이상 존재
            int rareRecipeCount = 0;
            foreach (var recipe in db.recipeList)
            {
                if (!recipe.isHidden && recipe.conditionType != ConditionType.Fixed) ++rareRecipeCount;
            }
            if (rareRecipeCount < 2)
            {
                r.passed = false;
                r.messageList.Add("G5 불가: 레어 레시피 " + rareRecipeCount + "개 (>=2 필요)");
            }

            if (r.passed) r.messageList.Add("G1-G5 정적 실현 가능성 충족");
            return r;
        }

        // ---- 헬퍼 ----

        private static Grade GradeOfResult(RecipeData recipe, Dictionary<string, UnitData> unitById)
        {
            UnitData unit;
            if (unitById.TryGetValue(recipe.resultId, out unit)) return unit.grade;
            return Grade.Common; // 알 수 없는 결과는 흔함으로 취급(등장 카운트에서 제외되도록)
        }

        private static int AddElementCount(Dictionary<Element, int> countByElement, string unitId, Dictionary<string, UnitData> unitById)
        {
            UnitData unit;
            if (!unitById.TryGetValue(unitId, out unit)) return 0;
            if (!countByElement.ContainsKey(unit.element)) countByElement[unit.element] = 0;
            countByElement[unit.element] += 1;
            return 1;
        }

        private static bool ContainsId(List<string> list, string id)
        {
            for (int i = 0; i < list.Count; ++i)
            {
                if (list[i] == id) return true;
            }
            return false;
        }

        // 결과 id 의 재료를 흔함 단위까지 전개해 목록에 채운다(중복 포함). depth 로 순환을 방어한다.
        private static void CollectTransitiveCommons(string id, Dictionary<string, RecipeData> recipeByResult, Dictionary<string, UnitData> unitById, List<string> output, int depth)
        {
            if (depth > 16) return; // 데이터 오류로 인한 순환 방어

            UnitData unit;
            if (unitById.TryGetValue(id, out unit) && unit.grade == Grade.Common)
            {
                output.Add(id);
                return;
            }

            RecipeData recipe;
            if (!recipeByResult.TryGetValue(id, out recipe))
            {
                return; // 레시피가 없으면 더 전개할 수 없다
            }
            CollectTransitiveCommons(recipe.mat1, recipeByResult, unitById, output, depth + 1);
            CollectTransitiveCommons(recipe.mat2, recipeByResult, unitById, output, depth + 1);
        }
    }
}
