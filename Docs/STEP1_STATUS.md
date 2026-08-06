# STEP1_STATUS.md

SYNTHESIS (가칭) - STEP 1 (기반 도구) 진행 상태

배치 위치: Docs/STEP1_STATUS.md

---

## 1. 완료한 것

| 항목 | 위치 | 상태 |
|---|---|---|
| Core 순수 C# 프로젝트 (UnityEngine 참조 없음) | Shared/Synthesis.Core/ | 완료, 빌드 통과 |
| 고정소수점 타입 (long, 스케일 1000) | Shared/Synthesis.Core/Fixed/Fixed.cs | 완료 |
| 시드 주입 xorshift128 PRNG | Shared/Synthesis.Core/Random/DeterministicRandom.cs | 완료 |
| 데이터 모델과 CSV 파서 | Shared/Synthesis.Core/Data/ | 완료 |
| Data/*.csv 초기 파일 6종 | Data/ | 완료 (아래 4장 주의) |
| 불변식 린터 CLI (INV-01 부터 INV-10) | Tools/Linter/ | 완료, CLI 동작 |
| 결정성 테스트, 파서 일치 테스트 | Tests/Synthesis.Core.Tests/ | 완료, 20건 통과 |
| Unity CSV to ScriptableObject 임포터 | DefenceGame/Assets/_Project/Scripts/ | 코드 완료, 에디터 미검증 (5장) |

주의: 문서 초안의 리포지토리 구조는 Unity 프로젝트를 Unity/ 로 두지만, 실제 프로젝트 폴더는 DefenceGame/ 이다. ARCHITECTURE.md 2 를 이에 맞춰 한 줄 갱신했다.

---

## 2. 실행 방법

Core 테스트 (Unity 없이 dotnet 만으로):

```
dotnet test Synthesis.slnx
```

린터 (INV-01 부터 INV-10 검증, CI 용):

```
dotnet run --project Tools/Linter/Synthesis.Linter.csproj -- ./Data
```

린터 종료 코드: Authoritative 불변식 위반이 하나라도 있으면 1, 없으면 0. 빌드 게이트에 그대로 물릴 수 있다.

Unity 임포터: 에디터 상단 메뉴 Synthesis -> Import CSV to ScriptableObjects. Data/*.csv 를 읽어 Assets/_Project/Data/Generated/SynthesisDatabase.asset 을 갱신하고, 되돌린 모델이 CSV 직접 파싱과 같은지 자기검증한다.

---

## 3. 린터 결과 요약 (현재 데이터 기준)

Authoritative (확정 데이터 기준, 위반 시 빌드 차단):

- INV-01 FAIL, INV-02 FAIL, INV-03 FAIL (아래 4장)
- INV-05 PASS, INV-06 PASS, INV-10 PASS

Provisional (TEMP 수치나 미정의 모델 기준, 경고만):

- INV-04 PASS, INV-07 PASS
- INV-08 WARN (도달 추정식 미정의), INV-09 WARN (레어 이상 placement 가 TEMP)

---

## 4. 발견한 구조 문제 (BALANCE_SPEC.md 4장 수정 필요)

린터가 BALANCE_SPEC.md 4장 레시피 표에서 불변식 위반 3건을 잡았다. 이는 수치가 아니라 구조 문제이므로
임의로 고치지 않고 보고한다 (CLAUDE.md 2, 5-2). BALANCE_SPEC 을 수정한 뒤 recipes.csv 를 맞춘다.

1. INV-01 위반: C03 서리 정령이 레어 레시피 1개(R02)에만 재료로 등장한다. 규칙은 흔함마다 최소 2개다.
2. INV-02 위반: R10 수호사제가 어떤 유니크/히든 레시피에도 재료로 쓰이지 않는 고아 레어다.
3. INV-03 위반: U02 빙하군주가 히든 레시피 2개(H02, H06)에 등장한다. 규칙은 유니크마다 정확히 1개다.

참고로 4-4 는 "재료 중복이 깊이를 만든다"며 재료 재사용을 의도한다. 이 의도와 INV-03(정확히 1개)이
정면 충돌한다. 둘 중 무엇을 살릴지 결정이 필요하다.

---

## 5. TEMP 로 채운 항목 (문서 미확정, 시뮬레이터로 확정)

BALANCE_SPEC.md 12 가 미확정으로 둔 값은 units.csv 등에 TEMP 로 표시해 임시로 채웠다. 확정 근거가 없다.

- 흔함 10종의 element/role/placement/cost 는 3장 표 그대로 (확정).
- 레어/유니크/히든의 element/role/placement 는 4장 성격 서술에서 추론한 TEMP 값.
- 모든 hp/atk/atkSpeed/range 와 isAdvance 는 TEMP 값.
- redeployCd(틱)와 보스 preDamageCapRatio(0.40)와 웨이브 보스 위치(8/16/24)는 문서 확정값.
- relics.csv 는 헤더만 (유물 규칙 미정의, STEP 6).
- leaders.csv 는 스키마 자체가 BALANCE_SPEC.md 10 에 없어 파서도 두지 않았다. 스키마 확정 후 진행한다.

---

## 6. Unity Core 공유 방식 (junction)

Core 소스는 Shared/Synthesis.Core 에 한 벌만 둔다 (ARCHITECTURE.md 1). Unity 는 이를 junction 으로 끌어온다.

- 링크 경로: DefenceGame/Assets/_Project/Scripts/Core.Link -> Shared/Synthesis.Core (Windows junction)
- 이 junction 경로는 .gitignore 로 무시한다. 소스는 Shared 에서만 추적된다.
- dotnet 빌드 산출물은 Directory.Build.props 의 artifacts 레이아웃으로 저장소 루트 artifacts/ 에 모아, 소스 폴더를 순수하게 유지한다 (Unity 가 bin/obj 를 임포트하지 않도록).

junction 재생성 (경로가 깨졌을 때, PowerShell):

```
New-Item -ItemType Junction -Path "DefenceGame\Assets\_Project\Scripts\Core.Link" -Target "Shared\Synthesis.Core"
```

---

## 7. STEP 1 DoD 대비 현황

- 린터가 CLI 에서 돌고 INV-01 부터 INV-10 을 검증한다: 도구는 완료. 단 현재 데이터로는 INV-01/02/03 이 FAIL 이다(4장). BALANCE_SPEC 수정 전까지 "전부 통과"는 성립하지 않는다.
- Unity 없이 dotnet test 로 Core 테스트가 돈다: 완료 (20건 통과).
- units.csv 수정 시 SO 갱신 및 린터 재검증: 린터 재검증 완료. SO 갱신(임포터)은 코드 완료이나 Unity 에디터 실행으로 최종 확인 필요.
