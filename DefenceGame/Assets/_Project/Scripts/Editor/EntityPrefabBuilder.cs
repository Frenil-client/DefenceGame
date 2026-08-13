using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Synthesis.Core.Data;
using Synthesis.Presentation;

namespace Synthesis.Editor
{
    // STEP 2/3(재작업). 에디터 툴 - 유닛/몬스터 프리팹을 코드로 생성한다(YAML 수기 금지).
    // 프로토는 프리미티브 Model. 이후 STEP 8 에서 Addressables NPR 모델로 스왑(코드 불변).
    // 생성 위치: Resources/Entities (EntityView 가 Resources.Load 로 찾는다).
    // 폴더 구성(나중에 몬스터/소환물/구조물과 구별):
    //   Entities/Units/<KLASS>/<id>  유닛별 프리팹(계열 폴더: WAR/ARC/MAG/PRI/THI/SPI, 도플갱어는 DOPP)
    //   Entities/Units/_Base         개별 프리팹이 없을 때 쓰는 공용 폴백
    //   Entities/Monsters/Monster    몬스터 폴백(추후 적별)
    //   Entities/Summons/            소환물(예정)
    //   Entities/Structures/         구조물: 석상 등(예정)
    public static class EntityPrefabBuilder
    {
        private const string EntitiesDir = "Assets/_Project/Resources/Entities";
        private const string UnitsDir = EntitiesDir + "/Units";
        private const string MonstersDir = EntitiesDir + "/Monsters";
        private const string SummonsDir = EntitiesDir + "/Summons";
        private const string StructuresDir = EntitiesDir + "/Structures";
        private const string MatDir = "Assets/_Project/Materials";

        [MenuItem("Synthesis/Build Entity Prefabs")]
        public static void BuildEntityPrefabs()
        {
            EnsureCategoryFolders();
            EnsureFolder(MatDir);

            Material modelMat = GetOrCreateMat("EntityModel", new Color(0.75f, 0.75f, 0.78f));
            Material rangeMat = GetOrCreateMat("RangeRing", new Color(0.30f, 0.80f, 0.90f));

            BuildUnitPrefab(modelMat, rangeMat);
            BuildMonsterPrefab(modelMat);
            int unitCount = BuildPerUnitPrefabs(rangeMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EntityPrefabBuilder] 공용 폴백 + 유닛별 " + unitCount + "종 프리팹 생성 완료: " + EntitiesDir);
        }

        [MenuItem("Synthesis/Build Unit Prefabs (per unit)")]
        public static void BuildUnitPrefabsMenu()
        {
            EnsureCategoryFolders();
            EnsureFolder(MatDir);
            Material rangeMat = GetOrCreateMat("RangeRing", new Color(0.30f, 0.80f, 0.90f));
            int unitCount = BuildPerUnitPrefabs(rangeMat);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EntityPrefabBuilder] 유닛별 프리팹 " + unitCount + "종 생성 완료: " + UnitsDir);
        }

        // 카테고리 폴더를 만든다(비어 있어도 구조를 잡아 몬스터/소환물/구조물과 구별).
        private static void EnsureCategoryFolders()
        {
            EnsureFolder(EntitiesDir);
            EnsureFolder(UnitsDir);
            EnsureFolder(MonstersDir);
            EnsureFolder(SummonsDir);
            EnsureFolder(StructuresDir);
        }

        // units.csv 의 모든 유닛에 대해 개별 프리팹을 만든다. 색은 계열, 크기와 표식은 등급.
        private static int BuildPerUnitPrefabs(Material rangeMat)
        {
            EnsureFolder(UnitsDir);

            GameDatabase db = RuntimeDataLoader.LoadDatabase();
            if (db.unitList.Count == 0)
            {
                Debug.LogError("[EntityPrefabBuilder] units.csv 를 읽지 못했습니다.");
                return 0;
            }

            Material pipMat = GetOrCreateMat("UnitPip", new Color(0.98f, 0.98f, 0.9f));

            int count = 0;
            foreach (var unit in db.unitList)
            {
                if (unit == null || string.IsNullOrEmpty(unit.id)) continue;
                // 색은 계열 기준(도플갱어는 별도)이라 머티리얼을 계열 단위로 공유한다.
                string matKey = unit.isDoppel ? "Unit_DOPP" : "Unit_Klass_" + unit.klass;
                Material klassMat = GetOrCreateMat(matKey, KlassColor(unit));
                BuildOneUnitPrefab(unit, klassMat, rangeMat, pipMat);
                ++count;
            }
            return count;
        }

        // 유닛 한 종의 프리팹: UnitView + 등급 크기 모델(계열 색) + 사거리 링 + 등급 수만큼의 표식 핍.
        private static void BuildOneUnitPrefab(UnitData unit, Material klassMat, Material rangeMat, Material pipMat)
        {
            GameObject root = new GameObject(unit.id);
            UnitView view = root.AddComponent<UnitView>();

            float size = 0.5f + Mathf.Max(0, unit.tier) * 0.08f; // 등급이 높을수록 큰 모델
            GameObject model = MakeMesh(PrimitiveType.Cube, "Model", root.transform, new Vector3(size, size, size), klassMat);

            // 사거리 링
            GameObject rangeGo = new GameObject("Range");
            rangeGo.transform.SetParent(root.transform, false);
            RangeIndicator range = rangeGo.AddComponent<RangeIndicator>();
            GameObject ring = MakeMesh(PrimitiveType.Cylinder, "Ring", rangeGo.transform, new Vector3(1f, 0.02f, 1f), rangeMat);

            // 등급 표식: 등급 수만큼 작은 핍을 모델 위에 한 줄로
            int pips = Mathf.Clamp(unit.tier, 0, 5);
            if (pips > 0)
            {
                GameObject pipRoot = new GameObject("TierPips");
                pipRoot.transform.SetParent(root.transform, false);
                pipRoot.transform.localPosition = new Vector3(0f, size * 0.5f + 0.12f, 0f);
                float step = 0.15f;
                float startX = -(pips - 1) * step * 0.5f;
                for (int i = 0; i < pips; ++i)
                {
                    GameObject pip = MakeMesh(PrimitiveType.Cube, "Pip" + i, pipRoot.transform, new Vector3(0.1f, 0.1f, 0.1f), pipMat);
                    pip.transform.localPosition = new Vector3(startX + i * step, 0f, 0f);
                }
            }

            var so = new SerializedObject(view);
            SetRef(so, "model", model.transform);
            SetRef(so, "rangeIndicator", range);
            so.ApplyModifiedProperties();

            var soRange = new SerializedObject(range);
            SetRef(soRange, "ring", ring.transform);
            soRange.ApplyModifiedProperties();

            // 계열별 하위 폴더에 저장한다.
            string subDir = UnitsDir + "/" + KlassFolder(unit);
            EnsureFolder(subDir);
            string path = subDir + "/" + unit.id + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        // 유닛이 들어갈 계열 폴더명. 도플갱어는 별도(DOPP).
        public static string KlassFolder(UnitData unit)
        {
            if (unit.isDoppel) return "DOPP";
            switch (unit.klass)
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

        // 계열별 색 (EntityView.KlassColor 와 동일). 도플갱어는 무채색.
        private static Color KlassColor(UnitData unit)
        {
            if (unit.isDoppel) return new Color(0.60f, 0.60f, 0.62f);
            switch (unit.klass)
            {
                case Klass.War: return new Color(0.80f, 0.30f, 0.25f);
                case Klass.Arc: return new Color(0.45f, 0.75f, 0.45f);
                case Klass.Mag: return new Color(0.50f, 0.55f, 0.95f);
                case Klass.Pri: return new Color(0.95f, 0.92f, 0.70f);
                case Klass.Thi: return new Color(0.55f, 0.45f, 0.70f);
                case Klass.Spi: return new Color(0.45f, 0.85f, 0.90f);
                default:        return Color.white;
            }
        }

        private static void BuildUnitPrefab(Material modelMat, Material rangeMat)
        {
            GameObject root = new GameObject("Unit");
            UnitView view = root.AddComponent<UnitView>();

            GameObject model = MakeMesh(PrimitiveType.Cube, "Model", root.transform, new Vector3(0.6f, 0.6f, 0.6f), modelMat);

            GameObject rangeGo = new GameObject("Range");
            rangeGo.transform.SetParent(root.transform, false);
            RangeIndicator range = rangeGo.AddComponent<RangeIndicator>();
            GameObject ring = MakeMesh(PrimitiveType.Cylinder, "Ring", rangeGo.transform, new Vector3(1f, 0.02f, 1f), rangeMat);

            var so = new SerializedObject(view);
            SetRef(so, "model", model.transform);
            SetRef(so, "rangeIndicator", range);
            so.ApplyModifiedProperties();

            var soRange = new SerializedObject(range);
            SetRef(soRange, "ring", ring.transform);
            soRange.ApplyModifiedProperties();

            // 개별 프리팹이 없는 유닛이 쓰는 공용 폴백. 유닛 id 와 겹치지 않게 _Base.
            SavePrefab(root, UnitsDir, "_Base");
        }

        private static void BuildMonsterPrefab(Material modelMat)
        {
            // HP게이지는 3D 가 아니라 HUD(MonsterHealthBarHud)로 그린다. 프리팹은 모델만.
            GameObject root = new GameObject("Monster");
            MonsterView view = root.AddComponent<MonsterView>();
            GameObject model = MakeMesh(PrimitiveType.Capsule, "Model", root.transform, new Vector3(0.4f, 0.4f, 0.4f), modelMat);

            var so = new SerializedObject(view);
            SetRef(so, "model", model.transform);
            so.ApplyModifiedProperties();

            SavePrefab(root, MonstersDir, "Monster");
        }

        // ---- 헬퍼 ----

        private static GameObject MakeMesh(PrimitiveType type, string name, Transform parent, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return go;
        }

        private static void SetRef(SerializedObject so, string prop, Object value)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SavePrefab(GameObject root, string dir, string name)
        {
            string path = dir + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static Material GetOrCreateMat(string name, Color color)
        {
            string path = MatDir + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                if (existing.HasProperty("_BaseColor")) existing.SetColor("_BaseColor", color);
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Directory.CreateDirectory(Path.GetFullPath(path));
            AssetDatabase.Refresh();
        }
    }
}
