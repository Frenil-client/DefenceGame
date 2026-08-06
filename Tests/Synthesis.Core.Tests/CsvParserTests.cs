using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Core.Tests
{
    // STEP 1. 검증 - 파서 일치 테스트 (ARCHITECTURE.md 5-1, 11).
    // Core 파서가 CSV 원본을 결정적으로 읽는지 확인한다.
    // Unity 임포터가 SO 로 변환할 때도 이 동일 파서를 쓰므로, 이 테스트가 통과하면
    // 'CSV 직접 파싱 == SO 변환' 의 파서 측 보증이 성립한다. (SO 측 대조는 Unity EditMode 테스트가 담당)
    public class CsvParserTests
    {
        [Fact]
        public void Units_LoadExpectedCount()
        {
            var units = CsvParsers.LoadUnits(TestPaths.ReadData("units.csv"));
            Assert.Equal(34, units.Count);
        }

        [Fact]
        public void Units_C01_FieldsMatchSpec()
        {
            var units = CsvParsers.LoadUnits(TestPaths.ReadData("units.csv"));
            UnitData c01 = FindById(units, "C01");

            Assert.NotNull(c01);
            Assert.Equal("불씨 정령", c01.name);
            Assert.Equal(Grade.Common, c01.grade);
            Assert.Equal(Element.Fire, c01.element);
            Assert.Equal(Role.Single, c01.role);
            Assert.Equal(Placement.Ranged, c01.placement);
            Assert.Equal(6, c01.cost);
        }

        [Fact]
        public void Units_ReparseIsIdentical()
        {
            string text = TestPaths.ReadData("units.csv");
            var first = CsvParsers.LoadUnits(text);
            var second = CsvParsers.LoadUnits(text);

            Assert.Equal(first.Count, second.Count);
            for (int i = 0; i < first.Count; ++i)
            {
                Assert.Equal(first[i].id, second[i].id);
                Assert.Equal(first[i].cost, second[i].cost);
                Assert.Equal(first[i].atk.raw, second[i].atk.raw);
                Assert.Equal(first[i].redeployCd, second[i].redeployCd);
            }
        }

        [Fact]
        public void Unit_SingleLine_RoundTrips()
        {
            // note 에 쉼표가 있어도 파서가 뒤 필드를 다시 이어붙이는지 확인한다.
            string line = "C99,테스트,common,fire,single,ranged,6,100,10,1.0,5.0,0,160,false,메모,쉼표포함";
            UnitData unit = CsvParsers.CsvToUnitData(line);

            Assert.Equal("C99", unit.id);
            Assert.Equal(6, unit.cost);
            Assert.Equal(160, unit.redeployCd);
            Assert.Equal("메모,쉼표포함", unit.note);
        }

        [Fact]
        public void Recipes_LoadAndFlags()
        {
            var recipes = CsvParsers.LoadRecipes(TestPaths.ReadData("recipes.csv"));
            Assert.Equal(24, recipes.Count);

            RecipeData r01 = FindRecipe(recipes, "R01");
            Assert.Equal(ConditionType.SameElement, r01.conditionType);
            Assert.False(r01.isHidden);

            RecipeData h06 = FindRecipe(recipes, "H06");
            Assert.True(h06.isHidden);
            Assert.False(h06.unlockedByDefault);
            Assert.Equal("U01", h06.mat1);
            Assert.Equal("U02", h06.mat2);
        }

        [Fact]
        public void Bosses_PreDamageCap_Is_040()
        {
            var bosses = CsvParsers.LoadBosses(TestPaths.ReadData("bosses.csv"));
            Assert.Equal(3, bosses.Count);
            foreach (var boss in bosses)
            {
                Assert.Equal(400L, boss.preDamageCapRatio.raw); // 0.40
            }
        }

        private static UnitData FindById(List<UnitData> list, string id)
        {
            foreach (var unit in list)
            {
                if (unit.id == id) return unit;
            }
            return null;
        }

        private static RecipeData FindRecipe(List<RecipeData> list, string id)
        {
            foreach (var recipe in list)
            {
                if (recipe.resultId == id) return recipe;
            }
            return null;
        }
    }
}
