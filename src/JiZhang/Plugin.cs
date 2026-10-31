using System.Collections.Concurrent;
using Dalamud.Game.Command;
using Dalamud.Game.Chat;
using Dalamud.Game.Network.Structures;
using Dalamud.Game.Text;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using JiZhang.Core;
using Lumina.Excel.Sheets;

namespace JiZhang;
public sealed partial class Plugin : IDalamudPlugin, ILedgerUiContext
{
    [PluginService] internal static IDalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IMarketBoard Market { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    internal Configuration Config { get; }
    internal LedgerStore Store { get; }
    internal LedgerEntry[] Entries { get; private set; } = [];
    internal BalanceSample? Current { get; private set; }
    internal string Status { get; private set; } = "正在加载账本…";
    private LedgerEngine? engine;
    private readonly Task<BalanceSample[]> initial;
    private readonly ConcurrentQueue<Evidence> incoming = new();
    private readonly List<Task> saves = [];
    private readonly LedgerWindow window;
    private readonly LedgerFonts fonts;
    private readonly WindowSystem windows = new("JiZhang");
    private Task<LedgerEntry[]>? read;
    private ulong readCharacter;
    private DateTimeOffset nextRead;
    private bool dirty;
    private bool disposed;
    private bool restoring;
    private Task? restoreTask;
    private Task<BalanceSample[]>? restoreStates;
    private ulong activeCharacter;
    private readonly object marketLock = new();
    private MarketRequest? request;
    private sealed record MarketRequest(ulong Character, DateTimeOffset Time, uint Item, uint Quantity, long Cost);
    private string? fatalError;
    public Plugin() {
        Config = Interface.GetPluginConfig() as Configuration ?? new();
        Store = new(Path.Combine(Interface.GetPluginConfigDirectory(), "ledger.sqlite"));
        initial = Store.LoadStatesAsync();
        fonts = new(Interface.UiBuilder, Config);
        window = new(this);
        windows.AddWindow(window);
        Commands.AddHandler("/jizhang", new CommandInfo((_, _) => window.IsOpen = !window.IsOpen) { HelpMessage = "打开金币账本" });
        Interface.UiBuilder.Draw += Draw;
        Interface.UiBuilder.OpenMainUi += Open;
        Interface.UiBuilder.OpenConfigUi += Open;
        Framework.Update += Update;
        Chat.ChatMessage += OnChat;
        Market.PurchaseRequested += OnMarketRequest;
        Market.ItemPurchased += OnMarketPurchase;
        StartTeleportAdapter();
    }
    private void Draw() => windows.Draw();
    private void Open() => window.IsOpen = true;
    internal void SaveConfig() { Interface.SavePluginConfig(Config); fonts.Refresh(); }
    private void Save(IEnumerable<Commit> commits) {
        foreach (var commit in commits) { saves.Add(Store.SaveAsync(commit)); dirty = true; }
    }
    private unsafe void Update(IFramework _) {
        if (disposed || fatalError != null) return;
        try {
            foreach (var task in saves.Where(t => t.IsCompleted).ToArray()) { task.GetAwaiter().GetResult(); saves.Remove(task); }
            if (restoring) {
                if (restoreTask is { IsCompleted: true } && restoreStates == null) { restoreTask.GetAwaiter().GetResult(); restoreStates = Store.LoadStatesAsync(); }
                if (restoreStates is { IsCompleted: true }) { engine = new(restoreStates.GetAwaiter().GetResult()); restoring = false; restoreTask = null; restoreStates = null; Entries = []; dirty = true; Status = "备份已恢复，正在重新核对余额"; }
                return;
            }
            if (engine == null) { if (!initial.IsCompleted) return; engine = new(initial.GetAwaiter().GetResult()); }
            if (Store.LastError != null) { Status = "存储出错，已暂停记录：" + Store.LastError; return; }
            teleport.Expire(DateTimeOffset.UtcNow);
            // Zoning may temporarily unload player state; an in-flight request keeps its baseline.
            if(Client.IsLoggedIn&&teleport.Pending&&(!Player.IsLoaded||Player.ContentId==0))return;
            if (!Client.IsLoggedIn || !Player.IsLoaded || Player.ContentId == 0) {
                Save(engine.Suspend(DateTimeOffset.UtcNow)); Current = null; activeCharacter = 0; teleport.Reset(); Status = "等待角色登录"; return;
            }
            var manager = InventoryManager.Instance();
            if (manager == null) { if(!teleport.Pending)Save(engine.Suspend(DateTimeOffset.UtcNow)); return; }
            var currency = manager->GetInventoryContainer(InventoryType.Currency);
            if (currency == null || !currency->IsLoaded) { if(!teleport.Pending)Save(engine.Suspend(DateTimeOffset.UtcNow)); return; }
            var now = DateTimeOffset.UtcNow;
            var location = ReadLocation();
            var balance = manager->GetGil();
            var old = Current;
            // Loading screens can briefly invalidate inventory snapshots. Treat interruptions as gaps.
            var sample = new BalanceSample(Player.ContentId, Player.CharacterName, now, balance, location);
            if (old != null && old.CharacterId == sample.CharacterId && old.Location.TerritoryId != location.TerritoryId && sample.Balance < old.Balance)
                sample = sample with { Location = old.Location };
            if (activeCharacter != sample.CharacterId) { activeCharacter = sample.CharacterId; Entries = []; dirty = true; }
            // Evidence must arrive before Observe flushes mature debits. Arrival uses the actual area,
            // while the entry stores the request's departure snapshot.
            var delta=old?.CharacterId==sample.CharacterId?sample.Balance-old.Balance:0;
            var departure=teleport.Departure;
            var teleportEvidence=teleport.Observe(sample with {Location=location},delta);
            if(teleportEvidence!=null)engine.AddEvidence(teleportEvidence);
            if((delta<0&&departure!=null&&teleport.Pending)||teleportEvidence!=null)sample=sample with {Location=departure??sample.Location};
            Save(engine.Observe(sample)); Current = sample;
            while (incoming.TryDequeue(out var e)) {
                if (e.ItemId.HasValue) {
                    var item = Data.GetExcelSheet<Item>().GetRowOrDefault(e.ItemId.Value % 1000000);
                    e = e with { ItemName = item?.Name.ToString() ?? $"物品 #{e.ItemId}" };
                }
                engine.AddEvidence(e);
            }
            if (old != null && old.CharacterId == sample.CharacterId && old.Balance != sample.Balance && Config.EnableExperimentalClassification)
                AddContextEvidence(sample, sample.Balance - old.Balance);
            if (read is { IsCompleted: true }) { var loaded = read.GetAwaiter().GetResult(); if (readCharacter == activeCharacter) Entries = loaded; read = null; }
            if (dirty && read == null && now >= nextRead && saves.Count == 0) {
                dirty = false; readCharacter = activeCharacter; read = Store.LoadEntriesAsync(activeCharacter); nextRead = now.AddSeconds(1);
            }
            Status = Config.EnableExperimentalClassification ? "正在自动记录 · 用途识别为实验功能" : "正在自动记录 · 事件金额匹配分类";
        } catch (Exception e) {
            if (restoring) { restoring = false; restoreTask = null; restoreStates = null; Status = "恢复失败，原账本保留：" + e.Message; }
            else { fatalError = e.Message; Status = "记录已暂停，请重新加载插件：" + e.Message; }
            Log.Error(e, "金币账本记录失败");
        }
    }
    private Location ReadLocation() {
        var territory = Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(Client.TerritoryType);
        var name = territory?.PlaceName.Value.Name.ToString();
        var player = Objects.LocalPlayer;
        // Store world coordinates explicitly; do not mislabel them as map coordinates.
        return new(Client.TerritoryType, string.IsNullOrWhiteSpace(name) ? $"区域 #{Client.TerritoryType}" : name, player?.Position.X, player?.Position.Z);
    }
    private unsafe void AddContextEvidence(BalanceSample sample, long amount) {
        var hints = new List<Category>();
        bool Visible(string name) { var addon = GameGui.GetAddonByName(name); return !addon.IsNull && addon.IsVisible; }
        if (Visible("RetainerBank")) hints.Add(Category.RetainerTransfer);
        if (Visible("FreeCompanyChest")) hints.Add(Category.FreeCompanyTransfer);
        if (Visible("Trade")) hints.Add(Category.PlayerTrade);
        if (Visible("Repair") && amount < 0) hints.Add(Category.Repair);
        if (Visible("Shop")) hints.Add(amount < 0 ? Category.NpcPurchase : Category.NpcSale);
        if (hints.Count != 1) return;
        engine!.AddEvidence(new(Guid.NewGuid().ToString("N"), sample.CharacterId, sample.Time, amount, hints[0], "交易发生时存在对应交互窗口；未验证的上下文推断。", "交互上下文（实验性，可能误判）"));
    }
    private void OnChat(IHandleableChatMessage message) {
        var type = (XivChatType)message.LogKind;
        var timestamp = message.Timestamp;
        // Never interpret player-authored chat as financial evidence or retainer sale notices as wallet income.
        if (type is not (XivChatType.SystemMessage or XivChatType.LootNotice or XivChatType.GatheringSystemMessage)) return;
        var cid = activeCharacter; if (cid == 0) return;
        var e = SystemMessageParser.Parse(cid, DateTimeOffset.UtcNow, message.Message.TextValue, $"chat:{cid}:{timestamp}:{(int)type}:{message.Message.TextValue}");
        if (e != null) incoming.Enqueue(e);
    }
    private void OnMarketRequest(IMarketBoardPurchaseHandler p) {
        lock (marketLock) request = new(activeCharacter, DateTimeOffset.UtcNow, p.CatalogId, p.ItemQuantity, checked((long)p.PricePerUnit * p.ItemQuantity + p.TotalTax));
    }
    private void OnMarketPurchase(IMarketBoardPurchase p) {
        lock (marketLock) {
            var r = request; request = null;
            if (r == null || r.Character == 0 || r.Character != activeCharacter || r.Item != p.CatalogId || r.Quantity != p.ItemQuantity || DateTimeOffset.UtcNow - r.Time > TimeSpan.FromMinutes(2)) return;
            incoming.Enqueue(new(Guid.NewGuid().ToString("N"), r.Character, DateTimeOffset.UtcNow, -r.Cost, Category.MarketPurchase,
                "市场购买确认，与含税金额核对。", "市场确认事件（待国服实测）", r.Item, Quantity: r.Quantity));
        }
    }
    internal void Restore(string path) => Framework.RunOnFrameworkThread(() => RestoreCore(path));
    private void RestoreCore(string path) {
        if (restoring || engine == null) return;
        Save(engine.Suspend(DateTimeOffset.UtcNow));
        restoring = true; activeCharacter = 0; Current = null; teleport.Reset();
        while (incoming.TryDequeue(out _)) { }
        lock (marketLock) request = null;
        restoreTask = Store.RestoreAsync(path);
    }
    public IDisposable PushFont(float tier) => fonts.Push(tier);
    bool ILedgerUiContext.ReducedMotion => Interface.UiBuilder.ShouldUseReducedMotion;
    double ILedgerUiContext.UiTime => Environment.TickCount64/1000d;
    Configuration ILedgerUiContext.Config => Config;
    LedgerEntry[] ILedgerUiContext.Entries => Entries;
    BalanceSample? ILedgerUiContext.Current => Current;
    string ILedgerUiContext.Status => Status;
    string ILedgerUiContext.DataDirectory => Store.DirectoryPath;
    void ILedgerUiContext.SaveConfig() => SaveConfig();
    Task<string> ILedgerUiContext.ExportAsync() => Current is { } current ? Store.ExportAsync(current.CharacterId) : Task.FromException<string>(new InvalidOperationException("请先登录角色"));
    Task<string> ILedgerUiContext.BackupAsync() => Store.BackupAsync();
    void ILedgerUiContext.Restore(string path) => Restore(path);
    public void Dispose() {
        disposed = true;
        teleportHook?.Dispose(); teleport.Reset();
        Framework.Update -= Update; Chat.ChatMessage -= OnChat;
        Market.PurchaseRequested -= OnMarketRequest; Market.ItemPurchased -= OnMarketPurchase;
        Interface.UiBuilder.Draw -= Draw; Interface.UiBuilder.OpenMainUi -= Open; Interface.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/jizhang");
        windows.RemoveAllWindows();
        fonts.Dispose();
        if (engine != null && !restoring) Save(engine.Suspend(DateTimeOffset.UtcNow));
        Store.Dispose();
        foreach (var task in saves) { try { task.GetAwaiter().GetResult(); } catch (Exception e) { Log.Error(e, "账本写入失败"); } }
    }
}
