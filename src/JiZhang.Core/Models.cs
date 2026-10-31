namespace JiZhang.Core;

public enum Category { Unknown, Teleport, NpcPurchase, NpcSale, MarketPurchase, Repair, QuestReward, SystemReward, PlayerTrade, RetainerTransfer, FreeCompanyTransfer, Gap, ChallengeReward }
public static class Categories
{
    public static string Label(this Category c) => c switch {
        Category.Teleport => "传送", Category.NpcPurchase => "NPC 购物", Category.NpcSale => "NPC 售卖",
        Category.MarketPurchase => "市场购买", Category.Repair => "修理", Category.QuestReward => "任务奖励",
        Category.SystemReward => "系统奖励", Category.ChallengeReward => "挑战笔记奖励", Category.PlayerTrade => "玩家交易", Category.RetainerTransfer => "雇员转账",
        Category.FreeCompanyTransfer => "部队转账", Category.Gap => "记录中断期间差额", _ => "未分类" };
    public static bool IsTransfer(this Category c) => c is Category.RetainerTransfer or Category.FreeCompanyTransfer;
}
public sealed record Location(uint TerritoryId, string Name, float? X = null, float? Y = null);
public sealed record BalanceSample(ulong CharacterId, string CharacterName, DateTimeOffset Time, long Balance, Location Location);
public sealed record Evidence(string Id, ulong CharacterId, DateTimeOffset Time, long Amount, Category Category,
    string Detail, string Source, uint? ItemId = null, string? ItemName = null, uint? Quantity = null, string? Destination = null);
public sealed record LedgerEntry(string Id, ulong CharacterId, string CharacterName, DateTimeOffset Time,
    long Before, long After, Location Location, Category Category = Category.Unknown,
    string Detail = "", string Source = "余额核对", uint? ItemId = null, string? ItemName = null,
    uint? Quantity = null, string? Destination = null)
{
    public long Amount => After - Before;
    public bool IsGap => Category == Category.Gap;
}
public sealed record Commit(BalanceSample State, IReadOnlyList<LedgerEntry> Entries);
public sealed record Totals(long Income, long Expense)
{
    public long Net => Income - Expense;
    public static Totals From(IEnumerable<LedgerEntry> entries, bool excludeTransfers) {
        var list = entries.Where(x => !x.IsGap && (!excludeTransfers || !x.Category.IsTransfer())).ToList();
        return new(list.Where(x => x.Amount > 0).Sum(x => x.Amount), -list.Where(x => x.Amount < 0).Sum(x => x.Amount));
    }
}
