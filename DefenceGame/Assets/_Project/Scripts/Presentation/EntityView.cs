using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 뷰 - GameManager.Context 의 시뮬 상태를 읽어 몬스터/유닛을 그린다.
    // Resources/Entities 의 프리팹을 풀로 재사용한다. 프리팹이 없으면 프리미티브로 폴백.
    // 상태는 Core 가 소유하고 여기서는 표현만 한다(단방향). 색은 모델 렌더러에 공유 머티리얼로 틴트.
    public sealed class EntityView : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private float unitSpeedMultiplier = 1.2f; // 유닛 걷기 속도 = 몬스터 기본 이동속도 * 이 배수

        private Transform monsterRoot;
        private Transform unitRoot;
        private Transform poolRoot;
        private GameObjectPool monsterPool;
        private GameObject unitPrefab;      // 공용 폴백 프리팹
        private GameObject monsterPrefab;

        // 유닛별 프리팹 캐시. id 로 Resources/Entities/Units/<id> 를 찾고, 없으면 공용 폴백.
        private readonly Dictionary<string, GameObject> unitPrefabById = new Dictionary<string, GameObject>();
        private readonly HashSet<string> tintFallbackIds = new HashSet<string>(); // 폴백을 쓰는 id (계열 색으로 틴트)

        private readonly Dictionary<LoopMonster, GameObject> monsterViews = new Dictionary<LoopMonster, GameObject>();
        private readonly Dictionary<LoopUnit, GameObject> unitViews = new Dictionary<LoopUnit, GameObject>();
        private readonly Dictionary<LoopUnit, bool> unitArrived = new Dictionary<LoopUnit, bool>(); // 배치 셀에 도착했는가(이동 중이면 false)
        private readonly Dictionary<LoopStatue, GameObject> statueViews = new Dictionary<LoopStatue, GameObject>();
        private readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
        private Shader tileShader;
        private Transform statueRoot;

        private static readonly Color MonsterColor = new Color(0.85f, 0.25f, 0.25f);
        private static readonly Color BossColor = new Color(0.95f, 0.55f, 0.15f); // 보스 구분색(주황)
        private static readonly Color StatueColor = new Color(0.55f, 0.35f, 0.75f);
        private const float MoveSmoothing = 25f; // 시뮬(20Hz)과 렌더(고FPS) 사이 위치 보간 강도

        private float unitMoveSpeed = 1.848f; // 유닛 걷기 속도(셀/초). 몬스터 기본속도 * 배수. Context 준비 시 갱신
        private bool unitSpeedResolved;
        private int lastRunId = -1;

        // 시뮬 위치는 틱마다만 바뀌므로, 렌더는 프레임마다 부드럽게 추종해 버벅임을 없앤다(프레임률 무관).
        private static Vector3 SmoothPos(Vector3 current, Vector3 target)
        {
            return Vector3.Lerp(current, target, 1f - Mathf.Exp(-MoveSmoothing * Time.deltaTime));
        }

        // 유닛 걷기 속도를 몬스터 기본 이동속도(웨이브 몬스터 통일값) * 배수로 정한다. 한 번만 계산해 캐시.
        private void ResolveUnitSpeed()
        {
            unitSpeedResolved = true;
            var enemies = game.Context.db.enemyList;
            if (enemies != null && enemies.Count > 0)
            {
                double baseSpeed = enemies[0].moveSpeed.ToDoubleForDisplay();
                if (baseSpeed > 0.0) unitMoveSpeed = (float)(baseSpeed * unitSpeedMultiplier);
            }
        }

        private void Start()
        {
            unitPrefab = Resources.Load<GameObject>("Entities/Units/_Base");
            monsterPrefab = Resources.Load<GameObject>("Entities/Monsters/Monster");

            Transform entities = new GameObject("Entities").transform;
            entities.SetParent(transform, false);
            monsterRoot = new GameObject("Monsters").transform;
            monsterRoot.SetParent(entities, false);
            unitRoot = new GameObject("Units").transform;
            unitRoot.SetParent(entities, false);
            statueRoot = new GameObject("Statues").transform;
            statueRoot.SetParent(entities, false);
            poolRoot = new GameObject("Pools").transform;
            poolRoot.SetParent(transform, false);

            monsterPool = new GameObjectPool(CreateMonster, poolRoot, 64);
        }

        // 유닛에 맞는 프리팹을 찾는다(캐시). 계열별 하위 폴더에서 로드하고, 없으면 공용 폴백 + 틴트.
        private GameObject GetUnitPrefab(UnitData data)
        {
            string id = data.id;
            GameObject cached;
            if (unitPrefabById.TryGetValue(id, out cached)) return cached;

            GameObject custom = Resources.Load<GameObject>("Entities/Units/" + KlassFolder(data) + "/" + id);
            if (custom == null)
            {
                custom = unitPrefab;
                tintFallbackIds.Add(id); // 폴백은 계열 색으로 틴트한다
            }
            unitPrefabById[id] = custom;
            return custom;
        }

        // 유닛이 들어간 계열 폴더명. (EntityPrefabBuilder.KlassFolder 와 동일 규칙)
        private static string KlassFolder(UnitData data)
        {
            switch (data.klass)
            {
                case Klass.War: return "WAR";
                case Klass.Arc: return "ARC";
                case Klass.Mag: return "MAG";
                case Klass.Pri: return "PRI";
                case Klass.Thi: return "THI";
                case Klass.Spi: return "SPI";
                default:        return "WAR";
            }
        }

        private void Update()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            if (lastRunId != game.RunId) { lastRunId = game.RunId; ResetRun(); }
            SyncMonsters(game.Context.sim);
            SyncUnits(game.Context.sim, game.MapView);
            SyncStatues(game.Context.sim, game.MapView);
        }

        // 재시작(RunId 변화) 시 이전 런의 엔티티 뷰를 모두 정리한다.
        private void ResetRun()
        {
            foreach (var pair in monsterViews) { if (pair.Value != null) monsterPool.Release(pair.Value); }
            monsterViews.Clear();
            foreach (var pair in unitViews) { if (pair.Value != null) Destroy(pair.Value); }
            unitViews.Clear();
            unitArrived.Clear();
            foreach (var pair in statueViews) { if (pair.Value != null) Destroy(pair.Value); }
            statueViews.Clear();
        }

        // 석상을 배치 칸 위 보라 오브젝트로 그린다. 파괴되면(alive=false) 뷰를 제거해 사라지게 한다.
        private void SyncStatues(LoopSimulator sim, LoopMapView mapView)
        {
            var list = sim.state.statueList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopStatue s = list[i];
                GameObject go;
                bool has = statueViews.TryGetValue(s, out go);

                if (!s.alive)
                {
                    if (has) { Destroy(go); statueViews.Remove(s); }
                    continue;
                }
                if (!has)
                {
                    go = MakePrimitive(PrimitiveType.Cube, new Vector3(0.7f, 0.9f, 0.7f), StatueColor, "Statue");
                    go.transform.SetParent(statueRoot, false);
                    go.transform.position = mapView.CellToWorldF(s.cellX, s.cellY) + new Vector3(0f, 0.55f, 0f);
                    statueViews.Add(s, go);
                }
            }
        }

        // 이 몬스터가 보스인가(enemyId 가 bossById 에 있으면 보스).
        private bool IsBoss(LoopMonster m)
        {
            return game.Context != null && game.Context.bossById != null && game.Context.bossById.ContainsKey(m.enemyId);
        }

        // 몬스터 몸체(보간된) 월드 위치. HP바 등이 몸체에 정렬해 함께 부드럽게 움직이도록 노출.
        public bool TryGetMonsterWorld(LoopMonster m, out Vector3 pos)
        {
            GameObject go;
            if (monsterViews.TryGetValue(m, out go) && go != null)
            {
                pos = go.transform.position;
                return true;
            }
            pos = Vector3.zero;
            return false;
        }

        private void SyncMonsters(LoopSimulator sim)
        {
            LoopMapView mapView = game.MapView;
            var list = sim.state.monsterList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopMonster m = list[i];
                GameObject go;
                bool has = monsterViews.TryGetValue(m, out go);

                if (!m.alive)
                {
                    if (has) { monsterPool.Release(go); monsterViews.Remove(m); }
                    continue;
                }
                if (!has)
                {
                    go = monsterPool.Get(monsterRoot);
                    MonsterView mv = go.GetComponent<MonsterView>();
                    if (mv != null)
                    {
                        mv.Bind(m);
                        Renderer r = mv.GetModelRenderer();
                        // 보스는 구분색으로 그린다(풀 재사용이라 매 획득 시 색을 지정한다).
                        if (r != null) r.sharedMaterial = GetMaterial(IsBoss(m) ? BossColor : MonsterColor);
                    }
                    monsterViews.Add(m, go);
                }
                MonsterView mView = go.GetComponent<MonsterView>();
                if (mView != null) mView.Refresh();

                Fixed fx, fy;
                sim.GetMonsterPosition(m, out fx, out fy);
                Vector3 target = mapView.CellToWorldF((float)fx.ToDoubleForDisplay(), (float)fy.ToDoubleForDisplay()) + new Vector3(0f, 0.35f, 0f);
                // 새로 생성된 뷰는 스냅, 기존 뷰는 부드럽게 추종.
                go.transform.position = has ? SmoothPos(go.transform.position, target) : target;
            }
        }

        private void SyncUnits(LoopSimulator sim, LoopMapView mapView)
        {
            var list = sim.state.unitList;

            // 조합 등으로 필드에서 빠진 유닛의 뷰를 정리한다.
            HashSet<LoopUnit> present = new HashSet<LoopUnit>(list);
            List<LoopUnit> stale = null;
            foreach (var pair in unitViews)
            {
                if (present.Contains(pair.Key)) continue;
                if (stale == null) stale = new List<LoopUnit>();
                stale.Add(pair.Key);
            }
            if (stale != null)
            {
                foreach (var u in stale)
                {
                    Destroy(unitViews[u]);
                    unitViews.Remove(u);
                    unitArrived.Remove(u);
                }
            }

            for (int i = 0; i < list.Count; ++i)
            {
                LoopUnit u = list[i];
                if (unitViews.ContainsKey(u)) continue;

                GameObject go = CreateUnitGO(u.data);
                UnitView uv = go.GetComponent<UnitView>();
                if (uv != null)
                {
                    uv.Bind(u);
                    // 개별 프리팹이 없어 공용 폴백을 쓰는 경우에만 계열 색으로 틴트한다.
                    if (tintFallbackIds.Contains(u.data.id))
                    {
                        Renderer r = uv.GetModelRenderer();
                        if (r != null) r.sharedMaterial = GetMaterial(KlassColor(u.data.klass));
                    }
                }
                go.name = "Unit_" + u.data.id;
                go.transform.SetParent(unitRoot, false);
                // 생성 시엔 배치 셀로 스냅(원점에서 미끄러져 들어오지 않게).
                go.transform.position = mapView.CellToWorldF(u.cellX, u.cellY) + new Vector3(0f, 0.3f, 0f);
                unitViews.Add(u, go);
            }

            // 유닛 이동: 집중 대상이 있으면 추격, 없으면 홈 셀로 복귀. 일정 속도로 걸어간다.
            // 도착 여부를 기록해, (집중 없이) 재배치 이동 중인 유닛은 전투에서 공격을 건너뛰게 한다(이동과 공격 분리).
            if (!unitSpeedResolved) ResolveUnitSpeed();
            float step = unitMoveSpeed * Time.deltaTime * game.Speed;
            foreach (var pair in unitViews)
            {
                LoopUnit u = pair.Key;

                // 집중 대상이 죽으면 해제하되, 원래 홈이 아니라 현재 위치에서 가장 가까운 빈 배치칸으로 홈을 재설정한다.
                bool focusEnded = false;
                if (u.focusMonster != null && !u.focusMonster.alive) { u.focusMonster = null; focusEnded = true; }
                if (u.focusStatue != null && !u.focusStatue.alive) { u.focusStatue = null; focusEnded = true; }
                if (focusEnded)
                {
                    Vector2 cc = game.MapView.WorldToCellF(pair.Value.transform.position);
                    sim.RelocateUnit(u, Mathf.RoundToInt(cc.x), Mathf.RoundToInt(cc.y));
                }

                Vector3 cur = pair.Value.transform.position;
                Vector3 goal;
                // 추적은 대상까지 전부 다가가지 않고 최대 사정거리 가장자리까지만 접근한다.
                if (u.focusMonster != null) goal = ApproachGoal(cur, MonsterGoal(sim, u.focusMonster), RangeWorld(u));
                else if (u.focusStatue != null) goal = ApproachGoal(cur, mapView.CellToWorldF(u.focusStatue.cellX, u.focusStatue.cellY) + new Vector3(0f, 0.3f, 0f), RangeWorld(u));
                else goal = mapView.CellToWorldF(u.cellX, u.cellY) + new Vector3(0f, 0.3f, 0f);

                Vector3 next = Vector3.MoveTowards(cur, goal, step);
                pair.Value.transform.position = next;
                unitArrived[u] = (next - goal).sqrMagnitude < 0.0025f; // 0.05 셀 이내면 도착으로 본다
            }
        }

        // 유닛의 최대 사정거리(월드 단위). 전투 사거리(셀)와 셀 크기를 곱한다.
        private float RangeWorld(LoopUnit u)
        {
            return (float)u.data.range.ToDoubleForDisplay() * game.MapView.cellSize;
        }

        // 대상까지 접근하되 최대 사정거리 가장자리(살짝 안쪽)에서 멈춘다. 이미 사거리 안이면 제자리(더 다가가지 않음).
        private Vector3 ApproachGoal(Vector3 current, Vector3 target, float rangeWorld)
        {
            Vector3 flat = target - current;
            flat.y = 0f; // 수평 거리로 판정(높이차 무시)
            float dist = flat.magnitude;
            if (dist <= rangeWorld || dist < 1e-4f) return current;
            Vector3 dir = flat / dist;
            Vector3 stop = target - dir * (rangeWorld * 0.95f);
            stop.y = target.y;
            return stop;
        }

        // 집중 대상 몬스터의 추격 목표 위치(보간된 몸체 우선, 유닛 높이로).
        private Vector3 MonsterGoal(LoopSimulator sim, LoopMonster m)
        {
            Vector3 w;
            if (TryGetMonsterWorld(m, out w)) { w.y = 0.3f; return w; }
            Fixed fx, fy;
            sim.GetMonsterPosition(m, out fx, out fy);
            return game.MapView.CellToWorldF((float)fx.ToDoubleForDisplay(), (float)fy.ToDoubleForDisplay()) + new Vector3(0f, 0.3f, 0f);
        }

        // 유닛이 (집중 없이) 홈 셀에 도착했는가. 전투가 이동 중 공격을 막는 데 쓴다. 정보가 없으면 도착으로 간주.
        public bool IsUnitArrived(LoopUnit u)
        {
            bool arrived;
            return !unitArrived.TryGetValue(u, out arrived) || arrived;
        }

        // 유닛의 현재 렌더 위치(전투가 실제 위치에서 공격/사거리 판정하도록 노출).
        public bool TryGetUnitWorld(LoopUnit u, out Vector3 pos)
        {
            GameObject go;
            if (unitViews.TryGetValue(u, out go) && go != null) { pos = go.transform.position; return true; }
            pos = Vector3.zero;
            return false;
        }

        private GameObject CreateMonster()
        {
            GameObject go = monsterPrefab != null
                ? Instantiate(monsterPrefab)
                : MakePrimitive(PrimitiveType.Capsule, new Vector3(0.4f, 0.4f, 0.4f), MonsterColor, "Monster");
            if (go.GetComponent<MonsterView>() == null) go.AddComponent<MonsterView>();
            return go;
        }

        private GameObject CreateUnitGO(UnitData data)
        {
            GameObject prefab = GetUnitPrefab(data);
            GameObject go = prefab != null
                ? Instantiate(prefab)
                : MakePrimitive(PrimitiveType.Cube, new Vector3(0.6f, 0.6f, 0.6f), Color.gray, "Unit");
            if (go.GetComponent<UnitView>() == null) go.AddComponent<UnitView>();
            return go;
        }

        private GameObject MakePrimitive(PrimitiveType type, Vector3 scale, Color color, string goName)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = goName;
            go.transform.localScale = scale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = GetMaterial(color);
            return go;
        }

        private Material GetMaterial(Color color)
        {
            Material cached;
            if (matCache.TryGetValue(color, out cached)) return cached;

            if (tileShader == null)
            {
                tileShader = Shader.Find("Universal Render Pipeline/Lit");
                if (tileShader == null) tileShader = Shader.Find("Standard");
            }
            Material mat = new Material(tileShader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            matCache[color] = mat;
            return mat;
        }

        // 계열별 색 (v0.4). 속성/역할 축은 폐기되고 계열이 유닛 정체성이다 (SPEC 3-2).
        private static Color KlassColor(Klass klass)
        {
            switch (klass)
            {
                case Klass.War: return new Color(0.80f, 0.30f, 0.25f); // 전사 - 적
                case Klass.Arc: return new Color(0.45f, 0.75f, 0.45f); // 궁수 - 녹
                case Klass.Mag: return new Color(0.50f, 0.55f, 0.95f); // 법사 - 청
                case Klass.Pri: return new Color(0.95f, 0.92f, 0.70f); // 사제 - 금
                case Klass.Thi: return new Color(0.55f, 0.45f, 0.70f); // 도적 - 보라
                case Klass.Spi: return new Color(0.45f, 0.85f, 0.90f); // 정령 - 청록
                default:        return Color.white;
            }
        }
    }
}
