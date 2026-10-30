using JiZhang.Core;
using Xunit;
namespace JiZhang.Tests;
public sealed class LedgerTests
{
    private static readonly DateTimeOffset T = new(2026,10,31,0,0,0,TimeSpan.Zero);
    private static BalanceSample S(long b, int seconds=0, ulong cid=1) => new(cid,"测试角色",T.AddSeconds(seconds),b,new(128,"利姆萨·罗敏萨",1,2));
    private static LedgerEntry[] Entries(IEnumerable<Commit> c) => c.SelectMany(x=>x.Entries).ToArray();
    [Fact] public void BaselineAndUnchangedBalancesCreateNoEntries() {
        var e=new LedgerEngine();Assert.Empty(Entries(e.Observe(S(1000))));Assert.Empty(Entries(e.Observe(S(1000,10))));Assert.Empty(Entries(e.Suspend(T.AddSeconds(20))));
    }
    [Fact] public void ConsecutiveChangesConserveBalanceAndPreserveLocation() {
        var e=new LedgerEngine();e.Observe(S(1000));var commits=new List<Commit>();commits.AddRange(e.Observe(S(900,1)));commits.AddRange(e.Observe(S(1200,2)));commits.AddRange(e.Observe(S(1150,3)));commits.AddRange(e.Flush(T.AddSeconds(10)));
        var entries=Entries(commits);
        Assert.Equal(3,entries.Length);Assert.Equal(1150,1000+entries.Sum(x=>x.Amount));Assert.All(entries,x=>Assert.Equal("利姆萨·罗敏萨",x.Location.Name));
    }
    [Theory][InlineData(-1)][InlineData(1)] public void EvidenceBeforeOrAfterBalanceClassifiesOnlyOnce(int offset) {
        var e=new LedgerEngine();e.Observe(S(1000));var proof=new Evidence("id",1,T.AddSeconds(5+offset),-100,Category.Teleport,"支付 100 金币传送","system");
        if(offset<0)e.AddEvidence(proof);e.Observe(S(900,5));if(offset>0)e.AddEvidence(proof);e.AddEvidence(proof);
        var entry=Assert.Single(Entries(e.Flush(T.AddSeconds(10))));Assert.Equal(Category.Teleport,entry.Category);Assert.Equal(-100,entry.Amount);
    }
    [Fact] public void CancelledPurchaseAndOtherCurrenciesCannotGenerateEntries() {
        var e=new LedgerEngine();e.Observe(S(1000));e.AddEvidence(new("cancelled",1,T,-100,Category.MarketPurchase,"request","market"));
        Assert.Empty(Entries(e.Observe(S(1000,3))));Assert.Empty(Entries(e.Flush(T.AddSeconds(10))));
    }
    [Fact] public void ConflictingAndCombinedEvidenceRemainUnknown() {
        var e=new LedgerEngine();e.Observe(S(1000));e.Observe(S(900,1));
        e.AddEvidence(new("1",1,T.AddSeconds(1),-100,Category.Repair,"","a"));e.AddEvidence(new("2",1,T.AddSeconds(1),-100,Category.Teleport,"","b"));
        Assert.Equal(Category.Unknown,Assert.Single(Entries(e.Flush(T.AddSeconds(5)))).Category);
        e.Observe(S(600,6));e.AddEvidence(new("3",1,T.AddSeconds(6),-100,Category.Teleport,"","a"));e.AddEvidence(new("4",1,T.AddSeconds(6),-200,Category.Repair,"","b"));
        Assert.Equal(Category.Unknown,Assert.Single(Entries(e.Flush(T.AddSeconds(10)))).Category);
    }
    [Fact] public void EqualAdjacentAmountsCannotStealOneEvidence() {
        var e=new LedgerEngine();e.Observe(S(1000));e.Observe(S(900,1));e.Observe(S(800,2));e.AddEvidence(new("1",1,T.AddSeconds(1),-100,Category.Repair,"","a"));
        var entries=Entries(e.Flush(T.AddSeconds(10)));Assert.Equal(2,entries.Length);Assert.All(entries,x=>Assert.Equal(Category.Unknown,x.Category));
    }
    [Fact] public void ReconnectCreatesGapAndCharactersAreIsolated() {
        var e=new LedgerEngine([S(1000),S(2000,cid:2)]);
        var gap=Assert.Single(Entries(e.Observe(S(900,1))));Assert.True(gap.IsGap);
        e.Observe(S(850,2));var commits=e.Observe(S(2100,3,2));
        Assert.Contains(Entries(commits),x=>x.CharacterId==1&&x.Amount==-50&&!x.IsGap);Assert.Contains(Entries(commits),x=>x.CharacterId==2&&x.Amount==100&&x.IsGap);
        Assert.Equal(0,Totals.From([gap],false).Net);
    }
    [Fact] public void RestartDoesNotLoseUnflushedDeltaFromCheckpoint() {
        var e=new LedgerEngine();e.Observe(S(1000));e.Observe(S(900,1));e.Observe(S(850,2));
        var first=Assert.Single(e.Flush(T.AddSeconds(3)));Assert.Equal(900,first.State.Balance);Assert.Single(first.Entries);
        var restarted=new LedgerEngine([first.State]);var gap=Assert.Single(Entries(restarted.Observe(S(850,10))));Assert.Equal(-50,gap.Amount);Assert.True(gap.IsGap);
    }
    [Theory][InlineData("传送支付了 1,234 金币",-1234,Category.Teleport)][InlineData("完成任务获得 500 金币",500,Category.QuestReward)][InlineData("从雇员取出 100 金币",100,Category.RetainerTransfer)][InlineData("向部队存入 200 金币",-200,Category.FreeCompanyTransfer)]
    public void ChineseRulesRequireActualGilAmount(string text,long amount,Category category) {
        var e=SystemMessageParser.Parse(1,T,text,"1");Assert.NotNull(e);Assert.Equal(amount,e.Amount);Assert.Equal(category,e.Category);
    }
    [Theory][InlineData("获得 100 军票")][InlineData("雇员出售了物品，获得 500 金币")][InlineData("金币")][InlineData("支付 100 金币，获得 200 金币")]
    public void NonWalletAndAmbiguousMessagesIgnored(string text)=>Assert.Null(SystemMessageParser.Parse(1,T,text,"1"));
    [Fact] public void TransferFilterExcludesOnlyTransfersAndGaps() {
        var entries=new[]{new LedgerEntry("1",1,"a",T,100,200,new(1,"a"),Category.RetainerTransfer),new LedgerEntry("2",1,"a",T,200,180,new(1,"a"),Category.PlayerTrade)};
        Assert.Equal(80,Totals.From(entries,false).Net);Assert.Equal(-20,Totals.From(entries,true).Net);
    }
    [Fact] public async Task StoreIsAtomicIdempotentAndRestoresBackup() {
        var path=Path.Combine(Path.GetTempPath(),"JiZhang-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
        try {
            using(var store=new LedgerStore(Path.Combine(path,"ledger.sqlite"))) {
                var e=new LedgerEngine();foreach(var c in e.Observe(S(1000)))await store.SaveAsync(c);e.Observe(S(900,1));var commit=Assert.Single(e.Flush(T.AddSeconds(4)));
                await store.SaveAsync(commit);await store.SaveAsync(commit);Assert.Single(await store.LoadEntriesAsync(1));
                var backup=await store.BackupAsync();e.Observe(S(800,5));await store.SaveAsync(Assert.Single(e.Flush(T.AddSeconds(8))));
                Assert.Equal(2,(await store.LoadEntriesAsync(1)).Length);await store.RestoreAsync(backup);Assert.Single(await store.LoadEntriesAsync(1));Assert.Equal(900,Assert.Single(await store.LoadStatesAsync()).Balance);
                Assert.Empty(await store.LoadEntriesAsync(2));
                var csv=await store.ExportAsync(1);Assert.Contains("利姆萨",await File.ReadAllTextAsync(csv));Assert.Contains("支出\",\"100",await File.ReadAllTextAsync(csv));
                await Assert.ThrowsAsync<InvalidDataException>(()=>store.RestoreAsync(Path.Combine(path,"ledger.sqlite")));
            }
            using(var reopened=new LedgerStore(Path.Combine(path,"ledger.sqlite")))Assert.Single(await reopened.LoadEntriesAsync(1));
        } finally {Directory.Delete(path,true);}
    }
    [Fact] public async Task FailedRestoreLeavesLedgerIntactAndExportEscapesFormula() {
        var path=Path.Combine(Path.GetTempPath(),"JiZhang-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
        try {using var store=new LedgerStore(Path.Combine(path,"ledger.sqlite"));
            var sample=S(100);var entry=new LedgerEntry("x",1,"=HYPERLINK(1)",T,200,100,new(1,"test"));await store.SaveAsync(new(sample,[entry]));
            var csv=await File.ReadAllTextAsync(await store.ExportAsync(1));Assert.Contains("'=HYPERLINK(1)",csv);
            var bad=Path.Combine(path,"invalid.sqlite");await File.WriteAllTextAsync(bad,"broken");await Assert.ThrowsAnyAsync<Exception>(()=>store.RestoreAsync(bad));Assert.Single(await store.LoadEntriesAsync(1));
        } finally {Directory.Delete(path,true);}
    }
    [Fact] public async Task FailedWriteCannotAdvanceLaterCheckpoints() {
        var path=Path.Combine(Path.GetTempPath(),"JiZhang-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
        try {using var store=new LedgerStore(Path.Combine(path,"ledger.sqlite"));await store.SaveAsync(new(S(1000),[]));
            // Inject an actual SQLite write failure without requiring filesystem-specific permissions.
            using(var db=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(path,"ledger.sqlite")};Pooling=False")) {
                db.Open();using var cmd=db.CreateCommand();cmd.CommandText="CREATE TRIGGER reject_entries BEFORE INSERT ON entries BEGIN SELECT RAISE(ABORT,'simulated full disk'); END;";cmd.ExecuteNonQuery();
            }
            var entry=new LedgerEntry("failed",1,"角色",T,1000,900,new(1,"test"));
            await Assert.ThrowsAnyAsync<Exception>(()=>store.SaveAsync(new(S(900),[entry])));
            await Assert.ThrowsAsync<IOException>(()=>store.SaveAsync(new(S(800),[])));
            Assert.Equal(1000,Assert.Single(await store.LoadStatesAsync()).Balance);Assert.Empty(await store.LoadEntriesAsync(1));Assert.NotNull(store.LastError);
        } finally {Directory.Delete(path,true);}
    }

}
