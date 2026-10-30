using System.Numerics;
using Dalamud.Bindings.ImGui;
using static JiZhang.LedgerTheme;
namespace JiZhang;
internal sealed partial class LedgerView
{
    private void Tooltip(string text) {
        if(string.IsNullOrEmpty(text))return;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(14,11)*Unit);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(0,5)*Unit);
        ImGui.PushStyleColor(ImGuiCol.PopupBg,new Vector4(.142f,.155f,.196f,.98f));
        ImGui.PushStyleColor(ImGuiCol.Text,Ink);
        try {ImGui.BeginTooltip();ImGui.PushTextWrapPos(320*Unit);ImGui.TextUnformatted(text);ImGui.PopTextWrapPos();ImGui.EndTooltip();}
        finally {ImGui.PopStyleColor(2);ImGui.PopStyleVar(2);}
    }
    private bool ToggleRow(string id,string label,string help,ref bool value) {
        var origin=ImGui.GetCursorScreenPos();var width=groupWidth>0?groupWidth-36*Unit:ImGui.GetContentRegionAvail().X;
        var draw=ImGui.GetWindowDrawList();var size=new Vector2(40,22)*Unit;
        var pos=origin+new Vector2(width-size.X,9*Unit);
        while(label.Length>1&&ImGui.CalcTextSize(label).X>width-56*Unit)label=label[..^2]+"…";
        draw.AddText(origin+new Vector2(0,11)*Unit,Color(Ink),label);
        var labelHovered=ImGui.IsMouseHoveringRect(origin,pos);
        ImGui.SetCursorScreenPos(pos);
        HitRegions[id]=new(pos.X,pos.Y,pos.X+size.X,pos.Y+size.Y);
        var clicked=ImGui.InvisibleButton(id,size);
        if(clicked)value=!value;
        var hover=motion.Approach(id+"-hover",ImGui.IsItemHovered()?1:0,18);
        if(ImGui.IsItemHovered())ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var on=motion.Approach(id+"-on",value?1:0,16);
        var track=Vector4.Lerp(Vector4.Lerp(Surface2,new(.22f,.24f,.30f,1),hover),Accent,on);
        draw.AddRectFilled(pos,pos+size,Color(track),11*Unit);
        draw.AddRect(pos,pos+size,Color(Vector4.Lerp(Border,new(.78f,.60f,1,1),on*.6f)),11*Unit);
        var center=pos+new Vector2(11+18*on,11)*Unit;
        draw.AddCircleFilled(center+new Vector2(0,Unit),8*Unit,Color(new(0,0,0,.3f)));
        draw.AddCircleFilled(center,8*Unit,Color(Ink));
        if(labelHovered)Tooltip(help);
        ImGui.SetCursorScreenPos(origin);ImGui.Dummy(new(width,40*Unit));
        return clicked;
    }
    private readonly Dictionary<string,double> popupTimes=[];
    private bool Dropdown(string id,string[] labels,ref int selected) {
        var origin=ImGui.GetCursorScreenPos();var width=Math.Max(1,ImGui.GetContentRegionAvail().X);var size=new Vector2(width,ImGui.GetFrameHeight());
        HitRegions[id]=new(origin.X,origin.Y,origin.X+width,origin.Y+size.Y);
        var popup=id+"-popup";var open=ImGui.IsPopupOpen(popup);
        var clicked=ImGui.InvisibleButton(id,size);var hover=motion.Approach(id+"-hover",ImGui.IsItemHovered()?1:0,18);
        var expanded=motion.Approach(id+"-expanded",open?1:0,16);
        if(ImGui.IsItemHovered())ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if(clicked&&!open){ImGui.OpenPopup(popup);popupTimes[id]=plugin.UiTime;open=true;}
        var border=Vector4.Lerp(Border,new(.78f,.60f,1,.85f),Math.Max(hover*.6f,expanded));
        SurfaceBox(origin,origin+size,Vector4.Lerp(Surface2,new(.22f,.20f,.29f,1),hover),border,7*Unit);
        var draw=ImGui.GetWindowDrawList();var label=labels[Math.Clamp(selected,0,labels.Length-1)];
        while(label.Length>1&&ImGui.CalcTextSize(label).X>width-44*Unit)label=label[..^2]+"…";
        draw.AddText(origin+new Vector2(11*Unit,(size.Y-ImGui.GetFontSize())/2),Color(Ink),label);
        var center=origin+new Vector2(width-17*Unit,size.Y/2);var flip=1-2*expanded;
        draw.AddLine(center+new Vector2(-4,-2*flip)*Unit,center+new Vector2(0,2*flip)*Unit,Color(Muted),1.7f*Unit);
        draw.AddLine(center+new Vector2(0,2*flip)*Unit,center+new Vector2(4,-2*flip)*Unit,Color(Muted),1.7f*Unit);
        if(!open)return false;
        var elapsed=(float)(plugin.UiTime-popupTimes.GetValueOrDefault(id,plugin.UiTime));
        var t=plugin.ReducedMotion?1:Math.Clamp(elapsed/.2f,0,1);var reveal=1-MathF.Pow(1-t,3);
        var fullHeight=Math.Min(300*Unit,labels.Length*34*Unit)+12*Unit;
        var height=fullHeight*(.42f+.58f*reveal);var viewport=ImGui.GetMainViewport();
        var below=origin.Y+size.Y+6*Unit;var above=below+fullHeight>viewport.WorkPos.Y+viewport.WorkSize.Y-6*Unit&&origin.Y-fullHeight>viewport.WorkPos.Y;
        var x=Math.Clamp(origin.X,viewport.WorkPos.X+6*Unit,Math.Max(viewport.WorkPos.X+6*Unit,viewport.WorkPos.X+viewport.WorkSize.X-width-6*Unit));
        ImGui.SetNextWindowPos(new(x,above?origin.Y-6*Unit-height:below));ImGui.SetNextWindowSize(new(width,height));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(6)*Unit);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha,ImGui.GetStyle().Alpha*Math.Max(.05f,reveal));
        ImGui.PushStyleColor(ImGuiCol.PopupBg,new Vector4(.108f,.118f,.152f,.99f));
        var changed=false;
        if(ImGui.BeginPopup(popup,ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoSavedSettings)) {
            for(var i=0;i<labels.Length;i++) {
                var row=ImGui.GetCursorScreenPos();HitRegions[id+"-option-"+i]=new(row.X,row.Y,row.X+ImGui.GetContentRegionAvail().X,row.Y+30*Unit);var progress=plugin.ReducedMotion?1:Math.Clamp((elapsed-Math.Min(i,9)*.014f)/.2f,0,1);
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha,ImGui.GetStyle().Alpha*Math.Max(.001f,1-MathF.Pow(1-progress,3)));
                if(ImGui.Selectable(labels[i]+"##"+id+i,i==selected,ImGuiSelectableFlags.None,new(0,30*Unit))) {selected=i;changed=true;ImGui.CloseCurrentPopup();}
                if(i==selected&&ImGui.IsWindowAppearing())ImGui.SetItemDefaultFocus();
                ImGui.PopStyleVar();
            }ImGui.EndPopup();
        }
        ImGui.PopStyleColor();ImGui.PopStyleVar(2);
        return changed;
    }
    private Vector2 groupOrigin;
    private float groupWidth;
    private void BeginSettingsCard(string title) {
        groupOrigin=ImGui.GetCursorScreenPos();groupWidth=ImGui.GetContentRegionAvail().X;
        var draw=ImGui.GetWindowDrawList();draw.ChannelsSplit(2);draw.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(groupOrigin+new Vector2(18,16)*Unit);
        ImGui.BeginGroup();ImGui.PushItemWidth(groupWidth-36*Unit);
        ImGui.TextUnformatted(title);ImGui.Spacing();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX()+groupWidth-36*Unit);
    }
    private void EndSettingsCard() {
        ImGui.PopTextWrapPos();ImGui.PopItemWidth();ImGui.EndGroup();
        var bottom=ImGui.GetItemRectMax().Y+16*Unit;
        var draw=ImGui.GetWindowDrawList();draw.ChannelsSetCurrent(0);
        SurfaceBox(groupOrigin,new(groupOrigin.X+groupWidth,bottom),new(.105f,.105f,.145f,.98f),new(.26f,.24f,.34f,.8f),12*Unit);
        draw.ChannelsMerge();ImGui.SetCursorScreenPos(new(groupOrigin.X,bottom));ImGui.Dummy(new(groupWidth,10*Unit));
    }
    private bool ActionButton(string label) {
        var origin=ImGui.GetCursorScreenPos();var size=ImGui.CalcTextSize(label)+new Vector2(24,14)*Unit;
        var clicked=ImGui.InvisibleButton(label,size);var hover=motion.Approach("button-"+label,ImGui.IsItemHovered()?1:0,18);
        var press=motion.Approach("press-"+label,ImGui.IsItemActive()?1:0,30);
        var lift=(hover-press)*Unit;var pos=origin-new Vector2(0,lift);
        SurfaceBox(pos,pos+size,Vector4.Lerp(Surface2,new(.27f,.22f,.38f,1),hover),Vector4.Lerp(Border,new(.62f,.47f,.80f,.65f),hover),7*Unit);
        ImGui.GetWindowDrawList().AddText(pos+(size-ImGui.CalcTextSize(label))/2,Color(Ink),label);
        if(ImGui.IsItemHovered())ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return clicked;
    }
}
