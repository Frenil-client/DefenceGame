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
| Data/*.csv 초기 파일 6종 | Data/ | 완료 (TEMP 항목은 5장) |
| 불변식 린터 CLI (INV-01 부터 INV-10) | Tools/Linter/ | 완료, Authoritative 전부 PASS |
| 결정성 테스트, 파서 일치 테스트 | Tests/Synthesis.Core.Tests/ | 완료, 20건 통과 (net8.0) |
| CI 게이트 (빌드/테스트/린터/Core 미참조 강제) | Tools/ci.sh, .github/workflows/ci.yml | 완료, 로컬 통과 |
| Unity CSV to ScriptableObject 임포터 | DefenceGame/Assets/_Project/Scripts/ | 코드 완료, 에디터 미검증 (6장) |

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

전체 CI 게이트 한 번에 (빌드 + 테스트 + 린터 + Core 미참조 검사):

```
bash Tools/ci.sh
```

Unity 임포터: 에디터 상단 메뉴 Synthesis -> Import CSV to ScriptableObjects. Data/*.csv 를 읽어 Assets/_Project/Data/Generated/SynthesisDatabase.asset 을 갱신하고, 되돌린 모델이 CSV 직접 파싱과 같은지 자기검증한다.

---

## 3. 린터 결과 요약 (현재 데이터 기준)

Authoritative (확정 데이터 기준, 위반 시 빌드 차단): 전부 PASS

- INV-01, INV-02, INV-03, INV-05, INV-06, INV-10 모두 PASS
- 린터 종료 코드 0

Provisional (TEMP 수치나 미정의 모델 기준, 경고만):

- INV-04 PASS, INV-07 PASS
- INV-08 WARN (도달 추정식 미정의), INV-09 WARN (레어 이상 placement 가 TEMP)
- 이 2건은 실제 수치를 시뮬레이터로 확정하는 STEP 7 에서 해소한다. STEP 1 통과를 막지 않는다.

---

## 4. 발견하고 해소한 구조 문제 (해결 완료)

린터가 초기 데이터에서 불변식 위반 3건을 잡았고, 검토 후 아래처럼 해소했다. 상세 근거는 BALANCE_SPEC.md 8-1(개정 이력).

1. INV-01 위반 (C03 서리 정령이 레어 레시피 1개에만 등장): 불변식을 '최소 2개' 에서 '최소 1개' 로 완화. 지속/관통 같은 유일 역할 흔함은 로스터상 레어 레시피가 1개뿐이라 2개가 수학적으로 불가능하고 4장 희소성 의도와도 모순되기 때문이다.
2. INV-02 위반 (R10 수호사제 고아 레어): 데이터 수정. H03 심판자의 재료를 R05 에서 R10 으로 교체(R05 는 U04 에서 계속 쓰임). 규칙이 아니라 4장의 누락이었다.
3. INV-03 위반 (U02 빙하군주가 히든 2개에 등장): 불변식을 '정확히 1개' 에서 '최소 1개' 로 완화. 4-4장 '재료 중복' 의도와 냉기 유니크 부족(U02 하나) 때문에 중복이 불가피하다.

결과: 린터 Authoritative 전부 PASS. INV-01/03 완화는 BALANCE_SPEC.md 8장과 린터 양쪽에 반영했다.

---

## 5. TEMP 로 채운 항목 (문서 미확정, 시뮬레이터로 확정)

BALANCE_SPEC.md 12 가 미확정으로 둔 값은 units.csv 등에 TEMP 로 표시해 임시로 채웠다. 확정 근거가 없다.

- 흔함 6종(계열당 1종)의 cost 는 3장 표 그대로 (확정). (덱 시스템은 폐기되어 6종 고정)
- 레어/유니크/히든의 element/role/placement 는 4장 성격 서술에서 추론한 TEMP 값.
- 모든 hp/atk/atkSpeed/range 와 isAdvance 는 TEMP 값.
- 보스 preDamageCapRatio(0.40)와 웨이브 보스 위치(10/20/30/40, 10의 배수)는 문서 확정값. (redeployCd 는 재배치가 즉시로 바뀌며 폐기)
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

- 린터가 CLI 에서 돌고 INV-01 부터 INV-10 을 검증한다: 완료. Authoritative 전부 PASS, 종료 코드 0. (bash Tools/ci.sh 또는 GitHub Actions 로 강제)
- Unity 없이 dotnet test 로 Core 테스트가 돈다: 완료 (20건 통과, net8.0).
- units.csv 수정 시 SO 갱신 및 린터 재검증: 린터 재검증 완료. SO 갱신(임포터)은 코드 완료이나 Unity 에디터 실행으로 최종 확인 필요(6장).

남은 것은 Unity 임포터 에디터 1회 실행 확인뿐이다. 그 외 DoD 는 충족했다.
