namespace OrcaCore.Runtime.Routing;

internal interface ICorrelationMutationSink
{
    void Add(string eventName, string correlationId, string instanceId);

    void Remove(string eventName, string correlationId, string instanceId);
}
