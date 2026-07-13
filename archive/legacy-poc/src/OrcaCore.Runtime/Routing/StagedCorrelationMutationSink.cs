namespace OrcaCore.Runtime.Routing;

internal sealed class StagedCorrelationMutationSink : ICorrelationMutationSink
{
    private readonly List<CorrelationMutation> _mutations = [];

    public void Add(string eventName, string correlationId, string instanceId) =>
        _mutations.Add(new CorrelationMutation(Add: true, eventName, correlationId, instanceId));

    public void Remove(string eventName, string correlationId, string instanceId) =>
        _mutations.Add(new CorrelationMutation(Add: false, eventName, correlationId, instanceId));

    public void ApplyTo(CorrelationIndex correlationIndex)
    {
        foreach (var mutation in _mutations)
        {
            if (mutation.Add)
            {
                correlationIndex.Add(mutation.EventName, mutation.CorrelationId, mutation.InstanceId);
            }
            else
            {
                correlationIndex.Remove(mutation.EventName, mutation.CorrelationId, mutation.InstanceId);
            }
        }
    }

    private sealed record CorrelationMutation(
        bool Add,
        string EventName,
        string CorrelationId,
        string InstanceId);
}
