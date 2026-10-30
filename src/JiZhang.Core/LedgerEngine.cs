namespace JiZhang.Core;

// All calls belong to the framework thread. Only observed balance deltas can create entries.
public sealed class LedgerEngine
{
    public static readonly TimeSpan CorrelationWindow = TimeSpan.FromSeconds(2);
    private readonly Dictionary<ulong, BalanceSample> states;
    private readonly List<LedgerEntry> pending = [];
    private readonly List<Evidence> evidence = [];
    private readonly HashSet<string> evidenceIds = [];
    private ulong? active;
    public LedgerEngine(IEnumerable<BalanceSample>? previous = null) => states = previous?.ToDictionary(x => x.CharacterId) ?? [];
    public IReadOnlyDictionary<ulong, BalanceSample> States => states;
    public void AddEvidence(Evidence e) {
        if (e.CharacterId != active || e.Amount == 0 || !evidenceIds.Add(e.Id)) return;
        evidence.Add(e);
    }
    public IReadOnlyList<Commit> Observe(BalanceSample s) {
        if (s.CharacterId == 0 || s.Balance is < 0 or > 999999999) return [];
        var commits = new List<Commit>();
        if (active != s.CharacterId) {
            commits.AddRange(Flush(s.Time, true));
            active = s.CharacterId; evidence.Clear(); evidenceIds.Clear();
            if (states.TryGetValue(s.CharacterId, out var saved) && saved.Balance != s.Balance)
                pending.Add(New(s, saved.Balance) with { Category = Category.Gap, Detail = "插件未连续记录，无法还原具体交易。", Source = "会话余额核对" });
            states[s.CharacterId] = s;
            // A new baseline must be persisted even when there is no transaction.
            commits.AddRange(Flush(s.Time, true));
            commits.Add(new(s, []));
            return commits;
        }
        var before = states[s.CharacterId];
        if (before.Balance != s.Balance) pending.Add(New(s, before.Balance));
        states[s.CharacterId] = s;
        commits.AddRange(Flush(s.Time));
        return commits;
    }
    public IReadOnlyList<Commit> Suspend(DateTimeOffset now) {
        var result = Flush(now, true); active = null; evidence.Clear(); evidenceIds.Clear(); return result;
    }
    public IReadOnlyList<Commit> Flush(DateTimeOffset now, bool force = false) {
        var expired = evidence.Where(e => now - e.Time > TimeSpan.FromSeconds(10)).ToList();
        foreach (var e in expired) { evidence.Remove(e); evidenceIds.Remove(e.Id); }
        if (evidenceIds.Count > 4096) evidenceIds.Clear();
        var ready = pending.Where(x => force || now - x.Time >= CorrelationWindow).ToList();
        if (ready.Count == 0) return [];
        var result = new List<Commit>();
        foreach (var group in ready.GroupBy(x => x.CharacterId)) {
            var entries = new List<LedgerEntry>();
            foreach (var entry in group) {
                var candidates = evidence.Where(e => e.CharacterId == entry.CharacterId && e.Amount == entry.Amount &&
                    (e.Time - entry.Time).Duration() <= CorrelationWindow).ToList();
                // Ambiguous or overlapping matches stay unknown; no fabricated split or item attribution.
                var unique = candidates.GroupBy(e => (e.Category, e.ItemId, e.Quantity, e.Destination)).ToList();
                var competing = pending.Count(p => p.CharacterId == entry.CharacterId && p.Amount == entry.Amount && !p.IsGap &&
                    candidates.Any(e => (p.Time - e.Time).Duration() <= CorrelationWindow));
                var match = !entry.IsGap && unique.Count == 1 && competing == 1 ? candidates.First() : null;
                entries.Add(match == null ? entry : entry with { Category = match.Category, Detail = match.Detail,
                    Source = match.Source, ItemId = match.ItemId, ItemName = match.ItemName, Quantity = match.Quantity, Destination = match.Destination });
                foreach (var e in candidates) evidence.Remove(e);
            }
            // Persist only through the last emitted entry, never past an unflushed transaction.
            var last = group.Last();
            var state = states[group.Key] with { Balance = last.After, Time = last.Time };
            result.Add(new(state, entries));
        }
        foreach (var entry in ready) pending.Remove(entry);
        return result;
    }
    private static LedgerEntry New(BalanceSample s, long before) => new(Guid.NewGuid().ToString("N"), s.CharacterId, s.CharacterName, s.Time, before, s.Balance, s.Location);
}
