using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace JiZhang;
internal static class LedgerTheme
{
    public static readonly Vector4 Ink = new(.12f,.17f,.23f,1);
    public static readonly Vector4 Muted = new(.38f,.44f,.50f,1);
    public static readonly Vector4 Green = new(.04f,.43f,.32f,1);
    public static readonly Vector4 Red = new(.76f,.23f,.25f,1);
    public static readonly Vector4 Gold = new(.58f,.38f,.11f,1);
    public static readonly Vector4 Border = new(.86f,.88f,.89f,1);
    public static readonly Vector4 Canvas = new(.96f,.965f,.96f,1);
    private static readonly (ImGuiCol,Vector4)[] Colors = [
        (ImGuiCol.WindowBg,Canvas),(ImGuiCol.ChildBg,Vector4.One),
        (ImGuiCol.Text,Ink),(ImGuiCol.TextDisabled,Muted),(ImGuiCol.Border,Border),
        (ImGuiCol.TitleBg,new(.94f,.95f,.94f,1)),(ImGuiCol.TitleBgActive,new(.94f,.95f,.94f,1)),
        (ImGuiCol.TitleBgCollapsed,new(.94f,.95f,.94f,1)),(ImGuiCol.PopupBg,Vector4.One),
        (ImGuiCol.FrameBg,new(.93f,.94f,.935f,1)),(ImGuiCol.FrameBgHovered,new(.88f,.91f,.89f,1)),
        (ImGuiCol.FrameBgActive,new(.84f,.88f,.85f,1)),(ImGuiCol.Button,new(.91f,.93f,.92f,1)),
        (ImGuiCol.ButtonHovered,new(.82f,.88f,.84f,1)),(ImGuiCol.ButtonActive,new(.75f,.83f,.78f,1)),
        (ImGuiCol.Header,new(.88f,.93f,.89f,1)),(ImGuiCol.HeaderHovered,new(.82f,.89f,.84f,1)),
        (ImGuiCol.HeaderActive,new(.76f,.85f,.79f,1)),(ImGuiCol.CheckMark,Green),
        (ImGuiCol.SliderGrab,Green),(ImGuiCol.SliderGrabActive,new(.03f,.34f,.24f,1)),
        (ImGuiCol.Separator,Border),(ImGuiCol.TableHeaderBg,new(.94f,.955f,.94f,1)),
        (ImGuiCol.TableRowBg,Vector4.One),(ImGuiCol.TableRowBgAlt,new(.975f,.98f,.975f,1)),
        (ImGuiCol.ScrollbarBg,new(.95f,.96f,.95f,1)),(ImGuiCol.ScrollbarGrab,new(.74f,.79f,.76f,1)),
        (ImGuiCol.ScrollbarGrabHovered,new(.63f,.70f,.65f,1)),(ImGuiCol.ScrollbarGrabActive,new(.53f,.62f,.56f,1)),
        (ImGuiCol.ResizeGrip,new(.44f,.53f,.47f,.35f)),(ImGuiCol.ResizeGripHovered,new(.30f,.45f,.35f,.65f)),
        (ImGuiCol.ResizeGripActive,Green),(ImGuiCol.TextSelectedBg,new(.70f,.83f,.74f,.65f)),
        (ImGuiCol.PlotHistogram,Green),(ImGuiCol.ModalWindowDimBg,new(.10f,.16f,.12f,.30f))
    ];
    public static void Push() {
        foreach(var (key,value) in Colors) ImGui.PushStyleColor(key,value);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(18,14));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(10,9));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(10,7));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,10f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,10f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding,8f);
    }
    public static void Pop() { ImGui.PopStyleVar(7); ImGui.PopStyleColor(Colors.Length); }
}
