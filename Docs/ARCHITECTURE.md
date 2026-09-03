# ARCHITECTURE.md

SYNTHESIS (가칭) - 아키텍처 사양

배치 위치: Docs/ARCHITECTURE.md / 버전 0.3

버전 0.3 변경: Core/Map 모듈 추가, Advance 모듈 삭제, 데이터 파일 목록 갱신, 린터 검증 범위 갱신

---

## 1. 핵심 결정: Core를 Unity 밖에서도 돌린다

Core 어셈블리는 UnityEngine을 참조하지 않는 순수 C#으로 작성하고, Unity 프로젝트와 (추후) Sim 콘솔 프로젝트가 **같은 소스를 공유**한다. Core가 순수하게 유지되어야 헤드리스 검증과 결정적 재현이 가능하다.

단, **전투/유닛 이동/투사체는 Core(결정적 시뮬)에서 들어내 Unity 실시간으로 처리한다.** Core 시뮬이 결정적으로 소유하는 것은 맵 생성, 몬스터 스폰과 루프 순회, 유닛 배치(칸 점유)와 재배치, 조합/뽑기다. 전투(타겟팅/쿨다운/방어력 계산/피해/몬스터 hp와 처치 처리)와 유닛 걷기는 Presentation의 실시간 전투 스크립트(CombatController)가 소유한다. Core 시뮬에는 처치 시 로스터(생존 수) 갱신만 알린다(OnMonsterKilled).

전투 밸런스는 직접 플레이로 판정하고, 시뮬/검증은 기능 구현 이후 필요할 때 사후 추가한다. 시뮬 제약이 기능을 막지 않는다. Core가 UnityEngine을 참조하는 순간 이 분리가 무너지므로, Core 순수성 규칙은 여전히 최우선이다.

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
    enemies.csv
    waves.csv
    bosses.csv
    relics.csv
    leaders.csv             히어로(리더) 데이터. 스키마는 STEP 4에서 확정
    mapgen.csv
  Shared/
    Synthesis.Core/          순수 C#. Unity와 (추후) Sim이 공유
      Synthesis.Core.csproj
      Data/                  데이터 모델과 CSV 파서
      Simulation/            틱 루프, 스폰/순회, 배치 (전투는 제외, Unity 실시간)
      Units/                 유닛 데이터와 인벤토리
      Combination/           합성 판정과 뽑기 (GachaEngine, CombinationEngine)
      Map/                   루프 맵 생성과 검증 (MAP_SPEC.md)
      Waves/                 웨이브 데이터 조회와 보스 해석
      Random/                시드 기반 PRNG
      Fixed/                 long 기반 고정소수점
      (Hero/ 는 STEP 4에서 신설 예정. Deck/ 은 덱 시스템 폐기로 두지 않는다)
  DefenceGame/               Unity 프로젝트 루트 (문서 초안의 Unity/ 에 해당)
    Assets/
      _Project/
        Scripts/
          Core.Link/         Shared/Synthesis.Core 를 junction 으로 링크
          Data/              ScriptableObject 정의
          Presentation/      Synthesis.Presentation.asmdef (View, 매니저, 실시간 전투/이동)
        Editor/              임포터, 씬 빌더, 맵 저작 툴
        Art/
        Addressables/
  Sim/
    Synthesis.Sim/           콘솔 실행 프로젝트 (아직 없음, STEP 7에서 신설)
  Tools/
    Linter/                  불변식 검증 CLI
    Demo/                    헤드리스 데모 (데이터/맵/스폰 확인)
  Reports/                   시뮬 출력 (git 무시)
```

Unity 쪽 Core.Link는 Shared/Synthesis.Core의 소스를 junction 으로 끌어온다. 소스는 한 벌만 존재한다.

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
- Core 로직은 고정 틱으로 돈다. 초당 20틱, 1틱 = 50ms
- Core 시간 값은 정수 틱으로 저장한다. 예: 스폰 간격 0.5초는 10틱이다
- 단, 전투/유닛 이동은 Unity 실시간(가변 프레임, Time.deltaTime 기반)에서 처리하므로 틱이 아니며 결정성 대상이 아니다
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
- **맵 검증기와 조합/뽑기 판정을 Core에 둔다.** 린터, 시뮬레이터, UI가 전부 같은 구현을 써야 한다. 여러 곳에서 따로 구현하면 반드시 갈라진다 (덱 도달 계산기는 덱 시스템 폐기로 함께 폐기됨)
- **맵 생성기는 Core/Map에 둔다.** Unity 없이 dotnet에서 1000개 맵을 생성해 분산을 측정할 수 있어야 한다
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
InputSource (터치 / 마우스)  ->  PointerEvent  ->  Interaction (선택, 배치, 회수, 합성, 카드 선택)
```

- Presentation은 PointerEvent만 소비한다. 플랫폼 분기는 InputSource 안에서 끝낸다
- 유닛 재배치(홀드 후 클릭업)와 자동 전투는 Unity 실시간에서 처리한다. 다만 조작 밀도가 낮아 복잡한 예측 처리는 넣지 않는다

---

## 8. 렌더링

- URP, 고정 쿼터뷰, 카메라 이동 없음
- 유닛 룩은 자체 NPR 셰이더로 통일한다. 프로토타입의 프리미티브에도 동일 셰이더를 적용해 룩 일관성을 확보한다
- 유닛 초상화, 도감 아이콘, 조합 연출은 3D 모델의 NPR 렌더로 생성한다. 별도 2D 일러를 만들지 않는다
- 오프스크린 RenderTexture로 초상화를 베이크하거나 런타임 렌더한다
- 카메라 고정이므로 컬링과 배칭이 단순하다. MaterialPropertyBlock으로 인스턴스별 색 변형을 처리해 SetPass Call을 억제한다
- **순회 몬스터는 GPU 인스턴싱으로 그린다.** 동시 60기가 성능 상한 기준이다
- 루프 경로에 스크롤 UV 이미시브를 얹어 흐름 방향을 보여준다
- 배치 타일은 지면보다 올려 높이차와 그림자로 배치 가능 여부를 읽히게 한다

---

## 9. 에디터 툴

STEP 1에서 골격을 만들고 이후 확장한다.

| 툴 | 역할 |
|---|---|
| CSV 임포터 | Data/*.csv를 ScriptableObject로 변환 |
| 합성 트리 뷰어 | 레시피 그래프를 시각화하고 고아 노드를 표시 |
| 불변식 린터 | BALANCE_SPEC.md 11장의 INV, MAP_SPEC.md 4장의 MAP 검증 |
| 맵 생성 프리뷰 | 시드를 넣어 루프 맵을 미리 보고 커버 효율을 확인 |
| 시뮬 리포트 뷰어 | Reports/의 CSV를 읽어 도달률과 승률을 표시 |
| 맵 파라미터 편집기 | mapgen.csv를 편집하고 즉시 생성 결과를 확인 |

린터는 CLI로도 돌 수 있어야 한다. Unity를 켜지 않고 CI에서 검증하기 위함이다.

---

## 10. 빌드 파이프라인

STEP 11 이후 구축한다.

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
| 불변식 테스트 | INV 전체와 MAP 전체 | 데이터 변경 시마다 |
| 맵 생성 재현성 | 같은 시드가 같은 맵을 내는가 | STEP 1부터 상시 |
| 크로스 플랫폼 대조 | Unity 빌드와 Sim 콘솔의 동일 시드 결과 비교 | STEP 7 이후 주기적 |

Core는 Unity 의존이 없으므로 일반 dotnet test로 검증한다. Unity Test Runner는 Presentation 계층에만 쓴다.
