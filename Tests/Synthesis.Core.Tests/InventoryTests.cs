using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Combination;
using Synthesis.Core.Units;

namespace Synthesis.Core.Tests
{
    // STEP 3. 검증 - 유닛 소유 모델(인벤토리)과 조합 소모.
    public class InventoryTests
    {
        private static List<RecipeData> LoadRecipes()
        {
            return CsvParsers.LoadRecipes(TestPaths.ReadData("recipes.csv"));
        }

        [Fact]
        public void Add_AssignsUniqueInstanceIds()
        {
            var inv = new Inventory();
            var a = inv.Add("C01");
            var b = inv.Add("C01");
            Assert.Equal(2, inv.Count);
            Assert.NotEqual(a.instanceId, b.instanceId);
        }

        [Fact]
        public void Combine_ConsumesTwoProducesOne()
        {
            var engine = new CombinationEngine(LoadRecipes());
            var inv = new Inventory();
            var c01 = inv.Add("C01");
            var c02 = inv.Add("C02");

            OwnedUnit result;
            bool ok = inv.TryCombine(engine, c01.instanceId, c02.instanceId, out result);

            Assert.True(ok);
            Assert.Equal("R01", result.unitId);
            Assert.Equal(1, inv.Count); // 둘 소모, 하나 생성
        }

        [Fact]
        public void Combine_InvalidPairKeepsInventory()
        {
            var engine = new CombinationEngine(LoadRecipes());
            var inv = new Inventory();
            var a = inv.Add("C01");
            var b = inv.Add("C06"); // C01+C06 은 성립하지 않음

            OwnedUnit result;
            bool ok = inv.TryCombine(engine, a.instanceId, b.instanceId, out result);

            Assert.False(ok);
            Assert.Equal(2, inv.Count);
        }
    }
}
