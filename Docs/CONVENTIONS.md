# CONVENTIONS.md

SYNTHESIS (가칭) - 코딩 컨벤션과 불변 규칙

배치 위치: Docs/CONVENTIONS.md

코드를 쓸 때 지키는 규칙을 모은 문서다. 어셈블리 구조와 결정성 규칙의 상세는 ARCHITECTURE.md, 게임 사양은 SPEC.md에 있다.
코드와 데이터 파일의 주석은 이 문서의 절 번호로 규칙을 인용한다. 예: CONVENTIONS.md 2-7.

---

## 1. 코딩 컨벤션

이 절의 규칙은 예외 없이 지킨다. 기존 코드와 다르면 코드를 규칙에 맞춘다.

### 1-1. 네이밍

- 한 글자 변수는 좌표와 반복 인덱스에만 쓴다: i, j, x, y, a, b, z
- 그 외 의미 있는 값은 뜻 그대로 camelCase
- min, max, index 같은 관용 축약은 허용: resultMax, minIndex
- 결과 좌표는 소문자 접미사: resultx, resulty
- 보조 메서드는 PascalCase
- 변환 메서드는 XToY 형식: StringToDate, CsvToUnitData
- 접근자는 Get 접두사: GetUnitCost
- 컬렉션은 xList, xDict: unitList, recipeDict

### 1-2. 문법과 서식

- 브레이스는 Allman
- for 증감은 전위 ++i
- 컬렉션과 배열은 명시 타입, 중간 계산값은 var
- 인덱스가 불필요하면 foreach
- 짧은 클램프 if는 단문으로 중괄호 생략
- continue 가드와 상태 변경은 중괄호 사용
- early continue 가드를 선호한다
- 문자열 파싱은 var split = x.Split(...)

```csharp
// STEP 1. 기반 도구 - CSV 한 줄을 유닛 데이터로 변환
public static UnitData CsvToUnitData(string line)
{
    var split = line.Split(',');
    if (split.Length < 14) return null;

    UnitData unitData = new UnitData();
    unitData.id = split[0].Trim();
    unitData.cost = int.Parse(split[6]);

    if (unitData.cost < 0) unitData.cost = 0;

    List<string> tagList = new List<string>();
    for (int i = 13; i < split.Length; ++i)
    {
        var tag = split[i].Trim();
        if (string.IsNullOrEmpty(tag))
        {
            continue;
        }
        tagList.Add(tag);
    }
    unitData.tagList = tagList;

    return unitData;
}
```

### 1-3. 원칙

- **명시적 for를 선호한다.** 함수 내부에서 Func<>과 LINQ를 회피한다
- **비트 연산을 회피한다.** 불가피할 때만 쓰고, 그때는 주석으로 이유를 설명한다. 예외는 시프트와 XOR이 알고리즘 자체인 xorshift PRNG(DeterministicRandom)다
- **STEP 주석을 필수로 단다.** 작성 순서를 기반 도구 -> 뼈대 -> 핵심 -> 검증으로 표기한다
- 날짜와 시각은 단조 증가 정수로 인코딩한다
- 전처리로 배열이나 딕셔너리를 만들어두고 조회하는 패턴을 선호한다

### 1-4. 문서 작성 규칙

md 파일과 주석에 키보드로 단순 입력할 수 없는 특수문자를 쓰지 않는다. 가운뎃점, 엠대시, 화살표, 원문자, 말줄임표 기호 등을 금지한다.

- 단어 나열은 쉼표 또는 슬래시
- 두 단어 연결은 "및", "와/과", 슬래시
- 대시와 구분선은 하이픈
- 화살표가 필요하면 "->" 또는 문장으로 풀어쓴다

키보드로 입력 가능한 문자(&, /, 괄호, 따옴표 등)는 허용한다.

---

## 2. 아키텍처 불변 규칙

상세는 ARCHITECTURE.md. 여기에는 절대 어기면 안 되는 것만 적는다. 항목은 2-1부터 2-10으로 인용한다.

- **2-1. Core 어셈블리는 UnityEngine을 참조하지 않는다.** Core는 Unity 프로젝트와 Sim 콘솔 프로젝트가 공유하는 순수 C#이다. 이 규칙이 깨지면 헤드리스 시뮬레이션이 불가능해지고 프로젝트의 존재 이유가 사라진다
- **2-2. Core에서 UnityEngine.Random, Time.deltaTime, DateTime.Now를 쓰지 않는다.** 난수는 주입된 시드 기반 PRNG만, 시간은 정수 틱만 사용한다
- **2-3. Dictionary 순회 순서에 의존하지 않는다.** 결정성이 깨진다. 순서가 필요하면 List나 정렬된 키를 쓴다
- **2-4. 전투 수치는 float가 아니라 long 기반 고정소수점을 쓴다.** 부동소수점 누적 오차가 재현성을 깬다
- **2-5. Core는 Presentation을 참조하지 않는다.** 단방향이다
- **2-6. 애셋 참조는 Addressables 주소 문자열로 추상화한다.** 프로토타입의 무료 애셋을 유료 애셋으로 교체할 때 코드가 바뀌면 안 된다
- **2-7. 맵 검증기 등 공유 판정 로직은 Core에 한 벌만 존재한다.** UI, 린터, 시뮬레이터가 같은 구현을 공유한다. 따로 구현하면 반드시 갈라진다 (덱 도달 계산기는 덱 시스템 폐기로 함께 폐기됨)
- **2-8. 히어로 경험치는 합성으로만 얻는다.** 적 처치로 경험치를 주는 코드를 추가하지 않는다. 합성이 곧 성장이라는 규칙이 이 게임의 코어다
- **2-9. 맵 생성기는 DeterministicRandom만 쓴다.** 같은 시드가 같은 맵을 내지 않으면 시뮬레이션 검증 전체가 무효가 된다
- **2-10. 오펜스 요소를 다시 넣지 않는다.** 구역 해금, 파견, 원정, 전선, 스폰 파괴는 검토 후 폐기한 개념이다. 근거는 SPEC.md 6장이다

---

## 3. 작업 규칙

### 3-1. 하지 말 것

- 사양에 없는 기능을 임의로 추가하지 않는다. 사양이 비어 있으면 임의로 채우지 않고, 무엇이 비었는지 먼저 정리한다
- 밸런스 수치를 감으로 정하지 않는다. 수치는 시뮬레이터로 검증한 뒤 확정한다. 초기값이 필요하면 임시값임을 주석으로 명시한다
- 합성식이나 유닛 데이터를 코드에 하드코딩하지 않는다. 전부 Data/*.csv에 둔다
- 맵 생성 파라미터도 코드에 박지 않는다. Data/mapgen.csv에 둔다
- 한 번에 여러 STEP을 건너뛰지 않는다
- 기존 컨벤션과 다른 스타일을 새로 도입하지 않는다

### 3-2. 커밋

- 한 커밋은 한 가지 일만 한다
- 메시지는 한국어, 명령형, 한 줄 요약 + 필요시 본문
- 예: "조합 판정 로직 추가 - 레어 단계 성립 조건 처리"
