using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Schedules ready DAG nodes as durable child workflow batches.
/// </summary>
public sealed class DurableDagRunner(DurableCommandProcessor commandProcessor)
{
    /// <summary>
    /// Schedules the DAG nodes whose dependencies are satisfied.
    /// </summary>
    public async Task<IReadOnlyList<DurableDagScheduleResult>> ScheduleReadyAsync(
        DurableDagScheduleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var batches = new WorkflowDagRunner(request.Plan, request.MaxConcurrency)
            .GetNextBatches(request.CompletedNodeIds, request.FailedNodeIds);
        if (batches.Count == 0)
        {
            return [];
        }

        var results = new List<DurableDagScheduleResult>(batches.Count);
        foreach (var batch in batches)
        {
            var result = await commandProcessor
                .ProcessAsync(
                    new DurableRunChildrenCommand(
                        CommandId.New(),
                        request.RootInstanceId,
                        request.RequestedAt,
                        batch.ChildDefinitionId,
                        batch.ChildDefinitionVersion,
                        batch.ItemSnapshots,
                        batch.FailurePolicy,
                        batch.MaxConcurrency),
                    cancellationToken)
                .ConfigureAwait(false);
            results.Add(new DurableDagScheduleResult(batch, result));
        }

        return results;
    }
}

/// <summary>
/// Request to schedule currently runnable DAG nodes.
/// </summary>
public sealed record DurableDagScheduleRequest(
    InstanceId RootInstanceId,
    WorkflowDagPlan Plan,
    IReadOnlyCollection<string> CompletedNodeIds,
    IReadOnlyCollection<string> FailedNodeIds,
    DateTimeOffset RequestedAt,
    int? MaxConcurrency = null);

/// <summary>
/// Result for one DAG child batch submitted to durable execution.
/// </summary>
public sealed record DurableDagScheduleResult(
    WorkflowDagChildBatch Batch,
    DurableCommandResult CommandResult);
