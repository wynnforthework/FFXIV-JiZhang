using Dalamud.Game.Chat;
using Dalamud.Game.DutyState;
using FFXIVClientStructs.FFXIV.Client.Game;
using JiZhang.Core;
using Lumina.Excel.Sheets;

namespace JiZhang;

public sealed partial class Plugin
{
    private (ulong Character, DateTimeOffset Time, long Amount)? tradeGil;
    private readonly Dictionary<uint, (int Parameter, string Detail)?> expenseTemplates = [];

    private void ResetGilEvidence() {
        tradeGil = null;
        systemMessages.Reset();
        lock (marketLock) request = null;
        while (incoming.TryDequeue(out _)) { }
    }

    private unsafe void SampleTrade(InventoryManager* inventory, BalanceSample sample) {
        if (tradeGil is { } old && (old.Character != sample.CharacterId || sample.Time - old.Time > LedgerEngine.CorrelationWindow))
            tradeGil = null;
        if (inventory->TradePartnerEntityId == 0 ||
            inventory->TradeLocalState is < TradeState.LockedIn or > TradeState.Confirmed ||
            inventory->TradeRemoteState is < TradeState.LockedIn or > TradeState.Confirmed) return;

        // The final slot in each trade offer holds gil.
        var offered = inventory->TradeItemsLocal[^1].Quantity;
        var received = inventory->TradeItemsRemote[^1].Quantity;
        tradeGil = (sample.CharacterId, sample.Time, (long)received - offered);
    }

    private void MatchMarketPurchase(ulong character, DateTimeOffset now, uint item, uint quantity) {
        var r = request;
        if (r == null || r.Character != character || r.Item != item % 1000000 || r.Quantity != quantity ||
            now - r.Time > TimeSpan.FromMinutes(2)) return;
        request = null;
        QueueGil(character, now, -r.Cost, Category.MarketPurchase, "市场购买确认（含税）", "市场购买事件与余额核对", r.Item, r.Quantity);
    }

    private void OnLogMessage(ILogMessage message) {
        var character = activeCharacter;
        if (character == 0) return;
        var now = DateTimeOffset.UtcNow;
        var id = message.LogMessageId;

        // LogMessage row IDs and integer parameter positions from the CN client.
        switch (id) {
            case 38: // Trade completed, including transactions that give and receive gil.
                if (tradeGil is { } trade && trade.Character == character && now - trade.Time <= LedgerEngine.CorrelationWindow)
                    QueueGil(character, now, trade.Amount, Category.PlayerTrade, "玩家交易完成（金币净额）", "系统交易确认与余额核对");
                tradeGil = null;
                return;
            case 35: // New trade
            case 36: // Cancellation
            case 37: // Offer changed
                tradeGil = null;
                return;
            case 734:
            case 735:
            case 6079:
                if (message.TryGetIntParameter(0, out var marketItem) && marketItem > 0) {
                    var quantity = id == 734 && message.TryGetIntParameter(1, out var count) ? count : 1;
                    if (quantity > 0) {
                        lock (marketLock) MatchMarketPurchase(character, now, (uint)marketItem, (uint)quantity);
                    }
                }
                return;
            case 1602:
                if (message.TryGetIntParameter(0, out var questId) && questId > 0) {
                    var quest = Data.GetExcelSheet<Quest>().GetRowOrDefault((uint)questId);
                    if (quest is { GilReward: > 0 } reward)
                        QueueGil(character, now, reward.GilReward, Category.QuestReward,
                            $"完成任务「{reward.Name.ExtractText()}」", "任务完成事件、任务报酬与余额核对");
                }
                return;
        }

        (Category Category, int Parameter, int Sign) rule = id switch {
            1687 or 1689 or 6043 => (Category.NpcPurchase, 2, -1),
            1688 => (Category.NpcSale, 2, 1),
            1396 => (Category.Repair, 0, -1),
            10106 => (Category.Repair, 0, 1),
            10921 => (Category.PlayerTrade, 0, -1),
            10923 => (Category.PlayerTrade, 0, 1),
            2163 or 2244 or 2246 => (Category.DutyReward, 1, 1),
            6080 => (Category.MarketPurchase, 0, -1),
            _ => (Category.Unknown, 0, 0)
        };
        if (rule.Category == Category.Unknown) {
            CaptureUnclassifiedExpense(message, character, now);
            return;
        }
        if (!message.TryGetIntParameter(rule.Parameter, out var gil) || gil <= 0) return;

        uint? item = null;
        uint? units = null;
        if (rule.Category is Category.NpcPurchase or Category.NpcSale) {
            if (message.TryGetIntParameter(0, out var itemId) && itemId > 0) item = (uint)itemId % 1000000;
            if (message.TryGetIntParameter(1, out var quantity) && quantity > 0) units = (uint)quantity;
        }
        QueueGil(character, now, (long)gil * rule.Sign, rule.Category, rule.Category.Label(), $"系统消息 #{id} 与余额核对", item, units);
    }

    private void CaptureUnclassifiedExpense(ILogMessage message, ulong character, DateTimeOffset now) {
        var id = message.LogMessageId;
        if (!expenseTemplates.TryGetValue(id, out var template)) {
            if (message.GameData.ValueNullable is not { } row) return;
            template = UnclassifiedExpense.GetAmountParameter(row.Text.ToMacroString()) is { } parameter
                ? (parameter, row.Text.ExtractText())
                : null;
            expenseTemplates[id] = template;
        }
        if (template is not { } expense || !message.TryGetIntParameter(expense.Parameter, out var gil) || gil <= 0) return;
        incoming.Enqueue(new(Guid.NewGuid().ToString("N"), character, now, -gil, Category.Unknown, expense.Detail,
            "系统扣款消息与余额核对", ExpenseMessageId: id));
    }

    private void OnDutyCompleted(IDutyStateEventArgs args) {
        var character = activeCharacter;
        if (character == 0 || args.ContentFinderCondition.ValueNullable is not { } duty ||
            !duty.Content.TryGetValue<InstanceContent>(out var instance) || instance.InstanceClearGil == 0) return;
        QueueGil(character, DateTimeOffset.UtcNow, instance.InstanceClearGil, Category.DutyReward,
            $"完成副本「{duty.Name.ExtractText()}」", "副本完成事件、通关报酬与余额核对");
    }

    private void QueueGil(ulong character, DateTimeOffset time, long amount, Category category,
        string detail, string source, uint? item = null, uint? quantity = null) {
        if (amount != 0)
            incoming.Enqueue(new(Guid.NewGuid().ToString("N"), character, time, amount, category, detail, source, item, Quantity: quantity));
    }
}
