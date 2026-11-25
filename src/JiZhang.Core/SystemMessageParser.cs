using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
namespace JiZhang.Core;
public static partial class SystemMessageParser
{
    [GeneratedRegex(@"(?<amount>[0-9]+(?:[,，][0-9]{3})*)\s*(?:枚)?金币")]
    private static partial Regex GilPattern();
    [GeneratedRegex("“[^”]*”|「[^」]*」")]
    private static partial Regex QuotedName();
    public static Evidence? Parse(ulong characterId, DateTimeOffset time, string text, string id) {
        var detail = text.Normalize(NormalizationForm.FormKC);
        text = QuotedName().Replace(detail, "").Trim();
        var matches = GilPattern().Matches(text);
        if (matches.Count != 1 || text.Contains("出售了") && text.Contains("雇员") ||
            text.StartsWith("在市场以", StringComparison.Ordinal) && text.Contains("成功售出") ||
            text.StartsWith("通过服装模特售出", StringComparison.Ordinal)) return null;
        if (!long.TryParse(matches[0].Groups["amount"].Value.Replace(",", "").Replace("，", ""), NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount <= 0) return null;
        var npcPrice = text.StartsWith("以", StringComparison.Ordinal) && text.Contains("金币的价格", StringComparison.Ordinal);
        var income = text.Contains("获得") || text.Contains("收到") || text.Contains("取出") || npcPrice && text.Contains("卖出");
        var expense = text.Contains("支付") || text.Contains("花费") || text.Contains("消耗") || text.Contains("存入") ||
            npcPrice && (text.Contains("买入") || text.Contains("回购")) ||
            text.StartsWith("修理费:", StringComparison.Ordinal) || text.StartsWith("交给了对方", StringComparison.Ordinal);
        if (income == expense) return null;
        var completion = CompletionCategory(text);
        Category c;
        if (text.Contains("部队") && (text.Contains("取出") || text.Contains("存入"))) c = Category.FreeCompanyTransfer;
        else if (text.Contains("雇员") && (text.Contains("取出") || text.Contains("存入"))) c = Category.RetainerTransfer;
        else if (text.Contains("传送") && expense) c = Category.Teleport;
        else if (text.Contains("修理") && (expense || text.Contains("修理委托的报酬"))) c = Category.Repair;
        else if (text.Contains("交易") || text.StartsWith("交给了对方", StringComparison.Ordinal) ||
            text.StartsWith("从对方那里收到了", StringComparison.Ordinal)) c = Category.PlayerTrade;
        else if (income && (text.Contains("随机任务") || text.Contains("初次完成该任务") || text.Contains("副本"))) c = Category.DutyReward;
        else if (income && completion is {} reward) c = reward;
        else if (text.Contains("任务") && income) c = Category.QuestReward;
        else if ((text.Contains("出售") || npcPrice && text.Contains("卖出")) && income) c = Category.NpcSale;
        else if ((text.Contains("市场") || text.Contains("服装模特")) && expense && text.Contains("购买")) c = Category.MarketPurchase;
        else if ((text.Contains("购买") || text.Contains("买入") || npcPrice && text.Contains("回购")) && expense) c = Category.NpcPurchase;
        else if (income) c = Category.SystemReward;
        else return null;
        return new(id, characterId, time, income ? amount : -amount, c, detail, "系统消息与余额核对");
    }
    internal static Category? CompletionCategory(string text) {
        if (text.Contains("挑战笔记") && text.Contains("成功完成")) return Category.ChallengeReward;
        if (text.Contains("完成了任务") || text.Contains("任务完成") ||
            text.StartsWith("理符任务", StringComparison.Ordinal) && text.TrimEnd().EndsWith("完成。", StringComparison.Ordinal))
            return Category.QuestReward;
        return null;
    }
}


// Correlates separate server notifications without generating wallet transactions.
public sealed class SystemMessageCorrelator
{
    private (ulong Character,DateTimeOffset Time,Category Category,string Text)? context;
    public void Reset()=>context=null;
    public Evidence? Process(ulong character,DateTimeOffset time,string text,string id) {
        if(context is {} old&&(old.Character!=character||time-old.Time>TimeSpan.FromSeconds(4)||time<old.Time))Reset();
        var parsed=SystemMessageParser.Parse(character,time,text,id);
        if(parsed!=null) {
            var previous=context;Reset();
            // Only the immediately following plain gil reward can inherit a completion description.
            if(parsed.Category==Category.SystemReward&&parsed.Amount>0&&previous is {} c&&
                text.TrimStart().StartsWith("获得",StringComparison.Ordinal))
                return parsed with {Category=c.Category,Detail=c.Text+"\n"+parsed.Detail,Source="系统完成提示 / 金币奖励 / 余额核对"};
            return parsed;
        }
        if(text.Contains("雇员")||text.Contains("金币")){Reset();return null;}
        var category=SystemMessageParser.CompletionCategory(text);
        if(category!=null)context=(character,time,category.Value,text);
        // A challenge can also award experience before its gil line. Keep that bounded context only.
        else if(!(text.TrimStart().StartsWith("获得",StringComparison.Ordinal)&&text.Contains("经验")))Reset();
        return null;
    }
}
