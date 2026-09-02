using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Simulation;
using Synthesis.Core.Combat;

namespace Synthesis.Presentation
{
    // 뷰 - HUD. UI 계층은 프리팹에 미리 만들어 두고, 여기서는 참조(statsText)만 갱신한다.
    // 배속 버튼의 onClick 은 프리팹에서 SetSpeed(float) 로 연결한다.
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [SerializeField] private WaveManager waves;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private Button skipButton; // 웨이브 스킵 버튼(스폰 완료 시 활성)

        [Header("선택 정보")]
        [SerializeField] private SelectionController selection; // 선택 상태 출처. 인스펙터 등록
        [SerializeField] private CombatController combat;       // 실효 수치 출처. 인스펙터 등록
        [SerializeField] private GameObject selectionPanel;     // 선택이 없으면 통째로 숨긴다
        [SerializeField] private TMP_Text selectionText;

        [Header("보스 배너")]
        [SerializeField] private GameObject bossBanner;   // 보스 웨이브에서만 켜지는 상단 중앙 배너
        [SerializeField] private TMP_Text bossTitle;      // "BOSS - 이름" 강조 텍스트
        [SerializeField] private RectTransform bossHpFill; // 남은 체력 비율(localScale.x 로 표현)
        [SerializeField] private TMP_Text bossHpText;     // "cur / max  남은 s"

        private int bossBannerWave = -1; // 배너 갱신 중인 웨이브(바뀌면 등장 여부 리셋)
        private bool bossSeen;           // 이번 보스 웨이브에서 보스가 필드에 등장한 적이 있는가

        // 배속 버튼(프리팹의 onClick 에서 호출).
        public void SetSpeed(float value)
        {
            if (game != null) game.Speed = value;
        }

        // 웨이브 스킵 버튼(프리팹의 onClick 에서 호출). 생성 완료된 일반 웨이브에서 다음 웨이브로 넘긴다.
        public void OnSkipClicked()
        {
            if (waves != null) waves.RequestSkip();
        }

        // 상점 버튼(프리팹의 onClick 에서 호출). 선택권으로 원하는 1성을 구매한다.
        public void OpenShop()
        {
            if (game == null || game.Context == null || !game.Context.IsValid()) return;
            if (UIManager.Instance == null) return;
            ShopPopup popup = UIManager.Instance.Open("ShopPopup") as ShopPopup;
            if (popup != null) popup.Setup(game.Context);
        }

        // 이번 웨이브 몬스터의 방어력과 그로 인한 피해 감소율. 방깎 유닛을 쓸지 판단하는 근거다(SPEC 3-5).
        //   감소율은 Core 의 ArmorFormula 로 구한다. 여기서 따로 계산하면 표시와 실제 피해가 갈라진다.
        private static string ArmorLabel(Fixed armor)
        {
            if (armor.raw <= 0) return "없음";
            return armor.ToIntTruncated() + "  (피해 " + ArmorLabelPercent(armor) + "% 감소)";
        }

        // 방어력으로 인한 피해 감소율(%). 공식은 Core 한 벌을 쓴다.
        private static int ArmorLabelPercent(Fixed armor)
        {
            return Mathf.RoundToInt((float)(ArmorFormula.ReductionRatio(armor) * 100.0));
        }

        // 선택한 유닛/몬스터의 정보. 선택이 없으면 패널을 숨긴다.
        //   실효 수치(오라 반영)를 기본값과 나란히 보여준다. 링으로 그리는 사거리와 같은 값을 쓴다.
        private void UpdateSelection()
        {
            if (selectionText == null) return;

            LoopUnit unit = selection != null ? selection.SelectedUnit : null;
            LoopMonster monster = selection != null ? selection.SelectedMonster : null;

            if (unit == null && monster == null)
            {
                if (selectionPanel != null) selectionPanel.SetActive(false);
                selectionText.text = "";
                return;
            }

            if (selectionPanel != null) selectionPanel.SetActive(true);
            selectionText.text = unit != null ? UnitInfo(unit) : MonsterInfo(monster);
        }

        private string UnitInfo(LoopUnit unit)
        {
            UnitData data = unit.data;
            float atk = combat != null ? combat.GetEffectiveAtk(unit) : (float)data.atk.ToDoubleForDisplay();
            float aps = combat != null ? combat.GetEffectiveAtkSpeed(unit) : (float)data.atkSpeed.ToDoubleForDisplay();
            float range = combat != null ? combat.GetEffectiveRange(unit) : (float)data.range.ToDoubleForDisplay();

            string text = data.name + "   " + data.tier + "성 " + data.klass + "   코스트 " + data.cost + "\n"
                + "공격력 " + StatLabel((float)data.atk.ToDoubleForDisplay(), atk) + "\n"
                + "공속 " + StatLabel((float)data.atkSpeed.ToDoubleForDisplay(), aps) + " 회/초\n"
                + "사거리 " + StatLabel((float)data.range.ToDoubleForDisplay(), range) + "\n"
                + "초당 피해 " + (atk * aps).ToString("F0");

            if (data.skillIds.Count == 0) return text + "\n스킬 없음";

            text += "\n스킬";
            var registry = game.Context.skillById;
            for (int i = 0; i < data.skillIds.Count; ++i)
            {
                string skillId = data.skillIds[i];
                SkillData skill;
                if (registry != null && registry.TryGetValue(skillId, out skill)) text += "\n  " + skillId + "  " + skill.note;
                else text += "\n  " + skillId;
            }
            return text;
        }

        private string MonsterInfo(LoopMonster monster)
        {
            string name = MonsterName(monster);
            Fixed armorFixed = combat != null ? combat.GetEffectiveArmor(monster) : monster.armor;
            float armor = (float)armorFixed.ToDoubleForDisplay();
            float baseArmor = (float)monster.armor.ToDoubleForDisplay();
            float speed = (float)monster.moveSpeed.ToDoubleForDisplay();
            float baseSpeed = (float)monster.baseMoveSpeed.ToDoubleForDisplay();

            string armorLine = "방어력 " + StatLabel(baseArmor, armor);
            if (armorFixed.raw > 0) armorLine += "   피해 " + ArmorLabelPercent(armorFixed) + "% 감소";

            return name + "\n"
                + "체력 " + monster.hp.ToIntTruncated() + "\n"
                + armorLine + "\n"
                + "이동속도 " + StatLabel(baseSpeed, speed);
        }

        // 몬스터 표시 이름. 보스면 보스 이름, 아니면 원형 이름. 못 찾으면 id 그대로.
        private string MonsterName(LoopMonster monster)
        {
            BossData boss;
            if (game.Context.bossById.TryGetValue(monster.enemyId, out boss)) return "BOSS  " + boss.name;

            EnemyData enemy;
            if (game.Context.enemyById.TryGetValue(monster.enemyId, out enemy)) return enemy.name;
            return monster.enemyId;
        }

        // 기본값과 실효값이 다르면 둘 다 보여준다. 같으면 하나만.
        private static string StatLabel(float baseValue, float effective)
        {
            if (Mathf.Abs(baseValue - effective) < 0.005f) return effective.ToString("0.##");
            return baseValue.ToString("0.##") + " -> " + effective.ToString("0.##");
        }

        // 보스 웨이브면 화면 상단 중앙 배너를 켜서 보스전임을 강조하고 남은 체력을 별도로 보여준다(SPEC 3-6).
        //   보스가 아니면 배너를 숨긴다. 등장 전엔 최대 체력, 등장 후 사라졌으면(격파) 0 으로 표시한다.
        private void UpdateBossBanner(int activeWave, float remain)
        {
            if (bossBanner == null) return;

            if (activeWave != bossBannerWave)
            {
                bossBannerWave = activeWave;
                bossSeen = false;
            }

            WaveData wave;
            BossData boss = null;
            if (game.Context.waveByIndex.TryGetValue(activeWave, out wave) && wave.isBoss && !string.IsNullOrEmpty(wave.bossId))
            {
                game.Context.bossById.TryGetValue(wave.bossId, out boss);
            }

            if (boss == null)
            {
                bossBanner.SetActive(false);
                return;
            }

            long maxHp = boss.hp.ToIntTruncated();
            long curHp = 0;
            bool found = false;
            var list = game.Context.sim.state.monsterList;
            for (int i = 0; i < list.Count; ++i)
            {
                LoopMonster m = list[i];
                if (m.alive && m.enemyId == boss.id) { curHp = m.hp.ToIntTruncated(); found = true; break; }
            }

            if (found) bossSeen = true;
            else curHp = bossSeen ? 0 : maxHp; // 등장 전 최대, 격파 후 0

            bossBanner.SetActive(true);
            if (bossTitle != null) bossTitle.text = "BOSS  -  " + boss.name;

            float ratio = maxHp > 0 ? (float)curHp / maxHp : 0f;
            if (bossHpFill != null) bossHpFill.localScale = new Vector3(Mathf.Clamp01(ratio), 1f, 1f);
            if (bossHpText != null)
                bossHpText.text = curHp + " / " + maxHp + "   방어 " + boss.armor.ToIntTruncated() + "   남은 " + remain.ToString("F1") + "s";
        }

        private void Update()
        {
            // 스킵 버튼 활성화: 생성이 완료된 일반 웨이브에서만 누를 수 있다.
            if (skipButton != null) skipButton.interactable = waves != null && waves.CanSkip;

            if (statsText == null || game == null || game.Context == null || !game.Context.IsValid())
            {
                if (bossBanner != null) bossBanner.SetActive(false);
                if (selectionPanel != null) selectionPanel.SetActive(false);
                return;
            }

            UpdateSelection();

            var s = game.Context.sim.state;
            int next = waves != null ? waves.NextWave : 1;
            string granted = waves != null ? waves.LastGranted : "-";
            bool cleared = waves != null && waves.Cleared;
            int shownWave = Mathf.Clamp(next - 1, 0, game.MaxWave);
            string phaseLabel = s.defeated ? "패배"
                : cleared ? "클리어"
                : (s.pendingSpawns > 0 ? "전투" : "대기");
            float remain = waves != null ? Mathf.Max(0f, waves.WaveTimer) : 0f;

            UpdateBossBanner(next - 1, remain);

            statsText.text =
                "웨이브 " + shownWave + " / " + game.MaxWave + "  [" + phaseLabel + "]  x" + game.Speed + "\n"
                + "제한시간 " + remain.ToString("F1") + "s\n"
                + "코스트 " + s.cost + " / " + s.costCap + "\n"
                + "필드 몬스터 " + s.aliveCount + " / " + (waves != null ? waves.AccumCap : 0) + "\n"
                + "몬스터 방어 " + ArmorLabel(s.spawnArmor) + "\n"
                + "인벤토리 " + game.Context.inventory.Count + "   최근 뽑기 " + granted + "\n"
                + "선택권 " + game.Context.selectionTokens;
        }
    }
}
