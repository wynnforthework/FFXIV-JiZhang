using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace JiZhang;
internal static class LedgerTheme
{
    public static readonly Vector4 Ink=new(.965f,.965f,.975f,1);
    public static readonly Vector4 Muted=new(.64f,.67f,.73f,1);
    public static readonly Vector4 Green=new(.46f,.86f,.66f,1);
    public static readonly Vector4 Red=new(.93f,.42f,.50f,1);
    public static readonly Vector4 Gold=new(.92f,.74f,.34f,1);
    public static readonly Vector4 Accent=new(.62f,.42f,.96f,1);
    public static readonly Vector4 Border=new(.19f,.21f,.27f,1);
    public static readonly Vector4 Canvas=new(.050f,.054f,.076f,1);
    public static readonly Vector4 Surface=new(.082f,.090f,.118f,1);
    public static readonly Vector4 Surface2=new(.142f,.155f,.196f,1);
    private static readonly (ImGuiCol,Vector4)[] Colors = [
        (ImGuiCol.WindowBg,Canvas),(ImGuiCol.ChildBg,Surface),(ImGuiCol.Text,Ink),(ImGuiCol.TextDisabled,Muted),
        (ImGuiCol.Border,Border),(ImGuiCol.TitleBg,Canvas),(ImGuiCol.TitleBgActive,Canvas),(ImGuiCol.TitleBgCollapsed,Canvas),
        (ImGuiCol.PopupBg,Surface),(ImGuiCol.FrameBg,Surface2),(ImGuiCol.FrameBgHovered,new(.18f,.20f,.25f,1)),
        (ImGuiCol.FrameBgActive,new(.22f,.23f,.30f,1)),(ImGuiCol.Button,Surface2),(ImGuiCol.ButtonHovered,new(.23f,.20f,.33f,1)),
        (ImGuiCol.ButtonActive,new(.30f,.23f,.44f,1)),(ImGuiCol.Header,new(.23f,.19f,.33f,1)),
        (ImGuiCol.HeaderHovered,Surface2),(ImGuiCol.HeaderActive,new(.30f,.23f,.44f,1)),(ImGuiCol.CheckMark,Accent),
        (ImGuiCol.SliderGrab,Accent),(ImGuiCol.SliderGrabActive,new(.78f,.60f,1f,1)),(ImGuiCol.Separator,Border),
        (ImGuiCol.TableHeaderBg,Surface2),(ImGuiCol.TableRowBg,Surface),(ImGuiCol.TableRowBgAlt,new(.10f,.11f,.14f,1)),
        (ImGuiCol.ScrollbarBg,Canvas),(ImGuiCol.ScrollbarGrab,new(.24f,.26f,.32f,1)),
        (ImGuiCol.ScrollbarGrabHovered,new(.34f,.36f,.42f,1)),(ImGuiCol.ScrollbarGrabActive,Accent),
        (ImGuiCol.ResizeGrip,new(.5f,.45f,.65f,.3f)),(ImGuiCol.ResizeGripHovered,Accent),(ImGuiCol.ResizeGripActive,Accent),
        (ImGuiCol.TextSelectedBg,new(.62f,.42f,.96f,.35f)),(ImGuiCol.PlotHistogram,Green),
        (ImGuiCol.ModalWindowDimBg,new(0,0,0,.5f))
    ];
    public static void Push() {
        foreach(var (key,value) in Colors) ImGui.PushStyleColor(key,value);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(18,14));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(10,9));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(10,7));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,7f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,10f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,14f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding,8f);
    }
    public static void Pop() { ImGui.PopStyleVar(7); ImGui.PopStyleColor(Colors.Length); }
}
