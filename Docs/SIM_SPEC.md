# SIM_SPEC.md

SYNTHESIS (가칭) - 헤드리스 시뮬레이터 사양

배치 위치: Docs/SIM_SPEC.md

---

## 1. 목적

밸런스 수치를 감으로 정하지 않기 위해 존재한다. Unity 없이 콘솔에서 수만 번의 런을 돌려 조합 도달률, 승률, 지배 전략 여부를 측정한다.

BALANCE_SPEC.md 9장의 SIM-01부터 SIM-08까지를 자동으로 답하는 것이 최종 목표다.

---

## 2. 프로젝트 구성

```
Sim/Synthesis.Sim/
  Program.cs           진입점, 인자 파싱
  Runner/
    BatchRunner.cs     다중 런 병렬 실행
    RunResult.cs       단일 런 결과 구조체
  Scenario/
    Scenario.cs        시나리오 정의 로드
  Policy/
    IPlayerPolicy.cs   자동 플레이 정책 인터페이스
    GreedyPolicy.cs    가능한 조합을 즉시 수행
    HoarderPolicy.cs   상위 조합을 노리고 재료를 아낌
    AdvancePolicy.cs   공세를 적극적으로 수행
    TurtlePolicy.cs    공세를 전혀 하지 않음
  Report/
    ReportWriter.cs    CSV 출력
```

Synthesis.Core.csproj를 참조한다. 전투 로직을 다시 구현하지 않는다.

---

## 3. 실행

```
dotnet run --project Sim/Synthesis.Sim -- \
  --data ./Data \
  --runs 100000 \
  --seed 1 \
  --policy greedy \
  --scenario default \
  --threads 8 \
  --out ./Reports/sim-001
```

| 인자 | 기본값 | 설명 |
|---|---|---|
| --data | ./Data | CSV 디렉터리 |
| --runs | 10000 | 실행할 런 수 |
| --seed | 1 | 시작 시드. 런 i는 seed + i를 사용 |
| --policy | greedy | 플레이어 정책 |
| --scenario | default | 시나리오 이름 |
| --threads | 코어 수 | 병렬 스레드 수 |
| --out | ./Reports/latest | 출력 디렉터리 |
| --verify | false | 같은 시드 2회 실행 후 해시 대조 |

---

## 4. 플레이어 정책

시뮬레이터는 사람이 아니므로 플레이 방침을 코드로 정의한다. **한 정책만 쓰면 그 정책에 최적화된 밸런스가 나온다.** 최소 4개 정책으로 교차 검증한다.

| 정책 | 조합 | 공세 | 용도 |
|---|---|---|---|
| Greedy | 가능한 즉시 조합 | 하지 않음 | 하한선. 초보 플레이 근사 |
| Hoarder | 상위 조합 재료를 아낌 | 하지 않음 | 조합 트리 도달률 상한 측정 |
| Advance | 즉시 조합 | 적극 파견 | 공세 가치 측정 |
| Turtle | 즉시 조합 | 절대 안 함 | Advance와 대조군 |

정책은 매 틱이 아니라 **의사결정 시점에만** 호출된다. 뽑기 직후, 웨이브 시작 전, 조합 가능 시점.

```csharp
public interface IPlayerPolicy
{
    // STEP 3. 핵심 - 웨이브 시작 전 조합과 배치를 결정한다
    void OnWaveStart(GameState gameState, ActionBuffer actionBuffer);
    void OnUnitGranted(GameState gameState, string unitId, ActionBuffer actionBuffer);
}
```

정책은 GameState를 읽기 전용으로 받고, 행동을 ActionBuffer에 적재한다. 정책이 상태를 직접 수정하면 결정성이 깨진다.

---

## 5. 시나리오

Scenario는 무엇을 고정하고 무엇을 변화시킬지 정의한다.

```json
{
  "name": "default",
  "leaderId": null,
  "ascension": 0,
  "mapId": "map01",
  "forceUnitPool": null,
  "disableGuarantee": false
}
```

| 필드 | 용도 |
|---|---|
| leaderId | null이면 런마다 무작위 선택. 고정하면 리더별 측정 |
| ascension | 승급 단계. 뮤테이터 적용 |
| forceUnitPool | 특정 유닛만 뽑히게 강제. 최악 시드 재현용 |
| disableGuarantee | 보장 규칙 G1-G5 해제. 5승급 검증용 |

---

## 6. 단일 런 결과

```csharp
public struct RunResult
{
    public long seed;
    public int clearedWave;          // 몇 웨이브까지 갔는가
    public bool isCleared;           // 24웨이브 완주 여부
    public string leaderId;
    public int policyId;

    public List<string> madeUnitList;      // 이 런에서 만든 유닛 전부
    public List<string> hiddenReachedList; // 도달한 히든 조합
    public int advanceCount;               // 파견 시도 횟수
    public int advanceFailCount;           // 파견 중 사망 횟수
    public int zoneUnlockedMax;            // 최대 해금 구역
    public int spawnDestroyedCount;        // 파괴한 스폰 수
    public long boss1PreDamage;            // 보스별 사전 타격량
    public long boss2PreDamage;
    public long boss3PreDamage;
    public int firstGuardKeyWave;          // 관통 또는 방깎을 처음 얻은 웨이브
    public ulong stateHash;                // 결정성 검증용
}
```

---

## 7. 리포트 출력

출력 디렉터리에 CSV로 쓴다. 사람이 읽는 요약은 stdout에 낸다.

| 파일 | 내용 |
|---|---|
| runs.csv | 런 단위 원본 결과 |
| hidden-reach.csv | 히든 조합별 도달 비율 |
| unit-usage.csv | 유닛별 제작 횟수와 그 런의 클리어율 |
| wave-death.csv | 웨이브별 사망 히스토그램 |
| advance-value.csv | 공세 정책과 무공세 정책의 승률 비교 |
| leader-balance.csv | 리더별 승률과 신뢰구간 |
| violations.txt | 불변식 위반 목록 |

stdout 요약 예시:

```
runs=100000  policy=greedy  cleared=31.4%
hidden reach:  H01 18.2%  H02 15.7%  H03 21.0%  H04 12.9%  H05 9.1%  H06 4.4%
  WARN  H05 below target range (10-25%)
  WARN  H06 below target range (5-12%)
guard key by wave 8: 100.0%   OK
wave death peaks: 8 (22.1%), 16 (28.4%), 24 (18.0%)   OK
determinism hash: stable
```

---

## 8. 검증 항목 구현

BALANCE_SPEC.md 9장과 1대1로 대응한다.

| ID | 측정 방법 | 판정 |
|---|---|---|
| SIM-01 | hiddenReachedList 집계 | H01-H05가 10-25%, H06이 5-12% |
| SIM-02 | 하위 5% 시드 추출 후 재실행 | 보스 1 격파율 80% 이상 |
| SIM-03 | firstGuardKeyWave <= 8 비율 | 100% |
| SIM-04 | Advance 정책과 Turtle 정책 승률 비교 | 둘 다 클리어 가능, Advance가 5-15%p 유리 |
| SIM-05 | 유닛별 제작 시 클리어율과 전체 클리어율의 차 | 상위 15% 초과 없음 |
| SIM-06 | 관통 경로 런과 방깎 경로 런 분리 집계 | 승률 차이 10%p 이내 |
| SIM-07 | clearedWave 히스토그램 | 8, 16, 24에 피크. 중간 절벽 없음 |
| SIM-08 | leaderId별 집계 | 승률 신뢰구간이 서로 겹침 |

각 항목은 통과와 실패를 자동 판정하고 violations.txt에 기록한다. 사람이 CSV를 눈으로 읽어 판단하는 구조로 만들지 않는다.

---

## 9. 성능 목표

- 10만 런을 8스레드에서 5분 이내
- 단일 런은 24웨이브, 웨이브당 최대 60초, 20틱이므로 최대 약 29,000틱
- 렌더링과 애셋 로드가 없으므로 순수 로직만 돈다
- 병렬화는 런 단위로만 한다. 런 내부는 단일 스레드로 유지해 결정성을 지킨다

메모리 할당을 줄이기 위해 런마다 상태 객체를 재사용한다. GC 스파이크가 나면 처리량이 급감한다.

---

## 10. 결정성 검증

--verify 옵션으로 같은 시드를 두 번 돌려 stateHash를 대조한다.

- CI에서 커밋마다 100런 verify를 돌린다
- stateHash는 매 웨이브 종료 시점의 전체 상태를 순서 고정으로 직렬화해 해시한다
- 해시가 불일치하면 즉시 실패시킨다. 결정성이 깨진 채로 쌓인 밸런싱은 전부 무효다

**Unity 빌드와 Sim 콘솔의 대조**는 STEP 6 이후 주기적으로 수행한다. Unity 쪽에 동일 시드로 헤드리스 런을 돌리고 stateHash를 비교하는 에디터 메뉴를 둔다.

---

## 11. 주의

- 시뮬레이터는 **밸런스가 균형 잡혔는지**를 답하지 **재미있는지**를 답하지 못한다
- 지배 전략이 완전히 제거된 게임이 밋밋해지는 경우가 있다. 로그라이트는 오히려 "이번 판 사기 조합 떴다"가 핵심 쾌감이므로, SIM-05의 기준을 지나치게 엄격하게 잡지 않는다
- 수치 판정은 시뮬로, 재미 판정은 직접 플레이로 한다. 둘을 섞지 않는다
