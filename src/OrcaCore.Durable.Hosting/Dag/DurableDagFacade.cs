using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Engine.Durable.Dag;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed record DurableDagFacadeStart(
    Guid RunId, bool WasExisting, StartIdempotencyConflict? Conflict, bool RunInputContractDiffers);

internal sealed record DurableDagFacadeLookup(bool Found, DagRunBindingV1? Definition, bool RunInputContractMatches);

// Durable side of the public DAG facade. Everything the hosting
// assembly cannot construct or decide (conflict, failure and fingerprint values, the
// run-input contract, start-key ownership, the pre-fault status) lives here.
internal sealed class DurableDagFacade(DurableDagRunStore runs, DurableDagRunInputCatalog catalog,
    DurableDagNotifications notifications, DurableDagRunCancellation cancellation)
{
    internal (string? Key, DefinitionRegistrationConflict? Conflict, Type? ExistingType) TryInstall(
        DagRunBindingV1 binding, DefinitionFingerprint attempted, Type inputType)
    {
        var installed = catalog.TryInstall(binding, inputType);
        if (installed.Key is not null) return (installed.Key, null, null);
        return installed.ExistingFingerprint != binding.Fingerprint
            ? (null, DurableDagValues.RegistrationConflict(binding.DefinitionId, binding.DefinitionVersion,
                installed.ExistingFingerprint!, attempted), null)
            : (null, null, installed.ExistingType);
    }

    internal async ValueTask<DurableDagFacadeStart> StartOrGetAsync(DagRunGraphV1 graph, DefinitionFingerprint fingerprint,
        object? input, Type inputType, StartIdempotencyKey key, CancellationToken token)
    {
        var created = await runs.CreateOrReopenAsync(
            new(Guid.CreateVersion7(), new(key.Value), graph, input, inputType), token).ConfigureAwait(false);
        var saved = created.Snapshot ?? throw new InvalidDataException("The installed DAG start binding is inconsistent.");
        if (created.Outcome != DurableDagCreateOutcomeV1.BindingConflict)
            return new(saved.RunId, created.Outcome == DurableDagCreateOutcomeV1.Existing, null, false);
        if (saved.Graph.Definition == graph.Definition && saved.Graph.RunInputContractKey != graph.RunInputContractKey)
            return new(saved.RunId, false, null, true);
        return new(saved.RunId, false, DurableDagValues.StartConflict(key, saved.Graph.Definition,
            saved.RunInput.Fingerprint, graph.Definition.DefinitionId, graph.Definition.DefinitionVersion,
            fingerprint, created.AttemptedInputFingerprint!), false);
    }

    internal async ValueTask<DurableDagFacadeLookup> FindRunAsync(Guid runId, DagRunGraphV1 installed, Type inputType,
        CancellationToken token)
    {
        var saved = await runs.ReadAsync(runId, token).ConfigureAwait(false);
        if (saved is null) return new(false, null, false);
        return new(true, saved.Graph.Definition,
            saved.Graph.Definition == installed.Definition && MatchesRunInput(saved, installed, inputType));
    }

    // Key equality is necessary only: the stored bytes must also decode with the installed
    // type and re-encode to themselves. The run input is immutable, so the verdict is cached.
    internal bool MatchesRunInput(DagRunSnapshotV1 saved, DagRunGraphV1 installed, Type inputType) =>
        catalog.Matches(saved, installed.RunInputContractKey, inputType);

    internal ValueTask<DagRunSnapshotV1?> ReadAsync(Guid runId, CancellationToken token) => runs.ReadAsync(runId, token);

    // A fault record overwrites the run state; the prior status is exact from node states.
    internal static DagRunStateV1 PublicState(DagRunSnapshotV1 run) =>
        run.State != DagRunStateV1.IntegrityFault ? run.State
        : run.Nodes.Any(node => node.State is DagNodeStateV1.CancellationRequested or DagNodeStateV1.Cancelled)
            ? DagRunStateV1.CancellationRequested : DagRunStateV1.Running;

    // Level-triggered: null means this recheck observed a faulted checkpoint.
    internal async ValueTask<DagRunSnapshotV1?> WaitForTerminalAsync(Guid runId, CancellationToken token)
    {
        var snapshot = await runs.ReadAsync(runId, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("DAG run does not exist.");
        while (true)
        {
            if (snapshot.State == DagRunStateV1.IntegrityFault) return null;
            if (snapshot.State is DagRunStateV1.Succeeded or DagRunStateV1.Failed or DagRunStateV1.Cancelled) return snapshot;
            snapshot = await notifications.WaitForChangeAsync(runId, snapshot.StreamVersion, token).ConfigureAwait(false);
        }
    }

    internal async ValueTask<DurableDagCancelOutcomeV1?> CancelAsync(Guid runId, CancellationToken token)
    {
        var result = await cancellation.RequestRunCancellationAsync(runId, token).ConfigureAwait(false);
        return result.Outcome == DurableDagCancelOutcomeV1.Faulted ? null : result.Outcome;
    }

    internal (bool HasOutput, object? Output) ReadOutput(DagRunSnapshotV1 run, int ordinal, Type? outputType)
    {
        var node = run.Nodes[ordinal];
        if (node.State != DagNodeStateV1.Succeeded || outputType is null || node.Output is null) return (false, null);
        var bytes = DurableDagValues.VerifiedBytes(node.Output, DurableDagWire.ChildOutputKey(run.Graph.Nodes[ordinal].Workflow));
        return (true, DurableDagValues.DecodeVerified(bytes, outputType));
    }

    internal static WorkflowFailure? NodeFailure(DagRunNodeStateV1 node) =>
        node.State is DagNodeStateV1.Failed or DagNodeStateV1.DependencyBlocked && node.FailureCode is not null
            ? DurableDagValues.NodeFailure(node.FailureCode!, node.Ordinal) : null;

    internal static PayloadFingerprint? InputFingerprint(DagRunNodeStateV1 node) =>
        node.Input is null ? null : DurableDagValues.PublicFingerprint(node.Input.Fingerprint);

    internal static DefinitionFingerprint Fingerprint(string value) => DurableDagValues.DefinitionFingerprint(value);
}
