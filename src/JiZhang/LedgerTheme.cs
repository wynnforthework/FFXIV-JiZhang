using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace JiZhang;
internal static class LedgerTheme
{
    // Paper & Pine: the ledger's own visual identity.
    public static readonly Vector4 Ink=new(.14f,.22f,.21f,1);
    public static readonly Vector4 Muted=new(.40f,.46f,.43f,1);
    public static readonly Vector4 Green=new(.13f,.45f,.34f,1);
    public static readonly Vector4 Red=new(.72f,.29f,.22f,1);
    public static readonly Vector4 Gold=new(.60f,.39f,.17f,1);
    public static readonly Vector4 Accent=new(.16f,.39f,.34f,1);
    public static readonly Vector4 Border=new(.82f,.83f,.78f,1);
    public static readonly Vector4 Canvas=new(.95f,.94f,.90f,1);
    public static readonly Vector4 Surface=new(.995f,.990f,.965f,1);
    public static readonly Vector4 Surface2=new(.89f,.91f,.85f,1);
    private static readonly (ImGuiCol,Vector4)[] Colors = [
        (ImGuiCol.WindowBg,Canvas),(ImGuiCol.ChildBg,Surface),(ImGuiCol.Text,Ink),(ImGuiCol.TextDisabled,Muted),
        (ImGuiCol.Border,Border),(ImGuiCol.TitleBg,Canvas),(ImGuiCol.TitleBgActive,Canvas),(ImGuiCol.TitleBgCollapsed,Canvas),
        (ImGuiCol.PopupBg,Surface),(ImGuiCol.FrameBg,Surface2),(ImGuiCol.FrameBgHovered,Surface2),
        (ImGuiCol.FrameBgActive,Surface2),(ImGuiCol.Button,Surface2),(ImGuiCol.ButtonHovered,Surface2),
        (ImGuiCol.ButtonActive,Surface2),(ImGuiCol.Header,Surface2),
        (ImGuiCol.HeaderHovered,Surface2),(ImGuiCol.HeaderActive,Surface2),(ImGuiCol.CheckMark,Accent),
        (ImGuiCol.SliderGrab,Accent),(ImGuiCol.SliderGrabActive,Gold),(ImGuiCol.Separator,Border),
        (ImGuiCol.TableHeaderBg,Surface2),(ImGuiCol.TableRowBg,Surface),(ImGuiCol.TableRowBgAlt,Surface2),
        (ImGuiCol.ScrollbarBg,Canvas),(ImGuiCol.ScrollbarGrab,Surface2),
        (ImGuiCol.ScrollbarGrabHovered,Surface2),(ImGuiCol.ScrollbarGrabActive,Accent),
        (ImGuiCol.ResizeGrip,Surface2),(ImGuiCol.ResizeGripHovered,Accent),(ImGuiCol.ResizeGripActive,Accent),
        (ImGuiCol.TextSelectedBg,Surface2),(ImGuiCol.PlotHistogram,Green),
        (ImGuiCol.ModalWindowDimBg,new(0,0,0,.5f))
    ];
    public static void Push() {
        foreach(var (key,value) in Colors) ImGui.PushStyleColor(key,value);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(10,9));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(10,7));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,7f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,6f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,8f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding,8f);
    }
    public static void Pop() { ImGui.PopStyleVar(7); ImGui.PopStyleColor(Colors.Length); }
}
