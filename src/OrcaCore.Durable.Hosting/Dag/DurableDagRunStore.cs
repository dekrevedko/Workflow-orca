using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Engine.Durable.Dag;
using OrcaCore.Engine.Durable.Definitions;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed record DurableDagRunCreateV1(
    Guid RunId, DagRunStartKeyV1 StartKey, DagRunGraphV1 Graph, object? Input, Type InputType);
internal enum DurableDagCreateOutcomeV1 { Created, Existing, BindingConflict }
internal sealed record DurableDagCreateResultV1(
    DurableDagCreateOutcomeV1 Outcome, DagRunSnapshotV1? Snapshot, string? AttemptedInputFingerprint = null);

internal sealed class DurableDagDefinitionUnavailableException() :
    InvalidOperationException("The exact DAG plan or child workflow is not installed on this host.");

// Durable.Hosting owns the provider port. Dag.Hosting will consume this through
// the reviewed bridge; provider objects and installed plans never cross that edge.
internal sealed class DurableDagRunStore(IWorkflowEventStore events, DurableDefinitionRegistry workflows,
    DurableDagRunInputCatalog inputs, TimeProvider time)
{
    internal async ValueTask<DagRunSnapshotV1?> ReadAsync(Guid runId, CancellationToken token)
    {
        var checkpoint = await events.LoadDagRunCheckpointAsync(WorkflowStreamId.ForDagRun(runId), token)
            .ConfigureAwait(false);
        if (!checkpoint.HasValue) return null;
        var run = DurableDagWire.Read(checkpoint.Value);
        _ = DurableDagValues.VerifiedRunInput(run.RunInput, run.Graph.RunInputContractKey);
        return run;
    }

    internal async ValueTask<IReadOnlyList<DurableDagRecoverableRun>> DiscoverAsync(long afterSequence, int maxCount,
        CancellationToken token)
    {
        var rows = await events.ListRecoverableDagRunsAsync(new(afterSequence, maxCount), token).ConfigureAwait(false);
        return rows.Select(row => new DurableDagRecoverableRun(row.CreationSequence, row.RunId, row.StreamVersion, row.Definition)).ToArray();
    }

    internal async ValueTask<DurableDagCreateResultV1> CreateOrReopenAsync(DurableDagRunCreateV1 request,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.StartKey);
        ArgumentNullException.ThrowIfNull(request.Graph);
        ArgumentNullException.ThrowIfNull(request.InputType);
        _ = WorkflowStreamId.ForDagRun(request.RunId);
        token.ThrowIfCancellationRequested();
        var inputType = inputs.Resolve(request.Graph);
        if (inputType is null) throw new DurableDagDefinitionUnavailableException();
        if (inputType != request.InputType) return Conflict();
        foreach (var node in request.Graph.Nodes)
        {
            var installed = workflows.ResolveDagBinding(node.Workflow.DefinitionId, node.Workflow.DefinitionVersion)
                ?? throw new DurableDagDefinitionUnavailableException();
            if (installed.Fingerprint != node.Workflow.Fingerprint ||
                node.InputContractKey != DurableDagWire.ChildInputKey(node.Workflow) ||
                node.OutputContractKey != (installed.OutputType is null ? null : DurableDagWire.ChildOutputKey(node.Workflow)))
                throw new DurableDagDefinitionUnavailableException();
        }

        // All user value serialization happens before any append or lease.
        var input = DurableDagValues.Normalize(request.Graph.RunInputContractKey, request.Input, inputType);
        token.ThrowIfCancellationRequested();
        var existing = await FindExistingAsync(request, input, token).ConfigureAwait(false);
        if (existing is not null) return existing;
        var now = time.GetUtcNow().ToUniversalTime();
        var nodes = request.Graph.Nodes.Select(node =>
        {
            var ready = node.DirectDependencies.Count == 0;
            return new DagRunNodeStateV1(node.Ordinal, ready ? DagNodeStateV1.Ready : DagNodeStateV1.Pending,
                null, null, null, null, ready ? now : null, null, null);
        }).ToArray();
        var initial = new DagRunSnapshotV1(request.RunId, new(1), request.StartKey, request.Graph, input,
            DagRunStateV1.Running, nodes, now, now, null);
        var result = await events.AppendAsync(DurableDagWire.Create(initial), token).ConfigureAwait(false);
        if (result.IsSuccess)
            return new(DurableDagCreateOutcomeV1.Created, await ReadAsync(request.RunId, token).ConfigureAwait(false)
                ?? throw new InvalidDataException("The successful DAG creation has no checkpoint."));
        // A concurrent create may have won either unique coordinate. Re-read the
        // authoritative record; do not report an unverified match or hide other failures.
        var winner = await FindExistingAsync(request, input, token).ConfigureAwait(false);
        if (winner is not null) return winner;
        if (result.Error.Code is "DAG_INTEGRITY_CONFLICT" or "DAG_START_KEY_CONFLICT") return Conflict();
        throw result.Error;
    }

    private async ValueTask<DurableDagCreateResultV1?> FindExistingAsync(DurableDagRunCreateV1 request,
        DagRunValueV1 input, CancellationToken token)
    {
        var keyOwner = await events.GetDagRunByStartKeyAsync(request.StartKey, token).ConfigureAwait(false);
        // A supplied run id is a separate unique coordinate: do not hide its collision
        // just because another run owns this start key.
        var byId = await ReadAsync(request.RunId, token).ConfigureAwait(false);
        if (byId is not null && byId.StartKey != request.StartKey) return Conflict(byId, input);
        var saved = keyOwner.HasValue
            ? await ReadAsync(keyOwner.Value.RunId, token).ConfigureAwait(false)
                ?? throw new InvalidDataException("DAG start-key owner has no checkpoint.")
            : byId;
        if (saved is null) return null;
        if (saved.StartKey != request.StartKey)
            throw new InvalidDataException("DAG start-key owner has a different recorded key.");
        if (!SameGraph(saved.Graph, request.Graph) || saved.RunInput.Fingerprint != input.Fingerprint ||
            !saved.RunInput.Bytes.AsSpan().SequenceEqual(input.Bytes))
            return Conflict(saved, input);
        return new(DurableDagCreateOutcomeV1.Existing, saved);
    }

    internal static bool SameGraph(DagRunGraphV1 left, DagRunGraphV1 right) =>
        left.Definition == right.Definition && left.RunInputContractKey == right.RunInputContractKey &&
        left.Nodes.Count == right.Nodes.Count &&
        left.Nodes.Zip(right.Nodes).All(pair =>
            pair.First.Ordinal == pair.Second.Ordinal && pair.First.NodeId == pair.Second.NodeId &&
            pair.First.Workflow == pair.Second.Workflow &&
            pair.First.InputContractKey == pair.Second.InputContractKey &&
            pair.First.OutputContractKey == pair.Second.OutputContractKey &&
            pair.First.DirectDependencies.SequenceEqual(pair.Second.DirectDependencies));

    private static DurableDagCreateResultV1 Conflict(DagRunSnapshotV1? owner = null, DagRunValueV1? input = null) =>
        new(DurableDagCreateOutcomeV1.BindingConflict, owner, input?.Fingerprint);
}
