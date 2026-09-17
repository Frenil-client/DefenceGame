using Synthesis.Core.Combat;
using Synthesis.Core.Data;

namespace Synthesis.Core.Tests
{
    // STEP 3. 검증 - 만료와 하한 경계에서 표시 목록과 실제 속도 비율이 함께 바뀐다.
    public class MonsterSlowStateTests
    {
        [Theory]
        [InlineData(99, 1, 650)]
        [InlineData(100, 0, 1000)]
        [InlineData(101, 0, 1000)]
        public void Expiry_UpdatesListAndSpeedTogether(int elapsed, int count, long speedRatio)
        {
            var state = new MonsterSlowState();
            var field = new AuraField();
            state.ApplyOnHit("CHILL2", Fixed.FromMilli(350), Fixed.FromMilli(100));
            state.Update(Fixed.FromMilli(elapsed), field, Fixed.Zero, Fixed.Zero);

            Assert.Equal(count, state.GetAppliedList().Count);
            Assert.Equal(speedRatio, state.SpeedRatio.raw);
            if (count > 0) Assert.Equal(1, state.GetAppliedList()[0].remaining.raw);
        }

        [Theory]
        [InlineData(699, 301, false)]
        [InlineData(700, 300, true)]
        [InlineData(701, 300, true)]
        public void SpeedFloor_UsesTheAppliedTotal(int magnitude, long speedRatio, bool capped)
        {
            var state = new MonsterSlowState();
            var field = new AuraField();
            state.ApplyOnHit("CHILL2", Fixed.FromMilli(magnitude), Fixed.One);
            state.Update(Fixed.Zero, field, Fixed.Zero, Fixed.Zero);

            Assert.Single(state.GetAppliedList());
            Assert.Equal(speedRatio, state.SpeedRatio.raw);
            Assert.Equal(capped, state.IsCapped);
        }

        [Fact]
        public void Aura_UsesDeduplicatedSamplesAndLeavesWithTheTarget()
        {
            var field = new AuraField();
            var sample = new AuraSample
            {
                skillId = "FROST1", effect = SkillEffect.Slow,
                radius = Fixed.One, magnitude = Fixed.FromMilli(200)
            };
            field.Add(sample);
            field.Add(sample);
            var state = new MonsterSlowState();
            state.ApplyOnHit("CHILL2", Fixed.FromMilli(350), Fixed.One);
            state.Update(Fixed.Zero, field, Fixed.Zero, Fixed.Zero);

            Assert.Equal(2, state.GetAppliedList().Count);
            Assert.True(state.GetAppliedList()[0].isAura);
            Assert.Equal(450, state.SpeedRatio.raw);

            state.Update(Fixed.Zero, field, Fixed.FromInt(2), Fixed.Zero);
            Assert.Single(state.GetAppliedList());
            Assert.False(state.GetAppliedList()[0].isAura);
            Assert.Equal(650, state.SpeedRatio.raw);
        }

        [Fact]
        public void Refresh_KeepsOneSourceAndStableOrderWithoutAdvancingOnRead()
        {
            var field = new AuraField();
            var state = new MonsterSlowState();
            state.ApplyOnHit("CHILL2", Fixed.FromMilli(350), Fixed.One);
            state.ApplyOnHit("CHILL1", Fixed.FromMilli(200), Fixed.One);
            state.Update(Fixed.FromMilli(900), field, Fixed.Zero, Fixed.Zero);
            state.ApplyOnHit("CHILL2", Fixed.FromMilli(350), Fixed.FromInt(2));
            state.Update(Fixed.Zero, field, Fixed.Zero, Fixed.Zero);

            Assert.Equal(2, state.GetAppliedList().Count);
            Assert.Equal("CHILL1", state.GetAppliedList()[0].skillId);
            Assert.Equal("CHILL2", state.GetAppliedList()[1].skillId);
            Assert.Equal(100, state.GetAppliedList()[0].remaining.raw);
            Assert.Equal(2000, state.GetAppliedList()[1].remaining.raw);
            Assert.Equal(450, state.SpeedRatio.raw);
        }
    }
}
