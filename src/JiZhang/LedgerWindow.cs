using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace JiZhang;
internal sealed class LedgerWindow : Window
{
    private readonly LedgerView view;
    public LedgerWindow(Plugin plugin) : base("金币账本###JiZhang",ImGuiWindowFlags.NoCollapse) {
        view=new(plugin);
        Size=new Vector2(1040,740);
        SizeCondition=ImGuiCond.FirstUseEver;
        SizeConstraints=new WindowSizeConstraints { MinimumSize=new(760,560),MaximumSize=new(float.MaxValue,float.MaxValue) };
        RespectCloseHotkey=true;
        ShowCloseButton=true;
    }
    public override void PreDraw() => LedgerTheme.Push();
    public override void PostDraw() => LedgerTheme.Pop();
    public override void Draw() => view.Draw();
}
