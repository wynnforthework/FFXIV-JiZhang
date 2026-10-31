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
    // Squarified treemap: grow each strip only while its worst aspect ratio improves.
    public static TreeCell[] Layout(IReadOnlyList<long> amounts, double width, double height) {
        if(width<=0||height<=0||!double.IsFinite(width)||!double.IsFinite(height)||amounts.Any(x=>x<=0))return [];
        var total=amounts.Sum(v=>(double)v);
        if(total==0)return [];
        var items=amounts.Select((v,i)=>(Index:i,Area:(double)v/total*width*height)).OrderByDescending(x=>x.Area).ToArray();
        var result=new List<TreeCell>();double x=0,y=0,w=width,h=height;
        double Worst(int start,int count,double side) {
            var sum=items.Skip(start).Take(count).Sum(v=>v.Area);
            var max=items[start].Area;var min=items[start+count-1].Area;
            return Math.Max(side*side*max/(sum*sum),sum*sum/(side*side*min));
        }
        for(var first=0;first<items.Length;) {
            var side=Math.Min(w,h);var count=1;
            while(first+count<items.Length&&Worst(first,count+1,side)<=Worst(first,count,side))count++;
            var area=items.Skip(first).Take(count).Sum(v=>v.Area);
            var last=first+count==items.Length;
            if(w>=h) {
                var strip=last?w:area/h;double cy=y;
                for(var i=0;i<count;i++) {
                    var item=items[first+i];var length=i==count-1?y+h-cy:item.Area/strip;
                    result.Add(new(item.Index,x,cy,strip,length));cy+=length;
                }
                x+=strip;w=Math.Max(0,w-strip);
            }else {
                var strip=last?h:area/w;double cx=x;
                for(var i=0;i<count;i++) {
                    var item=items[first+i];var length=i==count-1?x+w-cx:item.Area/strip;
                    result.Add(new(item.Index,cx,y,length,strip));cx+=length;
                }
                y+=strip;h=Math.Max(0,h-strip);
            }
            first+=count;
        }
        return result.ToArray();
    }
}
