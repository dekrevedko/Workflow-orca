using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

public sealed class ProviderAuthor(IWorkflowEventStore inner) : IWorkflowEventStore
{
    public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken) => inner.LoadCheckpointAsync(instanceId, cancellationToken);

    public Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken) => inner.AppendAsync(batch, cancellationToken);

    public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken) => inner.LoadTailAsync(streamId, afterVersion, cancellationToken);
}
