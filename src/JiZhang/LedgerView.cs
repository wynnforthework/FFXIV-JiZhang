using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using JiZhang.Core;
using static JiZhang.LedgerTheme;
namespace JiZhang;

// Shared by the game window and the offscreen visual verification host.
internal sealed class LedgerView(ILedgerUiContext plugin)
{
    private int tab;
    private string keyword="",from="",to="",location="";
    private int direction,page;
    private Category? category;
    private LedgerEntry? selected;
    private Task<string>? action;
    private string actionResult="",restorePath="";
    private bool confirmRestore;
    private LedgerEntry[]? source;
    private DateTime day;
    private bool exclude;
    private Totals todayTotal=new(0,0),monthTotal=new(0,0);
    private Totals[] trend=[];
    private (Category Category,long Amount)[] groups=[];
    private int tabForPreview=-1;
    internal void SelectTab(int value) => tabForPreview=value;
    private float Unit => ImGui.GetFontSize()/17f;
    private static string Money(long v) => v.ToString("N0",CultureInfo.CurrentCulture);
    private static uint Color(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);
    public void Draw() {
        ImGui.SetWindowFontScale(Math.Clamp(plugin.Config.Scale,.8f,1.6f));
        if(tabForPreview>=0){tab=tabForPreview;tabForPreview=-1;}
        Refresh();
        var sidebar=142*Unit;
        if(ImGui.BeginChild("navigation",new Vector2(sidebar,0),false,ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoBackground)) Sidebar();
        ImGui.EndChild();ImGui.SameLine(0,20*Unit);
        if(ImGui.BeginChild("content",Vector2.Zero,false,ImGuiWindowFlags.NoBackground)) {
            Heading(new[]{"资产概览","收支流水","偏好与数据"}[tab],new[]{"把每一笔金币，记得清清楚楚。","自动记录，无需手动填写。","按照你的习惯，照顾好每一笔数据。"}[tab]);
            if(tab==0)Overview();else if(tab==1)Transactions();else Settings();
        }ImGui.EndChild();
    }
    private void Refresh() {
        var date=DateTime.Today;
        if(ReferenceEquals(source,plugin.Entries)&&day==date&&exclude==plugin.Config.ExcludeTransfers)return;
        source=plugin.Entries;day=date;exclude=plugin.Config.ExcludeTransfers;
        var month=new DateTime(date.Year,date.Month,1);
        todayTotal=Totals.From(source.Where(e=>e.Time.LocalDateTime.Date==date),exclude);
        monthTotal=Totals.From(source.Where(e=>e.Time.LocalDateTime>=month),exclude);
        trend=Enumerable.Range(0,14).Select(i=>Totals.From(source.Where(e=>e.Time.LocalDateTime.Date==date.AddDays(i-13)),exclude)).ToArray();
        groups=source.Where(e=>!e.IsGap&&e.Amount<0&&e.Time.LocalDateTime>=month&&(!exclude||!e.Category.IsTransfer()))
            .GroupBy(e=>e.Category).Select(g=>(g.Key,-g.Sum(e=>e.Amount))).OrderByDescending(g=>g.Item2).ToArray();
    }
    private void Sidebar() {
        var p=ImGui.GetCursorScreenPos();var d=ImGui.GetWindowDrawList();
        d.AddCircleFilled(p+new Vector2(20,21)*Unit,19*Unit,Color(new(.97f,.90f,.72f,1)),32);
        d.AddCircle(p+new Vector2(20,21)*Unit,14*Unit,Color(Gold),32,1.5f*Unit);
        d.AddText(p+new Vector2(14,11)*Unit,Color(Gold),"G");
        ImGui.Dummy(new(1,50*Unit));
        TextLarge("金币账本",1.12f,Ink);ImGui.TextColored(Muted,"GIL LEDGER");
        ImGui.Dummy(new(1,25*Unit));
        for(var i=0;i<3;i++) {
            var active=tab==i;
            ImGui.PushStyleColor(ImGuiCol.Button,active?new Vector4(.13f,.29f,.23f,1):Canvas);
            ImGui.PushStyleColor(ImGuiCol.Text,active?Vector4.One:Muted);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered,active?new Vector4(.18f,.36f,.28f,1):new Vector4(.88f,.92f,.89f,1));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive,new Vector4(.23f,.42f,.31f,1));
            try {if(ImGui.Button(new[]{"资产概览","收支流水","偏好与数据"}[i],new(-1,43*Unit)))tab=i;}
            finally{ImGui.PopStyleColor(4);}
        }
        ImGui.Dummy(new(1,22*Unit));ImGui.Separator();ImGui.Spacing();
        ImGui.TextColored(Muted,"当前角色");ImGui.TextWrapped(plugin.Current?.CharacterName??"等待登录");
        var recording=plugin.Current!=null&&plugin.Status.StartsWith("正在自动记录",StringComparison.Ordinal);
        ImGui.Spacing();ImGui.TextColored(recording?Green:Muted,recording?"自动记账中":plugin.Current==null?"等待角色":"记录状态待确认");
        ImGui.PushTextWrapPos(0);ImGui.TextColored(Muted,"本地保存 · 自动记录");ImGui.PopTextWrapPos();
        ImGui.Dummy(new(1,16*Unit));ImGui.TextColored(Muted,"0.1.1 · /jizhang");
    }
    private void Heading(string title,string subtitle) {
        TextLarge(title,1.42f,Ink);ImGui.TextColored(Muted,subtitle);ImGui.Dummy(new(1,10*Unit));
    }
    private void TextLarge(string text,float factor,Vector4 color) {
        var fit=Math.Max(.85f,Math.Min(factor,ImGui.GetContentRegionAvail().X/Math.Max(1,ImGui.CalcTextSize(text).X)));
        var pos=ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(),ImGui.GetFontSize()*fit,pos,Color(color),text);
        ImGui.Dummy(new Vector2(Math.Min(ImGui.GetContentRegionAvail().X,ImGui.CalcTextSize(text).X*fit),ImGui.GetFontSize()*fit+3*Unit));
    }
    private bool Panel(string id,Vector2 size,Vector4? bg=null) {
        ImGui.PushStyleColor(ImGuiCol.ChildBg,bg??Vector4.One);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(18,16)*Unit);
        var visible=ImGui.BeginChild(id,size,true,ImGuiWindowFlags.AlwaysUseWindowPadding|ImGuiWindowFlags.NoScrollbar);
        ImGui.SetWindowFontScale(Math.Clamp(plugin.Config.Scale,.8f,1.6f));
        return visible;
    }
    private static void EndPanel(){ImGui.EndChild();ImGui.PopStyleVar();ImGui.PopStyleColor();}
    private void Overview() {
        if(Panel("wallet",new(-1,140*Unit),new(.13f,.25f,.21f,1))) {
            ImGui.TextColored(new Vector4(.75f,.84f,.78f,1),"当前随身金币");
            ImGui.Dummy(new(1,4*Unit));
            TextLarge(plugin.Current==null?"—":Money(plugin.Current.Balance),2.1f,new(.98f,.94f,.82f,1));
            ImGui.SameLine();ImGui.TextColored(new Vector4(.75f,.84f,.78f,1),"GIL");
            ImGui.PushTextWrapPos(0);ImGui.TextColored(new Vector4(.75f,.84f,.78f,1),plugin.Current==null?"登录角色后开始记录":"只记录随身金币，雇员销售款提取后入账");ImGui.PopTextWrapPos();
        }EndPanel();
        var gap=10*Unit;var inlineMetrics=ImGui.GetContentRegionAvail().X>=540*Unit;
        var width=inlineMetrics?(ImGui.GetContentRegionAvail().X-gap*2)/3:ImGui.GetContentRegionAvail().X;
        Metric("income","今日收入","+ "+Money(todayTotal.Income),"本月 "+Money(monthTotal.Income),Green,width);
        if(inlineMetrics)ImGui.SameLine(0,gap);Metric("expense","今日支出","- "+Money(todayTotal.Expense),"本月 "+Money(monthTotal.Expense),Red,width);
        if(inlineMetrics)ImGui.SameLine(0,gap);Metric("net","本月结余",monthTotal.Net.ToString("+#,0;-#,0;0"),"收入减去支出",Ink,width);
        var wide=ImGui.GetContentRegionAvail().X>680*Unit;
        var chartWidth=wide?(ImGui.GetContentRegionAvail().X-gap)*.61f:ImGui.GetContentRegionAvail().X;
        var chartHeight=(trend.All(t=>t.Income==0&&t.Expense==0)?205:245)*Unit;
        if(Panel("trend-panel",new(chartWidth,chartHeight))) {
            ImGui.TextUnformatted("收支趋势");ImGui.SameLine();ImGui.TextColored(Muted,"最近 14 天");
            if(trend.All(t=>t.Income==0&&t.Expense==0)) Empty("等待第一笔金币流动","有收入或支出后，这里会自动展示每日趋势。",125);
            else DrawTrend();
        }EndPanel();
        if(wide)ImGui.SameLine(0,gap);
        if(Panel("categories",new(wide?ImGui.GetContentRegionAvail().X:-1,chartHeight))) {
            ImGui.TextUnformatted("支出去向");ImGui.SameLine();ImGui.TextColored(Muted,"本月");
            if(groups.Length==0)Empty("本月还没有支出","每一笔开销，都会在这里找到去向。",125);
            else {
                var count=Math.Min(groups.Length,4);
                for(var i=0;i<count;i++) {
                    var g=groups[i];ImGui.Spacing();ImGui.TextUnformatted(g.Category.Label());ImGui.SameLine();ImGui.TextColored(Muted,Money(g.Amount));
                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram,i==0?Green:new Vector4(.64f,.73f,.65f,1));
                    ImGui.ProgressBar((float)g.Amount/Math.Max(1,monthTotal.Expense),new(-1,6*Unit),"");ImGui.PopStyleColor();
                }
                if(groups.Length>4&&ImGui.SmallButton("查看全部流水"))tab=1;
            }
        }EndPanel();
        if(Panel("recent",new(-1,plugin.Entries.Length==0?115*Unit:190*Unit))) {
            ImGui.TextUnformatted("最近流水");ImGui.SameLine();if(ImGui.SmallButton("查看全部"))tab=1;
            if(plugin.Entries.Length==0){ImGui.Spacing();ImGui.TextColored(Muted,"账本已就绪，无需手动记账。");ImGui.TextColored(Muted,"下一次传送、购物或领取金币后，流水会自动出现在这里。");}
            else foreach(var e in plugin.Entries.Take(3)) {
                ImGui.Separator();ImGui.TextUnformatted(e.Category.Label());ImGui.SameLine();ImGui.TextColored(e.Amount>0?Green:Red,e.Amount.ToString("+#,0;-#,0;0"));
                ImGui.TextColored(Muted,$"{e.Time.LocalDateTime:MM/dd HH:mm} · {e.Location.Name}");
            }
        }EndPanel();
        ImGui.TextColored(Muted,exclude?"统计已排除雇员／部队转账 · 离线差额不计入收支":"统计包含随身资金转入转出 · 离线差额不计入收支");
    }
    private void Metric(string id,string title,string value,string subtitle,Vector4 color,float width) {
        if(Panel(id,new(width,107*Unit))) {
            ImGui.TextColored(Muted,title);TextLarge(value,1.3f,color);ImGui.TextColored(Muted,subtitle);
        }EndPanel();
    }
    private void Empty(string title,string description,float height) {
        var width=ImGui.GetContentRegionAvail().X;var p=ImGui.GetCursorScreenPos();
        var draw=ImGui.GetWindowDrawList();var center=p+new Vector2(width/2,33*Unit);
        draw.AddCircleFilled(center,20*Unit,Color(new(.94f,.95f,.92f,1)),32);
        draw.AddLine(center+new Vector2(-9,5)*Unit,center+new Vector2(-3,-4)*Unit,Color(new(.57f,.65f,.58f,1)),2*Unit);
        draw.AddLine(center+new Vector2(-3,-4)*Unit,center+new Vector2(3,2)*Unit,Color(new(.57f,.65f,.58f,1)),2*Unit);
        draw.AddLine(center+new Vector2(3,2)*Unit,center+new Vector2(10,-9)*Unit,Color(new(.57f,.65f,.58f,1)),2*Unit);
        ImGui.Dummy(new(1,62*Unit));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX()+Math.Max(0,(width-ImGui.CalcTextSize(title).X)/2));ImGui.TextColored(Ink,title);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX()+width);ImGui.TextColored(Muted,description);ImGui.PopTextWrapPos();
        ImGui.Dummy(new(1,Math.Max(0,height-112)*Unit));
    }
    private void DrawTrend() {
        ImGui.TextColored(Green,"收入");ImGui.SameLine();ImGui.TextColored(Red,"支出");
        var pos=ImGui.GetCursorScreenPos();var size=new Vector2(ImGui.GetContentRegionAvail().X,150*Unit);
        var draw=ImGui.GetWindowDrawList();var max=Math.Max(1,trend.Max(t=>Math.Max(t.Income,t.Expense)));
        var step=size.X/14;var baseline=pos.Y+size.Y-25*Unit;var height=size.Y-42*Unit;
        for(var row=0;row<3;row++) {
            var y=baseline-height*row/2;
            draw.AddLine(new(pos.X,y),new(pos.X+size.X,y),Color(new(.90f,.92f,.90f,1)));
        }
        for(var i=0;i<14;i++) {
            var x=pos.X+i*step+step*.17f;var bar=step*.25f;
            if(trend[i].Income>0)draw.AddRectFilled(new(x,baseline-height*trend[i].Income/max),new(x+bar,baseline),Color(Green),2);
            if(trend[i].Expense>0)draw.AddRectFilled(new(x+bar+2,baseline-height*trend[i].Expense/max),new(x+2*bar+2,baseline),Color(Red),2);
            if(i%4==0)draw.AddText(new(x,baseline+7*Unit),Color(Muted),day.AddDays(i-13).ToString("MM/dd"));
        }
        ImGui.InvisibleButton("trend-hit",size);
        if(ImGui.IsItemHovered()) {
            var i=Math.Clamp((int)((ImGui.GetMousePos().X-pos.X)/step),0,13);
            if(trend[i].Income!=0||trend[i].Expense!=0) {
                // Tooltips have their own explicit palette; never inherit unreadable game colors.
                ImGui.PushStyleColor(ImGuiCol.PopupBg,new Vector4(.10f,.16f,.13f,1));ImGui.PushStyleColor(ImGuiCol.Text,Vector4.One);
                try {ImGui.BeginTooltip();ImGui.TextUnformatted(day.AddDays(i-13).ToString("yyyy-MM-dd"));ImGui.TextUnformatted($"收入  {Money(trend[i].Income)} GIL");ImGui.TextUnformatted($"支出  {Money(trend[i].Expense)} GIL");ImGui.EndTooltip();}
                finally {ImGui.PopStyleColor(2);}
            }
        }
    }
    private void Transactions() {
        if(Panel("filters",new(-1,175*Unit))) {
            if(ImGui.BeginTable("filter-layout",2,ImGuiTableFlags.SizingStretchSame)) {
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(ImGui.InputTextWithHint("##keyword","搜索用途、物品或角色",ref keyword,256))page=0;
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(ImGui.InputTextWithHint("##place","筛选地点",ref location,128))page=0;
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(ImGui.InputTextWithHint("##from","开始日期 YYYY-MM-DD",ref from,10))page=0;
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(ImGui.InputTextWithHint("##to","结束日期 YYYY-MM-DD",ref to,10))page=0;
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);
                if(ImGui.BeginCombo("##direction",new[]{"全部收支","收入","支出"}[direction])) {for(var i=0;i<3;i++)if(ImGui.Selectable(new[]{"全部收支","收入","支出"}[i],direction==i)){direction=i;page=0;}ImGui.EndCombo();}
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);
                if(ImGui.BeginCombo("##category",category?.Label()??"全部分类")){if(ImGui.Selectable("全部分类",category==null)){category=null;page=0;}foreach(var c in Enum.GetValues<Category>())if(ImGui.Selectable(c.Label(),category==c)){category=c;page=0;}ImGui.EndCombo();}
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
        if(Panel("preferences",new(-1,210*Unit))) {
            ImGui.TextUnformatted("记账偏好");ImGui.Spacing();
            var transfers=plugin.Config.ExcludeTransfers;if(ImGui.Checkbox("统计时排除雇员与部队资金转移",ref transfers)){plugin.Config.ExcludeTransfers=transfers;plugin.SaveConfig();}
            var experimental=plugin.Config.EnableExperimentalClassification;if(ImGui.Checkbox("启用实验性用途识别",ref experimental)){plugin.Config.EnableExperimentalClassification=experimental;plugin.SaveConfig();}
            ImGui.PushTextWrapPos(0);ImGui.TextColored(Muted,"关闭用途识别时仍会记录全部金币变化；未经确认的用途保留为未分类。");ImGui.PopTextWrapPos();
            var scale=plugin.Config.Scale;ImGui.SetNextItemWidth(210*Unit);if(ImGui.SliderFloat("界面缩放",ref scale,.8f,1.6f,"%.2f")){plugin.Config.Scale=scale;plugin.SaveConfig();}
        }EndPanel();
        if(Panel("data",new(-1,0))) {
            ImGui.TextUnformatted("本地数据");ImGui.TextWrapped(plugin.DataDirectory);
            if(action is {IsCompleted:true}){try{actionResult=action.GetAwaiter().GetResult();}catch(Exception e){actionResult="操作失败："+e.Message;}action=null;}
            ImGui.BeginDisabled(action!=null);
            ImGui.BeginDisabled(plugin.Current==null);if(ImGui.Button("导出当前角色 CSV"))action=plugin.ExportAsync();ImGui.EndDisabled();ImGui.SameLine();if(ImGui.Button("备份全部账本"))action=plugin.BackupAsync();
            ImGui.Spacing();
            if(ImGui.CollapsingHeader("从备份恢复")) {
                ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##backup","选择备份：输入完整 .sqlite 路径",ref restorePath,1024);
                ImGui.Checkbox("用备份替换全部角色账本（原数据将自动备份）",ref confirmRestore);
                ImGui.BeginDisabled(!confirmRestore||!File.Exists(restorePath));if(ImGui.Button("恢复备份")){plugin.Restore(restorePath);confirmRestore=false;actionResult="正在恢复，完成后将重新核对余额。";}ImGui.EndDisabled();
            }
            ImGui.EndDisabled();if(actionResult.Length>0)ImGui.TextWrapped(actionResult);
            ImGui.Spacing();ImGui.Separator();ImGui.TextUnformatted("关于与透明度");
            ImGui.TextWrapped("本插件使用 AI 辅助开发；当前图标由 AI 生成，并经裁切与缩放处理。代码、规则和素材均需维护者持续审核与实测。");
            ImGui.TextWrapped("首次启用只建立余额基线。停用和退出游戏期间的变化单独标为差额，不计入正常收支。雇员销售款只有提取到角色后才记随身流水。");
            ImGui.TextColored(Muted,plugin.Status);
        }EndPanel();
    }
}
