using System.Globalization;
using System.Text.RegularExpressions;

namespace JiZhang.Core;

public static partial class UnclassifiedExpense
{
    public static string Code(uint messageId) => FormattableString.Invariant($"GIL-{messageId}");

    // Inspect the template before substitution so names, quantities and other numeric parameters cannot become the gil amount.
    [GeneratedRegex(@"<(?:(?:kilo|num)\(lnum(?<parameter>[1-9][0-9]*)(?:,[^<>\r\n]*)?\))>\s*(?:枚)?金币")]
    private static partial Regex AmountParameter();

    [GeneratedRegex(@"支付了|花费了|消耗了|扣除了|扣取了|缴纳了|交纳了|金币的价格(?:买入了|回购了|购买了)|成功将金币附加到了邮件中")]
    private static partial Regex CompletedPayment();

    [GeneratedRegex(@"无法|不能|不足|失败|取消|需要|需支付|需花费|请支付|是否|预计|将支付|将花费|最多|返还|退还|获得|收到|[?？]")]
    private static partial Regex NonPayment();

    [GeneratedRegex(@"<[^<>]*>")]
    private static partial Regex Macro();

    [GeneratedRegex(@"^(?:你|您)?(?:已|共|一共|总共|共计|总计)?(?:支付|花费|消耗|扣除|扣取|缴纳|交纳)了?\s*(?:枚)?金币[。！!.\s]*$")]
    private static partial Regex GenericPayment();

    public static int? GetAmountParameter(string template) {
        var matches = AmountParameter().Matches(template);
        if (matches.Count != 1) return null;

        // Strip simple markup and argument macros. Remaining nested expressions are ambiguous.
        var text = Macro().Replace(template, "").Trim();
        if (text.IndexOfAny(['<', '>']) >= 0 || NonPayment().IsMatch(text) ||
            !CompletedPayment().IsMatch(text) || GenericPayment().IsMatch(text)) return null;

        return int.TryParse(matches[0].Groups["parameter"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            ? index - 1
            : null;
    }
}
