using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Synthesis.Core;
using Synthesis.Core.Data;
using Synthesis.Core.Simulation;
using Synthesis.Core.Combat;
using Synthesis.Core.Text;

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
            if (armor.raw <= 0) return StringManager.Get("str.hud.armor.none");

            armorScratch.Clear();
            armorScratch.Set("armor", armor.ToIntTruncated().ToString());
            armorScratch.Set("percent", ArmorLabelPercent(armor).ToString());
            return StringManager.Format("str.hud.armor.value", armorScratch);
        }

        // 문자열 조립용 재사용 버퍼. HUD 는 매 프레임 갱신이라 매번 새로 만들지 않는다.
        //   서로를 호출하는 헬퍼끼리 버퍼를 공유하면 바깥이 채운 값을 안쪽이 덮어쓴다. 그래서 용도별로 나눈다.
        private static readonly StringValues scratch = new StringValues();
        private static readonly StringValues armorScratch = new StringValues();
        private static readonly StringValues bossScratch = new StringValues();
        private static readonly SkillStringValues skillScratch = new SkillStringValues();

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
            float baseAtk = (float)data.atk.ToDoubleForDisplay();
            float baseAps = (float)data.atkSpeed.ToDoubleForDisplay();
            float baseRange = (float)data.range.ToDoubleForDisplay();
            float atk = combat != null ? combat.GetEffectiveAtk(unit) : baseAtk;
            float aps = combat != null ? combat.GetEffectiveAtkSpeed(unit) : baseAps;
            float range = combat != null ? combat.GetEffectiveRange(unit) : baseRange;

            scratch.Clear();
            scratch.Set("name", data.name);
            scratch.Set("tier", data.tier.ToString());
            scratch.Set("klass", data.klass.ToString());
            scratch.Set("cost", data.cost.ToString());

            string text = StringManager.Format("str.unit.header", scratch) + "\n"
                + StringManager.FormatStat("str.stat.atk", baseAtk, atk, "0") + "\n"
                + StringManager.FormatStat("str.stat.atkspeed", baseAps, aps) + "\n"
                + StringManager.FormatStat("str.stat.range", baseRange, range) + "\n"
                + StringManager.FormatStat("str.stat.dps", atk * aps, atk * aps, "0");

            if (data.skillIds.Count == 0) return text + "\n" + StringManager.Get("str.unit.skill.none");

            text += "\n" + StringManager.Get("str.unit.skill.header");
            var registry = game.Context.skillById;
            for (int i = 0; i < data.skillIds.Count; ++i)
            {
                string skillId = data.skillIds[i];
                text += "\n  " + StringManager.Get("str.skill." + skillId + ".name");

                SkillData skill;
                if (registry != null && registry.TryGetValue(skillId, out skill))
                    text += "  " + StringManager.Format("str.skill." + skillId + ".desc", skillScratch.Bind(skill));
            }
            return text;
        }

        private string MonsterInfo(LoopMonster monster)
        {
            Fixed armorFixed = combat != null ? combat.GetEffectiveArmor(monster) : monster.armor;
            float armor = (float)armorFixed.ToDoubleForDisplay();
            float baseArmor = (float)monster.armor.ToDoubleForDisplay();
            float speed = (float)monster.moveSpeed.ToDoubleForDisplay();
            float baseSpeed = (float)monster.baseMoveSpeed.ToDoubleForDisplay();

            string armorLine = StringManager.FormatStat("str.stat.armor", baseArmor, armor);
            if (armorFixed.raw > 0)
            {
                scratch.Clear();
                scratch.Set("percent", ArmorLabelPercent(armorFixed).ToString());
                armorLine += "   " + StringManager.Format("str.monster.armor.reduction", scratch);
            }

            // 이름을 먼저 뽑아 둔다. MonsterName 이 같은 scratch 를 쓰므로 hp 를 채운 뒤에 부르면 덮어쓴다.
            string name = MonsterName(monster);

            scratch.Clear();
            scratch.Set("value", monster.hp.ToIntTruncated().ToString());
            string hpLine = StringManager.Format("str.monster.hp", scratch);

            return name + "\n"
                + hpLine + "\n"
                + armorLine + "\n"
                + StringManager.FormatStat("str.stat.movespeed", baseSpeed, speed);
        }

        // 몬스터 표시 이름. 보스면 보스 이름, 아니면 원형 이름. 못 찾으면 id 그대로.
        private string MonsterName(LoopMonster monster)
        {
            BossData boss;
            if (game.Context.bossById.TryGetValue(monster.enemyId, out boss)) return BossTitle(boss.name);

            EnemyData enemy;
            if (game.Context.enemyById.TryGetValue(monster.enemyId, out enemy)) return enemy.name;
            return monster.enemyId;
        }

        private static string BossTitle(string name)
        {
            bossScratch.Clear();
            bossScratch.Set("name", name);
            return StringManager.Format("str.hud.boss.title", bossScratch);
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
            if (bossTitle != null) bossTitle.text = BossTitle(boss.name);

            float ratio = maxHp > 0 ? (float)curHp / maxHp : 0f;
            if (bossHpFill != null) bossHpFill.localScale = new Vector3(Mathf.Clamp01(ratio), 1f, 1f);
            if (bossHpText != null)
            {
                scratch.Clear();
                scratch.Set("current", curHp.ToString());
                scratch.Set("max", maxHp.ToString());
                scratch.Set("armor", boss.armor.ToIntTruncated().ToString());
                scratch.Set("sec", remain.ToString("F1"));
                bossHpText.text = StringManager.Format("str.hud.boss.hp", scratch);
            }
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
            string phaseLabel = StringManager.Get(s.defeated ? "str.hud.phase.defeat"
                : cleared ? "str.hud.phase.cleared"
                : (s.pendingSpawns > 0 ? "str.hud.phase.battle" : "str.hud.phase.idle"));
            float remain = waves != null ? Mathf.Max(0f, waves.WaveTimer) : 0f;

            UpdateBossBanner(next - 1, remain);

            scratch.Clear();
            scratch.Set("current", shownWave.ToString());
            scratch.Set("max", game.MaxWave.ToString());
            scratch.Set("phase", phaseLabel);
            scratch.Set("speed", game.Speed.ToString());
            string line = StringManager.Format("str.hud.wave", scratch) + "\n";

            scratch.Clear();
            scratch.Set("sec", remain.ToString("F1"));
            line += StringManager.Format("str.hud.timelimit", scratch) + "\n";

            scratch.Clear();
            scratch.Set("current", s.cost.ToString());
            scratch.Set("max", s.costCap.ToString());
            line += StringManager.Format("str.hud.cost", scratch) + "\n";

            scratch.Clear();
            scratch.Set("current", s.aliveCount.ToString());
            scratch.Set("max", (waves != null ? waves.AccumCap : 0).ToString());
            line += StringManager.Format("str.hud.fieldmonster", scratch) + "\n";

            scratch.Clear();
            scratch.Set("armor", ArmorLabel(s.spawnArmor));
            line += StringManager.Format("str.hud.monsterarmor", scratch) + "\n";

            scratch.Clear();
            scratch.Set("count", game.Context.inventory.Count.ToString());
            scratch.Set("granted", granted);
            line += StringManager.Format("str.hud.inventory", scratch) + "\n";

            scratch.Clear();
            scratch.Set("count", game.Context.selectionTokens.ToString());
            statsText.text = line + StringManager.Format("str.hud.selectiontoken", scratch);
        }
    }
}
