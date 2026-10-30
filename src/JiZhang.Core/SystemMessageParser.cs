using System.Globalization;
using System.Text.RegularExpressions;
namespace JiZhang.Core;
public static partial class SystemMessageParser
{
    [GeneratedRegex(@"(?<amount>[0-9][0-9,，]*)\s*(?:枚)?金币")]
    private static partial Regex GilPattern();
    public static Evidence? Parse(ulong characterId, DateTimeOffset time, string text, string id) {
        var matches = GilPattern().Matches(text);
        if (matches.Count != 1 || text.Contains("出售了") && text.Contains("雇员")) return null;
        if (!long.TryParse(matches[0].Groups["amount"].Value.Replace(",", "").Replace("，", ""), NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount <= 0) return null;
        var income = text.Contains("获得") || text.Contains("收到") || text.Contains("取出");
        var expense = text.Contains("支付") || text.Contains("花费") || text.Contains("消耗") || text.Contains("存入");
        if (income == expense) return null;
        Category c;
        if (text.Contains("部队") && (text.Contains("取出") || text.Contains("存入"))) c = Category.FreeCompanyTransfer;
        else if (text.Contains("雇员") && (text.Contains("取出") || text.Contains("存入"))) c = Category.RetainerTransfer;
        else if (text.Contains("传送") && expense) c = Category.Teleport;
        else if (text.Contains("修理") && expense) c = Category.Repair;
        else if (text.Contains("交易")) c = Category.PlayerTrade;
        else if (text.Contains("任务") && income) c = Category.QuestReward;
        else if (text.Contains("出售") && income) c = Category.NpcSale;
        else if ((text.Contains("购买") || text.Contains("买入")) && expense) c = Category.NpcPurchase;
        else if (income) c = Category.SystemReward;
        else return null;
        return new(id, characterId, time, income ? amount : -amount, c, text, "系统消息与余额匹配（国服规则待实测）");
    }
}
