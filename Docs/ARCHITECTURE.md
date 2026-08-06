# ARCHITECTURE.md

SYNTHESIS (가칭) - 아키텍처 사양

배치 위치: Docs/ARCHITECTURE.md

---

## 1. 핵심 결정: Core를 Unity 밖에서도 돌린다

이 프로젝트의 존재 이유는 **밸런스를 감이 아니라 10만 런 시뮬레이션으로 검증하는 것**이다. 그러려면 전투 로직이 Unity 없이 콘솔에서 돌아가야 한다.

따라서 Core 어셈블리는 UnityEngine을 참조하지 않는 순수 C#으로 작성하고, Unity 프로젝트와 Sim 콘솔 프로젝트가 **같은 소스를 공유**한다.

이 규칙이 깨지면 시뮬레이터를 별도로 다시 짜야 하고, 두 구현이 갈라지는 순간 검증은 의미를 잃는다. 모든 설계 판단에서 이 규칙이 최우선이다.

---

## 2. 저장소 구조

```
/
  CLAUDE.md
  Docs/
    SPEC.md
    BALANCE_SPEC.md
    ARCHITECTURE.md
    SIM_SPEC.md
    ROADMAP.md
  Data/
    units.csv
    recipes.csv
    waves.csv
    bosses.csv
    relics.csv
    leaders.csv
  Shared/
    Synthesis.Core/          순수 C#. Unity와 Sim이 공유
      Synthesis.Core.csproj
      Data/                  데이터 모델과 CSV 파서
      Simulation/            틱 루프, 전투 해결
      Units/                 유닛 상태와 행동
      Combination/           조합 판정
      Waves/                 웨이브 스폰과 보스
      Advance/               구역 해금과 파견
      Random/                시드 기반 PRNG
      Fixed/                 long 기반 고정소수점
  DefenceGame/                 Unity 프로젝트 루트 (문서 초안의 Unity/ 에 해당)
    Assets/
      _Project/
        Scripts/
          Core.Link/         Synthesis.Core.asmdef (Shared 소스를 링크)
          Data/              Synthesis.Data.asmdef (ScriptableObject 정의)
          Presentation/      Synthesis.Presentation.asmdef (View, ViewModel)
          Bootstrap/         Synthesis.Bootstrap.asmdef (진입점, DI 조립)
        Editor/              Synthesis.Editor.asmdef (임포터, 조합 트리 뷰어)
        Art/
        Addressables/
  Sim/
    Synthesis.Sim/           콘솔 실행 프로젝트
      Synthesis.Sim.csproj   Synthesis.Core.csproj 참조
  Tools/
    Linter/                  불변식 검증 CLI
  Reports/                   시뮬 출력 (git 무시)
```

Unity 쪽 Core.Link는 Shared/Synthesis.Core의 소스를 심볼릭 링크 또는 asmdef 참조로 끌어온다. 소스는 한 벌만 존재한다.

---

## 3. 어셈블리 의존 방향

```
Bootstrap  ->  Presentation  ->  Data  ->  Core
Editor     ->  Data, Core
Sim        ->  Core
Linter     ->  Core
```

역방향 참조는 금지한다. 특히:

- Core는 Data, Presentation, Bootstrap 중 어느 것도 참조하지 않는다
- Core는 UnityEngine을 참조하지 않는다
- Data는 Presentation을 참조하지 않는다

---

## 4. 결정성 규칙

시뮬레이션 재현성이 이 프로젝트의 생명선이다. 아래를 어기면 같은 시드로 다른 결과가 나온다.

### 4-1. 난수

- UnityEngine.Random 금지
- System.Random도 직접 쓰지 않는다. 구현이 런타임마다 다를 수 있다
- Core/Random/DeterministicRandom.cs에 xorshift 계열 PRNG를 직접 구현하고 시드를 주입받는다
- 난수 소비 순서가 곧 결과다. 순서를 바꾸는 리팩터링은 결과를 바꾼다는 점을 인지한다

### 4-2. 시간

- Time.deltaTime, DateTime.Now 금지
- 로직은 고정 틱으로 돈다. 초당 20틱, 1틱 = 50ms
- 모든 시간 값은 정수 틱으로 저장한다. 재배치 쿨타임 12초는 240틱이다
- 렌더링은 틱 사이를 보간한다. 보간은 Presentation의 책임이며 Core는 관여하지 않는다

### 4-3. 수치

- 전투 수치는 float가 아니라 long 기반 고정소수점을 쓴다
- Core/Fixed/Fixed.cs에 스케일 1000 고정소수점 타입을 정의한다
- 부동소수점 누적 오차는 수만 틱이 쌓이면 재현성을 깬다

### 4-4. 순회 순서

- Dictionary 순회 순서에 의존하지 않는다
- 순서가 결과에 영향을 주는 곳에서는 List를 쓰거나 정렬된 키 배열을 만들어 순회한다
- 유닛 처리 순서는 배치 순서가 아니라 명시적 정렬 기준(구역 번호, 타일 인덱스)을 따른다

### 4-5. 검증

- 같은 시드로 두 번 돌려 결과 해시가 같은지 확인하는 테스트를 STEP 1부터 유지한다
- Unity 빌드와 Sim 콘솔에서 같은 시드가 같은 결과를 내는지 정기적으로 대조한다

---

## 5. 데이터 파이프라인

```
Data/*.csv
   |
   +-- (Core의 CsvToXxx 파서)
   |
   +-> Sim: CSV를 직접 파싱해 메모리 로드
   |
   +-> Unity Editor: 임포터가 ScriptableObject 생성
          |
          +-> Addressables 번들
                 |
                 +-> 런타임 로드
```

### 5-1. 원칙

- **CSV가 원본이다.** ScriptableObject는 런타임 로딩 속도를 위한 캐시다
- 파서는 Core에 한 벌만 둔다. Sim과 Editor가 같은 파서를 쓴다
- SO로 변환한 결과가 CSV 파싱 결과와 동일한지 검증하는 테스트를 둔다. 두 경로가 갈라지면 시뮬 검증이 무효가 된다
- 조합식과 유닛 데이터를 코드에 하드코딩하지 않는다

### 5-2. Addressables

- 모든 애셋 참조는 주소 문자열로 추상화한다
- 프로토타입의 무료 애셋을 유료 애셋으로 교체할 때 코드가 바뀌면 안 된다
- 주소 규칙: `unit/{id}/model`, `unit/{id}/portrait`, `boss/{id}/model`, `ui/{name}`
- 플랫폼별 번들을 분리한다 (안드로이드, 윈도우)

---

## 6. Presentation 계층

### 6-1. MVVM

- **Model**: Core의 시뮬레이션 상태. 순수 데이터
- **ViewModel**: Core 상태를 UI가 소비할 형태로 변환하고 변경 통지를 발행한다. Presentation 어셈블리에 둔다
- **View**: MonoBehaviour와 UGUI. ViewModel만 바라본다

Core는 ViewModel의 존재를 모른다. 상태 변경은 Core가 발행하는 이벤트 목록을 Presentation이 매 틱 읽어가는 방식으로 전달한다. Core가 콜백을 직접 호출하지 않는다.

### 6-2. 틱과 렌더 분리

- Core는 고정 20틱으로 진행한다
- Presentation은 프레임마다 마지막 두 틱 사이를 보간해 그린다
- 배속 기능은 틱 진행 속도만 바꾼다. 렌더 로직은 건드리지 않는다

---

## 7. 입력 추상화

터치와 마우스를 하나의 경로로 처리한다.

```
InputSource (터치 / 마우스)  ->  PointerEvent  ->  Interaction (선택, 배치, 회수, 파견)
```

- Presentation은 PointerEvent만 소비한다. 플랫폼 분기는 InputSource 안에서 끝낸다
- 실시간 조작이 없으므로 입력 지연 요구가 낮다. 복잡한 예측 처리는 넣지 않는다

---

## 8. 렌더링

- URP, 고정 쿼터뷰, 카메라 이동 없음
- 유닛 룩은 자체 NPR 셰이더로 통일한다. 프로토타입의 프리미티브에도 동일 셰이더를 적용해 룩 일관성을 확보한다
- 유닛 초상화, 도감 아이콘, 조합 연출은 3D 모델의 NPR 렌더로 생성한다. 별도 2D 일러를 만들지 않는다
- 오프스크린 RenderTexture로 초상화를 베이크하거나 런타임 렌더한다
- 카메라 고정이므로 컬링과 배칭이 단순하다. MaterialPropertyBlock으로 인스턴스별 색 변형을 처리해 SetPass Call을 억제한다

---

## 9. 에디터 툴

STEP 1에서 골격을 만들고 이후 확장한다.

| 툴 | 역할 |
|---|---|
| CSV 임포터 | Data/*.csv를 ScriptableObject로 변환 |
| 조합 트리 뷰어 | 레시피 그래프를 시각화하고 고아 노드를 표시 |
| 불변식 린터 | BALANCE_SPEC.md 8장의 INV-01부터 INV-10까지 검증 |
| 시뮬 리포트 뷰어 | Reports/의 CSV를 읽어 도달률과 승률을 표시 |
| 맵 에디터 | 그리드, 경로, 구역, 배치 타일 편집 |

린터는 CLI로도 돌 수 있어야 한다. Unity를 켜지 않고 CI에서 검증하기 위함이다.

---

## 10. 빌드 파이프라인

STEP 9 이후 구축한다.

- Jenkins에서 안드로이드와 윈도우를 동시 빌드
- Addressables Content Update로 콘텐츠만 갱신하는 경로 확보
- 빌드 시간과 번들 크기를 메트릭으로 기록
- 빌드 결과를 Discord 웹훅으로 통지
- 빌드 전 린터를 실행해 불변식 위반 시 실패 처리

---

## 11. 테스트

| 종류 | 대상 | 시점 |
|---|---|---|
| 결정성 테스트 | 같은 시드 두 번 실행 시 결과 해시 일치 | STEP 1부터 상시 |
| 파서 일치 테스트 | CSV 직접 파싱 결과와 SO 변환 결과 동일 | STEP 1부터 상시 |
| 불변식 테스트 | INV-01부터 INV-10 | 데이터 변경 시마다 |
| 크로스 플랫폼 대조 | Unity 빌드와 Sim 콘솔의 동일 시드 결과 비교 | STEP 6 이후 주기적 |

Core는 Unity 의존이 없으므로 일반 dotnet test로 검증한다. Unity Test Runner는 Presentation 계층에만 쓴다.
