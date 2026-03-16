namespace OrcaCore.Runtime;

internal sealed class CorrelationIndex
{
    private readonly Dictionary<(string EventName, string CorrelationId), HashSet<string>> _index = new();

    public void Add(string eventName, string correlationId, string instanceId)
    {
        var key = (eventName, correlationId);
        if (!_index.TryGetValue(key, out var set))
        {
            set = [];
            _index[key] = set;
        }
        set.Add(instanceId);
    }

    public void Remove(string eventName, string correlationId, string instanceId)
    {
        var key = (eventName, correlationId);
        if (_index.TryGetValue(key, out var set))
        {
            set.Remove(instanceId);
            if (set.Count == 0)
                _index.Remove(key);
        }
    }

    /// <summary>
    /// Resolves the correlation to exactly one instance.
    /// Throws if zero or more than one instance is found.
    /// </summary>
    public string ResolveExactlyOne(string eventName, string correlationId)
    {
        var key = (eventName, correlationId);
        if (!_index.TryGetValue(key, out var set) || set.Count == 0)
            throw new InvalidOperationException(
                $"No active wait found for this CorrelationId: EventName='{eventName}', CorrelationId='{correlationId}'.");

        if (set.Count > 1)
            throw new InvalidOperationException(
                $"Ambiguous correlation — use instance-targeted or fanout routing: EventName='{eventName}', CorrelationId='{correlationId}' maps to {set.Count} instances.");

        return set.First();
    }
}
