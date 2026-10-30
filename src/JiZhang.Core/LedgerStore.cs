using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
namespace JiZhang.Core;

public sealed class LedgerStore : IDisposable
{
    private const int SchemaVersion = 1;
    private readonly Channel<Action<SqliteConnection>> queue = Channel.CreateUnbounded<Action<SqliteConnection>>(new() { SingleReader = true });
    private readonly Task worker;
    private readonly string path;
    private readonly Dictionary<ulong, List<LedgerEntry>> cache = [];
    private bool disposed;
    public string? LastError { get; private set; }
    public string DirectoryPath => Path.GetDirectoryName(path)!;
    public LedgerStore(string path) {
        this.path = Path.GetFullPath(path);
        Directory.CreateDirectory(DirectoryPath);
        using (var db = Open()) {
            using var version = db.CreateCommand(); version.CommandText = "PRAGMA user_version";
            var oldVersion = Convert.ToInt32(version.ExecuteScalar());
            if (oldVersion > SchemaVersion) throw new InvalidDataException("账本版本高于插件版本，请更新插件。");
            if (oldVersion < SchemaVersion) {
                if (oldVersion != 0 || new FileInfo(this.path).Length > 4096) Backup(db, "before-migration");
                using var tx = db.BeginTransaction();
                using var cmd = db.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS entries(id TEXT PRIMARY KEY,character_id TEXT NOT NULL,time TEXT NOT NULL,json TEXT NOT NULL); CREATE INDEX IF NOT EXISTS ix_entries_character_time ON entries(character_id,time); CREATE TABLE IF NOT EXISTS states(character_id TEXT PRIMARY KEY,json TEXT NOT NULL); PRAGMA user_version=1;";
                cmd.ExecuteNonQuery(); tx.Commit();
            }
        }
        worker = Task.Run(async () => {
            using var db = Open();
            await foreach (var job in queue.Reader.ReadAllAsync()) job(db);
        });
    }
    private SqliteConnection Open() {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()); db.Open();
        using var cmd = db.CreateCommand(); cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000;"; cmd.ExecuteNonQuery(); return db;
    }
    private Task<T> Run<T>(Func<SqliteConnection,T> action, bool fatal = false) {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!queue.Writer.TryWrite(db => {
            try { var result = action(db); completion.SetResult(result); }
            catch (Exception e) { if (fatal) LastError = e.Message; completion.SetException(e); }
        })) completion.SetException(new ObjectDisposedException(nameof(LedgerStore)));
        return completion.Task;
    }
    public Task SaveAsync(Commit commit) => Run(db => {
        if (LastError != null) throw new IOException("先前写入失败，已停止推进账本检查点：" + LastError);
        using var tx = db.BeginTransaction();
        var inserted = new List<LedgerEntry>();
        foreach (var entry in commit.Entries) {
            using var cmd = db.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = "INSERT OR IGNORE INTO entries VALUES($id,$cid,$time,$json)";
            cmd.Parameters.AddWithValue("$id", entry.Id); cmd.Parameters.AddWithValue("$cid", entry.CharacterId.ToString(CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$time", entry.Time.ToUniversalTime().ToString("O")); cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(entry)); if (cmd.ExecuteNonQuery() == 1) inserted.Add(entry);
        }
        using (var cmd = db.CreateCommand()) {
            cmd.Transaction = tx; cmd.CommandText = "INSERT INTO states VALUES($cid,$json) ON CONFLICT(character_id) DO UPDATE SET json=excluded.json";
            cmd.Parameters.AddWithValue("$cid", commit.State.CharacterId.ToString(CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(commit.State)); cmd.ExecuteNonQuery();
        }
        tx.Commit();
        foreach (var entry in inserted) if (cache.TryGetValue(entry.CharacterId, out var list)) list.Insert(0, entry);
        return true;
    }, fatal: true);
    public Task<BalanceSample[]> LoadStatesAsync() => Run(db => {
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT json FROM states";
        using var r = cmd.ExecuteReader(); var result = new List<BalanceSample>();
        while (r.Read()) result.Add(JsonSerializer.Deserialize<BalanceSample>(r.GetString(0))!); return result.ToArray();
    });
    public Task<LedgerEntry[]> LoadEntriesAsync(ulong cid) => Run(db => {
        if (cache.TryGetValue(cid, out var cached)) return cached.ToArray();
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT json FROM entries WHERE character_id=$cid ORDER BY time DESC, rowid DESC";
        cmd.Parameters.AddWithValue("$cid", cid.ToString(CultureInfo.InvariantCulture));
        using var r = cmd.ExecuteReader(); var result = new List<LedgerEntry>();
        while (r.Read()) result.Add(JsonSerializer.Deserialize<LedgerEntry>(r.GetString(0))!); cache[cid] = result; return result.ToArray();
    });
    public Task<string> BackupAsync() => Run(db => Backup(db, "backup"));
    private string Backup(SqliteConnection db, string prefix) {
        var backupPath = Path.Combine(DirectoryPath, $"{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}.sqlite");
        using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath, Pooling = false }.ToString()); target.Open(); db.BackupDatabase(target); return backupPath;
    }
    // Caller suspends capture, drains writes, then re-baselines after restore.
    public Task RestoreAsync(string backupPath) => Run(db => {
        var fullPath = Path.GetFullPath(backupPath);
        if (string.Equals(fullPath, path, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("不能将当前账本作为恢复源。");
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = fullPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()); source.Open();
        using (var check = source.CreateCommand()) {
            check.CommandText = "PRAGMA integrity_check"; if ((string?)check.ExecuteScalar() != "ok") throw new InvalidDataException("备份完整性检查失败。");
            check.CommandText = "PRAGMA user_version"; if (Convert.ToInt32(check.ExecuteScalar()) != SchemaVersion) throw new InvalidDataException("备份版本不兼容。");
            check.CommandText = "SELECT json FROM states UNION ALL SELECT json FROM entries LIMIT 0"; check.ExecuteNonQuery();
        }
        Backup(db, "before-restore"); source.BackupDatabase(db); cache.Clear(); LastError = null; return true;
    });
    public Task<string> ExportAsync(ulong cid) => Run(db => {
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT json FROM entries WHERE character_id=$cid ORDER BY time,rowid"; cmd.Parameters.AddWithValue("$cid", cid.ToString(CultureInfo.InvariantCulture));
        using var reader = cmd.ExecuteReader();
        var exportPath = Path.Combine(DirectoryPath, $"ledger-{cid}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        using var file = new StreamWriter(exportPath, false, new UTF8Encoding(true));
        file.WriteLine("角色,UTC时间,本地时间,地点,X,Z,收支,金额,变化前余额,变化后余额,分类,说明,识别依据,物品,数量,目的地");
        while (reader.Read()) {
            var e = JsonSerializer.Deserialize<LedgerEntry>(reader.GetString(0))!;
            file.WriteLine(string.Join(",", new[] { e.CharacterName, e.Time.ToUniversalTime().ToString("O"), e.Time.ToLocalTime().ToString("O"), e.Location.Name,
                e.Location.X?.ToString(CultureInfo.InvariantCulture), e.Location.Y?.ToString(CultureInfo.InvariantCulture), e.Amount > 0 ? "收入" : "支出", Math.Abs(e.Amount).ToString(CultureInfo.InvariantCulture),
                e.Before.ToString(CultureInfo.InvariantCulture), e.After.ToString(CultureInfo.InvariantCulture), e.Category.Label(), e.Detail, e.Source, e.ItemName, e.Quantity?.ToString(CultureInfo.InvariantCulture), e.Destination }.Select(Csv)));
        }
        return exportPath;
    });
    private static string Csv(string? value) {
        value ??= "";
        // Prevent formula execution in spreadsheet applications, including leading whitespace.
        if (value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    public void Dispose() { if (disposed) return; disposed = true; queue.Writer.TryComplete(); worker.GetAwaiter().GetResult(); }
}
