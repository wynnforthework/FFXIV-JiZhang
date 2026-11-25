using JiZhang.Core;
using Xunit;

namespace JiZhang.Tests;

public sealed class UnclassifiedExpenseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Location Here = new(1, "区域一");

    [Theory]
    [InlineData(@"成功将<kilo(lnum1,\,)>金币附加到了邮件中。", 0)]
    [InlineData(@"为<sheet(Item,lnum1,0)>缴纳了<kilo(lnum2,\,)>金币。", 1)]
    [InlineData(@"消耗了<kilo(lnum1,\,)>金币。", null)]
    [InlineData(@"金币不足，需要支付<num(lnum1)>金币。", null)]
    [InlineData(@"获得了<num(lnum1)>金币作为手续费。", null)]
    [InlineData(@"是否支付<num(lnum1)>金币办理手续？", null)]
    [InlineData(@"缴纳了<num(lnum1)>金币，退还<num(lnum2)>金币。", null)]
    public void RequiresDistinctiveCompletedDebitWithOneExplicitGilParameter(string template, int? parameter) =>
        Assert.Equal(parameter, UnclassifiedExpense.GetAmountParameter(template));

    [Fact]
    public void SameMessageGroupsDifferentCharactersPlacesAndAmounts() {
        var first = Record(1, Here, 100, 673);
        var second = Record(2, new(2, "区域二"), 130, 673);
        var third = Record(2, Here, 80, 8000);
        Assert.Equal("GIL-673", first.ExpenseCode);
        Assert.Equal(first.ExpenseCode, second.ExpenseCode);
        Assert.NotEqual(first.ExpenseCode, third.ExpenseCode);
        var report = LedgerReport.Create([first, second, third], Now.LocalDateTime.Date, ReportPeriod.All, false);
        Assert.Equal(new[] { ("GIL-673", 230L), ("GIL-8000", 80L) }, report.Spending.Select(g => (g.Label, g.Amount)));
    }

    [Theory]
    [InlineData(-99, false)]
    [InlineData(-100, true)]
    public void MismatchesAndConflictingCodesStayUnclassified(long evidenceAmount, bool conflict) {
        var engine = new LedgerEngine();
        var start = new BalanceSample(1, "角色", Now, 1000, Here);
        engine.Observe(start);
        engine.AddEvidence(new("one", 1, Now, evidenceAmount, Category.Unknown, "", "", ExpenseMessageId: 673));
        if (conflict) engine.AddEvidence(new("two", 1, Now, -100, Category.Unknown, "", "", ExpenseMessageId: 8000));
        Assert.Empty(engine.Flush(Now.AddMilliseconds(100)).SelectMany(c => c.Entries));
        engine.Observe(start with { Balance = 900 });
        var result = Assert.Single(engine.Flush(Now.AddSeconds(3)).SelectMany(c => c.Entries));
        Assert.Equal(Category.Unknown, result.Category);
        Assert.Null(result.ExpenseCode);
    }

    [Fact]
    public void RepeatedMessagesForOneDebitProduceOneCode() {
        var engine = new LedgerEngine();
        var start = new BalanceSample(1, "角色", Now, 1000, Here);
        engine.Observe(start);
        engine.AddEvidence(new("first", 1, Now, -100, Category.Unknown, "", "", ExpenseMessageId: 673));
        engine.AddEvidence(new("repeat", 1, Now, -100, Category.Unknown, "", "", ExpenseMessageId: 673));
        engine.Observe(start with { Balance = 900 });
        Assert.Equal("GIL-673", Assert.Single(engine.Flush(Now.AddSeconds(3)).SelectMany(c => c.Entries)).ExpenseCode);
    }

    [Fact]
    public void OneMessageCannotLabelTwoEqualDebits() {
        var engine = new LedgerEngine();
        var start = new BalanceSample(1, "角色", Now, 1000, Here);
        engine.Observe(start);
        engine.Observe(start with { Balance = 900 });
        engine.AddEvidence(new("system", 1, Now, -100, Category.Unknown, "", "", ExpenseMessageId: 673));
        engine.Observe(start with { Time = Now.AddMilliseconds(100), Balance = 800 });
        var entries = engine.Flush(Now.AddSeconds(3)).SelectMany(c => c.Entries).ToArray();
        Assert.Equal(2, entries.Length);
        Assert.All(entries, e => Assert.Null(e.ExpenseCode));
    }

    [Fact]
    public void KnownClassificationTakesPrecedenceOverProvisionalCode() {
        var engine = new LedgerEngine();
        var start = new BalanceSample(1, "角色", Now, 1000, Here);
        engine.Observe(start);
        engine.AddEvidence(new("system", 1, Now, -100, Category.Unknown, "", "", ExpenseMessageId: 673));
        engine.AddEvidence(new("confirmed", 1, Now, -100, Category.Repair, "", ""));
        engine.Observe(start with { Balance = 900 });
        var entry = Assert.Single(engine.Flush(Now.AddSeconds(3)).SelectMany(c => c.Entries));
        Assert.Equal(Category.Repair, entry.Category);
        Assert.Null(entry.ExpenseCode);
    }

    [Fact]
    public async Task CodeSurvivesStorageAndAppearsInCsv() {
        var root = Path.Combine(Path.GetTempPath(), "JiZhang-tests-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "ledger.sqlite");
        try {
            var entry = Record(1, Here, 100, 673);
            using (var store = new LedgerStore(path))
                await store.SaveAsync(new(new(1, "角色", Now, 900, Here), [entry]));
            using var reopened = new LedgerStore(path);
            Assert.Equal("GIL-673", Assert.Single(await reopened.LoadEntriesAsync(1)).ExpenseCode);
            var csv = await File.ReadAllLinesAsync(await reopened.ExportAsync(1));
            Assert.EndsWith(",待审核标识", csv[0]);
            Assert.EndsWith(",\"GIL-673\"", csv[1]);
        } finally { Directory.Delete(root, true); }
    }

    private static LedgerEntry Record(ulong character, Location place, long amount, uint id) {
        var start = new BalanceSample(character, "角色", Now, 1000, place);
        var engine = new LedgerEngine();
        engine.Observe(start);
        engine.AddEvidence(new("system", character, Now, -amount, Category.Unknown, "扣款", "系统消息", ExpenseMessageId: id));
        engine.Observe(start with { Balance = start.Balance - amount });
        return Assert.Single(engine.Flush(Now.AddSeconds(3)).SelectMany(c => c.Entries));
    }
}
