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
        private readonly Dictionary<LoopUnit, LineRenderer> unitBeams = new Dictionary<LoopUnit, LineRenderer>();
        private readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
        private Shader tileShader;
        private Material beamMaterial;

        private static readonly Color MonsterColor = new Color(0.85f, 0.25f, 0.25f);
        private static readonly Color BeamColor = new Color(1f, 0.92f, 0.35f);
        private const int BeamVisibleTicks = 4; // 공격 후 빔이 보이는 틱 수(20틱/초 기준 약 0.2초)

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

        // 유닛이 들어간 계열 폴더명. 도플갱어는 DOPP. (EntityPrefabBuilder.KlassFolder 와 동일 규칙)
        private static string KlassFolder(UnitData data)
        {
            if (data.isDoppel) return "DOPP";
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
            SyncMonsters(game.Context.sim);
            SyncUnits(game.Context.sim, game.MapView);
            SyncAttackBeams(game.Context.sim, game.MapView);
        }

        // 유닛이 최근에 공격했으면 대상까지 빔을 그린다(공격이 일어나는 것을 눈으로 확인).
        private void SyncAttackBeams(LoopSimulator sim, LoopMapView mapView)
        {
            foreach (var pair in unitViews)
            {
                LoopUnit u = pair.Key;
                GameObject go = pair.Value;

                LineRenderer lr;
                if (!unitBeams.TryGetValue(u, out lr))
                {
                    lr = CreateBeam(go);
                    unitBeams[u] = lr;
                }

                bool recent = sim.state.tick - u.lastAttackTick <= BeamVisibleTicks;
                lr.enabled = recent;
                if (!recent) continue;

                Vector3 from = go.transform.position;
                Vector3 to = mapView.CellToWorldF((float)u.lastTargetX.ToDoubleForDisplay(), (float)u.lastTargetY.ToDoubleForDisplay()) + new Vector3(0f, 0.35f, 0f);
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
            }
        }

        private LineRenderer CreateBeam(GameObject unitGo)
        {
            GameObject beamGo = new GameObject("AttackBeam");
            beamGo.transform.SetParent(unitGo.transform, false);
            LineRenderer lr = beamGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.widthMultiplier = 0.07f;
            lr.numCapVertices = 2;
            lr.sharedMaterial = GetBeamMaterial();
            lr.startColor = BeamColor;
            lr.endColor = new Color(BeamColor.r, BeamColor.g, BeamColor.b, 0.35f);
            lr.enabled = false;
            return lr;
        }

        private Material GetBeamMaterial()
        {
            if (beamMaterial != null) return beamMaterial;
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Color");
            beamMaterial = new Material(s);
            beamMaterial.color = BeamColor;
            if (beamMaterial.HasProperty("_BaseColor")) beamMaterial.SetColor("_BaseColor", BeamColor);
            return beamMaterial;
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
                        if (r != null) r.sharedMaterial = GetMaterial(MonsterColor);
                    }
                    monsterViews.Add(m, go);
                }
                MonsterView mView = go.GetComponent<MonsterView>();
                if (mView != null) mView.Refresh();

                Fixed fx, fy;
                sim.GetMonsterPosition(m, out fx, out fy);
                go.transform.position = mapView.CellToWorldF((float)fx.ToDoubleForDisplay(), (float)fy.ToDoubleForDisplay()) + new Vector3(0f, 0.35f, 0f);
            }
        }

        private void SyncUnits(LoopSimulator sim, LoopMapView mapView)
        {
            var list = sim.state.unitList;

            // 조합 등으로 필드에서 빠진 유닛의 뷰/빔을 정리한다.
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
                    LineRenderer lr;
                    if (unitBeams.TryGetValue(u, out lr)) { if (lr != null) Destroy(lr.gameObject); unitBeams.Remove(u); }
                    Destroy(unitViews[u]);
                    unitViews.Remove(u);
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
                go.transform.position = mapView.CellToWorld(u.cellX, u.cellY) + new Vector3(0f, 0.3f, 0f);
                unitViews.Add(u, go);
            }
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
