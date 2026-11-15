using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Engine.Durable.Dag;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed class DurableDagIntegrity(IWorkflowEventStore events, DurableDagRunStore runs,
    DurableDagChildEngine children, TimeProvider time)
{
    internal async ValueTask<DurableDagMutationResultV1> RecordIntegrityFaultAsync(
        Guid id, string code, DagRunFenceV1 fence, StreamVersion version, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var run = await runs.ReadAsync(id, token).ConfigureAwait(false);
        if (run is null) return new(DurableDagMutationOutcomeV1.NotFound, null, null);
        if (run.State == DagRunStateV1.IntegrityFault) return new(DurableDagMutationOutcomeV1.Existing, run.StreamVersion, run);
        if (run.State is DagRunStateV1.Cancelled or DagRunStateV1.Succeeded or DagRunStateV1.Failed)
            return new(DurableDagMutationOutcomeV1.AdmissionStopped, run.StreamVersion, run);
        return await AppendAsync(run, fence, version, DagRunRecordKindV1.IntegrityFaulted, DagRunStateV1.IntegrityFault,
            Guid.CreateVersion7(), [], token).ConfigureAwait(false);
    }

    internal async ValueTask<DurableDagMutationResultV1> ReconcilePoisonAsync(
        Guid id, Guid operation, DagRunFenceV1 fence, StreamVersion version, CancellationToken token)
    {
        if (operation == Guid.Empty) throw new ArgumentException("A DAG reconciliation requires an operation identity.", nameof(operation));
        var run = await runs.ReadAsync(id, token).ConfigureAwait(false);
        if (run is null) return new(DurableDagMutationOutcomeV1.NotFound, null, null);
        if (fence.RunId != id) return Result(DurableDagMutationOutcomeV1.StaleFence);
        var history = await events.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(id), StreamVersion.Empty, token)
            .ConfigureAwait(false);
        if (history.FirstOrDefault(record => record.OperationId == operation) is { } prior)
            return prior.Kind == DagRunRecordKindV1.PoisonReconciled
                ? new(DurableDagMutationOutcomeV1.Existing, prior.StreamVersion, run)
                : Result(DurableDagMutationOutcomeV1.IntegrityConflict);
        if (run.StreamVersion != version) return Result(DurableDagMutationOutcomeV1.VersionConflict);
        if (run.State != DagRunStateV1.IntegrityFault) return Result(DurableDagMutationOutcomeV1.AdmissionStopped);

        // Reconstruct only retryable work from the owning run and exact child
        // facts. Old poison rows remain intact, including their diagnostics.
        var cancelling = run.Nodes.Any(node => node.State is DagNodeStateV1.Cancelled or DagNodeStateV1.CancellationRequested);
        var writes = new List<OutboxWrite>();
        try
        {
            foreach (var node in run.Nodes.Where(node => node.ChildInstanceId is not null))
            {
                var observed = await children.ObserveAsync(run, node.Ordinal, token).ConfigureAwait(false);
                if (node.State is DagNodeStateV1.Succeeded or DagNodeStateV1.Failed or DagNodeStateV1.Cancelled)
                {
                    if (observed is not { IsTerminal: true }) return Result(DurableDagMutationOutcomeV1.IntegrityConflict);
                    continue;
                }
                if (node.State is not (DagNodeStateV1.StartAuthorized or DagNodeStateV1.CancellationRequested))
                    return Result(DurableDagMutationOutcomeV1.IntegrityConflict);
                var kind = observed is { IsTerminal: true } ? DurableDagKinds.Join :
                    cancelling ? DurableDagKinds.Cancel : observed is null ? DurableDagKinds.Start : null;
                if (kind is not null) writes.Add(DurableDagWire.ChildWrite(kind, id, node.Ordinal));
            }
        }
        catch (InvalidDataException) { return Result(DurableDagMutationOutcomeV1.IntegrityConflict); }
        return await AppendAsync(run, fence, version, DagRunRecordKindV1.PoisonReconciled,
            cancelling ? DagRunStateV1.CancellationRequested : DagRunStateV1.Running, operation, writes, token).ConfigureAwait(false);

        DurableDagMutationResultV1 Result(DurableDagMutationOutcomeV1 outcome) => new(outcome, run.StreamVersion, run);
    }

    private async ValueTask<DurableDagMutationResultV1> AppendAsync(DagRunSnapshotV1 run, DagRunFenceV1 fence,
        StreamVersion expected, DagRunRecordKindV1 kind, DagRunStateV1 state, Guid operation,
        IReadOnlyList<OutboxWrite> writes, CancellationToken token)
    {
        if (fence.RunId != run.RunId) return new(DurableDagMutationOutcomeV1.StaleFence, run.StreamVersion, run);
        if (run.StreamVersion != expected) return new(DurableDagMutationOutcomeV1.VersionConflict, run.StreamVersion, run);
        var now = time.GetUtcNow().ToUniversalTime();
        var record = new DagRunRecordV1(1, kind, run.RunId, operation, expected.Next(), state, null, [],
            now < run.UpdatedAt ? run.UpdatedAt : now, null);
        var result = await events.AppendAsync(DurableDagWire.Commit(run, [record], fence, writes), token).ConfigureAwait(false);
        if (result.IsSuccess)
            return new(DurableDagMutationOutcomeV1.Committed, result.Value.NewVersion,
                await runs.ReadAsync(run.RunId, token).ConfigureAwait(false));
        return new(result.Error.Code switch
        {
            "DAG_VERSION_CONFLICT" => DurableDagMutationOutcomeV1.VersionConflict,
            "DAG_STALE_FENCE" or "DAG_TURN_ALREADY_COMMITTED" => DurableDagMutationOutcomeV1.StaleFence,
            "DAG_INTEGRITY_CONFLICT" => DurableDagMutationOutcomeV1.IntegrityConflict,
            _ => throw result.Error
        }, run.StreamVersion, run);
    }
}
