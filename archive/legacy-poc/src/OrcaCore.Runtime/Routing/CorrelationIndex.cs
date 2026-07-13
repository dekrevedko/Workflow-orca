using OrcaCore.Runtime.Engine.Exceptions;

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

    public CorrelationResolution TryResolveSingle(string eventName, string correlationId)
    {
        lock (_gate)
        {
            var key = (eventName, correlationId);
            if (!_index.TryGetValue(key, out var set) || set.Count == 0)
                return CorrelationResolution.NoActiveWait();

            if (set.Count > 1)
                return CorrelationResolution.Ambiguous(set.Count);

            return CorrelationResolution.Success(set.First());
        }
    }

    public string ResolveExactlyOne(string eventName, string correlationId)
    {
        var resolution = TryResolveSingle(eventName, correlationId);

        return resolution.Kind switch
        {
            CorrelationResolutionKind.Success => resolution.InstanceId!,
            CorrelationResolutionKind.NoActiveWait => throw new NoActiveWaitException(eventName, correlationId),
            CorrelationResolutionKind.Ambiguous => throw new AmbiguousCorrelationException(eventName, correlationId, resolution.MatchCount),
            _ => throw new InvalidOperationException($"Unexpected correlation resolution '{resolution.Kind}'.")
        };
    }

    public readonly record struct CorrelationResolution(
        CorrelationResolutionKind Kind,
        string? InstanceId = null,
        int MatchCount = 0)
    {
        public static CorrelationResolution Success(string instanceId) =>
            new(CorrelationResolutionKind.Success, instanceId, 1);

        public static CorrelationResolution NoActiveWait() =>
            new(CorrelationResolutionKind.NoActiveWait);

        public static CorrelationResolution Ambiguous(int matchCount) =>
            new(CorrelationResolutionKind.Ambiguous, null, matchCount);
    }

    public enum CorrelationResolutionKind
    {
        Success,
        NoActiveWait,
        Ambiguous
    }
}
