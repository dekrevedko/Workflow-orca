using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Engine.Durable.Dag;

namespace OrcaCore.Durable.Hosting.Dag;

internal enum DurableDagCancelOutcomeV1 { Requested, AlreadyRequested, AlreadyTerminal, NotFound }
internal sealed record DurableDagCancelResultV1(DurableDagCancelOutcomeV1 Outcome, StreamVersion? Version);

internal sealed class DurableDagRunCancellation(IWorkflowEventStore events, DurableDagRunStore store,
    DurableDagRunTurns turns, TimeProvider time)
{
    internal async ValueTask<DurableDagCancelResultV1> RequestRunCancellationAsync(Guid runId, CancellationToken token)
    {
        DurableDagCancelResultV1? outcome = null;
        while (outcome is null)
        {
            token.ThrowIfCancellationRequested();
            outcome = Observe(await store.ReadAsync(runId, token).ConfigureAwait(false));
            if (outcome is not null) break;
            await turns.ExecuteAsync(runId, DagRunLeasePurposeV1.RunCancellation, async (fence, ct) =>
            {
                var current = await store.ReadAsync(runId, ct).ConfigureAwait(false);
                outcome = Observe(current);
                if (outcome is not null) return true;
                var run = current!;
                var now = time.GetUtcNow().ToUniversalTime();
                if (now < run.UpdatedAt) now = run.UpdatedAt;
                var operation = Guid.CreateVersion7();
                var changes = new List<DagRunNodeChangeV1>();
                var outbox = new List<OutboxWrite>();
                foreach (var node in run.Nodes)
                {
                    if (node.State is DagNodeStateV1.Pending or DagNodeStateV1.Ready)
                        changes.Add(new(node.Ordinal, DagNodeStateV1.Cancelled, null, null, null, null,
                            node.ReadyAt, null, now));
                    else if (node.State == DagNodeStateV1.StartAuthorized)
                    {
                        changes.Add(new(node.Ordinal, DagNodeStateV1.CancellationRequested, node.ChildInstanceId,
                            null, null, null, node.ReadyAt, node.StartedAt, null));
                        outbox.Add(DurableDagWire.ChildWrite(DurableDagKinds.Cancel, run.RunId, node.Ordinal));
                    }
                }
                var cancelled = new DagRunRecordV1(1, DagRunRecordKindV1.CancellationRequested, run.RunId,
                    operation, run.StreamVersion.Next(), DagRunStateV1.CancellationRequested, null, changes, now, null);
                DagRunRecordV1[] records = outbox.Count != 0 ? [cancelled] :
                    [cancelled, new(1, DagRunRecordKindV1.Terminal, run.RunId, operation, cancelled.StreamVersion.Next(),
                        DagRunStateV1.Cancelled, null, [], now, now)];
                var append = await events.AppendAsync(DurableDagWire.Commit(run, records, fence, outbox), ct)
                    .ConfigureAwait(false);
                if (append.IsFailure)
                {
                    if (append.Error.Code is "DAG_STALE_FENCE" or "DAG_VERSION_CONFLICT") return false;
                    throw append.Error;
                }
                outcome = new(DurableDagCancelOutcomeV1.Requested, append.Value.NewVersion);
                return true;
            }, token).ConfigureAwait(false);
            // Bounded backoff outside the broker if capacity or the fence was lost.
            if (outcome is null)
                await Task.Delay(TimeSpan.FromMilliseconds(250), time, token).ConfigureAwait(false);
        }
        return outcome;
    }

    private static DurableDagCancelResultV1? Observe(DagRunSnapshotV1? run) => run?.State switch
    {
        null => new(DurableDagCancelOutcomeV1.NotFound, null),
        DagRunStateV1.CancellationRequested => new(DurableDagCancelOutcomeV1.AlreadyRequested, run.StreamVersion),
        DagRunStateV1.Cancelled or DagRunStateV1.Succeeded or DagRunStateV1.Failed =>
            new(DurableDagCancelOutcomeV1.AlreadyTerminal, run.StreamVersion),
        DagRunStateV1.IntegrityFault => throw new InvalidDataException("A faulted DAG run requires reconciliation before cancellation."),
        _ => null
    };
}
