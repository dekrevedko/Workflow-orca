using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableExternalJobState
{
    private readonly List<DurableActiveExternalJob> activeJobs;

    private DurableExternalJobState(IEnumerable<DurableActiveExternalJob> activeJobs)
    {
        this.activeJobs = [.. activeJobs];
    }

    internal IReadOnlyList<DurableActiveExternalJob> ActiveJobs => [.. activeJobs];

    internal static DurableExternalJobState FromSnapshot(IEnumerable<DurableActiveExternalJob> activeJobs)
    {
        ArgumentNullException.ThrowIfNull(activeJobs);

        return new DurableExternalJobState(activeJobs);
    }

    internal DurableActiveExternalJob? Find(string externalJobId)
    {
        return activeJobs.FirstOrDefault(job =>
            string.Equals(job.ExternalJobId, externalJobId, StringComparison.Ordinal));
    }

    internal void Clear()
    {
        activeJobs.Clear();
    }

    internal IReadOnlyList<WorkflowExternalJobStopRequestedEvent> CreateStopRequestedEvents(
        DurableExternalJobEventContext context,
        IReadOnlySet<FiberId>? ownerFiberIds = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return activeJobs
            .Where(job => ownerFiberIds is null ||
                job.FiberId is { } fiberId && ownerFiberIds.Contains(fiberId))
            .Select(job => new WorkflowExternalJobStopRequestedEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = context.InstanceId,
            CommandId = context.CommandId,
            CausationId = context.CausationId,
            OccurredAt = context.RequestedAt,
            ParentInstanceId = context.ParentInstanceId,
            RootInstanceId = context.RootInstanceId,
            ExternalJobId = job.ExternalJobId,
            FiberId = job.FiberId,
            ScopeId = job.ScopeId
        }).ToArray();
    }

    internal void Apply(DurableWorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        switch (workflowEvent)
        {
            case WorkflowExternalJobStartedEvent externalJobStarted:
                activeJobs.RemoveAll(job =>
                    string.Equals(job.ExternalJobId, externalJobStarted.ExternalJobId, StringComparison.Ordinal));
                activeJobs.Add(new DurableActiveExternalJob(
                    externalJobStarted.ExternalJobId,
                    externalJobStarted.WaitId,
                    externalJobStarted.TimeoutTimerId)
                {
                    FiberId = externalJobStarted.FiberId,
                    ScopeId = externalJobStarted.ScopeId
                });
                break;
            case WorkflowExternalJobCompletedEvent externalJobCompleted:
                Remove(externalJobCompleted.ExternalJobId);
                break;
            case WorkflowExternalJobTimedOutEvent externalJobTimedOut:
                Remove(externalJobTimedOut.ExternalJobId);
                break;
            case WorkflowExternalJobStopRequestedEvent stopRequested:
                Remove(stopRequested.ExternalJobId);
                break;
        }
    }

    internal IReadOnlyList<CheckpointActiveExternalJob> CreateCheckpointActiveExternalJobs()
    {
        return activeJobs
            .Select(job => new CheckpointActiveExternalJob(
                job.ExternalJobId,
                job.WaitId,
                job.TimeoutTimerId)
            {
                FiberId = job.FiberId,
                ScopeId = job.ScopeId
            })
            .ToArray();
    }

    private void Remove(string externalJobId)
    {
        activeJobs.RemoveAll(job =>
            string.Equals(job.ExternalJobId, externalJobId, StringComparison.Ordinal));
    }
}

internal sealed record DurableExternalJobEventContext(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    InstanceId? ParentInstanceId,
    InstanceId RootInstanceId)
{
    internal CausationId CausationId => new(CommandId.Value);
}
