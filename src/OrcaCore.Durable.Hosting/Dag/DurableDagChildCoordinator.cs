using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Engine.Durable.Dag;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed class DurableDagChildCoordinator(IWorkflowEventStore store, DurableDagChildEngine children,
    DurableDagRunTurns turns, TimeProvider time) : IDurableDagChildHandler
{
    public async Task<bool> HandleAsync(OutboxWrite write, CancellationToken token)
    {
        var signal = DurableDagWire.ReadSignal(write);
        var run = await ReadAsync(signal.RunId, token).ConfigureAwait(false);
        if (signal.Ordinal >= run.Nodes.Count) throw new InvalidDataException("DAG child ordinal is outside its graph.");
        var node = run.Nodes[signal.Ordinal];
        if (node.ChildInstanceId is null || node.Input is null)
            throw new InvalidDataException("DAG child was never admitted.");
        if (run.State == DagRunStateV1.IntegrityFault) return false;
        switch (write.Kind)
        {
            case DurableDagKinds.Start:
                return await children.StartOrReattachAsync(run, signal.Ordinal, token).ConfigureAwait(false);
            case DurableDagKinds.Cancel:
                if (run.State is not (DagRunStateV1.CancellationRequested or DagRunStateV1.Cancelled))
                    throw new InvalidDataException("DAG child cancellation precedes the run barrier.");
                return await children.CancelAsync(run, signal.Ordinal, token).ConfigureAwait(false);
            case DurableDagKinds.Join:
                var fact = await children.ReadTerminalAsync(run, signal.Ordinal, token).ConfigureAwait(false);
                if (fact is null) return false;
                if (IsTerminal(node.State)) return MatchesJoined(node, fact, run);
                return await turns.ExecuteAsync(run.RunId, DagRunLeasePurposeV1.ChildJoin, async (fence, ct) =>
                {
                    var current = await ReadAsync(run.RunId, ct).ConfigureAwait(false);
                    if (current.State == DagRunStateV1.IntegrityFault) return false;
                    var currentNode = current.Nodes[signal.Ordinal];
                    if (IsTerminal(currentNode.State)) return MatchesJoined(currentNode, fact, current);
                    var records = Join(current, signal.Ordinal, fact, Now(current));
                    return (await store.AppendAsync(DurableDagWire.Commit(current, records, fence), ct).ConfigureAwait(false)).IsSuccess;
                }, token).ConfigureAwait(false);
            default:
                throw new InvalidDataException("Unknown DAG child record kind.");
        }
    }

    public async Task<bool> RecordFaultAsync(Guid runId, CancellationToken token)
    {
        DagRunSnapshotV1 run;
        try
        {
            var checkpoint = await store.LoadDagRunCheckpointAsync(WorkflowStreamId.ForDagRun(runId), token).ConfigureAwait(false);
            if (!checkpoint.HasValue) return true; // No admitted run to mutate; quarantine the orphan transport visibly.
            run = DurableDagWire.Read(checkpoint.Value);
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Text.Json.JsonException)
        {
            // Corrupt run truth cannot safely be rewritten. Quarantine its transport
            // with the original integrity diagnostic, retaining every uncertain slot.
            return true;
        }
        if (run.State is DagRunStateV1.IntegrityFault or DagRunStateV1.Succeeded or DagRunStateV1.Failed or DagRunStateV1.Cancelled)
            return true;
        return await turns.ExecuteAsync(runId, DagRunLeasePurposeV1.IntegrityReconciliation, async (fence, ct) =>
        {
            var current = await ReadAsync(runId, ct).ConfigureAwait(false);
            if (current.State == DagRunStateV1.IntegrityFault) return true;
            var record = new DagRunRecordV1(1, DagRunRecordKindV1.IntegrityFaulted, runId, Guid.CreateVersion7(),
                current.StreamVersion.Next(), DagRunStateV1.IntegrityFault, null, [], Now(current), null);
            return (await store.AppendAsync(DurableDagWire.Commit(current, [record], fence), ct).ConfigureAwait(false)).IsSuccess;
        }, token).ConfigureAwait(false);
    }

    private async Task<DagRunSnapshotV1> ReadAsync(Guid runId, CancellationToken token)
    {
        var checkpoint = await store.LoadDagRunCheckpointAsync(WorkflowStreamId.ForDagRun(runId), token).ConfigureAwait(false);
        return checkpoint.HasValue ? DurableDagWire.Read(checkpoint.Value) : throw new InvalidDataException("DAG owning run is absent.");
    }

    private DateTimeOffset Now(DagRunSnapshotV1 run)
    {
        var sample = time.GetUtcNow();
        return sample > run.UpdatedAt ? sample : run.UpdatedAt;
    }

    private static bool IsTerminal(DagNodeStateV1 state) => state is DagNodeStateV1.Succeeded or DagNodeStateV1.Failed or
        DagNodeStateV1.Cancelled or DagNodeStateV1.DependencyBlocked;

    private static (DagNodeStateV1 State, string? Code) Outcome(DurableDagChildFact fact, DagRunSnapshotV1 run) => fact.Status switch
    {
        WorkflowInstanceStatus.Completed => (DagNodeStateV1.Succeeded, null),
        WorkflowInstanceStatus.Cancelled when run.State is DagRunStateV1.CancellationRequested or DagRunStateV1.Cancelled => (DagNodeStateV1.Cancelled, null),
        WorkflowInstanceStatus.Cancelled => (DagNodeStateV1.Failed, "CHILD_CANCELLED"),
        WorkflowInstanceStatus.TimedOut => (DagNodeStateV1.Failed, "CHILD_TIMED_OUT"),
        WorkflowInstanceStatus.Terminated => (DagNodeStateV1.Failed, "CHILD_TERMINATED"),
        WorkflowInstanceStatus.Failed => (DagNodeStateV1.Failed, "CHILD_FAILED"),
        _ => throw new InvalidDataException("DAG join is not authoritative terminality.")
    };

    private static bool MatchesJoined(DagRunNodeStateV1 node, DurableDagChildFact fact, DagRunSnapshotV1 run)
    {
        var expected = Outcome(fact, run);
        if (node.State != expected.State || node.FailureCode != expected.Code ||
            node.Output?.Fingerprint != fact.Output?.Fingerprint || node.Output?.ContractKey != fact.Output?.ContractKey ||
            (node.Output is not null && fact.Output is not null && !node.Output.Bytes.AsSpan().SequenceEqual(fact.Output.Bytes)))
            throw new InvalidDataException("DAG joined outcome conflicts with authoritative child facts.");
        return true;
    }

    private static IReadOnlyList<DagRunRecordV1> Join(DagRunSnapshotV1 run, int ordinal, DurableDagChildFact fact, DateTimeOffset at)
    {
        var outcome = Outcome(fact, run);
        var nodes = run.Nodes.ToArray();
        nodes[ordinal] = nodes[ordinal] with { State = outcome.State, FailureCode = outcome.Code, Output = fact.Output, CompletedAt = at };
        var changed = new List<DagRunNodeChangeV1> { Change(nodes[ordinal], fact.Output) };
        if (run.State != DagRunStateV1.CancellationRequested)
        {
            if (outcome.State == DagNodeStateV1.Succeeded)
            {
                foreach (var node in run.Graph.Nodes)
                    if (nodes[node.Ordinal].State == DagNodeStateV1.Pending && node.DirectDependencies.All(d => nodes[d].State == DagNodeStateV1.Succeeded))
                    {
                        nodes[node.Ordinal] = nodes[node.Ordinal] with { State = DagNodeStateV1.Ready, ReadyAt = at };
                        changed.Add(Change(nodes[node.Ordinal], null));
                    }
            }
            else
            {
                var dependants = Enumerable.Range(0, nodes.Length).Select(_ => new List<int>()).ToArray();
                foreach (var node in run.Graph.Nodes)
                    foreach (var dependency in node.DirectDependencies) dependants[dependency].Add(node.Ordinal);
                var descendants = new HashSet<int> { ordinal };
                var pending = new Queue<int>(dependants[ordinal]);
                while (pending.TryDequeue(out var index))
                {
                    if (!descendants.Add(index)) continue;
                    foreach (var dependant in dependants[index]) pending.Enqueue(dependant);
                }
                foreach (var index in descendants.Where(i => i != ordinal).Order())
                    if (nodes[index].State is DagNodeStateV1.Pending or DagNodeStateV1.Ready)
                    {
                        nodes[index] = nodes[index] with { State = DagNodeStateV1.DependencyBlocked, FailureCode = "DEPENDENCY_BLOCKED", CompletedAt = at };
                        changed.Add(Change(nodes[index], null));
                    }
            }
        }
        var operation = Guid.CreateVersion7();
        var join = new DagRunRecordV1(1, DagRunRecordKindV1.ChildJoined, run.RunId, operation, run.StreamVersion.Next(),
            run.State, null, changed, at, null);
        if (nodes.Any(node => !IsTerminal(node.State))) return [join];
        var terminal = run.State == DagRunStateV1.CancellationRequested ? DagRunStateV1.Cancelled :
            nodes.All(node => node.State == DagNodeStateV1.Succeeded) ? DagRunStateV1.Succeeded : DagRunStateV1.Failed;
        return [join, new(1, DagRunRecordKindV1.Terminal, run.RunId, operation, join.StreamVersion.Next(), terminal, null, [], at, at)];
    }

    private static DagRunNodeChangeV1 Change(DagRunNodeStateV1 node, DagRunValueV1? output) =>
        new(node.Ordinal, node.State, node.ChildInstanceId, null, output, node.FailureCode, node.ReadyAt, node.StartedAt, node.CompletedAt);
}
