using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Providers.SqlServer.Internal;

internal sealed class SqlServerWorkflowStore(SqlServerStateDocumentStore documents, TimeProvider timeProvider) :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowStartIdempotencyStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    IWorkflowOperationalStore,
    IWorkflowProviderMaintenanceStore,
    ITimerScheduler
{
    public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.LoadCheckpointAsync(instanceId, cancellationToken), cancellationToken);

    public Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.AppendAsync(batch, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.LoadTailAsync(streamId, afterVersion, cancellationToken), cancellationToken);

    public Task<InboxAcceptanceCommitResult> AcceptAsync(
        InboxAcceptance acceptance,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.AcceptAsync(acceptance, cancellationToken), cancellationToken);

    public Task<InboxAcceptanceCommitResult> AcceptDefinitionFanoutAsync(
        InboxDefinitionFanoutAcceptance acceptance,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.AcceptDefinitionFanoutAsync(acceptance, cancellationToken), cancellationToken);

    public Task<InboxAcceptanceCommitResult> AcceptStartOrDeliverAsync(
        InboxStartOrDeliverAcceptance acceptance,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.AcceptStartOrDeliverAsync(acceptance, cancellationToken), cancellationToken);

    public Task<Option<InboxStartIntentRecord>> GetStartIntentAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.GetStartIntentAsync(startIdempotencyKey, cancellationToken), cancellationToken);

    public Task<Option<InboxRecord>> GetByEventIdAsync(
        EventId eventId,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.GetByEventIdAsync(eventId, cancellationToken), cancellationToken);

    public Task<Option<InboxRecord>> GetAsync(
        InstanceId instanceId,
        EventId eventId,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.GetAsync(instanceId, eventId, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<InboxRecord>> ListDefinitionFanoutTargetsAsync(
        EventId eventId,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.ListDefinitionFanoutTargetsAsync(eventId, cancellationToken), cancellationToken);

    public Task<InboxMatchSnapshot> GetMatchSnapshotAsync(
        InboxMatchRequest request,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.GetMatchSnapshotAsync(request, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<InboxRecord>> ListReceivedAsync(
        long afterAcceptanceSequence,
        int maxCount,
        CancellationToken cancellationToken) =>
        ReadAsync(
            engine => engine.ListReceivedAsync(afterAcceptanceSequence, maxCount, cancellationToken),
            cancellationToken);

    public Task<IReadOnlyList<InboxRecord>> ListHandoffRetriesAsync(
        DateTimeOffset eligibleAt,
        int maxCount,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.ListHandoffRetriesAsync(eligibleAt, maxCount, cancellationToken), cancellationToken);

    public Task MarkPoisonedAsync(
        EventId eventId,
        InboxRecordState expectedState,
        string code,
        string? detail,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            engine => engine.MarkPoisonedAsync(eventId, expectedState, code, detail, cancellationToken),
            cancellationToken);

    public Task MarkPoisonedAsync(
        InboxRecordIdentity recordIdentity,
        InboxRecordState expectedState,
        string code,
        string? detail,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            engine => engine.MarkPoisonedAsync(recordIdentity, expectedState, code, detail, cancellationToken),
            cancellationToken);

    public Task RecordHandoffFailureAsync(
        EventId eventId,
        InboxRecordState expectedState,
        int expectedFailureCount,
        int maxFailureCount,
        DateTimeOffset retryNotBefore,
        string code,
        string? detail,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            engine => engine.RecordHandoffFailureAsync(
                eventId,
                expectedState,
                expectedFailureCount,
                maxFailureCount,
                retryNotBefore,
                code,
                detail,
                cancellationToken),
            cancellationToken);

    public Task RecordHandoffFailureAsync(
        InboxRecordIdentity recordIdentity,
        InboxRecordState expectedState,
        int expectedFailureCount,
        int maxFailureCount,
        DateTimeOffset retryNotBefore,
        string code,
        string? detail,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            engine => engine.RecordHandoffFailureAsync(
                recordIdentity,
                expectedState,
                expectedFailureCount,
                maxFailureCount,
                retryNotBefore,
                code,
                detail,
                cancellationToken),
            cancellationToken);

    public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.GetStartedAsync(idempotencyKey, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        int maxCount,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ClaimAsync(maxCount, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        OutboxClaimRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ClaimAsync(request, cancellationToken), cancellationToken);

    public Task<Option<OutboxRecordState>> GetStateAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.GetStateAsync(outboxRecordId, cancellationToken), cancellationToken);

    public Task<Option<OutboxDispatchSnapshot>> GetDispatchSnapshotAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.GetDispatchSnapshotAsync(outboxRecordId, cancellationToken), cancellationToken);

    public Task MarkAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.MarkAsync(outboxRecordId, state, cancellationToken), cancellationToken);

    public Task MarkPoisonedAsync(
        OutboxRecordId outboxRecordId,
        string code,
        string? detail,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.MarkPoisonedAsync(outboxRecordId, code, detail, cancellationToken), cancellationToken);

    public Task ReleaseAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ReleaseAsync(outboxRecordId, cancellationToken), cancellationToken);

    public Task ApplyAsync(
        IReadOnlyList<ProjectionWrite> operations,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ApplyAsync(operations, cancellationToken), cancellationToken);

    public Task<Option<WorkflowProjectionSnapshot>> GetAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.GetAsync(instanceId, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken) =>
        ReadAsync(
            engine => engine.FindActiveWaitsAsync(definitionId, eventName, correlationId, cancellationToken),
            cancellationToken);

    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        EventContractVersion eventContractVersion,
        CorrelationId correlationId,
        CancellationToken cancellationToken) =>
        ReadAsync(
            engine => engine.FindActiveWaitsAsync(
                definitionId,
                eventName,
                eventContractVersion,
                correlationId,
                cancellationToken),
            cancellationToken);

    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> ListLeaseRecoveryCandidatesAsync(
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.ListLeaseRecoveryCandidatesAsync(cancellationToken), cancellationToken);

    public Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ScheduleAsync(request, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ClaimDueAsync(dueAtOrBefore, maxCount, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ClaimDueAsync(request, cancellationToken), cancellationToken);

    public Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.CompleteAsync(timerId, cancellationToken), cancellationToken);

    public Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ReleaseAsync(timerId, cancellationToken), cancellationToken);

    public Task RefreshStuckStateAsync(
        WorkflowOperatorStatisticsRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.RefreshStuckStateAsync(request, cancellationToken), cancellationToken);

    public Task<WorkflowOperatorStatistics> GetOperatorStatisticsAsync(CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.GetOperatorStatisticsAsync(cancellationToken), cancellationToken);

    public Task<WorkflowProviderMaintenanceInspection> InspectForMaintenanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        ReadAsync(engine => engine.InspectForMaintenanceAsync(instanceId, cancellationToken), cancellationToken);

    public Task<WorkflowProviderMaintenanceResult> ArchiveForMaintenanceAsync(
        WorkflowProviderMaintenanceRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ArchiveForMaintenanceAsync(request, cancellationToken), cancellationToken);

    public Task<WorkflowProviderMaintenanceResult> PurgeForMaintenanceAsync(
        WorkflowProviderMaintenanceRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.PurgeForMaintenanceAsync(request, cancellationToken), cancellationToken);

    private Task<TResult> ExecuteAsync<TResult>(
        Func<SqlServerWorkflowStateEngine, Task<TResult>> operation,
        CancellationToken cancellationToken) =>
        documents.ExecuteAsync(
            SqlServerStateDocumentStore.WorkflowStateKey,
            Restore,
            operation,
            Capture,
            cancellationToken);

    private Task<TResult> ReadAsync<TResult>(
        Func<SqlServerWorkflowStateEngine, Task<TResult>> operation,
        CancellationToken cancellationToken) =>
        documents.ReadAsync(
            SqlServerStateDocumentStore.WorkflowStateKey,
            Restore,
            operation,
            cancellationToken);

    private Task ExecuteAsync(
        Func<SqlServerWorkflowStateEngine, Task> operation,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            async engine =>
            {
                await operation(engine).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    private SqlServerWorkflowStateEngine Restore(string? payload) =>
        payload is null
            ? new SqlServerWorkflowStateEngine(timeProvider)
            : new SqlServerWorkflowStateEngine(
                JsonSerializer.Deserialize<SqlServerWorkflowStateEngine.StateSnapshot>(
                    payload,
                    SqlServerStateDocumentStore.JsonOptions)
                ?? throw new InvalidOperationException("The SQL Server workflow state document was empty."),
                timeProvider);

    private static string Capture(SqlServerWorkflowStateEngine engine) =>
        JsonSerializer.Serialize(engine.Capture(), SqlServerStateDocumentStore.JsonOptions);
}
