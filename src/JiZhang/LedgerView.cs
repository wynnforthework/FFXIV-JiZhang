using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using JiZhang.Core;
using static JiZhang.LedgerTheme;
namespace JiZhang;

// Shared by the game window and the offscreen visual verification host.
internal sealed partial class LedgerView(ILedgerUiContext plugin)
{
    private int tab;
    private string keyword="",from="",to="",location="";
    private int direction,page;
    private int rangeChoice;
    private DateTime[] bucketDates=[];
    private string PeriodLabel=>new[]{"当月","当年","所有"}[rangeChoice];
    private Category? category;
    private LedgerEntry? selected;
    private Task<string>? action;
    private string actionResult="",restorePath="";
    private bool confirmRestore,restoreExpanded;
    private LedgerEntry[]? source;
    private DateTime day;
    private bool exclude;
    private Totals todayTotal=new(0,0),monthTotal=new(0,0);
    private Totals[] trend=[];
    private (Category Category,long Amount)[] groups=[];
    private int tabForPreview=-1;
    internal void SelectTab(int value) => tabForPreview=value;
    private float Unit => unit>0?unit:1;
    private static string Money(long v) => v.ToString("N0",CultureInfo.CurrentCulture);
    private static uint Color(Vector4 c) => ImGui.GetColorU32(c);
    private float unit;
    private bool resetScroll;
    internal bool CloseRequested {get;private set;}
    internal void ClearCloseRequest()=>CloseRequested=false;
    private readonly UiMotion motion=new();
    private readonly FoldMotion fold=new();
    internal bool IsCompact=>fold.Target;
    internal bool IsFolding=>fold.Moving||fold.Target;
    internal float FoldAmount=>fold.Value;
    internal float NavPosition{get;private set;}
    internal float RangePosition{get;private set;}=1;
    internal float PageProgress{get;private set;}=1;
    internal Dictionary<string,Vector4> HitRegions{get;}=[];
    internal int DirectionForPreview=>direction;
    internal int ActiveTab=>tab;
    internal int ActiveRange=>rangeChoice;
    internal float ContentScrollForPreview{get;private set;}
    internal Dictionary<string,float> PanelScrollForPreview{get;}=[];
    internal void PrepareFrame()=>fold.Update(plugin.UiTime,plugin.ReducedMotion);
    internal void OnOpen(){motion.Clear();}
    internal void ToggleFold(){fold.Toggle(plugin.UiTime);}
    private void Navigate(int target){if(tab==target)return;tab=target;resetScroll=true;}

    private Vector2 expandedSize;
    private bool wasFolding;
    public void Draw() {
        using var bodyFont=plugin.PushFont(1f);
        unit=ImGui.GetFontSize()/17f;
        motion.Frame(plugin.UiTime,ImGui.GetIO().DeltaTime,plugin.ReducedMotion);
        PrepareFrame();
        if(tabForPreview>=0){tab=tabForPreview;tabForPreview=-1;}
        Refresh();
        var origin=ImGui.GetWindowPos();var size=ImGui.GetWindowSize();
        var header=96*Unit;var rail=0f;var footer=32*Unit;
        if(!IsFolding&&wasFolding) {ImGui.SetWindowSize(expandedSize);size=ImGui.GetWindowSize();wasFolding=false;}
        else if(!IsFolding)expandedSize=size;
        else if(expandedSize.Y>0) {wasFolding=true;ImGui.SetWindowSize(new Vector2(size.X,expandedSize.Y+(52*Unit-expandedSize.Y)*fold.Value));size=ImGui.GetWindowSize();}
        Ambient(origin,size);
        Chrome(origin,size,Math.Min(header,size.Y));
        if(fold.Value>=.999f)return;
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha,ImGui.GetStyle().Alpha*(1-fold.Value));
        var draw=ImGui.GetWindowDrawList();
        ImGui.SetCursorScreenPos(origin+new Vector2(24,54)*Unit);
        Sidebar();
        ImGui.SetCursorScreenPos(origin+new Vector2(rail,header));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(26,22)*Unit);
        if(ImGui.BeginChild("content",new Vector2(size.X-rail-6*Unit,size.Y-header-footer),false,ImGuiWindowFlags.NoBackground|ImGuiWindowFlags.AlwaysUseWindowPadding)) {
            if(resetScroll){ImGui.SetScrollY(0);resetScroll=false;}
            ContentScrollForPreview=ImGui.GetScrollY();
            PageProgress=motion.Reveal("page",tab);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY()+(1-PageProgress)*12*Unit);
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha,ImGui.GetStyle().Alpha*Math.Max(.001f,PageProgress));
            if(tab==0)Overview();
            else {Heading(tab==1?"收支流水":"偏好与数据",tab==1?"所有金币流动，自动归档。":"记账偏好、数据备份与透明度。");if(tab==1)Transactions();else Settings();}
        ImGui.PopStyleVar();
        }ImGui.EndChild();ImGui.PopStyleVar();
        var bottom=origin+new Vector2(0,size.Y-footer);
        draw.AddRectFilled(bottom,origin+size,Color(Surface2),0f);
        draw.AddLine(bottom,origin+new Vector2(size.X,size.Y-footer),Color(Border));
        draw.AddCircleFilled(bottom+new Vector2(22,16)*Unit,3*Unit,Color(plugin.Current==null?Muted:Green));
        draw.AddText(bottom+new Vector2(36,7)*Unit,Color(Muted),plugin.Current?.CharacterName??"等待角色登录");
        var note=exclude?"统计排除资金转移  ·  数据仅存本机":"自动记账  ·  数据仅存本机";
        draw.AddText(bottom+new Vector2(size.X-ImGui.CalcTextSize(note).X-20*Unit,7*Unit),Color(Muted),note);
        ImGui.PopStyleVar();
    }
    private void Ambient(Vector2 origin,Vector2 size) {
        var draw=ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin,origin+size,Color(Canvas),8*Unit);
    }
    private void Chrome(Vector2 origin,Vector2 size,float height) {
        var draw=ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin,origin+new Vector2(size.X,height),Color(Surface),8*Unit,ImDrawFlags.RoundCornersTop);
        draw.AddLine(origin+new Vector2(0,height),origin+new Vector2(size.X,height),Color(Border));
        // The title has a real drag hit region; controls retain their own hit regions.
        ImGui.SetCursorScreenPos(origin);
        ImGui.InvisibleButton("title-drag",new(size.X-94*Unit,48*Unit));
        if(ImGui.IsItemHovered()&&ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))ToggleFold();
        if(ImGui.IsItemActive()&&ImGui.IsMouseDragging(ImGuiMouseButton.Left))ImGui.SetWindowPos(origin+ImGui.GetIO().MouseDelta);
        BookIcon(origin+new Vector2(26,26)*Unit,12*Unit,Gold);
        draw.AddText(origin+new Vector2(51,17)*Unit,Color(Ink),"金币账本");
        var status=plugin.Current==null?"等待登录":plugin.Status.StartsWith("正在自动记录",StringComparison.Ordinal)?"记录中":"请检查记录状态";
        var statusColor=status=="记录中"?Green:Muted;
        var pill=origin+new Vector2(139,14)*Unit;var pillSize=new Vector2(ImGui.CalcTextSize(status).X+30*Unit,24*Unit);
        draw.AddRectFilled(pill,pill+pillSize,Color(statusColor with {W=.12f}),12*Unit);
        draw.AddRect(pill,pill+pillSize,Color(statusColor with {W=.45f}),12*Unit);
        draw.AddCircleFilled(pill+new Vector2(11,12)*Unit,3*Unit,Color(statusColor));
        draw.AddText(pill+new Vector2(20,4)*Unit,Color(statusColor),status);
        for(var i=0;i<2;i++) {
            var pos=origin+new Vector2(size.X-(84-i*38)*Unit,11*Unit);
            ImGui.SetCursorScreenPos(pos);
            if(ImGui.InvisibleButton(i==0?"collapse":"close",new Vector2(30,30)*Unit)) {
                if(i==1)CloseRequested=true;
                else ToggleFold();
            }
            var hover=motion.Approach("chrome-hover-"+i,ImGui.IsItemHovered()?1:0,18);
            var color=Vector4.Lerp(Muted,Ink,hover);
            if(hover>.001f)draw.AddRectFilled(pos,pos+new Vector2(30,30)*Unit,Color(Surface2 with {W=hover}),7*Unit);
            if(ImGui.IsItemHovered())ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var center=pos+new Vector2(15,15)*Unit;
            if(i==1){draw.AddLine(center-new Vector2(5,5)*Unit,center+new Vector2(5,5)*Unit,Color(color),2*Unit);draw.AddLine(center+new Vector2(-5,5)*Unit,center+new Vector2(5,-5)*Unit,Color(color),2*Unit);}
            else {draw.AddLine(center+new Vector2(-6,IsCompact?3:-3)*Unit,center+new Vector2(0,IsCompact?-3:3)*Unit,Color(color),2*Unit);draw.AddLine(center+new Vector2(0,IsCompact?-3:3)*Unit,center+new Vector2(6,IsCompact?3:-3)*Unit,Color(color),2*Unit);}
        }
    }
    private void BookIcon(Vector2 center,float radius,Vector4 color) {
        var draw=ImGui.GetWindowDrawList();
        draw.AddRect(center-new Vector2(radius,radius*.8f),center+new Vector2(radius,radius*.8f),Color(color),3*Unit,ImDrawFlags.None,1.8f*Unit);
        draw.AddLine(center+new Vector2(-radius*.45f,-radius*.8f),center+new Vector2(-radius*.45f,radius*.8f),Color(color),1.8f*Unit);
        draw.AddLine(center+new Vector2(-radius*.10f,-radius*.25f),center+new Vector2(radius*.6f,-radius*.25f),Color(color),1.5f*Unit);
        draw.AddLine(center+new Vector2(-radius*.10f,radius*.25f),center+new Vector2(radius*.4f,radius*.25f),Color(color),1.5f*Unit);
    }
    private void Refresh() {
        var date=DateTime.Today;
        if(ReferenceEquals(source,plugin.Entries)&&day==date&&exclude==plugin.Config.ExcludeTransfers)return;
        source=plugin.Entries;day=date;exclude=plugin.Config.ExcludeTransfers;
        var month=new DateTime(date.Year,date.Month,1);
        todayTotal=Totals.From(source.Where(e=>e.Time.LocalDateTime.Date==date),exclude);
        monthTotal=Totals.From(source.Where(e=>e.Time.LocalDateTime>=month),exclude);
        var report=LedgerReport.Create(source,date,(ReportPeriod)rangeChoice,exclude);
        trend=report.Buckets.Select(b=>b.Total).ToArray();bucketDates=report.Buckets.Select(b=>b.Start).ToArray();
        groups=report.Spending;

    }
    private void Sidebar() {
        var draw=ImGui.GetWindowDrawList();var origin=ImGui.GetCursorScreenPos();
        NavPosition=motion.Approach("nav-position",tab,16);
        var marker=origin+new Vector2(NavPosition*124,38)*Unit;
        draw.AddRectFilled(marker,marker+new Vector2(100,3)*Unit,Color(Accent),1*Unit);
        for(var i=0;i<3;i++) {
            var pos=origin+new Vector2(i*124,0)*Unit;var size=new Vector2(100,38)*Unit;
            ImGui.SetCursorScreenPos(pos);HitRegions["nav-"+i]=new(pos.X,pos.Y,pos.X+size.X,pos.Y+size.Y);
            if(ImGui.InvisibleButton("nav-"+i,size))Navigate(i);
            var hover=motion.Approach("nav-hover-"+i,ImGui.IsItemHovered()?1:0,18);
            if(i!=tab&&hover>.001f)draw.AddRectFilled(pos,pos+size,Color(Surface2 with {W=hover}),4*Unit);
            var text=new[]{"资产概览","收支流水","偏好与数据"}[i];
            draw.AddText(pos+(size-ImGui.CalcTextSize(text))/2,Color(tab==i?Accent:Muted),text);
            if(ImGui.IsItemHovered())ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }
    private void SurfaceBox(Vector2 min,Vector2 max,Vector4 fill,Vector4 border,float rounding) {
        var draw=ImGui.GetWindowDrawList();
        draw.AddRectFilled(min+new Vector2(0,2*Unit),max+new Vector2(0,2*Unit),Color(new(0,0,0,.025f)),6*Unit);
        draw.AddRectFilled(min,max,Color(fill),6*Unit);
        draw.AddRect(min,max,Color(border),6*Unit);
    }
    private void Heading(string title,string subtitle) {
        TextLarge(title,1.42f,Ink);ImGui.TextColored(Muted,subtitle);ImGui.Dummy(new(1,10*Unit));
    }
    private void TextLarge(string text,float factor,Vector4 color) {
        using var font=plugin.PushFont(factor>=1.8f?2f:factor>=1.2f?1.3f:1f);
        ImGui.PushStyleColor(ImGuiCol.Text,color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }
    private bool Panel(string id,Vector2 size,Vector4? bg=null) {
        var origin=ImGui.GetCursorScreenPos();var available=ImGui.GetContentRegionAvail();
        var extent=new Vector2(size.X<=0?available.X+size.X:size.X,size.Y<=0?available.Y+size.Y:size.Y);
        SurfaceBox(origin+new Vector2(Unit),origin+extent-new Vector2(Unit),bg??Surface,Border,6*Unit);
        ImGui.PushStyleColor(ImGuiCol.ChildBg,Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(18,16)*Unit);
        var visible=ImGui.BeginChild(id,size,false,ImGuiWindowFlags.AlwaysUseWindowPadding|ImGuiWindowFlags.NoScrollWithMouse|ImGuiWindowFlags.NoBackground);
        PanelScrollForPreview[id]=ImGui.GetScrollY();
        HitRegions["panel-"+id]=new(origin.X,origin.Y,origin.X+extent.X,origin.Y+extent.Y);
        ImGui.SetScrollY(0); // Cards share the outer viewport; never retain an inner scroll offset.
        return visible;
    }
    private static void EndPanel(){ImGui.EndChild();ImGui.PopStyleVar();ImGui.PopStyleColor();}
    private void Overview() {
        var hero=ImGui.GetCursorScreenPos();var available=ImGui.GetContentRegionAvail().X;
        ImGui.TextColored(Accent,"资产概览");
        ImGui.Dummy(new(1,2*Unit));
        TextLarge(plugin.Current==null?"等待登录":Money(plugin.Current.Balance),2f,Ink);
        ImGui.SameLine();ImGui.TextColored(Gold,"GIL");
        ImGui.TextColored(Muted,"随身余额 / GIL · 自动记账");
        if(available>650*Unit) {
            var draw=ImGui.GetWindowDrawList();var info=plugin.Current?.CharacterName??"登录后开始记录";
            draw.AddText(new(hero.X+available-ImGui.CalcTextSize(info).X,hero.Y+28*Unit),Color(Ink),info);
            var caption="雇员销售款提取后入账";
            draw.AddText(new(hero.X+available-ImGui.CalcTextSize(caption).X,hero.Y+55*Unit),Color(Muted),caption);
        }
        ImGui.Dummy(new(1,12*Unit));
        var gap=12*Unit;var inlineMetrics=ImGui.GetContentRegionAvail().X>=540*Unit;
        var width=inlineMetrics?(ImGui.GetContentRegionAvail().X-gap*2)/3:ImGui.GetContentRegionAvail().X;
        Metric("income","今日收入","+ "+Money(todayTotal.Income),"本月 "+Money(monthTotal.Income),Green,width);
        if(inlineMetrics)ImGui.SameLine(0,gap);Metric("expense","今日支出","- "+Money(todayTotal.Expense),"本月 "+Money(monthTotal.Expense),Red,width);
        if(inlineMetrics)ImGui.SameLine(0,gap);Metric("net","本月结余",monthTotal.Net.ToString("+#,0;-#,0;0"),"收入减去支出",Ink,width);
        ImGui.Dummy(new(1,4*Unit));
        var rangeOrigin=ImGui.GetCursorScreenPos();var rangeWidth=Math.Min(300*Unit,ImGui.GetContentRegionAvail().X);
        SurfaceBox(rangeOrigin,rangeOrigin+new Vector2(rangeWidth,36*Unit),Surface2,Border,6*Unit);
        RangePosition=motion.Approach("range-position",rangeChoice,18);
        var indicatorStart=rangeOrigin+new Vector2(3*Unit+RangePosition*(rangeWidth-6*Unit)/3,3*Unit);
        SurfaceBox(indicatorStart,indicatorStart+new Vector2((rangeWidth-6*Unit)/3,30*Unit),Surface,Border,4*Unit);
        for(var i=0;i<3;i++) {
            var start=rangeOrigin+new Vector2(3*Unit+i*(rangeWidth-6*Unit)/3,3*Unit);var size=new Vector2((rangeWidth-6*Unit)/3,30*Unit);
            ImGui.SetCursorScreenPos(start);
            HitRegions["range-"+i]=new(start.X,start.Y,start.X+size.X,start.Y+size.Y);
            if(ImGui.InvisibleButton("range-"+i,size)){rangeChoice=i;source=null;Refresh();}
            var hover=motion.Approach("range-hover-"+i,ImGui.IsItemHovered()?1:0,18);
            if(i!=rangeChoice&&hover>.001f)ImGui.GetWindowDrawList().AddRectFilled(start,start+size,Color(Surface2 with {W=hover}),15*Unit);
            if(ImGui.IsItemHovered())ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var text=new[]{"当月","当年","所有"}[i];
            ImGui.GetWindowDrawList().AddText(start+(size-ImGui.CalcTextSize(text))/2,Color(i==rangeChoice?Ink:Muted),text);
        }
        ImGui.SetCursorScreenPos(rangeOrigin);ImGui.Dummy(new(rangeWidth,36*Unit));
        var wide=ImGui.GetContentRegionAvail().X>680*Unit;
        var chartWidth=wide?(ImGui.GetContentRegionAvail().X-gap)*.60f:ImGui.GetContentRegionAvail().X;
        var chartHeight=(trend.All(t=>t.Income==0&&t.Expense==0)?220:315)*Unit;
        if(Panel("trend-panel",new(chartWidth,chartHeight))) {
            ImGui.TextUnformatted("收支趋势");ImGui.SameLine();ImGui.TextColored(Muted,PeriodLabel);
            if(trend.All(t=>t.Income==0&&t.Expense==0)) Empty("等待第一笔金币流动","有收入或支出后，这里会自动展示每日趋势。",125);
            else DrawTrend();
        }EndPanel();
        if(wide)ImGui.SameLine(0,gap);
        if(Panel("categories",new(wide?ImGui.GetContentRegionAvail().X:-1,chartHeight))) {
            ImGui.TextUnformatted("支出去向");ImGui.SameLine();ImGui.TextColored(Muted,PeriodLabel);
            if(groups.Length==0)Empty("此范围还没有支出","每一笔开销，都会在这里找到去向。",125);
            else DrawCategories();
        }EndPanel();
        if(Panel("recent",new(-1,plugin.Entries.Length==0?130*Unit:210*Unit))) {
            var recentTitle=ImGui.GetCursorScreenPos();HitRegions["recent-title"]=new(recentTitle.X,recentTitle.Y,recentTitle.X+80*Unit,recentTitle.Y+17*Unit);
            ImGui.TextUnformatted("最近流水");ImGui.SameLine();if(ImGui.SmallButton("查看全部"))Navigate(1);
            if(plugin.Entries.Length==0){ImGui.Spacing();ImGui.TextColored(Muted,"账本已就绪，无需手动记账。");ImGui.TextColored(Muted,"下一次传送、购物或领取金币后，流水会自动出现在这里。");}
            else foreach(var e in plugin.Entries.Take(3)) {
                ImGui.Separator();
                var row=ImGui.GetCursorScreenPos();var draw=ImGui.GetWindowDrawList();var rowWidth=ImGui.GetContentRegionAvail().X;
                draw.AddCircleFilled(row+new Vector2(11,13)*Unit,11*Unit,Color((e.Amount>0?Green:Red) with {W=.10f}));
                draw.AddText(row+new Vector2(6,4)*Unit,Color(e.Amount>0?Green:Red),e.Amount>0?"+":"-");
                draw.AddText(row+new Vector2(34,4)*Unit,Color(Ink),e.Category.Label());
                if(rowWidth>600*Unit)draw.AddText(row+new Vector2(170,4)*Unit,Color(Muted),$"{e.Time.LocalDateTime:MM/dd HH:mm} · {e.Location.Name}");
                var amount=e.Amount.ToString("+#,0;-#,0;0");draw.AddText(new(row.X+rowWidth-ImGui.CalcTextSize(amount).X,row.Y+4*Unit),Color(e.Amount>0?Green:Red),amount);
                ImGui.Dummy(new(rowWidth,26*Unit));
            }
        }EndPanel();

    }
    private void Metric(string id,string title,string value,string subtitle,Vector4 color,float width) {
        var origin=ImGui.GetCursorScreenPos();var size=new Vector2(width,82*Unit);
        SurfaceBox(origin,origin+size,Surface,Border,10*Unit);
        var draw=ImGui.GetWindowDrawList();
        draw.AddCircleFilled(origin+new Vector2(17,20)*Unit,3*Unit,Color(color));
        draw.AddText(origin+new Vector2(29,11)*Unit,Color(Muted),title);
        using(plugin.PushFont(1.3f))draw.AddText(origin+new Vector2(14,40)*Unit,Color(Ink),value);
        if(width>250*Unit&&ImGui.CalcTextSize(subtitle).X<width*.56f)
            draw.AddText(origin+new Vector2(width-ImGui.CalcTextSize(subtitle).X-14*Unit,56*Unit),Color(Muted),subtitle);
        ImGui.Dummy(size);
    }
    private void Empty(string title,string description,float height) {
        var width=ImGui.GetContentRegionAvail().X;var p=ImGui.GetCursorScreenPos();
        var draw=ImGui.GetWindowDrawList();var center=p+new Vector2(width/2,33*Unit);
        draw.AddCircleFilled(center,20*Unit,Color(Surface2),32);
        draw.AddLine(center+new Vector2(-9,5)*Unit,center+new Vector2(-3,-4)*Unit,Color(Muted),2*Unit);
        draw.AddLine(center+new Vector2(-3,-4)*Unit,center+new Vector2(3,2)*Unit,Color(Muted),2*Unit);
        draw.AddLine(center+new Vector2(3,2)*Unit,center+new Vector2(10,-9)*Unit,Color(Muted),2*Unit);
        ImGui.Dummy(new(1,62*Unit));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX()+Math.Max(0,(width-ImGui.CalcTextSize(title).X)/2));ImGui.TextColored(Ink,title);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX()+width);ImGui.TextColored(Muted,description);ImGui.PopTextWrapPos();
        ImGui.Dummy(new(1,Math.Max(0,height-112)*Unit));
    }
    private void DrawCategories() {
        Vector4[] colors=[Accent,new(.36f,.49f,.39f,1),Gold,new(.34f,.43f,.44f,1),new(.47f,.39f,.29f,1)];
        var origin=ImGui.GetCursorScreenPos();var size=new Vector2(ImGui.GetContentRegionAvail().X,185*Unit);
        var total=groups.Sum(g=>g.Amount);var draw=ImGui.GetWindowDrawList();
        foreach(var cell in Treemap.Layout(groups.Select(g=>g.Amount).ToArray(),size.X,size.Y)) {
            var p=origin+new Vector2((float)cell.X,(float)cell.Y);var end=p+new Vector2((float)cell.Width,(float)cell.Height);
            var g=groups[cell.Index];var fill=colors[cell.Index%colors.Length];
            draw.AddRectFilled(p,end,Color(fill),2*Unit);
            draw.AddRect(p,end,Color(Surface),0,ImDrawFlags.None,2*Unit);
            draw.PushClipRect(p+new Vector2(5*Unit),end-new Vector2(5*Unit),true);
            var label=g.Category.Label();
            if(cell.Width>ImGui.CalcTextSize(label).X+16*Unit&&cell.Height>28*Unit)draw.AddText(p+new Vector2(10,10)*Unit,Color(new(1,1,.97f,1)),label);
            if(cell.Width>90*Unit&&cell.Height>70*Unit){draw.AddText(p+new Vector2(10,36)*Unit,Color(new(1,1,.97f,1)),Money(g.Amount));draw.AddText(p+new Vector2(10,60)*Unit,Color(new(1,1,.97f,.8f)),((double)g.Amount/total).ToString("P1"));}
            draw.PopClipRect();
        }
        ImGui.InvisibleButton("spending-treemap",size);
        if(ImGui.IsItemHovered()) {
            var mouse=ImGui.GetMousePos()-origin;
            foreach(var c in Treemap.Layout(groups.Select(g=>g.Amount).ToArray(),size.X,size.Y))if(mouse.X>=c.X&&mouse.X<c.X+c.Width&&mouse.Y>=c.Y&&mouse.Y<c.Y+c.Height) {
                var g=groups[c.Index];Tooltip($"{g.Category.Label()}\n支出 {Money(g.Amount)} GIL · {(double)g.Amount/total:P1}");break;
            }
        }
        ImGui.TextColored(Muted,"矩形面积代表金额 · 悬停查看明细");
    }
    private void DrawTrend() {
        var income=trend.Max(t=>t.Income);var expense=trend.Max(t=>t.Expense);
        var split=income>0&&expense>0&&(double)Math.Max(income,expense)/Math.Min(income,expense)>=8;
        if(income>0){ImGui.TextColored(Green,"收入");if(expense>0)ImGui.SameLine();}
        if(expense>0)ImGui.TextColored(Red,"支出");
        ImGui.TextColored(Muted,split?"量级差异较大 · 上下图独立刻度（GIL）":income==0?"仅支出 · 金额（GIL）":expense==0?"仅收入 · 金额（GIL）":"同一刻度 · 金额（GIL）");
        var origin=ImGui.GetCursorScreenPos();var size=new Vector2(ImGui.GetContentRegionAvail().X,185*Unit);
        var draw=ImGui.GetWindowDrawList();var left=70*Unit;var plotWidth=Math.Max(1,size.X-left-12*Unit);
        var step=plotWidth/Math.Max(1,trend.Length-1);
        void Lane(float top,float height,long max,bool showIncome,bool showExpense) {
            var baseline=origin.Y+top+height;var peak=origin.Y+top+8*Unit;
            for(var row=0;row<3;row++) {
                var y=baseline-(baseline-peak)*row/2;
                draw.AddLine(new(origin.X+left,y),new(origin.X+size.X,y),Color(Border));
                var value=AxisAmount(max*row/2d);draw.AddText(new(origin.X,y-7*Unit),Color(Muted),value);
            }
            void Series(bool positive,Vector4 color) {
                Vector2? previous=null;
                for(var i=0;i<trend.Length;i++) {
                    var amount=positive?trend[i].Income:trend[i].Expense;
                    var point=new Vector2(origin.X+left+i*step,baseline-(baseline-peak)*(float)((double)amount/max));
                    if(previous.HasValue)draw.AddLine(previous.Value,point,Color(color),2*Unit);
                    if(amount>0)draw.AddCircleFilled(point,3*Unit,Color(color),12);
                    previous=point;
                }
            }
            if(showIncome)Series(true,Green);if(showExpense)Series(false,Red);
        }
        if(split){Lane(0,67*Unit,income,true,false);Lane(88*Unit,67*Unit,expense,false,true);}
        else Lane(0,155*Unit,Math.Max(income,expense),income>0,expense>0);
        foreach(var i in new[]{0,(trend.Length-1)/3,(trend.Length-1)*2/3,trend.Length-1}.Distinct()) {
            var yearly=rangeChoice==2&&bucketDates.Length>1&&bucketDates[1].Year>bucketDates[0].Year;
            var label=bucketDates[i].ToString(rangeChoice==0?"MM/dd":yearly?"yyyy":"yy/MM");
            draw.AddText(new(origin.X+left+i*step-ImGui.CalcTextSize(label).X/2,origin.Y+164*Unit),Color(Muted),label);
        }
        ImGui.InvisibleButton("trend-hit",size);
        if(ImGui.IsItemHovered()&&ImGui.GetMousePos().X>=origin.X+left) {
            var i=Math.Clamp((int)Math.Round((ImGui.GetMousePos().X-origin.X-left)/step),0,trend.Length-1);
            if(trend[i].Income!=0||trend[i].Expense!=0) {
                Tooltip($"{bucketDates[i]:yyyy-MM-dd}\n收入  {Money(trend[i].Income)} GIL\n支出  {Money(trend[i].Expense)} GIL");
            }
        }
    }
    private static string AxisAmount(double value) => value>=100000000?(value/100000000).ToString("0.#")+"亿":value>=10000?(value/10000).ToString("0.#")+"万":value.ToString("0.#");
    private void Transactions() {
        if(Panel("filters",new(-1,175*Unit))) {
            if(ImGui.BeginTable("filter-layout",2,ImGuiTableFlags.SizingStretchSame)) {
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(ImGui.InputTextWithHint("##keyword","搜索用途、物品或角色",ref keyword,256))page=0;
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(ImGui.InputTextWithHint("##place","筛选地点",ref location,128))page=0;
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(ImGui.InputTextWithHint("##from","开始日期 YYYY-MM-DD",ref from,10))page=0;
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(ImGui.InputTextWithHint("##to","结束日期 YYYY-MM-DD",ref to,10))page=0;
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);
                if(Dropdown("direction",new[]{"全部收支","收入","支出"},ref direction))page=0;
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);
                var categories=Enum.GetValues<Category>();var categoryIndex=category==null?0:Array.IndexOf(categories,category.Value)+1;
                if(Dropdown("category",new[]{"全部分类"}.Concat(categories.Select(c=>c.Label())).ToArray(),ref categoryIndex)){category=categoryIndex==0?null:categories[categoryIndex-1];page=0;}
                ImGui.EndTable();
            }
            if(ImGui.SmallButton("今日")){from=to=DateTime.Today.ToString("yyyy-MM-dd");page=0;}ImGui.SameLine();
            if(ImGui.SmallButton("本月")){from=new DateTime(day.Year,day.Month,1).ToString("yyyy-MM-dd");to="";page=0;}ImGui.SameLine();
            if(ImGui.SmallButton("重置筛选")){from=to=keyword=location="";category=null;direction=page=0;}
        }EndPanel();
        DateTime? start=null,end=null;
        if(from.Length>0){if(DateTime.TryParseExact(from,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d))start=d;else{ImGui.TextColored(Red,"开始日期格式应为 YYYY-MM-DD");return;}}
        if(to.Length>0){if(DateTime.TryParseExact(to,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d))end=d;else{ImGui.TextColored(Red,"结束日期格式应为 YYYY-MM-DD");return;}}
        if(start>end){ImGui.TextColored(Red,"开始日期不能晚于结束日期");return;}
        var list=plugin.Entries.Where(e=>(start==null||e.Time.LocalDateTime.Date>=start)&&(end==null||e.Time.LocalDateTime.Date<=end)&&
            (direction==0||(direction==1?e.Amount>0:e.Amount<0))&&(category==null||e.Category==category)&&e.Location.Name.Contains(location,StringComparison.OrdinalIgnoreCase)&&
            (keyword.Length==0||$"{e.Detail} {e.ItemName} {e.CharacterName} {e.Category.Label()}".Contains(keyword,StringComparison.OrdinalIgnoreCase))).ToArray();
        var totals=Totals.From(list,exclude);
        ImGui.TextColored(Muted,$"{list.Length:N0} 笔    收入 {Money(totals.Income)}    支出 {Money(totals.Expense)}");
        if(list.Length==0) {
            if(Panel("no-transactions",new(-1,240*Unit)))Empty(plugin.Entries.Length==0?"还没有金币流水":"没有符合条件的流水",plugin.Entries.Length==0?"金币发生实际变化后会自动记录，无需手动添加。":"试试调整日期、分类或搜索条件。",180);EndPanel();return;
        }
        var pages=Math.Max(1,(list.Length+49)/50);page=Math.Clamp(page,0,pages-1);
        var tableHeight=Math.Max(180*Unit,ImGui.GetContentRegionAvail().Y-(selected==null?50:190)*Unit);
        if(ImGui.BeginTable("ledger",5,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.ScrollY|ImGuiTableFlags.SizingStretchProp,new(0,tableHeight))) {
            ImGui.TableSetupColumn("时间");ImGui.TableSetupColumn("分类 / 用途");ImGui.TableSetupColumn("地点");ImGui.TableSetupColumn("收支金币");ImGui.TableSetupColumn("余额");ImGui.TableSetupScrollFreeze(0,1);ImGui.TableHeadersRow();
            DateTime? date=null;
            foreach(var e in list.Skip(page*50).Take(50)) {
                if(date!=e.Time.LocalDateTime.Date){date=e.Time.LocalDateTime.Date;ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextColored(Gold,date.Value.ToString("yyyy-MM-dd"));}
                ImGui.TableNextRow(0,34*Unit);ImGui.TableNextColumn();
                if(ImGui.Selectable(e.Time.LocalDateTime.ToString("HH:mm:ss")+"##"+e.Id,selected?.Id==e.Id,ImGuiSelectableFlags.SpanAllColumns))selected=e;
                ImGui.TableNextColumn();ImGui.TextUnformatted(e.Category.Label());ImGui.TableNextColumn();ImGui.TextUnformatted(e.Location.Name);
                ImGui.TableNextColumn();ImGui.TextColored(e.IsGap?Muted:e.Amount>0?Green:Red,e.Amount.ToString("+#,0;-#,0;0"));ImGui.TableNextColumn();ImGui.TextUnformatted(Money(e.After));
            }ImGui.EndTable();
        }
        ImGui.BeginDisabled(page==0);if(ImGui.Button("上一页"))page--;ImGui.EndDisabled();ImGui.SameLine();ImGui.TextUnformatted($"{page+1} / {pages}");ImGui.SameLine();ImGui.BeginDisabled(page+1>=pages);if(ImGui.Button("下一页"))page++;ImGui.EndDisabled();
        if(selected is {} s&&plugin.Current?.CharacterId==s.CharacterId) {
            ImGui.Separator();ImGui.TextColored(Gold,"交易详情 · "+s.Category.Label());ImGui.SameLine();if(ImGui.SmallButton("收起"))selected=null;
            ImGui.TextWrapped($"{s.Time.LocalDateTime:yyyy-MM-dd HH:mm:ss} · {s.Location.Name} · 世界坐标 X {s.Location.X:F1} / Z {s.Location.Y:F1}");
            ImGui.TextUnformatted($"余额 {Money(s.Before)} → {Money(s.After)}");ImGui.TextWrapped("识别依据："+s.Source);
            if(s.Detail.Length>0)ImGui.TextWrapped(s.Detail);if(s.ItemId!=null)ImGui.TextWrapped($"物品：{s.ItemName??s.ItemId.ToString()} × {s.Quantity}");if(s.Destination!=null)ImGui.TextWrapped("目的地："+s.Destination);
        }
    }
    private void Settings() {
        BeginSettingsCard("记账偏好"); {
            var transfers=plugin.Config.ExcludeTransfers;if(ToggleRow("transfers","统计排除资金转移","只排除已识别的雇员及部队转账，普通交易仍保留。",ref transfers)){plugin.Config.ExcludeTransfers=transfers;plugin.SaveConfig();}
            var experimental=plugin.Config.EnableExperimentalClassification;if(ToggleRow("classification","交互窗口辅助分类","实验性：根据商店、修理等窗口推断用途，可能误判。",ref experimental)){plugin.Config.EnableExperimentalClassification=experimental;plugin.SaveConfig();}
            ImGui.PushTextWrapPos(0);ImGui.TextColored(Muted,"市场确认、传送核对与系统消息始终参与金额匹配；此开关仅控制商店、修理等窗口推断。缺少证据时保留未分类。");ImGui.PopTextWrapPos();
            var scale=plugin.Config.Scale;ImGui.SetNextItemWidth(210*Unit);if(ImGui.SliderFloat("界面缩放",ref scale,.8f,1.6f,"%.2f")){plugin.Config.Scale=scale;plugin.SaveConfig();}
        }EndSettingsCard();
        BeginSettingsCard("本地数据"); {ImGui.TextWrapped(plugin.DataDirectory);
            if(action is {IsCompleted:true}){try{actionResult=action.GetAwaiter().GetResult();}catch(Exception e){actionResult="操作失败："+e.Message;}action=null;}
            ImGui.BeginDisabled(action!=null);
            ImGui.BeginDisabled(plugin.Current==null);if(ActionButton("导出当前角色 CSV"))action=plugin.ExportAsync();ImGui.EndDisabled();ImGui.SameLine();if(ActionButton("备份全部账本"))action=plugin.BackupAsync();
            ImGui.Spacing();
            if(ActionButton(restoreExpanded?"收起恢复选项":"从备份恢复"))restoreExpanded=!restoreExpanded;
            if(restoreExpanded) {
                ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##backup","选择备份：输入完整 .sqlite 路径",ref restorePath,1024);
                ImGui.Checkbox("用备份替换全部角色账本（原数据将自动备份）",ref confirmRestore);
                ImGui.BeginDisabled(!confirmRestore||!File.Exists(restorePath));if(ActionButton("恢复备份")){plugin.Restore(restorePath);confirmRestore=false;actionResult="正在恢复，完成后将重新核对余额。";}ImGui.EndDisabled();
            }
            ImGui.EndDisabled();if(actionResult.Length>0)ImGui.TextWrapped(actionResult);
            ImGui.Spacing();ImGui.TextUnformatted("关于与透明度");
            ImGui.TextWrapped("本插件使用 AI 辅助开发；当前图标由 AI 生成，并经裁切与缩放处理。代码、规则和素材均需维护者持续审核与实测。");
            ImGui.TextWrapped("首次启用只建立余额基线。停用和退出游戏期间的变化单独标为差额，不计入正常收支。雇员销售款只有提取到角色后才记随身流水。");
            ImGui.TextColored(Muted,plugin.Status);
        }EndSettingsCard();
    }
}
