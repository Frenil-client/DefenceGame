# CLAUDE.md

프로젝트: SYNTHESIS (가칭) - SD 서브컬처 랜덤 조합 디펜스 로그라이트

배치 위치: 저장소 루트

---

## 1. 이 프로젝트가 무엇인가

랜덤으로 지급되는 흔함 유닛을 조합해 상위 유닛을 만들어 웨이브를 막는 디펜스 로그라이트다. 8, 16, 24웨이브에 고정 보스가 등장하며, 플레이어는 방어에만 집중할 수도 있고 유닛을 파견해 보스를 미리 깎아둘 수도 있다.

- 엔진: Unity, URP
- 언어: C#
- 개발 인원: 1인
- 개발은 모바일 제약으로, 출시는 스팀 유료 단품
- 상세 게임 사양은 Docs/SPEC.md, 밸런스 규칙은 Docs/BALANCE_SPEC.md

---

## 2. 문서 지도

작업 전 관련 문서를 먼저 읽는다.

| 문서 | 내용 | 언제 읽나 |
|---|---|---|
| Docs/SPEC.md | 게임 사양, 코어 루프, 스코프 경계 | 기능 추가 전 항상 |
| Docs/BALANCE_SPEC.md | 등급 격자, 조합식, 불변식, 시뮬 검증 항목 | 데이터, 밸런스, 조합 관련 작업 |
| Docs/ARCHITECTURE.md | asmdef 구조, 결정성 규칙, 데이터 파이프라인 | 새 클래스나 어셈블리 추가 시 |
| Docs/SIM_SPEC.md | 헤드리스 시뮬레이터 사양 | Sim 프로젝트 작업 |
| Docs/ROADMAP.md | STEP별 목표와 완료 조건 | 작업 착수와 종료 판정 |

**문서에 없는 것을 임의로 정하지 않는다.** 사양이 비어 있으면 구현하지 말고 무엇이 비었는지 먼저 보고한다.

---

## 3. 코딩 컨벤션

이 절은 협상 대상이 아니다. 기존 코드와 다르면 기존 코드가 틀린 것이다.

### 3-1. 네이밍

- 한 글자 변수는 좌표와 반복 인덱스에만 쓴다: i, j, x, y, a, b, z
- 그 외 의미 있는 값은 뜻 그대로 camelCase
- min, max, index 같은 관용 축약은 허용: resultMax, minIndex
- 결과 좌표는 소문자 접미사: resultx, resulty
- 보조 메서드는 PascalCase
- 변환 메서드는 XToY 형식: StringToDate, CsvToUnitData
- 접근자는 Get 접두사: GetUnitCost
- 컬렉션은 xList, xDict: unitList, recipeDict

### 3-2. 문법과 서식

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

### 3-3. 원칙

- **명시적 for를 선호한다.** 함수 내부에서 Func<>과 LINQ를 회피한다
- **비트 연산을 회피한다.** 불가피할 때만 쓰고, 그때는 주석으로 이유를 설명한다
- **STEP 주석을 필수로 단다.** 작성 순서를 기반 도구 -> 뼈대 -> 핵심 -> 검증으로 표기한다
- 날짜와 시각은 단조 증가 정수로 인코딩한다
- 전처리로 배열이나 딕셔너리를 만들어두고 조회하는 패턴을 선호한다

### 3-4. 문서 작성 규칙

md 파일과 주석에 키보드로 단순 입력할 수 없는 특수문자를 쓰지 않는다. 가운뎃점, 엠대시, 화살표, 원문자, 말줄임표 기호 등을 금지한다.

- 단어 나열은 쉼표 또는 슬래시
- 두 단어 연결은 "및", "와/과", 슬래시
- 대시와 구분선은 하이픈
- 화살표가 필요하면 "->" 또는 문장으로 풀어쓴다

키보드로 입력 가능한 문자(&, /, 괄호, 따옴표 등)는 허용한다.

---

## 4. 아키텍처 불변 규칙

상세는 Docs/ARCHITECTURE.md. 여기서는 절대 어기면 안 되는 것만 적는다.

1. **Core 어셈블리는 UnityEngine을 참조하지 않는다.** Core는 Unity 프로젝트와 Sim 콘솔 프로젝트가 공유하는 순수 C#이다. 이 규칙이 깨지면 헤드리스 시뮬레이션이 불가능해지고 프로젝트의 존재 이유가 사라진다
2. **Core에서 UnityEngine.Random, Time.deltaTime, DateTime.Now를 쓰지 않는다.** 난수는 주입된 시드 기반 PRNG만, 시간은 정수 틱만 사용한다
3. **Dictionary 순회 순서에 의존하지 않는다.** 결정성이 깨진다. 순서가 필요하면 List나 정렬된 키를 쓴다
4. **전투 수치는 float가 아니라 long 기반 고정소수점을 쓴다.** 부동소수점 누적 오차가 재현성을 깬다
5. **Core는 Presentation을 참조하지 않는다.** 단방향이다
6. **애셋 참조는 Addressables 주소 문자열로 추상화한다.** 프로토타입의 무료 애셋을 유료 애셋으로 교체할 때 코드가 바뀌면 안 된다

---

## 5. 작업 방식

### 5-1. 기본 루프

1. 관련 문서를 읽는다
2. 무엇을 만들지 한 문단으로 요약하고 확인을 받는다
3. 구현한다
4. 린터와 테스트를 돌린다
5. 무엇이 바뀌었는지 보고한다

### 5-2. 하지 말 것

- 사양에 없는 기능을 임의로 추가하지 않는다
- 밸런스 수치를 감으로 정하지 않는다. 수치는 시뮬레이터로 검증한 뒤 확정한다. 초기값이 필요하면 임시값임을 주석으로 명시한다
- 조합식이나 유닛 데이터를 코드에 하드코딩하지 않는다. 전부 Data/*.csv에 둔다
- 한 번에 여러 STEP을 건너뛰지 않는다
- 기존 컨벤션과 다른 스타일을 새로 도입하지 않는다

### 5-3. 커밋

- 한 커밋은 한 가지 일만 한다
- 메시지는 한국어, 명령형, 한 줄 요약 + 필요시 본문
- 예: "조합 판정 로직 추가 - 레어 단계 성립 조건 처리"

---

## 6. 현재 단계

Docs/ROADMAP.md의 STEP 1(기반 도구)부터 시작한다. CSV 임포터, ScriptableObject 생성, 린터 골격이 첫 목표다.

STEP 3 완료 시점에 프리미티브 상태로 뽑기와 조합만 돌려보고 재미를 판정한다. 이 판정 전까지 아트, 사운드, 연출에 시간을 쓰지 않는다.
