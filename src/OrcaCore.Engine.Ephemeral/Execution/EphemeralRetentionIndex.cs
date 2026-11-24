using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

// All access uses the host admission gate. Only eligible terminals belong to the two orderings.
internal sealed class EphemeralRetentionIndex
{
    private readonly Dictionary<InstanceId, Entry> tracked = [];
    private readonly SortedDictionary<long, Entry> byOrdinal = [];
    private readonly SortedSet<(ulong Timestamp, long Ordinal)> byAge = [];
    private long lastOrdinal;

    internal int TrackedCount => tracked.Count;

    internal void Track(EphemeralInstanceLifetime lifetime) =>
        tracked.Add(lifetime.InstanceId, new Entry(lifetime));

    internal bool NeedsStamp(EphemeralInstanceLifetime lifetime) =>
        tracked.TryGetValue(lifetime.InstanceId, out var entry) && entry.Ordinal == 0 && lifetime.IsPhysicallySettled;

    internal void Stamp(EphemeralInstanceLifetime lifetime, long timestamp)
    {
        // The clock sample was taken outside the gate. A concurrent release may already have stamped or retired it.
        if (!tracked.TryGetValue(lifetime.InstanceId, out var entry) || entry.Ordinal != 0) return;
        entry.Ordinal = checked(++lastOrdinal);
        entry.Timestamp = unchecked((ulong)timestamp);
        if (lifetime.IsEligible) AddEligible(entry);
    }

    internal void SetPinned(InstanceId id, bool pinned)
    {
        if (!tracked.TryGetValue(id, out var entry) || entry.Ordinal == 0) return;
        if (pinned) RemoveEligible(entry);
        else AddEligible(entry);
    }

    internal void Forget(InstanceId id)
    {
        if (tracked.Remove(id, out var entry)) RemoveEligible(entry);
    }

    private void AddEligible(Entry entry)
    {
        byOrdinal.Add(entry.Ordinal, entry);
        byAge.Add((entry.Timestamp, entry.Ordinal));
    }

    private void RemoveEligible(Entry entry)
    {
        if (byOrdinal.Remove(entry.Ordinal)) byAge.Remove((entry.Timestamp, entry.Ordinal));
    }

    internal List<(long Ordinal, Action Release)>? Select(long now, Int128 minimumAge, int maximumCount)
    {
        List<(long Ordinal, Action Release)>? selected = null;
        if (byAge.Count != 0 && minimumAge <= long.MaxValue)
        {
            // Signed elapsed time is unchecked(now - stamp). Its expired stamps form one interval on the
            // unsigned timestamp ring. A delayed clock sample can put timestamp and ordinal in different orders.
            var first = unchecked((ulong)(now - long.MaxValue));
            var last = unchecked((ulong)(now - (long)minimumAge));
            List<long>? expired = null;
            if (first <= last) CollectExpired(first, last, ref expired);
            else
            {
                CollectExpired(first, ulong.MaxValue, ref expired);
                CollectExpired(0, last, ref expired);
            }
            if (expired is not null)
                foreach (var ordinal in expired) Retire(byOrdinal[ordinal]);
        }
        while (byOrdinal.Count > maximumCount) Retire(byOrdinal.First().Value);
        return selected;

        void Retire(Entry entry)
        {
            // Selection also removes this entry from both orderings, including when an explicit eviction wins.
            var release = entry.Lifetime.SelectRetirement() ??
                throw new InvalidOperationException("The retention index selected an ineligible instance.");
            (selected ??= []).Add((entry.Ordinal, release));
        }
    }

    private void CollectExpired(ulong first, ulong last, ref List<long>? expired)
    {
        if (byAge.Min.Timestamp > last || byAge.Max.Timestamp < first) return;
        foreach (var key in byAge.GetViewBetween((first, 0), (last, long.MaxValue)))
            (expired ??= []).Add(key.Ordinal);
    }

    private sealed class Entry(EphemeralInstanceLifetime lifetime)
    {
        internal EphemeralInstanceLifetime Lifetime { get; } = lifetime;
        internal ulong Timestamp { get; set; }
        internal long Ordinal { get; set; }
    }
}
