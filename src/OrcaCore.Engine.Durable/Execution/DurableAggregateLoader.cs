using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Engine.Durable.Execution;

internal sealed class DurableAggregateLoader
{
    private readonly IWorkflowEventStore eventStore;

    public DurableAggregateLoader(IWorkflowEventStore eventStore)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        this.eventStore = eventStore;
    }

    public async Task<DurableWorkflowAggregate> LoadAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var checkpointOption = await eventStore
            .LoadCheckpointAsync(instanceId, cancellationToken)
            .ConfigureAwait(false);
        var checkpointVersion = checkpointOption.HasValue
            ? checkpointOption.Value.StreamVersion
            : StreamVersion.Empty;
        var tail = await eventStore
            .LoadTailAsync(new WorkflowStreamId(instanceId), checkpointVersion, cancellationToken)
            .ConfigureAwait(false);

        return DurableWorkflowAggregate.Rehydrate(
            checkpointOption.HasValue ? DurableCheckpointMapper.ToAggregateCheckpoint(checkpointOption.Value) : null,
            tail);
    }
}
