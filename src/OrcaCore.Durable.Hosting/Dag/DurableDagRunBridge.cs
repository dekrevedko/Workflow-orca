using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Engine.Durable;
using OrcaCore.Engine.Durable.Dag;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed record DurableDagRecoverableRun(long CreationSequence, Guid RunId, StreamVersion StreamVersion,
    DagRunBindingV1 Definition);

internal interface IDurableDagRunBridgeV1
{
    ValueTask<DurableDagTurnResultV1> BeginTurnAsync(DurableDagTurnRequestV1 request, CancellationToken cancellationToken);
    ValueTask<DurableDagTurnResultV1> RenewTurnAsync(DurableDagTurnV1 turn, CancellationToken cancellationToken);
    ValueTask EndTurnAsync(DurableDagTurnV1 turn, CancellationToken cancellationToken);
    ValueTask AbandonTurnRequestAsync(DurableDagTurnRequestV1 request, CancellationToken cancellationToken);
    IDurableDagChildBridgeV1 OpenChildBridgeForEvaluation();
    ValueTask<DurableDagPreparedMappingV1> PrepareMappingAsync(DurableDagNodeContractV1 node,
        DurableDagMappedInputV1? input, bool mappingInvalid, CancellationToken cancellationToken);
    IDurableDagChildBridgeV1 OpenChildBridgeForTurn(DurableDagTurnV1 turn, DurableDagPreparedMappingV1 prepared, int maxConcurrentNodes);
    ValueTask<DurableDagCreateResultV1> CreateOrReopenAsync(DurableDagRunCreateV1 run, CancellationToken cancellationToken);
    ValueTask<DagRunSnapshotV1?> ReadAsync(Guid runId, CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> AdmitPreparedMappingAsync(DurableDagPreparedMappingV1 prepared,
        DagRunFenceV1 fence, StreamVersion expectedVersion, int maxConcurrentNodes, CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> ApplyChildTerminalAsync(Guid runId, int ordinal, CancellationToken cancellationToken);
    ValueTask<DurableDagCancelResultV1> RequestRunCancellationAsync(Guid runId, CancellationToken cancellationToken);
    ValueTask<DagRunSnapshotV1> WaitForChangeAsync(Guid runId, StreamVersion afterVersion, CancellationToken cancellationToken);
    ValueTask<DagRunSnapshotV1> WaitForTerminalAsync(Guid runId, CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> RecordIntegrityFaultAsync(Guid runId, string code, DagRunFenceV1 fence,
        StreamVersion expectedVersion, CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> ReconcilePoisonAsync(Guid runId, Guid operationId, DagRunFenceV1 fence,
        StreamVersion expectedVersion, CancellationToken cancellationToken);
}

// Dag.Hosting consumes this internal composition seam, never the provider store.
internal sealed class DurableDagRunBridge(DurableDagRunStore runs, DurableDagRunInputCatalog inputs,
    DurableDagMappingReader mapping, DurableDagAdmission admission, DurableDagRunTurns turns,
    DurableDagRunCancellation cancellation, DurableDagNotifications notifications, DurableDagChildEngine children,
    DurableFacadeNotificationHub childNotifications, DurableDagChildCoordinator childCoordinator, DurableDagIntegrity integrity)
    : IDurableDagRunBridgeV1
{
    internal string InstallRunInput(DagRunBindingV1 binding, Type type) => inputs.Install(binding, type);
    public ValueTask<DurableDagCreateResultV1> CreateOrReopenAsync(DurableDagRunCreateV1 run, CancellationToken token) =>
        runs.CreateOrReopenAsync(run, token);
    internal bool MatchesInstalledGraph(DagRunGraphV1 committed, DagRunGraphV1 installed) =>
        DurableDagRunStore.SameGraph(committed, installed);
    public ValueTask<DagRunSnapshotV1?> ReadAsync(Guid id, CancellationToken token) => runs.ReadAsync(id, token);
    internal ValueTask<IReadOnlyList<DurableDagRecoverableRun>> DiscoverAsync(long afterSequence, int maxCount, CancellationToken token) =>
        runs.DiscoverAsync(afterSequence, maxCount, token);
    public IDurableDagChildBridgeV1 OpenChildBridgeForEvaluation() =>
        new DurableDagChildBridge(runs, mapping, admission, children, childNotifications, notifications, turns, null, null, 0);
    public IDurableDagChildBridgeV1 OpenChildBridgeForTurn(DurableDagTurnV1 turn,
        DurableDagPreparedMappingV1 prepared, int maxConcurrentNodes)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentNodes, 1);
        return new DurableDagChildBridge(runs, mapping, admission, children, childNotifications,
            notifications, turns, turn, prepared, maxConcurrentNodes);
    }
    public ValueTask<DurableDagPreparedMappingV1> PrepareMappingAsync(DurableDagNodeContractV1 node,
        DurableDagMappedInputV1? input, bool invalid, CancellationToken token) =>
        admission.PrepareMappingAsync(node, input, invalid, token);
    public ValueTask<DurableDagTurnResultV1> BeginTurnAsync(DurableDagTurnRequestV1 request, CancellationToken token) =>
        turns.BeginTurnAsync(request, token);
    public ValueTask<DurableDagTurnResultV1> RenewTurnAsync(DurableDagTurnV1 turn, CancellationToken token) =>
        turns.RenewTurnAsync(turn, token);
    public ValueTask EndTurnAsync(DurableDagTurnV1 turn, CancellationToken token) => turns.EndTurnAsync(turn, token);
    public ValueTask AbandonTurnRequestAsync(DurableDagTurnRequestV1 request, CancellationToken token) =>
        turns.AbandonTurnRequestAsync(request, token);
    internal ValueTask<DurableDagMutationResultV1> AdmitPreparedMappingAsync(DurableDagPreparedMappingV1 prepared,
        DurableDagTurnV1 turn, int limit, CancellationToken token) =>
        AdmitPreparedMappingAsync(prepared, turn.Fence, turn.Version, limit, token);
    public ValueTask<DurableDagMutationResultV1> AdmitPreparedMappingAsync(DurableDagPreparedMappingV1 prepared,
        DagRunFenceV1 fence, StreamVersion expectedVersion, int maxConcurrentNodes, CancellationToken token) =>
        admission.AdmitPreparedMappingAsync(prepared, fence, expectedVersion, maxConcurrentNodes, token);
    public ValueTask<DurableDagMutationResultV1> ApplyChildTerminalAsync(Guid runId, int ordinal, CancellationToken token) =>
        childCoordinator.ApplyChildTerminalAsync(runId, ordinal, token);
    public ValueTask<DurableDagCancelResultV1> RequestRunCancellationAsync(Guid id, CancellationToken token) =>
        cancellation.RequestRunCancellationAsync(id, token);
    public ValueTask<DagRunSnapshotV1> WaitForChangeAsync(Guid id, StreamVersion after, CancellationToken token) =>
        notifications.WaitForChangeAsync(id, after, token);
    public ValueTask<DagRunSnapshotV1> WaitForTerminalAsync(Guid id, CancellationToken token) =>
        notifications.WaitForTerminalAsync(id, token);
    public ValueTask<DurableDagMutationResultV1> RecordIntegrityFaultAsync(Guid runId, string code, DagRunFenceV1 fence,
        StreamVersion expectedVersion, CancellationToken token) =>
        integrity.RecordIntegrityFaultAsync(runId, code, fence, expectedVersion, token);
    public ValueTask<DurableDagMutationResultV1> ReconcilePoisonAsync(Guid runId, Guid operationId, DagRunFenceV1 fence,
        StreamVersion expectedVersion, CancellationToken token) =>
        integrity.ReconcilePoisonAsync(runId, operationId, fence, expectedVersion, token);
}
