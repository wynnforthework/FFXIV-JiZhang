using JiZhang.Core;
using Xunit;
namespace JiZhang.Tests;
public class HistoricalClassificationTests
{
    [Fact] public async Task ReclassificationUsesSavedEvidenceBacksUpAndPreservesBalances() {
        var folder=Path.Combine(Path.GetTempPath(),"JiZhang-history-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try {
            using var store=new LedgerStore(Path.Combine(folder,"ledger.sqlite"));
            var time=DateTimeOffset.UtcNow;
            LedgerEntry E(string id,long before,long after,string detail="",string source="余额核对",ulong cid=1,Category category=Category.Unknown)=>new(id,cid,"角色",time,before,after,new(128,"地点"),category,detail,source);
            LedgerEntry[] entries=[E("reward",1000,2000,"成功完成了挑战笔记中的“搭档讨伐数1”挑战！\n获得了1000金币。","系统消息"),
                E("no-evidence",2000,1900),E("wrong-amount",1900,1800,"传送支付了200金币","系统消息"),
                E("untrusted",1800,1700,"传送支付了100金币"),E("classified",1700,1600,"传送支付了100金币","系统消息",category:Category.Repair),
                E("other-character",1000,900,"传送支付了100金币","系统消息",cid:2)];
            var state=new BalanceSample(1,"角色",time,1600,new(128,"地点"));await store.SaveAsync(new(state,entries));
            var before=await store.LoadEntriesAsync(1); // Warm the cache, then verify invalidation.
            var result=await store.ReclassifyAsync(1);Assert.Equal(4,result.Examined);Assert.Equal(1,result.Updated);Assert.Equal(3,result.Unchanged);Assert.True(File.Exists(result.BackupPath));
            var after=await store.LoadEntriesAsync(1);Assert.Equal(before.Length,after.Length);
            Assert.Equal(Category.ChallengeReward,after.Single(e=>e.Id=="reward").Category);
            foreach(var entry in before) {
                var updated=after.Single(e=>e.Id==entry.Id);Assert.Equal(entry.Amount,updated.Amount);Assert.Equal(entry.Before,updated.Before);Assert.Equal(entry.After,updated.After);Assert.Equal(entry.Time,updated.Time);Assert.Equal(entry.Location,updated.Location);
            }
            Assert.Equal(Category.Repair,after.Single(e=>e.Id=="classified").Category);Assert.Equal(Category.Unknown,Assert.Single(await store.LoadEntriesAsync(2)).Category);
            Assert.Equal(state,Assert.Single(await store.LoadStatesAsync()));
            var repeated=await store.ReclassifyAsync(1);Assert.Equal(0,repeated.Updated);Assert.Null(repeated.BackupPath);
            await store.RestoreAsync(result.BackupPath!);Assert.Equal(Category.Unknown,(await store.LoadEntriesAsync(1)).Single(e=>e.Id=="reward").Category);
        }finally{Directory.Delete(folder,true);}
    }
    [Fact] public async Task FailedClassificationTransactionLeavesCacheAndDataUnchanged() {
        var folder=Path.Combine(Path.GetTempPath(),"JiZhang-history-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try {
            var path=Path.Combine(folder,"ledger.sqlite");using var store=new LedgerStore(path);
            var time=DateTimeOffset.UtcNow;var e=new LedgerEntry("a",1,"角色",time,1000,900,new(1,"地点"),Detail:"传送支付了100金币",Source:"系统消息");
            await store.SaveAsync(new(new(1,"角色",time,900,new(1,"地点")),[e]));await store.LoadEntriesAsync(1);
            using(var db=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False")){db.Open();using var cmd=db.CreateCommand();cmd.CommandText="CREATE TRIGGER reject_update BEFORE UPDATE ON entries BEGIN SELECT RAISE(ABORT,'test failure'); END;";cmd.ExecuteNonQuery();}
            await Assert.ThrowsAnyAsync<Exception>(()=>store.ReclassifyAsync(1));Assert.Equal(e,Assert.Single(await store.LoadEntriesAsync(1)));
        }finally{Directory.Delete(folder,true);}
    }
}
