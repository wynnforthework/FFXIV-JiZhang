using Xunit;
using JiZhang.Core;
namespace JiZhang.Tests;
public class ReportingTests
{
    private static LedgerEntry E(DateTime local,long amount,Category category=Category.Unknown)=>new(Guid.NewGuid().ToString(),1,"测试",new DateTimeOffset(local),10000,10000+amount,new(1,"测试"),category);
    [Fact] public void PeriodBoundariesAndTransfers() {
        var date=new DateTime(2026,10,31);LedgerEntry[] entries=[E(new(2026,10,1),-100),E(new(2026,9,30),50),E(new(2025,12,31),30),E(new(2026,10,2),90,Category.RetainerTransfer),E(new(2026,10,3),500,Category.Gap)];
        var month=LedgerReport.Create(entries,date,ReportPeriod.Month,true);Assert.Equal(new Totals(0,100),month.Total);Assert.Equal(31,month.Buckets.Length);
        Assert.Equal(new Totals(50,100),LedgerReport.Create(entries,date,ReportPeriod.Year,true).Total);
        Assert.Equal(new Totals(80,100),LedgerReport.Create(entries,date,ReportPeriod.All,true).Total);
        Assert.Equal(90,LedgerReport.Create(entries,date,ReportPeriod.Month,false).Total.Income);
        Assert.Equal(month.Total.Expense,month.Spending.Sum(g=>g.Amount));
    }
    [Fact] public void EmptyAndSingleBucketAreValid() {
        var empty=LedgerReport.Create([],new(2026,10,1),ReportPeriod.Month,false);Assert.Single(empty.Buckets);Assert.Empty(empty.Spending);
        var all=LedgerReport.Create([E(new(2000,1,1),100)],new(2026,10,31),ReportPeriod.All,false);Assert.Equal(27,all.Buckets.Length);Assert.Equal(100,all.Buckets.Sum(b=>b.Total.Income));
    }
    [Theory][InlineData(300,180)][InlineData(40,200)]
    public void TreemapPreservesAreaAndDoesNotOverlap(double width,double height) {
        long[] values=[500000,10000,80,7,1];var cells=Treemap.Layout(values,width,height);Assert.Equal(values.Length,cells.Length);
        Assert.Equal(width*height,cells.Sum(c=>c.Width*c.Height),6);
        foreach(var c in cells) {
            Assert.InRange(c.X,0,width);Assert.InRange(c.Y,0,height);Assert.True(c.X+c.Width<=width+.0001&&c.Y+c.Height<=height+.0001);
            Assert.Equal((double)values[c.Index]/values.Sum(),c.Width*c.Height/(width*height),8);
            foreach(var d in cells.Where(d=>d.Index!=c.Index))Assert.True(Math.Min(c.X+c.Width,d.X+d.Width)-Math.Max(c.X,d.X)<.0001||Math.Min(c.Y+c.Height,d.Y+d.Height)-Math.Max(c.Y,d.Y)<.0001);
        }
    }
    [Fact] public void EqualCategoriesFormCompactTilesAndRetainInputIdentity() {
        var cells=Treemap.Layout([1,1,1,1],200,200);Assert.Equal(4,cells.Length);
        Assert.All(cells,c=>{Assert.Equal(100,c.Width,6);Assert.Equal(100,c.Height,6);});
        long[] amounts=[1,9,3,7,2];var mixed=Treemap.Layout(amounts,360,220);
        foreach(var cell in mixed)Assert.Equal((double)amounts[cell.Index]/amounts.Sum(),cell.Width*cell.Height/(360*220),8);
        Assert.Empty(Treemap.Layout([],200,200));Assert.Empty(Treemap.Layout([0,1],200,200));
    }
    [Fact] public void SingleCategoryFillsChartAndInvalidDimensionsAreRejected() {
        Assert.Equal(new TreeCell(0,0,0,320,205),Assert.Single(Treemap.Layout([750],320,205)));
        Assert.Empty(Treemap.Layout([1],0,205));
        Assert.Empty(Treemap.Layout([1],320,-1));
        Assert.Empty(Treemap.Layout([1],double.NaN,205));
        Assert.Empty(Treemap.Layout([1],320,double.PositiveInfinity));
        Assert.Empty(Treemap.Layout([-1,2],320,205));
    }
}
