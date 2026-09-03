using System.Collections.Generic;
using Synthesis.Core.Combat;
using Synthesis.Core.Data;
using Synthesis.Core.Waves;

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
            Assert.True(war.atk.raw > 0);
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

        // 방어력은 보스 전용이 아니다. 원형별 방어력이 있어야 방깎 유닛이 보스 4회 밖에서도 일한다.
        [Fact]
        public void Enemies_LoadArmor()
        {
            var enemies = CsvParsers.LoadEnemies(TestPaths.ReadData("enemies.csv"));
            Assert.Equal(5, enemies.Count);

            int armored = 0;
            foreach (var e in enemies)
            {
                Assert.True(e.armor.raw >= 0, e.id + " 의 방어력이 음수다");
                Assert.True(e.moveSpeed.raw > 0, e.id + " 의 이동 속도가 0 이하다");
                if (e.armor.raw > 0) ++armored;
            }
            Assert.True(armored >= 2, "방어력을 가진 원형이 " + armored + "종뿐이다");
        }

        // 일반 웨이브의 스폰 수는 전 웨이브 25기로 같다. 수를 늘려 난이도를 올리지 않는다(BALANCE 12).
        [Fact]
        public void Waves_NormalSpawnCountIsUniform()
        {
            var waves = CsvParsers.LoadWaves(TestPaths.ReadData("waves.csv"));
            Assert.Equal(40, waves.Count);

            int normalCount = 0;
            foreach (var w in waves)
            {
                if (w.isBoss)
                {
                    continue;
                }
                Assert.Equal(25, w.spawnCount);
                ++normalCount;
            }
            Assert.Equal(36, normalCount);
        }

        // 난이도는 능력치로만 오른다. 체력 배수와 방어력 증가가 웨이브를 따라 단조 증가해야 한다.
        [Fact]
        public void Waves_DifficultyRisesByStatsOnly()
        {
            var waves = CsvParsers.LoadWaves(TestPaths.ReadData("waves.csv"));

            Fixed prevScale = Fixed.Zero;
            int prevArmorAdd = -1;
            foreach (var w in waves)
            {
                if (w.isBoss)
                {
                    // 보스는 bosses.csv 절대값을 쓴다. 웨이브 스케일을 얹지 않는다.
                    Assert.Equal(0, w.armorAdd);
                    continue;
                }
                Assert.True(w.hpScale.raw >= prevScale.raw, "웨이브 " + w.waveIndex + " 에서 체력 배수가 내려간다");
                Assert.True(w.armorAdd >= prevArmorAdd, "웨이브 " + w.waveIndex + " 에서 방어력 증가가 내려간다");
                prevScale = w.hpScale;
                prevArmorAdd = w.armorAdd;
            }

            Assert.True(prevScale.ToDoubleForDisplay() > 2.0, "마지막 일반 웨이브의 체력 배수가 너무 낮다");
            Assert.True(prevArmorAdd > 0, "마지막 일반 웨이브의 방어력 증가가 0 이다");
        }

        // 원형 원본이 오염되면 안 된다. 같은 원형을 쓰는 두 웨이브가 서로 다른 결과를 내야 한다.
        [Fact]
        public void ResolveEnemy_AppliesWaveScaleWithoutMutatingSource()
        {
            var enemies = CsvParsers.LoadEnemies(TestPaths.ReadData("enemies.csv"));
            var bosses = CsvParsers.LoadBosses(TestPaths.ReadData("bosses.csv"));
            var waves = CsvParsers.LoadWaves(TestPaths.ReadData("waves.csv"));

            var enemyById = WaveResolver.BuildEnemyLookup(enemies);
            var bossById = WaveResolver.BuildBossLookup(bosses);
            var waveByIndex = WaveResolver.BuildWaveLookup(waves);

            WaveData early = waveByIndex[1];   // E01, 배수 1.0, 방어 +0
            WaveData late = waveByIndex[36];   // E01, 배수 2.75, 방어 +17

            EnemyData baseline = enemyById["E01"];
            long baseHpRaw = baseline.hp.raw;
            long baseArmorRaw = baseline.armor.raw;

            EnemyData earlyEnemy = WaveResolver.ResolveEnemy(early, enemyById, bossById);
            EnemyData lateEnemy = WaveResolver.ResolveEnemy(late, enemyById, bossById);

            Assert.True(lateEnemy.hp.raw > earlyEnemy.hp.raw, "후반 웨이브 체력이 더 높지 않다");
            Assert.True(lateEnemy.armor.raw > earlyEnemy.armor.raw, "후반 웨이브 방어력이 더 높지 않다");

            // 방어력 0 원형도 후반에는 방어력을 갖는다(armorAdd 가 덧셈이라서).
            Assert.Equal(0, baseArmorRaw);
            Assert.True(lateEnemy.armor.raw > 0, "방어력 0 원형이 후반에도 방어력을 못 얻는다");

            // 원본은 그대로여야 한다.
            Assert.Equal(baseHpRaw, enemyById["E01"].hp.raw);
            Assert.Equal(baseArmorRaw, enemyById["E01"].armor.raw);
        }

        // 보스도 같은 경로로 방어력을 싣는다. 예전에는 ResolveEnemy 가 armor 를 버려 호출부가 따로 챙겼다.
        [Fact]
        public void ResolveEnemy_CarriesArmor()
        {
            var enemies = CsvParsers.LoadEnemies(TestPaths.ReadData("enemies.csv"));
            var bosses = CsvParsers.LoadBosses(TestPaths.ReadData("bosses.csv"));
            var waves = CsvParsers.LoadWaves(TestPaths.ReadData("waves.csv"));

            var enemyById = WaveResolver.BuildEnemyLookup(enemies);
            var bossById = WaveResolver.BuildBossLookup(bosses);
            var waveByIndex = WaveResolver.BuildWaveLookup(waves);

            WaveData bossWave = waveByIndex[10];
            EnemyData asEnemy = WaveResolver.ResolveEnemy(bossWave, enemyById, bossById);
            Assert.NotNull(asEnemy);
            Assert.Equal(bossById[bossWave.bossId].armor.raw, asEnemy.armor.raw);

            WaveData plainWave = waveByIndex[1];
            EnemyData plain = WaveResolver.ResolveEnemy(plainWave, enemyById, bossById);
            Assert.NotNull(plain);
            Assert.Equal(enemyById[plainWave.enemySetId].armor.raw, plain.armor.raw);
        }

        // 표시와 실제 피해가 같은 공식을 쓰는지. 방어력 0 이면 감소가 없어야 한다.
        [Fact]
        public void ArmorFormula_ReducesAndMatchesRatio()
        {
            Fixed atk = Fixed.FromInt(100);

            Assert.Equal(atk.raw, ArmorFormula.Reduced(atk, Fixed.Zero).raw);
            Assert.Equal(0.0, ArmorFormula.ReductionRatio(Fixed.Zero));

            Fixed armor = Fixed.FromInt(20);
            Fixed reduced = ArmorFormula.Reduced(atk, armor);
            Assert.True(reduced.raw < atk.raw);

            // 감소율 표시가 실제 감소와 일치해야 한다(고정소수점 반올림 폭 안에서).
            double byRatio = 100.0 * (1.0 - ArmorFormula.ReductionRatio(armor));
            Assert.True(System.Math.Abs(byRatio - reduced.ToDoubleForDisplay()) < 0.1);
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
            Assert.Equal(32, skills.Count);

            SkillData area = null, crit = null, warcry = null, haste = null;
            foreach (var s in skills)
            {
                if (s.id == "AREA1") area = s;
                if (s.id == "CRIT2") crit = s;
                if (s.id == "WARCRY1") warcry = s;
                if (s.id == "HASTE2") haste = s;
            }

            Assert.NotNull(area);
            Assert.Equal(SkillTrigger.Passive, area.trigger);
            Assert.Equal(SkillEffect.AreaDamage, area.effect);

            Assert.NotNull(crit);
            Assert.Equal(SkillTrigger.ChanceOnAttack, crit.trigger);
            Assert.Equal(SkillEffect.Crit, crit.effect);

            Assert.NotNull(warcry);
            Assert.Equal(SkillEffect.AllyBuff, warcry.effect);
            Assert.Equal(BuffStat.Atk, warcry.buffStat);

            Assert.NotNull(haste);
            Assert.Equal(SkillEffect.AllyBuff, haste.effect);
            Assert.Equal(BuffStat.AtkSpeed, haste.buffStat);
        }

        // 지속 피해는 디버프가 아니라 장판이다. 반경이 있어야 하고 상시 발동이어야 한다.
        [Fact]
        public void Skills_DamageZonesArePassiveAndHaveRadius()
        {
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));

            int zoneCount = 0;
            foreach (var s in skills)
            {
                if (s.effect != SkillEffect.DamageZone)
                {
                    continue;
                }
                Assert.Equal(SkillTrigger.Passive, s.trigger);
                Assert.True(s.radius.raw > 0, s.id + " 장판에 반경이 없다");
                Assert.True(s.magnitude.raw > 0, s.id + " 장판에 dps 가 없다");
                ++zoneCount;
            }
            Assert.True(zoneCount > 0, "장판 스킬이 하나도 없다");
        }

        // 오라(반경 있는 패시브)는 지속시간을 쓰지 않는다. 온힛 감속만 지속시간을 갖는다.
        [Fact]
        public void Skills_OnlyOnHitSlowUsesDuration()
        {
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));

            foreach (var s in skills)
            {
                if (s.duration.raw <= 0)
                {
                    continue;
                }
                Assert.Equal(SkillEffect.Slow, s.effect);
                Assert.True(s.radius.raw <= 0, s.id + " 는 오라인데 지속시간을 갖는다");
            }
        }

        // 감속이 아무리 쌓여도 기본 속도의 30% 밑으로 내려가지 않는다.
        [Fact]
        public void CombatRules_SlowIsCappedAtFloor()
        {
            Assert.Equal(Fixed.One.raw, CombatRules.SpeedRatioAfterSlow(Fixed.Zero).raw);

            Fixed half = CombatRules.SpeedRatioAfterSlow(Fixed.FromRatio(50, 100));
            Assert.Equal(Fixed.FromRatio(50, 100).raw, half.raw);

            // 합산이 하한을 넘겨도 30% 에서 멈춘다.
            Fixed piled = CombatRules.SpeedRatioAfterSlow(Fixed.FromRatio(200, 100));
            Assert.Equal(CombatRules.MinSpeedRatio.raw, piled.raw);
        }

        // 스킬 분배 규칙 1: 스킬 수는 등급-1 이다 (UNIT_SKILLS.md 2장). 1성은 0개.
        [Fact]
        public void UnitSkills_CountMatchesTier()
        {
            var units = CsvParsers.LoadUnits(TestPaths.ReadData("units.csv"));
            foreach (var u in units)
            {
                Assert.Equal(u.tier - 1, u.skillIds.Count);
            }
        }

        // 유닛이 참조하는 스킬 id 는 전부 skills.csv 에 있어야 하고, 정의된 스킬은 전부 최소 1회 쓰여야 한다.
        [Fact]
        public void UnitSkills_ReferenceIntegrity()
        {
            var units = CsvParsers.LoadUnits(TestPaths.ReadData("units.csv"));
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));

            Dictionary<string, int> useCountById = new Dictionary<string, int>();
            foreach (var s in skills) useCountById[s.id] = 0;

            foreach (var u in units)
            {
                foreach (var skillId in u.skillIds)
                {
                    Assert.True(useCountById.ContainsKey(skillId), u.id + " 이 없는 스킬 " + skillId + " 을 참조한다");
                    useCountById[skillId] += 1;
                }
            }

            foreach (var s in skills)
            {
                Assert.True(useCountById[s.id] > 0, "스킬 " + s.id + " 이 어떤 유닛에도 쓰이지 않는다");
            }
        }

        // 스킬 분배 규칙 4: 계열마다 주축 효과를 갖지 않는 유닛이 최소 1종 있어야 한다 (역할 고정 방지).
        [Fact]
        public void UnitSkills_EveryKlassHasOffAxisUnit()
        {
            var units = CsvParsers.LoadUnits(TestPaths.ReadData("units.csv"));
            var skills = CsvParsers.LoadSkills(TestPaths.ReadData("skills.csv"));

            Dictionary<string, SkillEffect> effectById = new Dictionary<string, SkillEffect>();
            foreach (var s in skills) effectById[s.id] = s.effect;

            Klass[] klassList = { Klass.War, Klass.Arc, Klass.Mag, Klass.Pri, Klass.Thi, Klass.Spi };
            for (int i = 0; i < klassList.Length; ++i)
            {
                Klass klass = klassList[i];
                bool found = false;
                foreach (var u in units)
                {
                    if (u.klass != klass || u.tier < 2) continue;
                    if (HasMainAxis(u, klass, effectById))
                    {
                        continue;
                    }
                    found = true;
                    break;
                }
                Assert.True(found, klass + " 계열이 전부 주축 효과를 갖는다");
            }
        }

        // 계열 주축 효과: WAR 고피해, ARC 관통, MAG 광역, PRI 아군버프, THI 치명타, SPI 지속/상태이상.
        private static bool HasMainAxis(UnitData unit, Klass klass, Dictionary<string, SkillEffect> effectById)
        {
            foreach (var skillId in unit.skillIds)
            {
                SkillEffect effect;
                if (!effectById.TryGetValue(skillId, out effect)) continue;

                if (klass == Klass.War && effect == SkillEffect.BonusDamage) return true;
                if (klass == Klass.Arc && effect == SkillEffect.Pierce) return true;
                if (klass == Klass.Mag && effect == SkillEffect.AreaDamage) return true;
                if (klass == Klass.Pri && effect == SkillEffect.AllyBuff) return true;
                if (klass == Klass.Thi && effect == SkillEffect.Crit) return true;
                if (klass == Klass.Spi && (effect == SkillEffect.DamageZone || effect == SkillEffect.Slow)) return true;
            }
            return false;
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
