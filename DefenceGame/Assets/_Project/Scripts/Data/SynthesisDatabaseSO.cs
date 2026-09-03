using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core;
using Synthesis.Core.Data;

namespace Synthesis.Data
{
    // STEP 1. 기반 도구 - CSV 를 런타임 로딩용으로 캐시하는 ScriptableObject (v0.4).
    // 원본은 어디까지나 Data/*.csv 다. 이 SO 는 캐시이며 임포터가 생성한다 (ARCHITECTURE.md 5-1).
    // 전투 수치는 Fixed 의 raw(long) 를 그대로 저장해 파싱 결과와 완전히 동일하게 보관한다.

    [System.Serializable]
    public class UnitRow
    {
        public string id;
        public string unitName;
        public int tier;
        public Klass klass;
        public long atkRaw;
        public long atkSpeedRaw;
        public long rangeRaw;
        public List<string> skillIds = new List<string>();
        public string note;

        public static UnitRow FromModel(UnitData model)
        {
            UnitRow row = new UnitRow();
            row.id          = model.id;
            row.unitName    = model.name;
            row.tier        = model.tier;
            row.klass       = model.klass;
            row.atkRaw      = model.atk.raw;
            row.atkSpeedRaw = model.atkSpeed.raw;
            row.rangeRaw    = model.range.raw;
            row.skillIds    = new List<string>(model.skillIds);
            row.note        = model.note;
            return row;
        }

        public UnitData ToModel()
        {
            UnitData model = new UnitData();
            model.id       = id;
            model.name     = unitName;
            model.tier     = tier;
            model.klass    = klass;
            model.atk      = Fixed.FromRaw(atkRaw);
            model.atkSpeed = Fixed.FromRaw(atkSpeedRaw);
            model.range    = Fixed.FromRaw(rangeRaw);
            model.skillIds = new List<string>(skillIds);
            model.note     = note;
            return model;
        }
    }

    [System.Serializable]
    public class RecipeRow
    {
        public string resultId;
        public List<string> materials = new List<string>();

        public static RecipeRow FromModel(RecipeData model)
        {
            RecipeRow row = new RecipeRow();
            row.resultId  = model.resultId;
            row.materials = new List<string>(model.materials);
            return row;
        }

        public RecipeData ToModel()
        {
            RecipeData model = new RecipeData();
            model.resultId  = resultId;
            model.materials = new List<string>(materials);
            return model;
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

        public List<RecipeData> BuildRecipeModels()
        {
            List<RecipeData> resultList = new List<RecipeData>();
            foreach (var row in recipeList)
            {
                resultList.Add(row.ToModel());
            }
            return resultList;
        }
    }
}
