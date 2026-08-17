# SIM_SPEC.md

SYNTHESIS (가칭) - 헤드리스 시뮬레이터 사양

배치 위치: Docs/SIM_SPEC.md / 버전 0.3

버전 0.3 변경: 공세 정책 삭제, 배치 정책 신설, 맵 생성 검증(SIM-10, SIM-11) 추가, RunResult에 누적과 맵 필드 추가

> **개정 필요 (v0.4 반영 대기).** 이 문서는 v0.3 설계 기준이라 아래가 현재와 어긋난다. Sim 콘솔 프로젝트는 아직 없고(STEP 7에서 신설), 그때 이 문서를 아래 방향으로 다시 쓴다. 지어내지 않기 위해 세부 재설계는 STEP 7로 미룬다.
> - **덱 시스템 폐기.** IDeckPolicy/SynergyDeck/GuardDeck/RandomDeck, deck-sweep(210조합), deckList 필드, SIM-06(210조합)은 무효다.
> - **필드 누적 상한 패배 폐기.** 패배는 보스 제한시간 미처리다(SPEC 2-4). accumMax/accumAtWaveN, accum-curve, SIM-04(누적 45)는 밀집 지표로 재해석하거나 폐기한다.
> - **전투/유닛 이동/투사체는 Unity 실시간으로 이전.** Core 시뮬은 맵/스폰/순회/배치/코스트/조합만 결정적으로 소유한다. 전투를 헤드리스로 재현하려면 Core에 별도 전투 모델을 추가하는 결정이 선행해야 한다(현재 없음).
> - Fusion/ 은 실제 Combination/ 이다.

---

## 1. 목적

밸런스 수치를 감으로 정하지 않기 위해 존재한다. Unity 없이 콘솔에서 수만 번의 런을 돌려 합성 도달률, 밀집 곡선, (히어로 도입 후)히어로 성장, 맵 난이도 분산을 측정한다.

BALANCE_SPEC.md의 SIM 항목을 자동으로 답하는 것이 최종 목표다.

**우선순위 주의**: 시뮬레이터는 도구이지 목적이 아니다(시뮬/검증은 기능 이후 필요할 때 사후 추가한다). 재미 판정은 직접 플레이로 하고, 시뮬은 "운이 나빠도 유저 선택으로 클리어 가능한가"를 확인하는 데 쓴다.

---

## 2. 프로젝트 구성

```
Sim/Synthesis.Sim/
  Program.cs                진입점, 인자 파싱
  Runner/
    BatchRunner.cs          다중 런 병렬 실행
    RunResult.cs            단일 런 결과
  Scenario/
    Scenario.cs             시나리오 정의
  Policy/
    PolicySet.cs            4개 축의 정책 묶음
    Deck/
      IDeckPolicy.cs        런 시작 전 덱 6종 선택
      SynergyDeck.cs        합성 도달 수 최대화
      GuardDeck.cs          관통 또는 방깎 + 라인 반드시 포함
      RandomDeck.cs         무작위 6종 (하한 측정)
    Fusion/
      IFusionPolicy.cs      합성 판단
      GreedyFusion.cs       가능하면 즉시 합성
      HoarderFusion.cs      상위 레시피 재료를 아낌
    Card/
      ICardPolicy.cs        스킬 카드 3택 1
      CombatCard.cs         전투형 우선
      AuraCard.cs           오라형 우선
    Placement/
      IPlacementPolicy.cs   유닛과 히어로 배치 위치 결정
      GreedyCoverage.cs     커버 효율이 가장 높은 타일부터
      AuraCluster.cs        히어로 오라 반경 안에 아군을 몰아넣음
  Report/
    ReportWriter.cs         CSV 출력
```

Synthesis.Core.csproj를 참조한다. 전투 로직과 맵 생성기를 다시 구현하지 않는다.

---

## 3. 실행

```
dotnet run --project Sim/Synthesis.Sim -- \
  --data ./Data \
  --runs 100000 \
  --seed 1 \
  --deck guard \
  --fusion greedy \
  --card combat \
  --placement coverage \
  --hero all \
  --threads 8 \
  --out ./Reports/sim-001
```

| 인자 | 기본값 | 설명 |
|---|---|---|
| --data | ./Data | CSV 디렉터리 |
| --runs | 10000 | 런 수 |
| --seed | 1 | 시작 시드. 런 i는 seed + i |
| --deck | guard | 덱 정책 |
| --fusion | greedy | 합성 정책 |
| --card | combat | 카드 선택 정책 |
| --placement | coverage | 배치 정책 |
| --hero | all | all이면 런마다 무작위, ID 지정 시 고정 |
| --deck-sweep | false | 210가지 덱 전수 순회 (SIM-06) |
| --map-sweep | false | 맵 1000개 생성 후 각각 측정 (SIM-10, SIM-11) |
| --map-seed | 런 시드와 동일 | 맵 시드를 분리하고 싶을 때 |
| --ascension | 0 | 승급 단계 |
| --threads | 코어 수 | 병렬 스레드 |
| --out | ./Reports/latest | 출력 디렉터리 |
| --verify | false | 같은 시드 2회 실행 후 해시 대조 |

---

## 4. 플레이어 정책

한 정책만 쓰면 그 정책에 최적화된 밸런스가 나온다. 4개 축을 조합해 교차 검증한다.

### 4-1. 배치 정책이 새로 중요해진 이유

루프형에서 배치는 곧 커버 효율이다. 그리고 히어로 오라가 들어오면 커버 효율과 오라 밀집도가 충돌한다. 두 극단 정책으로 상하한을 잡는다.

| 정책 | 방침 | 용도 |
|---|---|---|
| GreedyCoverage | 커버 효율이 높은 타일부터 채운다. 오라 무시 | 오라 없는 하한 |
| AuraCluster | 히어로 오라 반경 안에 아군을 최대한 몰아넣는다 | 오라 활용 상한 |

두 정책의 승률 차이가 **오라 시스템의 실질 가치**다. 차이가 작으면 오라가 장식이라는 뜻이므로 auraValue를 올려야 한다.

### 4-2. 호출 시점

정책은 의사결정 시점에만 호출된다.

```csharp
public interface IPlacementPolicy
{
    // STEP 4. 핵심 - 새 유닛을 어느 배치 타일에 놓을지 결정한다
    int GetPlacementTile(GameState gameState, string unitId);
    int GetHeroPlacementTile(GameState gameState);
}
```

정책은 GameState를 읽기 전용으로 받고 행동을 ActionBuffer에 적재한다.

---

## 5. 단일 런 결과

```csharp
public struct RunResult
{
    public long seed;
    public long mapSeed;
    public int clearedWave;
    public bool isCleared;

    public string heroId;
    public List<string> deckList;
    public int policyHash;

    public List<string> madeUnitList;
    public List<string> hiddenReachedList;
    public List<string> cardTakenList;
    public List<string> evolvedList;

    public int heroLevelFinal;
    public int heroLevelAtWave10;
    public int heroLevelAtWave20;
    public int heroTileIndex;          // 최종 히어로 배치 타일

    public int accumMax;               // 런 중 최대 누적
    public int accumAtWave10;
    public int accumAtWave20;
    public int accumAtWave25;
    public int failWave;               // 누적 상한 도달 웨이브. 클리어 시 -1

    public bool deckHasGuardKey;       // 관통 또는 방깎
    public bool deckHasLine;           // 라인 역할
    public int firstLineWave;          // 라인딜 수단을 처음 확보한 웨이브

    public int mapPerimeter;
    public int mapCornerCount;
    public int mapBuildArea;
    public int mapCoverageIndex;

    public ulong stateHash;
}
```

---

## 6. 리포트 출력

| 파일 | 내용 |
|---|---|
| runs.csv | 런 단위 원본 |
| hidden-reach.csv | 히든별 도달 비율 |
| accum-curve.csv | 웨이브별 평균 누적과 분위수 |
| deck-balance.csv | 덱 210조합별 클리어율 |
| hero-balance.csv | 히어로별 승률과 신뢰구간 |
| hero-growth.csv | 웨이브별 히어로 평균 레벨 |
| skillcard-balance.csv | 카드별 선택률과 클리어율 |
| aura-value.csv | GreedyCoverage 대 AuraCluster 승률 비교 |
| map-variance.csv | 맵별 클리어율과 커버 지수 |
| map-optimal-tile.csv | 맵별 최적 히어로 배치 타일 |
| violations.txt | 자동 판정 실패 목록 |

stdout 요약 예시:

```
runs=100000  deck=guard fusion=greedy card=combat placement=coverage
cleared=28.6%
hidden reach:  H01 17.4%  H02 15.1%  H03 19.8%  H04 22.6%  H05 8.7%  H06 4.1%
  WARN  H05 below target range (10-25%)
accum curve:  w10 avg 12  w20 avg 28  w25 avg 41  peak 47   OK
line damage by wave 15 (line decks only): 96.2%   OK
aura value:  coverage 28.6%  cluster 34.1%  delta 5.5%p   OK
map variance: mean 28.6%  stdev 3.1%  (11% of mean)   OK
map optimal tile: 82% of maps differ from the modal tile   OK
determinism hash: stable
```

---

## 7. 검증 항목 구현

| ID | 측정 방법 | 판정 |
|---|---|---|
| SIM-01 | hiddenReachedList 집계 | H01-H05가 10-25%, H06이 5-12% |
| SIM-02 | 하위 5% 시드 재실행 | 보스 1 격파율 80% 이상 |
| SIM-03 | deckHasLine이 true인 런의 firstLineWave <= 15 비율 | 95% 이상 |
| SIM-04 | accumAtWave25 평균 | 45 미만 |
| SIM-05 | 유닛별 제작 시 클리어율과 전체 클리어율의 차 | 상위 15% 초과 없음 |
| SIM-06 | --deck-sweep 210조합 각 1000런 | 클리어율 0% 없음, 최고와 최저 차이 40%p 이내 |
| SIM-07 | heroId별 집계 | 승률 신뢰구간이 서로 겹침 |
| SIM-08 | heroLevelAtWave10 / 20 / Final 평균 | 각각 3-4, 5-6, 7-8 |
| SIM-09 | cardTakenList 집계 | 선택률 60% 초과 또는 5% 미만 카드 없음 |
| SIM-10 | --map-sweep 1000맵 각 200런 | 클리어율 표준편차가 평균의 15% 이내 |
| SIM-11 | 맵별 최적 heroTileIndex 분포 | 최빈 타일과 다른 맵이 70% 이상 |

**SIM-11이 랜덤 맵 생성의 존재 이유를 검증한다.** 맵이 달라도 최적 배치가 같다면 랜덤화는 장식이므로 mapgen 파라미터를 다시 잡거나 맵 랜덤화를 포기한다.

추가 측정(판정 없음, 참고용):

- GreedyCoverage 대 AuraCluster 승률 차이. 5%p 미만이면 오라가 약하다는 신호

---

## 8. 성능 목표

- 10만 런을 8스레드에서 5분 이내
- SIM-06의 21만 런은 15분 이내
- SIM-10의 20만 런은 15분 이내
- 단일 런은 30웨이브, 웨이브당 최대 45초, 20틱이므로 최대 약 27,000틱
- 병렬화는 런 단위로만. 런 내부는 단일 스레드로 결정성을 지킨다
- 런마다 상태 객체를 재사용해 GC 스파이크를 막는다

---

## 9. 결정성 검증

- --verify로 같은 시드를 두 번 돌려 stateHash 대조
- CI에서 커밋마다 100런 verify
- stateHash는 매 웨이브 종료 시점 전체 상태를 순서 고정으로 직렬화해 해시
- **맵 생성 재현성도 함께 검증한다.** 같은 mapSeed가 같은 LoopMap을 내는지 확인
- 불일치 시 즉시 실패

---

## 10. 주의

- 시뮬레이터는 밸런스가 균형 잡혔는지를 답하지 재미있는지를 답하지 못한다
- 지배 전략이 완전히 제거된 게임이 밋밋해진다. 로그라이트는 "이번 판 사기 조합 떴다"가 핵심 쾌감이므로 SIM-05와 SIM-09 기준을 지나치게 엄격하게 잡지 않는다
- 특히 히어로 스킬 카드는 강력한 카드가 존재하는 것 자체가 재미다. SIM-09는 "아무도 안 고르는 카드"와 "항상 고르는 카드"를 잡아내는 용도다
- 수치 판정은 시뮬로, 재미 판정은 직접 플레이로. 둘을 섞지 않는다
