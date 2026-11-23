using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Durable.Hosting.Dag;

namespace OrcaCore.Dag.Hosting;

// Projects durable facts through the public API, runtime view and two handle constructors.
// Opaque conflict, failure and fingerprint values come from the durable side.
internal sealed class DagFacadeRegistry(DagDefinitionRegistry definitions, DurableDagFacade facade) : IDagDefinitionRegistry
{
    public DagRegistrationResult<TRunInput> Register<TRunInput>(WorkflowDagPlan<TRunInput> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var binding = new DagRunBindingV1(plan.DefinitionId, plan.DefinitionVersion, plan.DefinitionFingerprint.Value);
        var attempt = facade.TryInstall(binding, plan.DefinitionFingerprint, typeof(TRunInput));
        if (attempt.Conflict is { } conflict) return new DagRegistrationResult<TRunInput>.Conflict(conflict);
        if (attempt.ExistingType is { } existing)
            return new DagRegistrationResult<TRunInput>.RunInputConflict(
                plan.DefinitionId, plan.DefinitionVersion, plan.DefinitionFingerprint, existing, typeof(TRunInput));
        var installed = definitions.Install(plan);

        DagRunHandle Open(Guid id) => new(
            DagRunId.Parse(id.ToString("D")),
            plan.Nodes,
            async token => Project(plan, installed, await ReadAsync(id, token).ConfigureAwait(false)),
            async (node, token) =>
            {
                var run = await ReadAsync(id, token).ConfigureAwait(false);
                var ordinal = IndexOf(plan.Nodes, node);
                var state = run.Nodes[ordinal];
                var output = facade.ReadOutput(run, ordinal, installed.Nodes[ordinal].Workflow.OutputType);
                return (Status(state.State), output.HasOutput, output.Output, DurableDagFacade.NodeFailure(state));
            },
            async token => await facade.WaitForTerminalAsync(id, token).ConfigureAwait(false) is { } terminal
                ? Project(plan, installed, terminal) : null,
            async token => await facade.CancelAsync(id, token).ConfigureAwait(false) switch
            {
                null => null,
                DurableDagCancelOutcomeV1.Requested => DagCancellationRequestStatus.Requested,
                DurableDagCancelOutcomeV1.AlreadyRequested => DagCancellationRequestStatus.AlreadyRequested,
                DurableDagCancelOutcomeV1.AlreadyTerminal => DagCancellationRequestStatus.AlreadyTerminal,
                _ => throw new InvalidOperationException("The DAG run disappeared after it was opened.")
            });

        return new DagRegistrationResult<TRunInput>.Registered(new DagDefinitionHandle<TRunInput>(
            plan.DefinitionId,
            plan.DefinitionVersion,
            plan.DefinitionFingerprint,
            async (input, key, token) =>
            {
                var started = await facade.StartOrGetAsync(installed.Graph, plan.DefinitionFingerprint,
                    input, typeof(TRunInput), key, token).ConfigureAwait(false);
                if (started.RunInputContractDiffers)
                    return new DagStartResult.RunInputConflict(DagRunId.Parse(started.RunId.ToString("D")),
                        plan.DefinitionId, plan.DefinitionVersion, plan.DefinitionFingerprint, typeof(TRunInput));
                return started.Conflict is { } startConflict
                    ? new DagStartResult.Conflict(startConflict)
                    : new DagStartResult.Accepted(Open(started.RunId), started.WasExisting);
            },
            async (runId, token) =>
            {
                var found = await facade.FindRunAsync(runId.Value, installed.Graph, typeof(TRunInput), token).ConfigureAwait(false);
                if (!found.Found) return (null, null, null, null, false);
                var actual = found.Definition!;
                if (actual != binding)
                    return (null, actual.DefinitionId, actual.DefinitionVersion, DurableDagFacade.Fingerprint(actual.Fingerprint), false);
                return found.RunInputContractMatches
                    ? (Open(runId.Value), null, null, null, false)
                    : (null, actual.DefinitionId, actual.DefinitionVersion, plan.DefinitionFingerprint, true);
            }));

        async ValueTask<DagRunSnapshotV1> ReadAsync(Guid id, CancellationToken token) =>
            await facade.ReadAsync(id, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The DAG run disappeared after it was opened.");
    }

    private static DagRunSnapshot Project<TRunInput>(WorkflowDagPlan<TRunInput> plan, InstalledDagPlan installed,
        DagRunSnapshotV1 run) =>
        new(DagRunId.Parse(run.RunId.ToString("D")), plan.DefinitionId, plan.DefinitionVersion, plan.DefinitionFingerprint,
            DurableDagFacade.PublicState(run) switch
            {
                DagRunStateV1.Running => DagRunStatus.Running,
                DagRunStateV1.CancellationRequested => DagRunStatus.CancellationRequested,
                DagRunStateV1.Succeeded => DagRunStatus.Succeeded,
                DagRunStateV1.Failed => DagRunStatus.Failed,
                DagRunStateV1.Cancelled => DagRunStatus.Cancelled,
                _ => throw new InvalidDataException("Unknown committed DAG run state.")
            },
            run.CreatedAt, run.State == DagRunStateV1.IntegrityFault ? null : run.CompletedAt,
            Array.AsReadOnly(run.Nodes.Select(node => new DagNodeSnapshot(
                plan.Nodes[node.Ordinal].NodeId, node.Ordinal, Status(node.State),
                Array.AsReadOnly(installed.Nodes[node.Ordinal].Dependencies.Select(d => plan.Nodes[d].NodeId).ToArray()),
                node.ChildInstanceId, DurableDagFacade.InputFingerprint(node),
                node.State == DagNodeStateV1.Succeeded && node.Output is not null,
                node.ReadyAt, node.StartedAt, node.CompletedAt, DurableDagFacade.NodeFailure(node))).ToArray()));

    private static DagNodeStatus Status(DagNodeStateV1 state) => state switch
    {
        DagNodeStateV1.Pending => DagNodeStatus.Pending,
        DagNodeStateV1.Ready => DagNodeStatus.Ready,
        DagNodeStateV1.StartAuthorized => DagNodeStatus.Running,
        DagNodeStateV1.CancellationRequested => DagNodeStatus.CancellationRequested,
        DagNodeStateV1.Succeeded => DagNodeStatus.Succeeded,
        DagNodeStateV1.Failed => DagNodeStatus.Failed,
        DagNodeStateV1.Cancelled => DagNodeStatus.Cancelled,
        DagNodeStateV1.DependencyBlocked => DagNodeStatus.DependencyBlocked,
        _ => throw new InvalidDataException("Unknown committed DAG node state.")
    };

    private static int IndexOf(IReadOnlyList<DagNodeRef> nodes, DagNodeRef node)
    {
        for (var index = 0; index < nodes.Count; index++)
            if (ReferenceEquals(nodes[index], node)) return index;
        throw new ArgumentException("The node reference belongs to another DAG plan.", nameof(node));
    }
}
