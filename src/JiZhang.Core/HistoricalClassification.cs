using System.Globalization;
using System.Text.Json;
namespace JiZhang.Core;

public sealed record ReclassificationResult(int Examined,int Updated,int Unchanged,string? BackupPath);
public sealed partial class LedgerStore
{
    // Re-run trusted saved evidence only. Never infer a category from amount, location or neighboring rows.
    public Task<ReclassificationResult> ReclassifyAsync(ulong characterId)=>Run<ReclassificationResult>(db=> {
        if(LastError!=null)throw new IOException("账本写入异常，请先恢复正常记录后再重新识别。");
        var unknown=new List<LedgerEntry>();
        using(var query=db.CreateCommand()) {
            query.CommandText="SELECT json FROM entries WHERE character_id=$cid ORDER BY time,rowid";
            query.Parameters.AddWithValue("$cid",characterId.ToString(CultureInfo.InvariantCulture));
            using var reader=query.ExecuteReader();
            while(reader.Read()) {
                var e=JsonSerializer.Deserialize<LedgerEntry>(reader.GetString(0))!;
                if(e.Category==Category.Unknown)unknown.Add(e);
            }
        }
        var changes=new List<LedgerEntry>();
        foreach(var e in unknown) {
            if(e.Amount==0||e.Detail.Length==0||!e.Source.Contains("系统",StringComparison.Ordinal))continue;
            var proof=SystemMessageParser.Parse(e.CharacterId,e.Time,e.Detail,"historical:"+e.Id);
            if(proof==null||proof.Amount!=e.Amount)continue;
            changes.Add(e with {Category=proof.Category,Source=e.Source+" · 按保存的原始证据重新识别"});
        }
        if(changes.Count==0)return new(unknown.Count,0,unknown.Count,null);
        var backup=Backup(db,"before-reclassification");
        using(var tx=db.BeginTransaction()) {
            foreach(var e in changes) {
                using var update=db.CreateCommand();update.Transaction=tx;
                update.CommandText="UPDATE entries SET json=$json WHERE id=$id AND character_id=$cid";
                update.Parameters.AddWithValue("$json",JsonSerializer.Serialize(e));update.Parameters.AddWithValue("$id",e.Id);
                update.Parameters.AddWithValue("$cid",characterId.ToString(CultureInfo.InvariantCulture));
                if(update.ExecuteNonQuery()!=1)throw new InvalidDataException("历史分类更新条目不一致，已取消修改。");
            }
            tx.Commit();
        }
        cache.Remove(characterId);
        return new(unknown.Count,changes.Count,unknown.Count-changes.Count,backup);
    });
}
