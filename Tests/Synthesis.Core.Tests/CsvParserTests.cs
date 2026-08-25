using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Core.Tests
{
    // STEP 1(v0.4). 검증 - CSV 파서.
    public class CsvParserTests
    {
        [Fact]
        public void Units_LoadCountAndFields()
        {
            var units = CsvParsers.LoadUnits(TestPaths.ReadData("units.csv"));
            Assert.Equal(42, units.Count); // 1성 6, 2성 12, 3성 12, 4성 8, 5성 4

            UnitData war = Find(units, "T1-WAR");
            Assert.NotNull(war);
            Assert.Equal(1, war.tier);
            Assert.Equal(Klass.War, war.klass);
            Assert.Equal(5, war.cost);
        }

        [Fact]
        public void Recipes_LoadMaterials()
        {
            var recipes = CsvParsers.LoadRecipes(TestPaths.ReadData("recipes.csv"));
            Assert.Equal(36, recipes.Count); // 12+12+8+4

            RecipeData t2 = FindR(recipes, "T2-WAR-01");
            Assert.Equal(2, t2.materials.Count);
            Assert.Equal("T1-WAR", t2.materials[0]);

            RecipeData t5 = FindR(recipes, "T5-WAR-01");
            Assert.Equal(4, t5.materials.Count);
        }

        [Fact]
        public void Bosses_LoadTimeLimit()
        {
            var bosses = CsvParsers.LoadBosses(TestPaths.ReadData("bosses.csv"));
            Assert.Equal(4, bosses.Count);
            foreach (var b in bosses) Assert.True(b.timeLimitTicks > 0);
        }

        [Fact]
        public void Skills_LoadComposable()
        {
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));
            Assert.Equal(9, skills.Count);

            SkillData splash = null, crit = null, warcry = null;
            foreach (var s in skills)
            {
                if (s.id == "SPLASH") splash = s;
                if (s.id == "CRIT30") crit = s;
                if (s.id == "WARCRY") warcry = s;
            }

            Assert.NotNull(splash);
            Assert.Equal(SkillTrigger.Passive, splash.trigger);
            Assert.Equal(SkillEffect.AreaDamage, splash.effect);

            Assert.NotNull(crit);
            Assert.Equal(SkillTrigger.ChanceOnAttack, crit.trigger);
            Assert.Equal(SkillEffect.Crit, crit.effect);

            Assert.NotNull(warcry);
            Assert.Equal(SkillEffect.AllyBuff, warcry.effect);
            Assert.Equal(BuffStat.Atk, warcry.buffStat);
        }

        private static UnitData Find(List<UnitData> list, string id)
        {
            foreach (var u in list) if (u.id == id) return u;
            return null;
        }

        private static RecipeData FindR(List<RecipeData> list, string id)
        {
            foreach (var r in list) if (r.resultId == id) return r;
            return null;
        }
    }
}
