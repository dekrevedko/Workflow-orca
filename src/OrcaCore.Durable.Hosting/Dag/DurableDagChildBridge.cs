using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable;
using OrcaCore.Engine.Durable.Dag;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed record DurableDagInputReceiptV1(
    DurableDagNodeIdentityV1 Identity, InstanceId ChildInstanceId, PayloadFingerprint MappedInputFingerprint);

internal abstract record DurableDagInputCommitResultV1
{
    private protected DurableDagInputCommitResultV1() { }
    internal sealed record Committed(DurableDagInputReceiptV1 Receipt, bool WasExisting) : DurableDagInputCommitResultV1;
    internal sealed record MappingInvalid : DurableDagInputCommitResultV1;
    internal sealed record AdmissionStopped : DurableDagInputCommitResultV1;
    internal sealed record CapacityDeferred : DurableDagInputCommitResultV1;
}

internal abstract record DurableDagChildStartResultV1
{
    private protected DurableDagChildStartResultV1() { }
    internal sealed record Started(InstanceId ChildInstanceId, bool WasExisting) : DurableDagChildStartResultV1;
    internal sealed record CancelledBeforeStart : DurableDagChildStartResultV1;
}

internal sealed record DurableDagChildSnapshotV1(
    InstanceId ChildInstanceId, InstanceId RootInstanceId, InstanceId ParentInstanceId,
    WorkflowInstanceStatus Status, bool HasOutput, WorkflowFailure? Failure);

internal interface IDurableDagChildBridgeV1
{
    ValueTask<DurableDagMappingInputsV1> ReadMappingInputsAsync(DurableDagNodeContractV1 node, CancellationToken cancellationToken);
    ValueTask<DurableDagInputCommitResultV1> CommitMappedInputAsync(DurableDagNodeContractV1 node,
        DurableDagMappedInputV1 input, CancellationToken cancellationToken);
    ValueTask<DurableDagChildStartResultV1> StartOrReattachAsync(DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
    ValueTask<DurableDagChildSnapshotV1?> GetChildAsync(DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
    ValueTask<DurableDagChildSnapshotV1> WaitForChildTerminalAsync(DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
    ValueTask<WorkflowCancellationRequestStatus> RequestChildCancellationAsync(DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
}

internal sealed class DurableDagChildBridge(DurableDagRunStore runs, DurableDagMappingReader mapping,
    DurableDagAdmission admission, DurableDagChildEngine children, DurableFacadeNotificationHub notifications,
    DurableDagNotifications runNotifications, DurableDagRunTurns turns, DurableDagTurnV1? turn,
    DurableDagPreparedMappingV1? prepared, int maxConcurrentNodes)
    : IDurableDagChildBridgeV1
{
    public ValueTask<DurableDagMappingInputsV1> ReadMappingInputsAsync(DurableDagNodeContractV1 node, CancellationToken cancellationToken)
    {
        if (turn is not null) throw WrongMode();
        return mapping.ReadMappingInputsAsync(node, cancellationToken);
    }

    public async ValueTask<DurableDagInputCommitResultV1> CommitMappedInputAsync(DurableDagNodeContractV1 node,
        DurableDagMappedInputV1 input, CancellationToken cancellationToken)
    {
        if (turn is null || prepared is null) throw WrongMode();
        if (node.Identity != prepared.Node.Identity || node.Workflow != prepared.Node.Workflow ||
            !node.DirectDependencies.SequenceEqual(prepared.Node.DirectDependencies) ||
            !ReferenceEquals(input, prepared.SourceInput))
            throw new InvalidDataException("The bound DAG mapping receipt does not match the requested commit.");
        if (await turns.IsActiveAsync(turn, cancellationToken).ConfigureAwait(false) == false)
            throw new WorkflowConcurrencyException("The captured DAG turn is no longer active.");
        var result = await admission.AdmitPreparedMappingAsync(prepared, turn.Fence, turn.Version,
            maxConcurrentNodes, cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            DurableDagMutationOutcomeV1.MappingInvalid => new DurableDagInputCommitResultV1.MappingInvalid(),
            DurableDagMutationOutcomeV1.AdmissionStopped => new DurableDagInputCommitResultV1.AdmissionStopped(),
            DurableDagMutationOutcomeV1.CapacityDeferred => new DurableDagInputCommitResultV1.CapacityDeferred(),
            DurableDagMutationOutcomeV1.Committed or DurableDagMutationOutcomeV1.Existing => Committed(node.Identity, result),
            DurableDagMutationOutcomeV1.VersionConflict or DurableDagMutationOutcomeV1.StaleFence =>
                throw new WorkflowConcurrencyException("The captured DAG turn or version is stale."),
            _ => throw new InvalidDataException("The DAG mapping receipt cannot be committed to this run.")
        };
    }

    public async ValueTask<DurableDagChildStartResultV1> StartOrReattachAsync(DurableDagNodeIdentityV1 node,
        CancellationToken cancellationToken)
    {
        await RequireReleasedAsync(cancellationToken).ConfigureAwait(false);
        var run = await LoadAsync(node, cancellationToken).ConfigureAwait(false);
        var state = run.Nodes[node.AuthoredOrdinal];
        if (state.ChildInstanceId is null)
        {
            if (state.State == DagNodeStateV1.Cancelled) return new DurableDagChildStartResultV1.CancelledBeforeStart();
            throw new InvalidOperationException("DAG child start was not authorized.");
        }
        var existing = await children.ReadBoundStartAsync(run, node.AuthoredOrdinal, cancellationToken).ConfigureAwait(false);
        if (!await children.StartOrReattachAsync(run, node.AuthoredOrdinal, cancellationToken).ConfigureAwait(false))
            throw new WorkflowConcurrencyException("The authorized child is not available to start on this host.");
        return new DurableDagChildStartResultV1.Started(state.ChildInstanceId, existing is not null);
    }

    public async ValueTask<DurableDagChildSnapshotV1?> GetChildAsync(DurableDagNodeIdentityV1 node,
        CancellationToken cancellationToken)
    {
        await RequireReleasedAsync(cancellationToken).ConfigureAwait(false);
        var run = await LoadAsync(node, cancellationToken).ConfigureAwait(false);
        if (run.Nodes[node.AuthoredOrdinal].ChildInstanceId is null) return null;
        var observed = await children.ObserveAsync(run, node.AuthoredOrdinal, cancellationToken).ConfigureAwait(false);
        return observed is null ? null : Snapshot(observed);
    }

    public async ValueTask<DurableDagChildSnapshotV1> WaitForChildTerminalAsync(DurableDagNodeIdentityV1 node,
        CancellationToken cancellationToken)
    {
        await RequireReleasedAsync(cancellationToken).ConfigureAwait(false);
        var run = await LoadAsync(node, cancellationToken).ConfigureAwait(false);
        var child = run.Nodes[node.AuthoredOrdinal].ChildInstanceId
            ?? throw new InvalidOperationException("DAG child start was not authorized.");
        while (true)
        {
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnCommit(InstanceId id) { if (id == child) signal.TrySetResult(); }
            notifications.InstanceCommitted += OnCommit;
            try
            {
                var observed = await children.ObserveAsync(run, node.AuthoredOrdinal, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("DAG child has not materialized.");
                if (observed.IsTerminal) return Snapshot(observed);
                // A remote child commit is visible on this host via its durable join.
                using var round = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var remote = runNotifications.WaitForChangeAsync(run.RunId, run.StreamVersion, round.Token).AsTask();
                try
                {
                    if (await Task.WhenAny(signal.Task, remote).ConfigureAwait(false) == remote)
                        run = await remote.ConfigureAwait(false);
                }
                finally
                {
                    await round.CancelAsync().ConfigureAwait(false);
                    try { await remote.ConfigureAwait(false); }
                    catch (OperationCanceledException) when (round.IsCancellationRequested) { }
                }
            }
            finally { notifications.InstanceCommitted -= OnCommit; }
        }
    }

    public async ValueTask<WorkflowCancellationRequestStatus> RequestChildCancellationAsync(DurableDagNodeIdentityV1 node,
        CancellationToken cancellationToken)
    {
        await RequireReleasedAsync(cancellationToken).ConfigureAwait(false);
        var run = await LoadAsync(node, cancellationToken).ConfigureAwait(false);
        if (run.State is not (DagRunStateV1.CancellationRequested or DagRunStateV1.Cancelled))
            throw new InvalidOperationException("Child cancellation requires a committed DAG cancellation barrier.");
        if (run.Nodes[node.AuthoredOrdinal].ChildInstanceId is null) return WorkflowCancellationRequestStatus.AlreadyTerminal;
        return await children.TryCancelAsync(run, node.AuthoredOrdinal, cancellationToken).ConfigureAwait(false)
            ?? throw new WorkflowConcurrencyException("The authorized child is not available to cancel on this host.");
    }

    private async ValueTask RequireReleasedAsync(CancellationToken token)
    {
        if (turn is null || await turns.IsActiveAsync(turn, token).ConfigureAwait(false)) throw WrongMode();
    }

    private async ValueTask<DagRunSnapshotV1> LoadAsync(DurableDagNodeIdentityV1 node, CancellationToken token)
    {
        if (prepared?.Node.Identity != node)
            throw new InvalidDataException("The child adapter is bound to a different run and node.");
        var run = await runs.ReadAsync(node.RunId, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("DAG run does not exist.");
        if ((uint)node.AuthoredOrdinal >= (uint)run.Nodes.Count)
            throw new InvalidDataException("DAG child is outside the persisted plan.");
        return run;
    }

    private static DurableDagInputCommitResultV1 Committed(DurableDagNodeIdentityV1 identity, DurableDagMutationResultV1 result)
    {
        var saved = result.Snapshot!.Nodes[identity.AuthoredOrdinal];
        return new DurableDagInputCommitResultV1.Committed(new(identity, saved.ChildInstanceId!,
            DurableDagChildEngine.RehydrateFingerprint(saved.Input!.Fingerprint)), result.Outcome == DurableDagMutationOutcomeV1.Existing);
    }

    private static DurableDagChildSnapshotV1 Snapshot(DurableDagChildObservation child) =>
        new(child.ChildId, child.RootId, child.ParentId, child.Status, child.HasOutput, child.Failure);

    private static InvalidOperationException WrongMode() =>
        new("This DAG child operation is not permitted by the adapter's evaluation or turn scope.");
}
