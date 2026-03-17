namespace OrcaCore.Runtime.Routing;

internal sealed class CorrelationIndex : ICorrelationMutationSink
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string EventName, string CorrelationId), HashSet<string>> _index = new();

    public void Add(string eventName, string correlationId, string instanceId)
    {
        lock (_gate)
        {
            var key = (eventName, correlationId);
            if (!_index.TryGetValue(key, out var set))
            {
                set = [];
                _index[key] = set;
            }

            set.Add(instanceId);
        }
    }

    public void Remove(string eventName, string correlationId, string instanceId)
    {
        lock (_gate)
        {
            var key = (eventName, correlationId);
            if (_index.TryGetValue(key, out var set))
            {
                set.Remove(instanceId);
                if (set.Count == 0)
                    _index.Remove(key);
            }
        }
    }

    public string ResolveExactlyOne(string eventName, string correlationId)
    {
        lock (_gate)
        {
            var key = (eventName, correlationId);
            if (!_index.TryGetValue(key, out var set) || set.Count == 0)
                throw new NoActiveWaitException(eventName, correlationId);

            if (set.Count > 1)
                throw new AmbiguousCorrelationException(eventName, correlationId, set.Count);

            return set.First();
        }
    }
}
