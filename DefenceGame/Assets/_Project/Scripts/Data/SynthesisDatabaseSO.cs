using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core;
using Synthesis.Core.Data;

namespace Synthesis.Data
{
    // STEP 1. 기반 도구 - CSV 를 런타임 로딩용으로 캐시하는 ScriptableObject.
    // 원본은 어디까지나 Data/*.csv 다. 이 SO 는 캐시이며 임포터가 생성한다 (ARCHITECTURE.md 5-1).
    // 전투 수치는 Fixed 의 raw(long) 를 그대로 저장해 파싱 결과와 완전히 동일하게 보관한다.

    [System.Serializable]
    public class UnitRow
    {
        public string id;
        public string unitName;
        public Grade grade;
        public Element element;
        public Role role;
        public Placement placement;
        public int cost;
        public long hpRaw;
        public long atkRaw;
        public long atkSpeedRaw;
        public long rangeRaw;
        public int blockCount;
        public int redeployCd;
        public bool isAdvance;
        public string note;

        public static UnitRow FromModel(UnitData model)
        {
            UnitRow row = new UnitRow();
            row.id          = model.id;
            row.unitName    = model.name;
            row.grade       = model.grade;
            row.element     = model.element;
            row.role        = model.role;
            row.placement   = model.placement;
            row.cost        = model.cost;
            row.hpRaw       = model.hp.raw;
            row.atkRaw      = model.atk.raw;
            row.atkSpeedRaw = model.atkSpeed.raw;
            row.rangeRaw    = model.range.raw;
            row.blockCount  = model.blockCount;
            row.redeployCd  = model.redeployCd;
            row.isAdvance   = model.isAdvance;
            row.note        = model.note;
            return row;
        }

        public UnitData ToModel()
        {
            UnitData model = new UnitData();
            model.id         = id;
            model.name       = unitName;
            model.grade      = grade;
            model.element    = element;
            model.role       = role;
            model.placement  = placement;
            model.cost       = cost;
            model.hp         = Fixed.FromRaw(hpRaw);
            model.atk        = Fixed.FromRaw(atkRaw);
            model.atkSpeed   = Fixed.FromRaw(atkSpeedRaw);
            model.range      = Fixed.FromRaw(rangeRaw);
            model.blockCount = blockCount;
            model.redeployCd = redeployCd;
            model.isAdvance  = isAdvance;
            model.note       = note;
            return model;
        }
    }

    [System.Serializable]
    public class RecipeRow
    {
        public string resultId;
        public string mat1;
        public string mat2;
        public ConditionType conditionType;
        public bool isHidden;
        public bool unlockedByDefault;

        public static RecipeRow FromModel(RecipeData model)
        {
            RecipeRow row = new RecipeRow();
            row.resultId          = model.resultId;
            row.mat1              = model.mat1;
            row.mat2              = model.mat2;
            row.conditionType     = model.conditionType;
            row.isHidden          = model.isHidden;
            row.unlockedByDefault = model.unlockedByDefault;
            return row;
        }
    }

    [CreateAssetMenu(menuName = "Synthesis/Database", fileName = "SynthesisDatabase")]
    public class SynthesisDatabaseSO : ScriptableObject
    {
        public List<UnitRow> unitList = new List<UnitRow>();
        public List<RecipeRow> recipeList = new List<RecipeRow>();

        // 캐시된 SO 를 Core 모델 묶음으로 되돌린다. 런타임/검증에서 CSV 파싱 결과와 동일해야 한다.
        public List<UnitData> BuildUnitModels()
        {
            List<UnitData> resultList = new List<UnitData>();
            foreach (var row in unitList)
            {
                resultList.Add(row.ToModel());
            }
            return resultList;
        }
    }
}
