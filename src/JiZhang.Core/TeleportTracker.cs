namespace JiZhang.Core;

// Accepted requests are evidence only. Completion + an exact wallet debit are both required.
public sealed class TeleportTracker
{
    public static readonly TimeSpan RequestLifetime=TimeSpan.FromSeconds(90);
    public DateTimeOffset? Deadline=>request?.Time+RequestLifetime;
    private sealed record Request(ulong Character,DateTimeOffset Time,uint Territory,long Cost,string Destination,Location Departure);
    private Request? request;
    private BalanceSample? debit;
    public bool Pending=>request!=null;
    public void Reset(){request=null;debit=null;}
    public void Expire(DateTimeOffset now){if(request is {} r&&now-r.Time>RequestLifetime)Reset();}
    public void Accept(ulong character,DateTimeOffset time,uint territory,long cost,string destination,Location departure) {
        Reset();if(character!=0&&territory!=0&&cost>0)request=new(character,time,territory,cost,destination,departure);
    }
    public Location? Departure=>request?.Departure;
    public Evidence? Observe(BalanceSample sample,long delta) {
        if(request is not {} r)return null;
        if(sample.CharacterId!=r.Character||sample.Time-r.Time>RequestLifetime||sample.Time<r.Time){Reset();return null;}
        if(delta!=0) {
            if(delta!=-r.Cost||debit!=null){Reset();return null;}
            debit=sample;
        }
        if(debit is {} d) {
            // A same-area request has no independently observable arrival, so remain unclassified.
            if(sample.Time>r.Time+RequestLifetime){Reset();return null;}
            if(sample.Location.TerritoryId==r.Territory&&r.Departure.TerritoryId!=r.Territory) {
                Reset();return new(Guid.NewGuid().ToString("N"),r.Character,d.Time,-r.Cost,Category.Teleport,
                    "已接受传送请求、目的地区域与实际随身扣款三项核对一致。","传送请求 / 到达 / 余额核对（待国服验收）",Destination:r.Destination);
            }
        }
        return null;
    }
}
