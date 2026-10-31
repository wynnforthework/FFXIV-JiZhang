using JiZhang.Core;
using Xunit;
namespace JiZhang.Tests;
public class SystemMessageTests
{
    private readonly DateTimeOffset now=DateTimeOffset.UtcNow;
    private const string Challenge="成功完成了挑战笔记中的“搭档讨伐数1”挑战！";
    [Fact] public void SuppliedChallengeAndRewardWorkAsSeparateMessages() {
        var parser=new SystemMessageCorrelator();Assert.Null(parser.Process(1,now,Challenge,"challenge"));
        var e=parser.Process(1,now.AddMilliseconds(300),"获得了1000金币。","reward");Assert.NotNull(e);
        Assert.Equal(Category.ChallengeReward,e.Category);Assert.Equal(1000,e.Amount);Assert.Contains("搭档讨伐数1",e.Detail);
        Assert.Equal(Category.SystemReward,parser.Process(1,now.AddSeconds(1),"获得了1000金币。","next")!.Category);
    }
    [Fact] public void ExperienceRewardBetweenCompletionAndGilKeepsChallengeContext() {
        var parser=new SystemMessageCorrelator();parser.Process(1,now,Challenge,"c");
        Assert.Null(parser.Process(1,now.AddMilliseconds(100),"获得了2000经验值。","xp"));
        Assert.Equal(Category.ChallengeReward,parser.Process(1,now.AddMilliseconds(200),"获得了1000金币。","gil")!.Category);
    }
    [Fact] public void CombinedMessageAndFullWidthAmountsWork() {
        var e=SystemMessageParser.Parse(1,now,Challenge+"\n获得了1000金币。","a");Assert.Equal(Category.ChallengeReward,e!.Category);
        Assert.Equal(1234,SystemMessageParser.Parse(1,now,"获得了１，２３４金币。","b")!.Amount);
    }
    [Fact] public void StaleOtherCharacterOrInterveningMessageCannotInheritChallenge() {
        foreach(var mode in new[]{"stale","character","intervening"}) {
            var parser=new SystemMessageCorrelator();parser.Process(1,now,Challenge,"c");
            if(mode=="intervening")parser.Process(1,now.AddMilliseconds(100),"其他系统通知","x");
            var e=parser.Process(mode=="character"?2UL:1UL,now.AddSeconds(mode=="stale"?5:1),"获得了1000金币。","r");
            Assert.Equal(Category.SystemReward,e!.Category);
        }
    }
    [Fact] public void RetainerSaleNeverBecomesWalletIncomeAndChallengeAloneCreatesNoEntries() {
        var p=new SystemMessageCorrelator();Assert.Null(p.Process(1,now,"雇员出售了物品，获得了1000金币。","sale"));
        var engine=new LedgerEngine();var sample=new BalanceSample(1,"角色",now,1000,new(1,"地点"));engine.Observe(sample);
        p.Process(1,now,Challenge,"c");var e=p.Process(1,now,"获得了1000金币。","r");engine.AddEvidence(e!);
        Assert.Empty(engine.Observe(sample with {Time=now.AddSeconds(3)}).SelectMany(c=>c.Entries));
    }
    [Fact] public void ChallengeRewardAndRealBalanceProduceOneClassifiedEntry() {
        var engine=new LedgerEngine();var s=new BalanceSample(1,"角色",now,1000,new(1,"地点"));engine.Observe(s);
        var parser=new SystemMessageCorrelator();parser.Process(1,now,Challenge,"c");var proof=parser.Process(1,now.AddSeconds(1),"获得了1000金币。","r")!;
        engine.AddEvidence(proof);engine.AddEvidence(proof);engine.Observe(s with {Balance=2000,Time=now.AddSeconds(1)});
        var e=Assert.Single(engine.Flush(now.AddSeconds(4)).SelectMany(c=>c.Entries));Assert.Equal(Category.ChallengeReward,e.Category);Assert.Equal(1000,e.Amount);
    }
}
