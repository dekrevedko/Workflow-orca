using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Engine.Durable.Dag;

internal static class DurableDagTerminalNotice
{
    internal static async Task<OutboxWrite?> CreateAsync(IWorkflowEventStore store, DurableWorkflowAggregate child,
        DurableDecision decision, CancellationToken token)
    {
        if (child.ParentInstanceId is not { } parent || child.RootInstanceId?.Value != parent.Value ||
            !decision.Events.Any(e => e is WorkflowCompletedEvent or WorkflowTerminalEvent)) return null;
        var checkpoint = await store.LoadDagRunCheckpointAsync(WorkflowStreamId.ForDagRun(parent.Value), token).ConfigureAwait(false);
        if (!checkpoint.HasValue) return null; // An ordinary workflow parent is not a DAG anchor.
        var run = DurableDagWire.Read(checkpoint.Value);
        var node = run.Nodes.SingleOrDefault(n => n.ChildInstanceId?.Value == child.InstanceId.Value);
        if (node is null || child.InstanceId.Value != DurableDagWire.ChildId(run.RunId, node.Ordinal))
            throw new InvalidDataException("DAG child terminality has inconsistent lineage.");
        return DurableDagWire.ChildWrite(DurableDagKinds.Join, run.RunId, node.Ordinal);
    }
}
