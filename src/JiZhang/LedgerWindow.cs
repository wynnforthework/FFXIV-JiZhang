using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace JiZhang;
internal sealed class LedgerWindow : Window
{
    private readonly LedgerView view;
    public LedgerWindow(Plugin plugin) : base("金币账本###JiZhang",ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoCollapse|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoScrollWithMouse) {
        view=new(plugin);
        Size=new Vector2(1040,780);
        SizeCondition=ImGuiCond.FirstUseEver;
        SizeConstraints=new WindowSizeConstraints { MinimumSize=new(760,52),MaximumSize=new(float.MaxValue,float.MaxValue) };
        RespectCloseHotkey=true;
        ShowCloseButton=true;
    }
    public override void PreDraw() {
        Flags=ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoCollapse|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoScrollWithMouse;
        if(view.IsCompact)Flags|=ImGuiWindowFlags.NoResize;
        SizeConstraints=new WindowSizeConstraints { MinimumSize=new(760,view.IsCompact?52:560),MaximumSize=new(float.MaxValue,float.MaxValue) };
        LedgerTheme.Push();
    }
    public override void PostDraw() => LedgerTheme.Pop();
    public override void Draw() { view.Draw();if(view.CloseRequested){IsOpen=false;view.ClearCloseRequest();} }
}
