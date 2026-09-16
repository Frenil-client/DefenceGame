# SYNTHESIS

**뽑은 유닛을 합성하고 재배치해, 루프를 순회하는 몬스터와 보스를 막는 랜덤 조합 디펜스.**

Unity / C# 1인 개발 프로젝트입니다. 현재 뽑기, 합성, 전투와 상점이 연결된 그레이박스 프로토타입에서 플레이 재미를 검증하고 있습니다.

| 항목 | 내용 |
|---|---|
| 개발 환경 | Unity 6000.3.9f1 / URP / C# |
| 개발 범위 | 게임 규칙과 데이터 설계, 전투 및 조작, UI, 에디터 도구, 자동 검증 |
| 현재 구현 | 유닛 42종, 조합식 36개, 패시브 스킬 32종, 40웨이브 데이터와 보스 4종 |
| 개발 단계 | STEP 3 - 뽑기와 합성의 재미 검증 (2026년 9월 기준) |

![10웨이브 보스전. 상단에 보스 체력과 제한시간, 하단에 보유 유닛과 조작 버튼을 표시](Docs/images/gameplay-wave.png)

10웨이브 보스전 화면. 유닛의 배치와 조합으로 적을 처리하고, 보스 제한시간과 필드 누적 몬스터 수를 관리합니다.

[문제 해결 사례](#문제-해결-사례) / [실행 방법](#실행-방법) / [기술 상세](Docs/IMPLEMENTATION_NOTES.md) / [전체 포트폴리오](https://github.com/Frenil-client/frenil-portfolio)

## 핵심 구현

| 구현 | 해결한 문제 | 확인할 근거 |
|---|---|---|
| 데이터로 조립하는 스킬과 전투 계산 | 스킬 조합 순서와 시간 반올림에 따라 달라지던 피해를 일관되게 계산 | [전투 계산](Shared/Synthesis.Core/Combat/CombatRules.cs), [실행 테스트](Tests/Synthesis.Core.Tests/SkillExecutionTests.cs) |
| Unity 없이 실행하는 Core와 검증 도구 | 에디터를 켜지 않고 게임 규칙과 CSV 오류를 확인 | [Core](Shared/Synthesis.Core), [검증 파이프라인](Tools/ci.sh) |
| 문자열 편집 도구와 재사용 UI 패키지 | 문자열 검색과 적용을 연결하고, 팝업 수명과 레이어 관리를 공통화 | [문자열 도구](DefenceGame/Assets/_Project/Scripts/Editor/StringTableWindow.cs), [UI 패키지](https://github.com/Frenil-client/unity-ui-system) |

## 플레이 흐름

1. **뽑기와 배치:** 지급받은 1성 유닛을 배치해 루프 경로의 적을 공격합니다.
2. **합성:** 보유 재료로 상위 유닛을 만들고 패시브 스킬을 얻습니다. 조합 UI는 가능한 조합을 강조하고 결과 유닛 기준으로 갱신됩니다.
3. **선택권 사용:** 석상과 보스 보상으로 얻은 선택권을 상점에서 원하는 1성으로 교환해 부족한 재료를 채웁니다.
4. **전투 중 조작:** 유닛을 재배치하거나 적과 석상에 집중 명령을 내려 공격 위치와 대상을 바꿉니다.

10의 배수 웨이브에는 보스가 등장합니다. 보스 처치 제한시간을 넘기거나 필드 몬스터 누적 상한을 초과하면 패배하고, 마지막 보스를 처치하면 클리어합니다.

![프리스트 선택 화면. 지면에 공격 사거리, 우측에 버프가 반영된 공격력과 스킬 설명을 표시](Docs/images/unit-select-info.png)

유닛을 선택하면 공격 사거리와 현재 능력치를 함께 확인할 수 있습니다. `공격력 32 (+4)`처럼 버프를 반영한 총합과 증가분을 표시합니다.

## 문제 해결 사례

### 1. 스킬 순서를 바꿔도 같은 피해가 나오도록 계산 단계 분리

**문제:** 추가 피해와 치명타를 데이터에 적힌 순서대로 적용해, 같은 스킬 구성이 600% 또는 400% 피해를 냈습니다.

**변경:** 발동한 효과를 먼저 모은 뒤 `(1 + 추가 피해 합) x 치명타 배율` 순서로 계산합니다. 데이터 나열 순서가 산술 우선순위를 바꾸지 않도록 했습니다.

**검증:** 강타와 치명타가 함께 발동하면 600%가 나오는지, 스킬 순서를 뒤집어도 같은지 테스트합니다.

[계산 코드](Shared/Synthesis.Core/Combat/CombatRules.cs) / [회귀 테스트: Heavy3AndCrit2_CombineTo600Percent](Tests/Synthesis.Core.Tests/SkillExecutionTests.cs)

### 2. 프레임 시간의 잔여분을 보존해 장판 피해 오차 해결

**문제:** 프레임 시간을 밀리초로 반올림하면서 잔여 시간을 버려, 같은 지속시간에도 프레임률에 따라 장판 피해량이 달라졌습니다.

**변경:** 마이크로초 단위로 시간을 누적하고, 계산에 사용한 밀리초를 제외한 잔여분은 다음 프레임으로 넘깁니다.

**검증:** 30 / 60 / 120fps로 나눈 1초 입력에서 POISON1의 누적 피해가 정의된 초당 피해와 일치하는지 검사합니다. 시간 누산과 피해 계산을 검증하며, Unity 전투 전체의 재현성을 보장하지는 않습니다.

[시간 누산기](Shared/Synthesis.Core/Combat/TickAccumulator.cs) / [회귀 테스트: Poison1_DealsDefinedDpsOverOneSecond](Tests/Synthesis.Core.Tests/SkillExecutionTests.cs)

### 3. 문자열 검색부터 UI 적용과 오류 확인까지 에디터에서 연결

**문제:** 문자열 키를 찾고 UI에 옮겨 적는 작업이 분리되어 있고, 키 오타는 실행 후에 발견하기 쉬웠습니다.

**변경:** 키와 번역 문구 양쪽으로 검색하고, 선택한 항목을 대상 컴포넌트에 적용해 TMP 텍스트를 즉시 갱신합니다. 존재하지 않는 키는 인스펙터에서 표시합니다.

![문자열 테이블에서 shop을 검색하고 선택한 키를 StringParser와 TMP 텍스트에 적용한 화면](Docs/images/stringtable.png)

왼쪽에서 문자열을 검색하고 선택하면, 오른쪽의 키와 실제 텍스트에 반영됩니다.

**검증:** 한국어와 영어의 치환자 일치, 스킬 이름과 설명 키의 누락, 설명에 사용한 치환자의 유효성을 자동 검사합니다.

[문자열 에디터](DefenceGame/Assets/_Project/Scripts/Editor/StringTableWindow.cs) / [문자열 테스트](Tests/Synthesis.Core.Tests/StringTableTests.cs)

## 구조와 검증 범위

게임 규칙과 순수 계산은 UnityEngine을 참조하지 않는 Core에 두고, 전투 진행과 화면 및 입력은 Unity에서 처리합니다. 전투 계산을 Core로 분리해 실시간 플레이를 유지하면서 계산 규칙을 테스트할 수 있게 했습니다.

| 영역 | 담당 |
|---|---|
| Core | 시드 기반 맵 생성, 스폰과 루프 순회, 배치, 조합, 뽑기, 순수 전투 계산 |
| Unity | 프레임 시간과 난수 공급, 전투 진행과 처치, 유닛 이동, 투사체, 입력과 화면 |
| CSV와 도구 | 유닛 및 조합식과 스킬 원본, 데이터 검증, ScriptableObject 임포터 |
| 별도 UI 패키지 | 팝업 스택, 모달 배경, 레이어 정렬과 씬 수명 관리 |

Core는 Unity와 .NET 프로젝트가 같은 소스를 공유합니다. 정수 틱, 시드 주입 난수, 고정소수점으로 Core의 재현성을 관리합니다. **전투는 Unity 실시간으로 진행되므로 전체 런의 동일 시드 재현은 보장하지 않습니다.**

[GitHub Actions](.github/workflows/ci.yml)는 `main` push와 `main` 대상 PR에서 다음 검증을 실행합니다.

- Core의 UnityEngine 참조 검사와 .NET 빌드
- 맵 재현성, 조합, 뽑기, 전투 계산과 문자열 규칙 테스트
- CSV 참조와 구조 불변식, 지원하지 않는 스킬 조합 검사

2026-09-16 로컬 Release 실행에서 **테스트 134건 통과, 실패 0건**을 확인했습니다. 스킬 실행 테스트는 32종 정의 모두에서 효과가 발생하는지 확인하고, 개별 테스트로 발동 주기, 피해 배수, 대상 선택과 오라 중첩을 검사합니다.

Unity 씬, 입력, 프리팹과 실제 빌드 동작은 별도 확인 대상입니다. 설계 변경의 배경과 세부 검증 범위는 [기술 상세](Docs/IMPLEMENTATION_NOTES.md)에 정리했습니다.

## 실행 방법

### Unity 에디터에서 플레이

1. 저장소 전체를 받은 뒤 Unity Hub에서 `DefenceGame/`을 엽니다. 프로젝트 버전은 **6000.3.9f1**입니다.
2. `DefenceGame/Assets/_Project/Scripts/Core.Link`가 저장소의 `Shared/Synthesis.Core`를 가리키는지 확인합니다. 이 링크는 Git 추적 대상이 아니므로 새 환경에서는 아래와 같이 생성합니다.
3. 패키지 복원이 끝나면 `Assets/_Project/Scenes/LoopGame.unity`를 열고 Play를 누릅니다. Git URL로 참조하는 UI 패키지를 받으려면 Git과 네트워크 연결이 필요합니다.

Windows PowerShell에서 저장소 루트 기준으로, `Core.Link`가 없을 때 실행합니다.

```powershell
New-Item -ItemType Junction -Path "DefenceGame/Assets/_Project/Scripts/Core.Link" -Target (Resolve-Path "Shared/Synthesis.Core").Path
```

현재 에디터 플레이는 저장소의 `Data/`에서 CSV를 직접 읽습니다. ScriptableObject 캐시를 생성하거나 갱신하려면 `Synthesis -> Import CSV to ScriptableObjects` 메뉴를 사용합니다.

### Core와 데이터 검증

`.slnx`를 지원하는 .NET SDK와 .NET 8 테스트 런타임이 필요합니다. 명령은 저장소 루트에서 실행합니다.

```sh
dotnet test Synthesis.slnx -c Release
dotnet run --project Tools/Linter/Synthesis.Linter.csproj -c Release -- ./Data
```

전체 게이트는 Bash 환경에서 `bash Tools/ci.sh`로 실행합니다.

## 현재 상태와 다음 목표

뽑기, 합성, 선택권 상점, 패시브 스킬, 전투 중 조작, 보스 승패와 재시작을 구현했습니다. 다음 목표는 도감과 함께 뽑기 및 합성의 재미를 검증하는 것입니다. 기준은 **프리미티브 상태로 3판 연속 하고 싶은가**입니다.

히어로는 보류 상태이며, 유물, 런 저장과 복구, 배치 시뮬레이션과 리포트 자동 판정은 후속 범위입니다. 밸런스 수치는 임시값이고, 실기 성능 측정과 아트 및 사운드 작업도 남아 있습니다.

1인 개발 과정에서 Claude Code를 활용하고, 설계 및 검증 규칙은 저장소 문서로 관리합니다. 주요 설계 판단과 변경 근거는 위 사례와 기술 상세에서 확인할 수 있습니다.

## 관련 문서와 프로젝트

- [기술 상세](Docs/IMPLEMENTATION_NOTES.md): 책임 경계 변경, 스킬 조립과 중첩, UI 패키지 분리
- [게임 사양](Docs/SPEC.md) / [밸런스 사양](Docs/BALANCE_SPEC.md) / [유닛 스킬](Docs/UNIT_SKILLS.md)
- [아키텍처](Docs/ARCHITECTURE.md) / [맵 생성](Docs/MAP_SPEC.md) / [개발 로드맵](Docs/ROADMAP.md)
- [unity-ui-system](https://github.com/Frenil-client/unity-ui-system): 게임에서 사용하는 자체 UPM UI 패키지
- [전체 포트폴리오](https://github.com/Frenil-client/frenil-portfolio): 렌더링, 셰이더와 재사용 시스템 작업
