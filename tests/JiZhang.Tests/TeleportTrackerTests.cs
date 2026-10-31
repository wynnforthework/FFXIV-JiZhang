using Xunit;
using JiZhang.Core;
namespace JiZhang.Tests;
public class TeleportTrackerTests
{
    private readonly DateTimeOffset now=DateTimeOffset.UtcNow;
    private BalanceSample Sample(double seconds,long balance=900,uint territory=2,ulong character=1)=>new(character,"测试",now.AddSeconds(seconds),balance,new(territory,"目的地"));
    private TeleportTracker Request(long cost=100,uint destination=2) {var t=new TeleportTracker();t.Accept(1,now,destination,cost,"目的地",new(1,"出发地"));return t;}
    [Fact] public void RequestAndArrivalAloneNeverBook() {var t=Request();Assert.Null(t.Observe(Sample(1,1000),0));Assert.True(t.Pending);}
    [Fact] public void ConfirmedArrivalMatchesDebitTimeAndAmount() {var t=Request();Assert.Null(t.Observe(Sample(4,territory:1),-100));var e=t.Observe(Sample(4.5),0);Assert.NotNull(e);Assert.Equal(-100,e.Amount);Assert.Equal(now.AddSeconds(4),e.Time);Assert.Equal(Category.Teleport,e.Category);Assert.False(t.Pending);Assert.Null(t.Observe(Sample(5),0));}
    [Fact] public void SimultaneousArrivalAndDebitMatches() {Assert.NotNull(Request().Observe(Sample(4),-100));}
    [Fact] public void CancelledExpiredFreeMismatchedOrOtherCharacterStayUnknown() {
        Assert.Null(Request().Observe(Sample(2,territory:1),0));
        Assert.Null(Request().Observe(Sample(91),-100));
        Assert.Null(Request(0).Observe(Sample(2),-100));
        Assert.Null(Request().Observe(Sample(2),-101));
        Assert.Null(Request().Observe(Sample(2,character:2),-100));
        Assert.Null(Request(100,1).Observe(Sample(2,territory:1),-100));
        var late=Request();late.Observe(Sample(2,territory:1),-100);Assert.Null(late.Observe(Sample(95),0));
        var reset=Request();reset.Reset();Assert.Null(reset.Observe(Sample(2),-100));
        var expired=Request();expired.Expire(now.AddSeconds(91));Assert.False(expired.Pending);
    }
    [Fact] public void SlowLoadingKeepsDebitUntilArrivalAndDoesNotAdvanceCheckpointPastIt() {
        var engine=new LedgerEngine();engine.Observe(Sample(0,1000,1));var tracker=Request();
        var debit=Sample(4,900,1);Assert.Null(tracker.Observe(debit,-100));engine.Observe(debit,tracker.Deadline);
        Assert.Empty(engine.Observe(Sample(8,900,1))); // Beyond the ordinary two-second window.
        var proof=tracker.Observe(Sample(24,900,2),0);Assert.NotNull(proof);engine.AddEvidence(proof);
        engine.ReleaseDeferred();var commits=engine.Observe(Sample(24,900,2));
        var entry=Assert.Single(commits.SelectMany(c=>c.Entries));Assert.Equal(Category.Teleport,entry.Category);
        Assert.Equal(now.AddSeconds(4),entry.Time);Assert.Equal(900,commits.Last().State.Balance);
    }
    [Fact] public void DeferredPrefixKeepsLaterChangesAndEvidenceInOrder() {
        var engine=new LedgerEngine();engine.Observe(Sample(0,1000,1));engine.Observe(Sample(4,900,1),now.AddSeconds(90));
        engine.AddEvidence(new("reward",1,now.AddSeconds(5),1000,Category.SystemReward,"","system"));
        engine.Observe(Sample(5,1900,1));Assert.Empty(engine.Flush(now.AddSeconds(30)));
        engine.ReleaseDeferred();var commits=engine.Flush(now.AddSeconds(31));var entries=commits.SelectMany(c=>c.Entries).ToArray();
        Assert.Equal(2,entries.Length);Assert.Equal(Category.Unknown,entries[0].Category);Assert.Equal(Category.SystemReward,entries[1].Category);
        Assert.Equal(1900,1000+entries.Sum(e=>e.Amount));Assert.Equal(1900,commits.Last().State.Balance);
    }
    [Fact] public void TimedOutDebitEventuallyCommitsUnknownAndSuspendFlushesHeldEntries() {
        var engine=new LedgerEngine();engine.Observe(Sample(0,1000,1));engine.Observe(Sample(4,900,1),now.AddSeconds(90));
        Assert.Empty(engine.Flush(now.AddSeconds(89)));Assert.Equal(Category.Unknown,Assert.Single(engine.Flush(now.AddSeconds(91)).SelectMany(c=>c.Entries)).Category);
        engine.Observe(Sample(92,800,1),now.AddSeconds(180));Assert.Single(engine.Suspend(now.AddSeconds(93)).SelectMany(c=>c.Entries));
    }
    [Fact] public void ConflictingDestinationsRemainUnknown() {
        var engine=new LedgerEngine();engine.Observe(Sample(0,1000,1));var sample=Sample(4);
        engine.AddEvidence(new("first",1,sample.Time,-100,Category.Teleport,"","request",Destination:"区域甲"));
        engine.AddEvidence(new("second",1,sample.Time,-100,Category.Teleport,"","request",Destination:"区域乙"));
        engine.Observe(sample);Assert.Equal(Category.Unknown,Assert.Single(engine.Flush(now.AddSeconds(7)).SelectMany(c=>c.Entries)).Category);
    }
    [Fact] public void EvidenceAndWalletProduceOneEntry() {
        var engine=new LedgerEngine();engine.Observe(Sample(0,1000,1));var t=Request();var sample=Sample(4);
        engine.AddEvidence(t.Observe(sample,-100)!);engine.AddEvidence(new("chat",1,sample.Time,-100,Category.Teleport,"传送消耗100金币","系统消息"));engine.Observe(sample);
        var entries=engine.Flush(now.AddSeconds(7)).SelectMany(c=>c.Entries).ToArray();Assert.Single(entries);Assert.Equal(Category.Teleport,entries[0].Category);Assert.Equal(900,entries[0].After);Assert.Equal("目的地",entries[0].Destination);
    }
}
