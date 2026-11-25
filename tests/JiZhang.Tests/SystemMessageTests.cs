using JiZhang.Core;
using Xunit;
namespace JiZhang.Tests;
public class SystemMessageTests
{
    private readonly DateTimeOffset now=DateTimeOffset.UtcNow;
    private const string Challenge="成功完成了挑战笔记中的“搭档讨伐数1”挑战！";
    [Theory]
    [InlineData("以1,250金币的价格买入了“修理工具”×2。", -1250, Category.NpcPurchase)]
    [InlineData("以320金币的价格回购了“测试物品”×1。", -320, Category.NpcPurchase)]
    [InlineData("以320金币的价格卖出了“测试物品”×1。", 320, Category.NpcSale)]
    [InlineData("修理费：230金币。", -230, Category.Repair)]
    [InlineData("交给了对方1,000金币。", -1000, Category.PlayerTrade)]
    [InlineData("从对方那里收到了1,000金币。", 1000, Category.PlayerTrade)]
    [InlineData("通过服装模特购买了多件装备，共计花费8,000金币。", -8000, Category.MarketPurchase)]
    [InlineData("队伍中有初次完成该任务的队员。\n获得了额外奖励的1,000点经验值及500金币。", 500, Category.DutyReward)]
    [InlineData("获得了额外的1,500金币作为随机任务的奖励。", 1500, Category.DutyReward)]
    public void ParsesCnClientMessages(string text, long amount, Category category) {
        var evidence = Assert.IsType<Evidence>(SystemMessageParser.Parse(1, now, text, "gil"));
        Assert.Equal(amount, evidence.Amount);
        Assert.Equal(category, evidence.Category);
    }
    [Theory]
    [InlineData("完成了任务“测试任务”！")]
    [InlineData("理符任务“测试任务”完成。")]
    public void QuestCompletionLabelsTheFollowingGilMessage(string completion) {
        var parser = new SystemMessageCorrelator();
        Assert.Null(parser.Process(1, now, completion, "quest"));
        var evidence = Assert.IsType<Evidence>(parser.Process(1, now.AddMilliseconds(200), "获得了400金币。", "gil"));
        Assert.Equal(Category.QuestReward, evidence.Category);
        Assert.Equal(400, evidence.Amount);
    }
    [Theory]
    [InlineData("无法在市场中购买物品，金币不足。")]
    [InlineData("从市场购买了“100金币兑换券”×1。")]
    [InlineData("交易完成。")]
    [InlineData("在市场以1,000的价格出售的“测试物品”成功售出，获得了950金币。")]
    [InlineData("通过服装模特售出多件装备，共计获得900金币。")]
    public void NotificationsWithoutWalletAmountDoNotBecomeAmountEvidence(string text) =>
        Assert.Null(SystemMessageParser.Parse(1, now, text, "no-amount"));
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
