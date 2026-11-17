// Host side of the RV-1 proof: everything below uses public OrcaCore/OrcaCore.Dag API plus
// exactly the two internal handle constructors. Conflict, failure and fingerprint values
// arrive from the durable bridge as opaque public objects; the host never constructs them,
// and it constructs no DAG exception.
using OrcaCore;
using OrcaCore.Dag;
using Proof.Dag;

namespace Proof.Dag.Hosting;

// Stand-in for the existing Durable.Hosting -> Dag.Hosting bridge.
internal interface IDurableDagFacadeBridge
{
    // The durable side owns the run-input contract key (fixed-codec shape digest) for a declared type.
    (DefinitionRegistrationConflict? Conflict, Type? ExistingRunInputType) TryInstall(
        DefinitionId id, DefinitionVersion version, DefinitionFingerprint fingerprint, Type runInputType);

    ValueTask<(Guid RunId, bool WasExisting, StartIdempotencyConflict? Conflict, bool RunInputContractDiffers)> StartOrGetAsync(
        DefinitionId id, DefinitionVersion version, DefinitionFingerprint fingerprint,
        object? input, Type runInputType, StartIdempotencyKey key, CancellationToken token);

    ValueTask<(bool Found, DefinitionId? Id, DefinitionVersion? Version, DefinitionFingerprint? Fingerprint,
        bool RunInputContractMatches)> FindRunAsync(Guid runId, Type runInputType, CancellationToken token);

    ValueTask<DagRunSnapshot> ReadAsync(Guid runId, CancellationToken token);
    ValueTask<DagRunSnapshot?> WaitForTerminalAsync(Guid runId, CancellationToken token);
    ValueTask<(DagNodeStatus Status, bool HasOutput, object? Output, WorkflowFailure? Failure)> ReadOutputAsync(
        Guid runId, int ordinal, CancellationToken token);
    ValueTask<DagCancellationRequestStatus?> CancelAsync(Guid runId, CancellationToken token);
}

internal sealed class DagDefinitionRegistry(IDurableDagFacadeBridge bridge) : IDagDefinitionRegistry
{
    public DagRegistrationResult<TRunInput> Register<TRunInput>(WorkflowDagPlan<TRunInput> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var installed = bridge.TryInstall(
            plan.DefinitionId, plan.DefinitionVersion, plan.DefinitionFingerprint, typeof(TRunInput));
        if (installed.Conflict is { } conflict)
            return new DagRegistrationResult<TRunInput>.Conflict(conflict);
        if (installed.ExistingRunInputType is { } existing)
            return new DagRegistrationResult<TRunInput>.RunInputConflict(
                plan.DefinitionId, plan.DefinitionVersion, plan.DefinitionFingerprint, existing, typeof(TRunInput));

        DagRunHandle Open(Guid id) => new(
            DagRunId.Parse(id.ToString("D")),
            plan.Nodes,
            token => bridge.ReadAsync(id, token),
            (node, token) => bridge.ReadOutputAsync(id, IndexOf(plan.Nodes, node), token),
            token => bridge.WaitForTerminalAsync(id, token),
            token => bridge.CancelAsync(id, token));

        return new DagRegistrationResult<TRunInput>.Registered(new DagDefinitionHandle<TRunInput>(
            plan.DefinitionId,
            plan.DefinitionVersion,
            plan.DefinitionFingerprint,
            async (input, key, token) =>
            {
                var started = await bridge.StartOrGetAsync(plan.DefinitionId, plan.DefinitionVersion,
                    plan.DefinitionFingerprint, input, typeof(TRunInput), key, token).ConfigureAwait(false);
                if (started.RunInputContractDiffers) return null;
                return started.Conflict is { } startConflict
                    ? new DagStartResult.Conflict(startConflict)
                    : new DagStartResult.Accepted(Open(started.RunId), started.WasExisting);
            },
            async (runId, token) =>
            {
                var found = await bridge.FindRunAsync(runId.Value, typeof(TRunInput), token).ConfigureAwait(false);
                if (!found.Found) return (null, null, null, null, false);
                var sameDefinition = found.Id!.Equals(plan.DefinitionId) && found.Version!.Equals(plan.DefinitionVersion) &&
                    found.Fingerprint!.Equals(plan.DefinitionFingerprint);
                if (!sameDefinition) return (null, found.Id, found.Version, found.Fingerprint, false);
                return found.RunInputContractMatches
                    ? (Open(runId.Value), null, null, null, false)
                    : (null, found.Id, found.Version, found.Fingerprint, true);
            }));
    }

    private static int IndexOf(IReadOnlyList<DagNodeRef> nodes, DagNodeRef node)
    {
        for (var index = 0; index < nodes.Count; index++)
            if (ReferenceEquals(nodes[index], node)) return index;
        throw new ArgumentException("The node reference belongs to another DAG plan.", nameof(node));
    }
}

// The application journey from doc 17 compiles cast-free against the proposed surface.
internal static class Journey
{
    internal static async ValueTask<DagRunSnapshot> RunAsync<TRunInput, TOutput>(
        IDagDefinitionRegistry registry, WorkflowDagPlan<TRunInput> plan, DagNodeRef<TOutput> node,
        TRunInput input, CancellationToken token)
    {
        var definition = registry.Register(plan).GetHandleOrThrow();
        var start = await definition.StartOrGetAsync(input, StartIdempotencyKey.Create("dag-start"), token);
        var run = start.GetHandleOrThrow();
        var reopened = await definition.GetRunAsync(run.RunId, token);
        _ = await reopened.GetSnapshotAsync(token);
        _ = await run.GetOutputAsync(node, token);
        _ = await run.RequestCancellationAsync(token);
        return await run.WaitForTerminalAsync(token);
    }
}
