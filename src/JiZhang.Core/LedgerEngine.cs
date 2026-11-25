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
    private readonly Dictionary<string,DateTimeOffset> deferred=[];
    public void ReleaseDeferred()=>deferred.Clear();
    public LedgerEngine(IEnumerable<BalanceSample>? previous = null) => states = previous?.ToDictionary(x => x.CharacterId) ?? [];
    public IReadOnlyDictionary<ulong, BalanceSample> States => states;
    public void AddEvidence(Evidence e) {
        if (e.CharacterId != active || e.Amount == 0 || !evidenceIds.Add(e.Id)) return;
        evidence.Add(e);
    }
    public IReadOnlyList<Commit> Observe(BalanceSample s, DateTimeOffset? classificationDeadline = null) {
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
        if (before.Balance != s.Balance) {
            var entry=New(s,before.Balance);pending.Add(entry);
            if(classificationDeadline is {} deadline&&deadline>s.Time)deferred[entry.Id]=deadline;
        }
        states[s.CharacterId] = s;
        commits.AddRange(Flush(s.Time));
        return commits;
    }
    public IReadOnlyList<Commit> Suspend(DateTimeOffset now) {
        var result = Flush(now, true); active = null; evidence.Clear(); evidenceIds.Clear(); return result;
    }
    public IReadOnlyList<Commit> Flush(DateTimeOffset now, bool force = false) {
        var expired = evidence.Where(e => now - e.Time > TimeSpan.FromSeconds(10)&&!pending.Any(p=>p.CharacterId==e.CharacterId&&p.Amount==e.Amount&&(p.Time-e.Time).Duration()<=CorrelationWindow)).ToList();
        foreach (var e in expired) { evidence.Remove(e); evidenceIds.Remove(e.Id); }
        if (evidenceIds.Count > 4096) evidenceIds.Clear();
        // Commit a contiguous prefix only: a held teleport debit must not be skipped by later checkpoints.
        var ready = pending.GroupBy(e=>e.CharacterId).SelectMany(g=>g.TakeWhile(x => force ||
            now-x.Time>=CorrelationWindow&&(!deferred.TryGetValue(x.Id,out var deadline)||now>=deadline))).ToList();
        if (ready.Count == 0) return [];
        var result = new List<Commit>();
        foreach (var group in ready.GroupBy(x => x.CharacterId)) {
            var entries = new List<LedgerEntry>();
            foreach (var entry in group) {
                var candidates = evidence.Where(e => e.CharacterId == entry.CharacterId && e.Amount == entry.Amount &&
                    (e.Time - entry.Time).Duration() <= CorrelationWindow).ToList();
                var preferred = candidates.Where(e => !e.IsContextHint && e.ExpenseMessageId == null).ToList();
                if (preferred.Count == 0) preferred = candidates.Where(e => !e.IsContextHint).ToList();
                if (preferred.Count == 0) preferred = candidates;
                if (preferred.Any(e => e.Category is Category.QuestReward or Category.DutyReward or Category.ChallengeReward))
                    preferred = preferred.Where(e => e.Category != Category.SystemReward).ToList();
                // A confirmation without item details can agree with a more detailed event.
                var categoryConflict = preferred.Select(e => e.Category).Distinct().Skip(1).Any();
                var itemConflict = preferred.Where(e => e.ItemId != null).Select(e => e.ItemId).Distinct().Skip(1).Any();
                var quantityConflict = preferred.Where(e => e.Quantity != null).Select(e => e.Quantity).Distinct().Skip(1).Any();
                var expenseCodeConflict = preferred.Select(e => e.ExpenseMessageId).Distinct().Skip(1).Any();
                // Missing destination is compatible with a more detailed confirmation; two distinct destinations conflict.
                var destinationConflict=preferred.Where(e=>e.Destination!=null).Select(e=>e.Destination).Distinct().Skip(1).Any();
                var competing = pending.Count(p => p.CharacterId == entry.CharacterId && p.Amount == entry.Amount && !p.IsGap &&
                    preferred.Any(e => (p.Time - e.Time).Duration() <= CorrelationWindow));
                var match = !entry.IsGap && !categoryConflict && !itemConflict && !quantityConflict &&
                    !destinationConflict && !expenseCodeConflict && competing == 1
                    ? preferred.OrderByDescending(e => e.Destination != null).ThenByDescending(e => e.ItemId != null).First()
                    : null;
                entries.Add(match == null ? entry : entry with { Category = match.Category, Detail = match.Detail,
                    Source = match.Source, ItemId = match.ItemId, ItemName = match.ItemName, Quantity = match.Quantity,
                    Destination = match.Destination, ExpenseMessageId = match.ExpenseMessageId });
                foreach (var e in candidates) evidence.Remove(e);
            }
            // Persist only through the last emitted entry, never past an unflushed transaction.
            var last = group.Last();
            var state = states[group.Key] with { Balance = last.After, Time = last.Time };
            result.Add(new(state, entries));
        }
        foreach (var entry in ready) {pending.Remove(entry);deferred.Remove(entry.Id);}
        return result;
    }
    private static LedgerEntry New(BalanceSample s, long before) => new(Guid.NewGuid().ToString("N"), s.CharacterId, s.CharacterName, s.Time, before, s.Balance, s.Location);
}
