using UISystem;

namespace Synthesis.Presentation
{
    // 씬의 주 화면(전투 화면). 씬의 UI Canvas 에 붙여 두면 UIManager 가 씬 로드 시 자동으로 스택에 편입한다.
    // 화면 자체는 내용이 없다. HUD 부품(HudView/InventoryView/MonsterHealthBarHud)이 자식으로 붙어 각자 갱신한다.
    // UIScreen 은 UIRoot 로 옮기지 않고 씬에 남으므로, 이 Canvas 의 CanvasScaler 가 UILayerSettings 와 같아야 한다.
    public sealed class GameScreen : UIScreen
    {
    }
}
