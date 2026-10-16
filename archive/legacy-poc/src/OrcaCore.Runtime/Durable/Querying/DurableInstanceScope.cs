/// <summary>
/// Admin/management view over a single durable instance.
/// </summary>
namespace OrcaCore.Runtime.Durable.Querying;

public sealed class DurableInstanceScope
{
    private readonly DurableWorkflowEngine _engine;
    private readonly string _instanceId;

    internal DurableInstanceScope(DurableWorkflowEngine engine, string instanceId)
    {
        _engine = engine;
        _instanceId = instanceId;
    }

    public Task<DurableInstanceSnapshot> GetAsync(CancellationToken cancellationToken = default) =>
        _engine.GetSnapshotAsync(_instanceId, cancellationToken);

    public Task<TState> GetStateAsync<TState>(CancellationToken cancellationToken = default) =>
        _engine.GetStateAsync<TState>(_instanceId, cancellationToken);

    public Task<IReadOnlyList<WaitRecord>> GetActiveWaitsAsync(CancellationToken cancellationToken = default) =>
        _engine.GetActiveWaitsAsync(_instanceId, cancellationToken);

    public Task RaiseEvent(EventEnvelope envelope, CancellationToken cancellationToken = default) =>
        _engine.RaiseEventToInstanceAsync(_instanceId, envelope, cancellationToken);

    public Task DeleteAsync(CancellationToken cancellationToken = default) =>
        _engine.DeleteInstanceAsync(_instanceId, cancellationToken);

    public Task PurgeArtifactsAsync(DurableArtifactRetentionPolicy policy, CancellationToken cancellationToken = default) =>
        _engine.PurgeInstanceArtifactsAsync(_instanceId, policy, cancellationToken);
}
