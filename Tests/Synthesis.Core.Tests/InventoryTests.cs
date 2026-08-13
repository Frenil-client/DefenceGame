using System.Collections.Generic;
using Synthesis.Core.Data;
using Synthesis.Core.Combination;
using Synthesis.Core.Units;

namespace Synthesis.Core.Tests
{
    // STEP 3(v0.4). 검증 - 유닛 소유 모델(인벤토리)과 조합 소모.
    public class InventoryTests
    {
        private static CombinationEngine Engine()
        {
            return new CombinationEngine(CsvParsers.LoadRecipes(TestPaths.ReadData("recipes.csv")));
        }

        [Fact]
        public void Add_AssignsUniqueInstanceIds()
        {
            var inv = new Inventory();
            var a = inv.Add("T1-WAR");
            var b = inv.Add("T1-WAR");
            Assert.Equal(2, inv.Count);
            Assert.NotEqual(a.instanceId, b.instanceId);
        }

        [Fact]
        public void Craft_ConsumesMaterialsProducesResult()
        {
            var engine = Engine();
            var inv = new Inventory();
            inv.Add("T1-WAR");
            inv.Add("T1-WAR"); // T2-WAR-01 = 전사 x2

            OwnedUnit result;
            bool ok = inv.TryCraft(engine, "T2-WAR-01", out result);

            Assert.True(ok);
            Assert.Equal("T2-WAR-01", result.unitId);
            Assert.Equal(1, inv.Count); // 재료 2 소모, 결과 1 생성
        }

        [Fact]
        public void Craft_InsufficientMaterialsKeepsInventory()
        {
            var engine = Engine();
            var inv = new Inventory();
            inv.Add("T1-WAR"); // 재료 하나뿐

            OwnedUnit result;
            bool ok = inv.TryCraft(engine, "T2-WAR-01", out result);

            Assert.False(ok);
            Assert.Equal(1, inv.Count);
        }
    }
}
