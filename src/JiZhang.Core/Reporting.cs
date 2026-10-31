namespace JiZhang.Core;

public enum ReportPeriod { Month, Year, All }
public sealed record ReportBucket(DateTime Start, Totals Total);
public sealed record LedgerReport(ReportBucket[] Buckets, (Category Category,long Amount)[] Spending, Totals Total)
{
    public static LedgerReport Create(IEnumerable<LedgerEntry> entries, DateTime today, ReportPeriod period, bool excludeTransfers) {
        var all=entries.Where(e=>!e.IsGap&&(!excludeTransfers||!e.Category.IsTransfer())&&e.Time.LocalDateTime.Date<=today.Date).ToArray();
        var start=period switch {ReportPeriod.Month=>new DateTime(today.Year,today.Month,1),ReportPeriod.Year=>new DateTime(today.Year,1,1),_=>all.Length==0?new DateTime(today.Year,today.Month,1):new DateTime(all.Min(e=>e.Time.LocalDateTime).Year,all.Min(e=>e.Time.LocalDateTime).Month,1)};
        var selected=all.Where(e=>e.Time.LocalDateTime>=start).ToArray();
        var buckets=new List<ReportBucket>();
        // All-time history switches to years after ten years to bound chart work and keep labels legible.
        var years=period==ReportPeriod.All&&today.Year-start.Year>=10;
        if(years)start=new DateTime(start.Year,1,1);
        for(var cursor=start;cursor<=today.Date;) {
            var end=period==ReportPeriod.Month?cursor.AddDays(1):years?cursor.AddYears(1):cursor.AddMonths(1);
            buckets.Add(new(cursor,Totals.From(selected.Where(e=>e.Time.LocalDateTime>=cursor&&e.Time.LocalDateTime<end),false)));
            cursor=end;
        }
        return new(buckets.ToArray(),selected.Where(e=>e.Amount<0).GroupBy(e=>e.Category).Select(g=>(g.Key,-g.Sum(e=>e.Amount))).OrderByDescending(g=>g.Item2).ToArray(),Totals.From(selected,false));
    }
}

public readonly record struct TreeCell(int Index, double X, double Y, double Width, double Height);
public static class Treemap
{
    // Balanced binary area partition: no hidden minimum area and no unaccounted category.
    public static TreeCell[] Layout(IReadOnlyList<long> amounts, double width, double height) {
        if(width<=0||height<=0||amounts.Any(x=>x<=0))return [];
        var result=new List<TreeCell>();
        void Split(int first,int count,double x,double y,double w,double h) {
            if(count==0)return;
            if(count==1){result.Add(new(first,x,y,w,h));return;}
            var total=amounts.Skip(first).Take(count).Sum(v=>(double)v);double sum=0;var cut=1;
            for(var i=1;i<count;i++){sum+=amounts[first+i-1];cut=i;if(sum>=total/2)break;}
            // Choose the closer split, while keeping both groups nonempty.
            if(cut>1&&Math.Abs(sum-amounts[first+cut-1]-total/2)<Math.Abs(sum-total/2)){sum-=amounts[first+--cut];}
            var ratio=sum/total;
            if(w>=h){Split(first,cut,x,y,w*ratio,h);Split(first+cut,count-cut,x+w*ratio,y,w*(1-ratio),h);}
            else {Split(first,cut,x,y,w,h*ratio);Split(first+cut,count-cut,x,y+h*ratio,w,h*(1-ratio));}
        }
        Split(0,amounts.Count,0,0,width,height);return result.ToArray();
    }
}
