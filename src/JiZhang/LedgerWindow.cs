using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using JiZhang.Core;
namespace JiZhang;

internal sealed class LedgerWindow(Plugin plugin)
{
    public bool IsOpen;
    private int tab;
    private string keyword = "";
    private string from = "";
    private string to = "";
    private string location = "";
    private int direction;
    private Category? category;
    private int page;
    private LedgerEntry? selected;
    private Task<string>? action;
    private string actionResult = "";
    private string restorePath = "";
    private bool confirmRestore;
    private static readonly Vector4 Ink = new(.13f, .18f, .25f, 1);
    private static readonly Vector4 Muted = new(.43f, .48f, .55f, 1);
    private static readonly Vector4 Green = new(.05f, .49f, .34f, 1);
    private static readonly Vector4 Red = new(.81f, .25f, .30f, 1);
    private static readonly Vector4 Gold = new(.70f, .48f, .12f, 1);
    private LedgerEntry[]? overviewSource;
    private DateTime overviewDay;
    private bool overviewExclude;
    private Totals dayTotals = new(0,0);
    private Totals monthTotals = new(0,0);
    private Totals[] trendTotals = [];
    private (Category Category,long Amount)[] expenseGroups = [];
    private int gapCount;
    private LedgerEntry[]? filterSource;
    private string filterKey = "";
    private LedgerEntry[] filtered = [];
    private static string Money(long v) => v.ToString("N0", CultureInfo.CurrentCulture);
    public void Draw() {
        if (!IsOpen) return;
        PushTheme();
        try {
            ImGui.SetNextWindowSize(new(1020, 780), ImGuiCond.FirstUseEver);
            var visible = ImGui.Begin("金币账本###JiZhang", ref IsOpen, ImGuiWindowFlags.NoCollapse);
            try { if (visible) {
                ImGui.SetWindowFontScale(plugin.Config.Scale);
                ImGui.TextColored(Gold, "GIL  /  金币账本");
                ImGui.SameLine(); ImGui.TextColored(Muted, "   每一笔冒险，都有迹可循");
                ImGui.Spacing();
                ImGui.TextUnformatted(plugin.Current?.CharacterName ?? "尚未登录角色");
                ImGui.SameLine(); ImGui.TextColored(Muted, " · " + plugin.Status);
                ImGui.Spacing();
                foreach (var (name, index) in new[] { ("资产概览", 0), ("收支流水", 1), ("偏好与数据", 2) }) {
                    if (index != 0) ImGui.SameLine();
                    if (ImGui.Button((tab == index ? "•  " : "   ") + name, new(145 * plugin.Config.Scale, 36 * plugin.Config.Scale))) tab = index;
                }
                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                if (tab == 0) Overview(); else if (tab == 1) Transactions(); else Settings();
            } } finally { ImGui.End(); }
        } catch (Exception e) { Plugin.Log.Error(e, "账本界面绘制失败"); }
        finally { ImGui.PopStyleVar(4); ImGui.PopStyleColor(24); }
    }
    private static void PushTheme() {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.96f,.97f,.985f,1));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.One);
        ImGui.PushStyleColor(ImGuiCol.Text, Ink);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.86f,.89f,.92f,1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.91f,.94f,.97f,1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(.85f,.90f,.95f,1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(.80f,.86f,.93f,1));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.88f,.92f,.96f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.78f,.85f,.93f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.70f,.80f,.90f,1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.87f,.92f,.96f,1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.81f,.88f,.95f,1));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(.74f,.84f,.94f,1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Gold);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Gold);
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, new Vector4(.90f,.93f,.96f,1));
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(.94f,.96f,.98f,1));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(.88f,.92f,.96f,1));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(.86f,.91f,.96f,1));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, new Vector4(.88f,.92f,.96f,1));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(.98f,.99f,1,1));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Gold);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Vector4(.81f,.61f,.24f,1));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(24,20));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10,10));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 7f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 12f);
    }
    private void Overview() {
        var today = DateTime.Now.Date;
        var month = new DateTime(today.Year, today.Month, 1);
        if (!ReferenceEquals(overviewSource,plugin.Entries) || overviewDay!=today || overviewExclude!=plugin.Config.ExcludeTransfers) {
            overviewSource=plugin.Entries;overviewDay=today;overviewExclude=plugin.Config.ExcludeTransfers;
            dayTotals=Totals.From(plugin.Entries.Where(e=>e.Time.LocalDateTime.Date==today),overviewExclude);
            monthTotals=Totals.From(plugin.Entries.Where(e=>e.Time.LocalDateTime>=month),overviewExclude);
            trendTotals=Enumerable.Range(0,14).Select(i=>Totals.From(plugin.Entries.Where(e=>e.Time.LocalDateTime.Date==today.AddDays(i-13)),overviewExclude)).ToArray();
            expenseGroups=plugin.Entries.Where(e=>!e.IsGap&&e.Amount<0&&e.Time.LocalDateTime>=month&&(!overviewExclude||!e.Category.IsTransfer()))
                .GroupBy(e=>e.Category).Select(g=>(Category:g.Key,Amount:-g.Sum(e=>e.Amount))).OrderByDescending(g=>g.Amount).ToArray();
            gapCount=plugin.Entries.Count(e=>e.IsGap);
        }
        var todayTotal=dayTotals;var monthTotal=monthTotals;
        var width = Math.Max(160, (ImGui.GetContentRegionAvail().X - 24) / 3);
        Card("balance", "随身金币", Money(plugin.Current?.Balance ?? 0), "GIL · 当前角色", Gold, width);
        ImGui.SameLine(); Card("income", "今日收入", "+ " + Money(todayTotal.Income), "本月收入  " + Money(monthTotal.Income), Green, width);
        ImGui.SameLine(); Card("expense", "今日支出", "− " + Money(todayTotal.Expense), "本月支出  " + Money(monthTotal.Expense), Red, width);
        ImGui.Spacing();
        ImGui.TextColored(Muted, $"本月净变化  {monthTotal.Net:+#,0;-#,0;0} GIL     ·     {(plugin.Config.ExcludeTransfers ? "已排除雇员／部队转账" : "包含资金转移")}     ·     离线差额不计入统计");
        ImGui.Spacing();
        ImGui.TextUnformatted("最近 14 天 · 每日金币流向");
        DrawTrend(today);
        ImGui.Spacing(); ImGui.TextUnformatted("本月支出去向");
        var groups = expenseGroups;
        if (groups.Length == 0) Empty("还没有支出记录。开始冒险后，账本会自动填满。");
        else foreach (var g in groups) { ImGui.TextUnformatted(g.Category.Label()); ImGui.SameLine(); ImGui.TextColored(Muted, Money(g.Amount) + " GIL"); ImGui.ProgressBar((float)g.Amount / Math.Max(1,monthTotal.Expense), new(-1,10), ""); }
        var gaps = gapCount;
        if (gaps > 0) ImGui.TextColored(Muted, $"账本含 {gaps} 条记录中断差额，可在流水中查看。");
    }
    private void Card(string id, string title, string value, string subtitle, Vector4 color, float width) {
        if (ImGui.BeginChild(id, new Vector2(width,125 * plugin.Config.Scale), false)) {
            ImGui.TextColored(Muted, title); ImGui.Spacing(); ImGui.TextColored(color, value); ImGui.TextColored(Muted, subtitle);
        } ImGui.EndChild();
    }
    private void DrawTrend(DateTime today) {
        var totals = trendTotals;
        var pos = ImGui.GetCursorScreenPos(); var size = new Vector2(ImGui.GetContentRegionAvail().X,150 * plugin.Config.Scale);
        var draw = ImGui.GetWindowDrawList(); var max = Math.Max(1, totals.Max(t => Math.Max(t.Income,t.Expense)));
        var step = size.X / 14; var baseline = pos.Y + size.Y - 25; var height = size.Y - 35;
        draw.AddLine(new(pos.X,baseline),new(pos.X+size.X,baseline),ImGui.GetColorU32(ImGuiCol.Border));
        for (var i=0;i<14;i++) {
            var x=pos.X+i*step+step*.18f; var bar=step*.24f;
            if (totals[i].Income>0) draw.AddRectFilled(new(x,baseline-height*totals[i].Income/max),new(x+bar,baseline),ImGui.ColorConvertFloat4ToU32(Green),3);
            if (totals[i].Expense>0) draw.AddRectFilled(new(x+bar+2,baseline-height*totals[i].Expense/max),new(x+2*bar+2,baseline),ImGui.ColorConvertFloat4ToU32(Red),3);
            if (i%2==0) draw.AddText(new(x,baseline+5),ImGui.ColorConvertFloat4ToU32(Muted),today.AddDays(i-13).ToString("MM/dd"));
        }
        ImGui.InvisibleButton("trend",size);
        if (ImGui.IsItemHovered()) { var i=Math.Clamp((int)((ImGui.GetMousePos().X-pos.X)/step),0,13); ImGui.SetTooltip($"{today.AddDays(i-13):yyyy-MM-dd}\n收入 {Money(totals[i].Income)}   支出 {Money(totals[i].Expense)}"); }
    }
    private void Transactions() {
        ImGui.SetNextItemWidth(230); if (ImGui.InputTextWithHint("##keyword","搜索用途、物品或角色",ref keyword,256)) page=0;
        ImGui.SameLine(); ImGui.SetNextItemWidth(150); if (ImGui.InputTextWithHint("##place","地点",ref location,128)) page=0;
        ImGui.SetNextItemWidth(150); if (ImGui.InputTextWithHint("##from","开始 YYYY-MM-DD",ref from,10)) page=0;
        ImGui.SameLine(); ImGui.SetNextItemWidth(150); if (ImGui.InputTextWithHint("##to","结束 YYYY-MM-DD",ref to,10)) page=0;
        ImGui.SameLine(); ImGui.SetNextItemWidth(100);
        if (ImGui.BeginCombo("##direction",new[]{"全部收支","收入","支出"}[direction])) { for (var i=0;i<3;i++) if (ImGui.Selectable(new[]{"全部收支","收入","支出"}[i],direction==i)) {direction=i;page=0;} ImGui.EndCombo(); }
        ImGui.SameLine(); ImGui.SetNextItemWidth(140);
        if (ImGui.BeginCombo("##category",category?.Label() ?? "全部分类")) { if (ImGui.Selectable("全部分类",category==null)) {category=null;page=0;} foreach(var c in Enum.GetValues<Category>()) if (ImGui.Selectable(c.Label(),category==c)) {category=c;page=0;} ImGui.EndCombo(); }
        if (ImGui.Button("今日")) {from=to=DateTime.Today.ToString("yyyy-MM-dd");page=0;} ImGui.SameLine();
        if (ImGui.Button("本月")) {from=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1).ToString("yyyy-MM-dd");to="";page=0;} ImGui.SameLine();
        if (ImGui.Button("重置筛选")) {from=to=keyword=location="";category=null;direction=page=0;}
        DateTime? start=null,end=null;
        if (from.Length>0) { if (DateTime.TryParseExact(from,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)) start=d; else {ImGui.TextColored(Red,"开始日期格式应为 YYYY-MM-DD");return;} }
        if (to.Length>0) { if (DateTime.TryParseExact(to,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)) end=d; else {ImGui.TextColored(Red,"结束日期格式应为 YYYY-MM-DD");return;} }
        if (start>end) {ImGui.TextColored(Red,"开始日期不能晚于结束日期");return;}
        var key=$"{start:O}|{end:O}|{direction}|{category}|{location}|{keyword}";
        if(!ReferenceEquals(filterSource,plugin.Entries)||filterKey!=key) {
            filterSource=plugin.Entries;filterKey=key;
            filtered=plugin.Entries.Where(e => (start==null || e.Time.LocalDateTime.Date>=start) && (end==null || e.Time.LocalDateTime.Date<=end) &&
                (direction==0 || (direction==1 ? e.Amount>0 : e.Amount<0)) && (category==null || e.Category==category) &&
                e.Location.Name.Contains(location,StringComparison.OrdinalIgnoreCase) &&
                (keyword.Length==0 || $"{e.Detail} {e.ItemName} {e.CharacterName} {e.Category.Label()}".Contains(keyword,StringComparison.OrdinalIgnoreCase))).ToArray();
        }
        var list=filtered;
        var totals=Totals.From(list,plugin.Config.ExcludeTransfers);
        ImGui.TextColored(Muted,$"{list.Length:N0} 笔 · 收入 {Money(totals.Income)} · 支出 {Money(totals.Expense)} · 净变化 {Money(totals.Net)}");
        var pages=Math.Max(1,(list.Length+49)/50);page=Math.Clamp(page,0,pages-1);
        if (ImGui.BeginTable("ledger",5,ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,new(0,300 * plugin.Config.Scale))) {
            ImGui.TableSetupColumn("时间");ImGui.TableSetupColumn("分类 / 用途");ImGui.TableSetupColumn("地点");ImGui.TableSetupColumn("收支金币");ImGui.TableSetupColumn("余额");ImGui.TableSetupScrollFreeze(0,1);ImGui.TableHeadersRow();
            DateTime? date=null;
            foreach(var e in list.Skip(page*50).Take(50)) {
                if(date!=e.Time.LocalDateTime.Date) {date=e.Time.LocalDateTime.Date;ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextColored(Gold,date.Value.ToString("yyyy-MM-dd"));}
                ImGui.TableNextRow();ImGui.TableNextColumn();
                if(ImGui.Selectable(e.Time.LocalDateTime.ToString("HH:mm:ss")+"##"+e.Id,selected?.Id==e.Id,ImGuiSelectableFlags.SpanAllColumns)) selected=e;
                ImGui.TableNextColumn();ImGui.TextUnformatted(e.Category.Label());
                ImGui.TableNextColumn();ImGui.TextUnformatted(e.Location.Name);
                ImGui.TableNextColumn();ImGui.TextColored(e.IsGap ? Muted : e.Amount>0 ? Green : Red,e.Amount.ToString("+#,0;-#,0;0"));
                ImGui.TableNextColumn();ImGui.TextUnformatted(Money(e.After));
            } ImGui.EndTable();
        }
        if(list.Length==0) Empty("这里还没有流水。只有实际发生金币变化时才会入账。");
        if(ImGui.Button("上一页") && page>0) page--;ImGui.SameLine();ImGui.TextUnformatted($"{page+1} / {pages}");ImGui.SameLine();if(ImGui.Button("下一页") && page+1<pages)page++;
        if(selected is { } s && plugin.Current?.CharacterId == s.CharacterId) {
            ImGui.Separator();ImGui.TextColored(Gold,"交易详情 · "+s.Category.Label());
            ImGui.TextWrapped($"{s.Time.LocalDateTime:yyyy-MM-dd HH:mm:ss} · {s.Location.Name} · 世界坐标 X {s.Location.X:F1} / Z {s.Location.Y:F1}");
            ImGui.TextUnformatted($"余额 {Money(s.Before)} → {Money(s.After)}");ImGui.TextWrapped("识别依据："+s.Source);
            if(s.Detail.Length>0)ImGui.TextWrapped(s.Detail);
            if(s.ItemId!=null)ImGui.TextWrapped($"物品：{s.ItemName ?? s.ItemId.ToString()} × {s.Quantity}");
            if(s.Destination!=null)ImGui.TextWrapped("目的地："+s.Destination);
        }
    }
    private void Settings() {
        var exclude=plugin.Config.ExcludeTransfers;if(ImGui.Checkbox("统计时排除雇员与部队资金转移",ref exclude)){plugin.Config.ExcludeTransfers=exclude;plugin.SaveConfig();}
        var experimental=plugin.Config.EnableExperimentalClassification;
        if(ImGui.Checkbox("启用实验性用途识别",ref experimental)){plugin.Config.EnableExperimentalClassification=experimental;plugin.SaveConfig();}
        ImGui.TextColored(Muted,"国服用途规则尚未实测，默认保留未分类。开启后，推断依据会显示在交易详情。");
        var scale=plugin.Config.Scale;ImGui.SetNextItemWidth(300);if(ImGui.SliderFloat("界面缩放",ref scale,.8f,1.6f,"%.2f")){plugin.Config.Scale=scale;plugin.SaveConfig();}
        ImGui.Spacing();ImGui.Separator();ImGui.TextUnformatted("数据只保存在本机，按角色隔离");ImGui.TextWrapped(plugin.Store.DirectoryPath);
        if(action is {IsCompleted:true}){try{actionResult=action.GetAwaiter().GetResult();}catch(Exception e){actionResult="操作失败："+e.Message;}action=null;}
        ImGui.BeginDisabled(action!=null);
        if(ImGui.Button("导出当前角色 CSV") && plugin.Current!=null)action=plugin.Store.ExportAsync(plugin.Current.CharacterId);
        ImGui.SameLine();if(ImGui.Button("备份全部账本"))action=plugin.Store.BackupAsync();
        ImGui.InputTextWithHint("##backup","恢复文件的完整 .sqlite 路径",ref restorePath,1024);
        ImGui.Checkbox("我确认用备份替换本地全部账本（恢复前自动备份）",ref confirmRestore);
        ImGui.BeginDisabled(!confirmRestore || !File.Exists(restorePath));
        if(ImGui.Button("恢复备份")){plugin.Restore(restorePath);confirmRestore=false;actionResult="正在恢复，完成后将重新核对余额。";}
        ImGui.EndDisabled();ImGui.EndDisabled();
        if(actionResult.Length>0)ImGui.TextWrapped(actionResult);
        ImGui.Spacing();ImGui.TextWrapped("首次开启仅建立余额基线。停用、退出游戏期间的交易无法还原，余额差异单独留档，不计入收支统计。未领取的雇员销售收入不属于随身金币。");
        ImGui.TextColored(Muted,"金币账本 0.1.0 · 国服 API 15 · /jizhang");
    }
    private static void Empty(string text) {ImGui.Spacing();ImGui.TextColored(Muted,text);ImGui.Spacing();}
}
