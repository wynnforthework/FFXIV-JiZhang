using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
namespace JiZhang.Core;
public static partial class SystemMessageParser
{
    [GeneratedRegex(@"(?<amount>[0-9][0-9,，]*)\s*(?:枚)?金币")]
    private static partial Regex GilPattern();
    public static Evidence? Parse(ulong characterId, DateTimeOffset time, string text, string id) {
        text=text.Normalize(NormalizationForm.FormKC);
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
        else if (text.Contains("挑战笔记") && income) c = Category.ChallengeReward;
        else if (text.Contains("任务") && income) c = Category.QuestReward;
        else if (text.Contains("出售") && income) c = Category.NpcSale;
        else if ((text.Contains("购买") || text.Contains("买入")) && expense) c = Category.NpcPurchase;
        else if (income) c = Category.SystemReward;
        else return null;
        return new(id, characterId, time, income ? amount : -amount, c, text, "系统消息与余额匹配（国服规则待实测）");
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
        Category? category=text.Contains("挑战笔记")&&text.Contains("成功完成")?Category.ChallengeReward:
            text.Contains("任务")&&(text.Contains("完成了任务")||text.Contains("任务完成"))?Category.QuestReward:null;
        if(category!=null)context=(character,time,category.Value,text);
        // A challenge can also award experience before its gil line. Keep that bounded context only.
        else if(!(text.TrimStart().StartsWith("获得",StringComparison.Ordinal)&&text.Contains("经验")))Reset();
        return null;
    }
}
