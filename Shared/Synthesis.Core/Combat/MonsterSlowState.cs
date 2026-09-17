using System.Collections.Generic;
using Synthesis.Core.Data;

namespace Synthesis.Core.Combat
{
    // STEP 3. 기반 도구 - 이번 갱신에서 실제 적용된 감속 한 건.
    public struct SlowInfo
    {
        public string skillId;
        public Fixed magnitude;
        public Fixed remaining;
        public bool isAura;
    }

    // STEP 3. 뼈대 - 표시 계층에는 마지막 갱신 결과의 조회만 노출한다.
    public interface IMonsterSlowSnapshot
    {
        Fixed SpeedRatio { get; }
        bool IsCapped { get; }
        IReadOnlyList<SlowInfo> GetAppliedList();
    }

    // STEP 3. 핵심 - 감속 수명, 적용 목록, 속도 비율을 한 번에 확정한다.
    // Unity 는 시간을 주입하고 확정된 비율을 적용한다. HUD 조회는 상태를 진행시키지 않는다.
    public sealed class MonsterSlowState : IMonsterSlowSnapshot
    {
        private readonly Dictionary<string, SlowInfo> onHitDict = new Dictionary<string, SlowInfo>();
        private readonly List<string> onHitIdList = new List<string>();
        private readonly List<AuraSample> auraList = new List<AuraSample>();
        private readonly List<SlowInfo> appliedList = new List<SlowInfo>();

        public Fixed SpeedRatio { get; private set; } = Fixed.One;
        public bool IsCapped { get; private set; }

        public IReadOnlyList<SlowInfo> GetAppliedList()
        {
            return appliedList;
        }

        public void ApplyOnHit(string skillId, Fixed magnitude, Fixed duration)
        {
            if (magnitude.raw <= 0 || duration.raw <= 0) return;

            if (!onHitDict.ContainsKey(skillId))
            {
                onHitIdList.Add(skillId);
                onHitIdList.Sort(System.StringComparer.Ordinal);
            }
            onHitDict[skillId] = new SlowInfo
            {
                skillId = skillId,
                magnitude = magnitude,
                remaining = duration,
                isAura = false
            };
        }

        public void Update(Fixed elapsed, AuraField auraField, Fixed targetx, Fixed targety)
        {
            if (elapsed.raw < 0) elapsed = Fixed.Zero;
            appliedList.Clear();
            auraField.GetSamplesAt(SkillEffect.Slow, targetx, targety, auraList);
            foreach (AuraSample sample in auraList)
            {
                appliedList.Add(new SlowInfo
                {
                    skillId = sample.skillId,
                    magnitude = sample.magnitude,
                    remaining = Fixed.Zero,
                    isAura = true
                });
            }

            for (int i = 0; i < onHitIdList.Count; ++i)
            {
                var skillId = onHitIdList[i];
                var source = onHitDict[skillId];
                source.remaining = source.remaining - elapsed;
                if (source.remaining.raw <= 0)
                {
                    onHitDict.Remove(skillId);
                    onHitIdList.RemoveAt(i);
                    --i;
                    continue;
                }
                onHitDict[skillId] = source;
                appliedList.Add(source);
            }

            var total = Fixed.Zero;
            foreach (SlowInfo source in appliedList)
            {
                total = total + source.magnitude;
            }
            SpeedRatio = CombatRules.SpeedRatioAfterSlow(total);
            IsCapped = SpeedRatio.raw <= CombatRules.MinSpeedRatio.raw;
        }
    }
}
