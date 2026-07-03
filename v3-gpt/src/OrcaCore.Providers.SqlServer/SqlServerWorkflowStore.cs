using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;
using OrcaCore.Providers.Relational;

namespace OrcaCore.Providers.SqlServer;

/// <summary>
/// Provides the SQL Server event-store slice for durable event, checkpoint, inbox, and outbox commits.
/// </summary>
public sealed class SqlServerWorkflowStore :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowStartIdempotencyStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    IWorkflowRetentionStore,
    ITimerScheduler,
    IResourcePoolStore,
    IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    private readonly string connectionString;
    private readonly SqlServerRetentionStore retentionStore;
    private readonly SqlServerResourcePoolStore resourcePoolStore;

    /// <summary>
    /// Initializes the SQL Server workflow store from a connection string.
    /// </summary>
    public SqlServerWorkflowStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
        retentionStore = new SqlServerRetentionStore(connectionString);
        resourcePoolStore = new SqlServerResourcePoolStore(connectionString);
    }

    /// <summary>
    /// Creates the provider-owned schema for the SQL Server event-store slice.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await RelationalMigrationRunner
            .ApplyAsync(
                connection,
                SqlServerWorkflowStoreMigrations.Journal,
                SqlServerWorkflowStoreMigrations.All,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        return LoadCheckpointCoreAsync(instanceId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return AppendCoreAsync(batch, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken)
    {
        return LoadTailCoreAsync(streamId, afterVersion, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Option<InboxRecordState>> GetAsync(EventId eventId, CancellationToken cancellationToken)
    {
        return GetInboxCoreAsync(eventId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return GetStartedCoreAsync(idempotencyKey, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken)
    {
        return ClaimAsync(
            new OutboxClaimRequest(maxCount, DateTimeOffset.UtcNow, DefaultLeaseDuration),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        OutboxClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxCount);
        ThrowIfInvalidLease(request.LeaseDuration);
        return ClaimCoreAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Option<OutboxRecordState>> GetStateAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken)
    {
        return GetOutboxStateCoreAsync(outboxRecordId, cancellationToken);
    }

    /// <inheritdoc />
    public Task MarkAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken)
    {
        return MarkCoreAsync(outboxRecordId, state, cancellationToken);
    }

    /// <inheritdoc />
    public Task ReleaseAsync(OutboxRecordId outboxRecordId, CancellationToken cancellationToken)
    {
        return ReleaseOutboxCoreAsync(outboxRecordId, cancellationToken);
    }

    /// <inheritdoc />
    public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return ApplyCoreAsync(operations, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        return ListCoreAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        return CountCoreAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        return ListActiveWaitsCoreAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        return GetStatisticsCoreAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ArchiveResult> ArchiveAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        return retentionStore.ArchiveAsync(policy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PurgeResult> PurgeAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        return retentionStore.PurgeAsync(policy, cancellationToken);
    }

    /// <inheritdoc />
    public Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return ScheduleCoreAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken)
    {
        return ClaimDueAsync(
            new TimerClaimRequest(dueAtOrBefore, maxCount, dueAtOrBefore, DefaultLeaseDuration),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxCount);
        ThrowIfInvalidLease(request.LeaseDuration);
        cancellationToken.ThrowIfCancellationRequested();

        return ClaimDueCoreAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        return CompleteTimerCoreAsync(timerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        return ReleaseTimerCoreAsync(timerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken)
    {
        return resourcePoolStore.UpsertPoolAsync(definition, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ResourcePoolAcquireResult> AcquireAsync(
        ResourcePoolAcquireRequest request,
        CancellationToken cancellationToken)
    {
        return resourcePoolStore.AcquireAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ResourcePoolReleaseResult> ReleaseAsync(
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken)
    {
        return resourcePoolStore.ReleaseAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(string poolName, CancellationToken cancellationToken)
    {
        return resourcePoolStore.GetPoolAsync(poolName, cancellationToken);
    }

    /// <inheritdoc />
    public Task ResizePoolAsync(string poolName, int capacity, CancellationToken cancellationToken)
    {
        return resourcePoolStore.ResizePoolAsync(poolName, capacity, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        return resourcePoolStore.ExpireTicketsAsync(now, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ResourcePoolForceReleaseResult> ForceReleaseTicketAsync(
        Guid ticketId,
        string reason,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken)
    {
        return resourcePoolStore.ForceReleaseTicketAsync(ticketId, reason, releasedAt, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task<Option<CheckpointWrite>> LoadCheckpointCoreAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            select stream_version, content_type, payload, definition_id, definition_version, status,
                   last_step_path, error_summary, outcome_name, continue_as_new_generation
            from dbo.orcacore_checkpoints
            where instance_id = @instance_id;
            """,
            connection);
        command.Parameters.AddWithValue("@instance_id", instanceId.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return Option<CheckpointWrite>.None;
        }

        return Option<CheckpointWrite>.Some(new CheckpointWrite(
            instanceId,
            new StreamVersion(reader.GetInt64(0)),
            reader.GetString(1),
            (byte[])reader[2])
        {
            DefinitionId = reader.IsDBNull(3) ? null : new DefinitionId(reader.GetGuid(3)),
            DefinitionVersion = reader.IsDBNull(4) ? null : new DefinitionVersion(reader.GetInt32(4)),
            Status = reader.IsDBNull(5) ? null : Enum.Parse<WorkflowStatus>(reader.GetString(5)),
            LastStepPath = reader.IsDBNull(6) ? null : reader.GetString(6),
            ErrorSummary = reader.IsDBNull(7) ? null : reader.GetString(7),
            OutcomeName = reader.IsDBNull(8) ? null : reader.GetString(8),
            ContinueAsNewGeneration = reader.GetInt32(9)
        });
    }

    private async Task<Result<AppendEventsResult>> AppendCoreAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var actualVersion = await LoadActualVersionAsync(connection, transaction, batch.StreamId, cancellationToken)
                .ConfigureAwait(false);
            if (actualVersion != batch.ExpectedVersion)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return EventStoreConflict.ExpectedVersionMismatch(batch.ExpectedVersion, actualVersion);
            }

            var nextVersion = batch.ExpectedVersion.Value;
            foreach (var workflowEvent in batch.Events)
            {
                nextVersion++;
                await InsertEventAsync(
                    connection,
                    transaction,
                    batch.StreamId,
                    new StreamVersion(nextVersion),
                    workflowEvent,
                    cancellationToken).ConfigureAwait(false);
            }

            if (batch.Checkpoint is { } checkpoint)
            {
                await UpsertCheckpointAsync(connection, transaction, checkpoint, cancellationToken).ConfigureAwait(false);
            }

            await ApplyInboxOperationsAsync(connection, transaction, batch.InboxOperations, cancellationToken)
                .ConfigureAwait(false);
            await ApplyStartIdempotencyOperationsAsync(connection, transaction, batch.StartIdempotencyOperations, cancellationToken)
                .ConfigureAwait(false);
            await InsertOutboxRecordsAsync(connection, transaction, batch.StreamId.InstanceId, batch.OutboxRecords, cancellationToken)
                .ConfigureAwait(false);
            await ApplyProjectionOperationsAsync(connection, transaction, batch.ProjectionOperations, cancellationToken)
                .ConfigureAwait(false);
            await UpsertTimerSchedulesAsync(connection, transaction, batch.TimerSchedules, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result<AppendEventsResult>.Success(new AppendEventsResult(new StreamVersion(nextVersion)));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken).ConfigureAwait(false);
            var actualVersion = await LoadActualVersionAsync(connection, null, batch.StreamId, cancellationToken)
                .ConfigureAwait(false);
            return EventStoreConflict.ExpectedVersionMismatch(batch.ExpectedVersion, actualVersion);
        }
    }

    private async Task<IReadOnlyList<WorkflowEvent>> LoadTailCoreAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            select event_type, payload
            from dbo.orcacore_events
            where stream_id = @stream_id and version > @after_version
            order by version;
            """,
            connection);
        command.Parameters.AddWithValue("@stream_id", streamId.InstanceId.Value);
        command.Parameters.AddWithValue("@after_version", afterVersion.Value);

        var events = new List<WorkflowEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(WorkflowEventCodec.Deserialize(reader.GetString(0), reader.GetString(1)));
        }

        return events;
    }

    private async Task<Option<InboxRecordState>> GetInboxCoreAsync(EventId eventId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand("select state from dbo.orcacore_inbox where event_id = @event_id;", connection);
        command.Parameters.AddWithValue("@event_id", eventId.Value);
        var state = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return state is null
            ? Option<InboxRecordState>.None
            : Option<InboxRecordState>.Some(Enum.Parse<InboxRecordState>((string)state));
    }

    private async Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedCoreAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            select instance_id, definition_id, definition_version
            from dbo.orcacore_start_idempotency
            where idempotency_key = @idempotency_key;
            """,
            connection);
        command.Parameters.AddWithValue("@idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return Option<StartedWorkflowIdempotencyRecord>.None;
        }

        return Option<StartedWorkflowIdempotencyRecord>.Some(new StartedWorkflowIdempotencyRecord(
            idempotencyKey,
            new InstanceId(reader.GetGuid(0)),
            new DefinitionId(reader.GetGuid(1)),
            new DefinitionVersion(reader.GetInt32(2))));
    }

    private async Task<IReadOnlyList<OutboxWrite>> ClaimCoreAsync(
        OutboxClaimRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            ;with claimed as (
                select top (@max_count) outbox_record_id
                from dbo.orcacore_outbox with (updlock, readpast, rowlock)
                where state in (@pending, @retryable)
                   or (state = @claimed and (claimed_until is null or claimed_until <= @claimed_at))
                order by outbox_record_id
            )
            update dbo.orcacore_outbox
            set
                state = @claimed,
                claimed_until = @claimed_until
            output inserted.outbox_record_id, inserted.kind, inserted.payload
            where outbox_record_id in (select outbox_record_id from claimed);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@max_count", request.MaxCount);
        command.Parameters.AddWithValue("@pending", OutboxRecordState.Pending.ToString());
        command.Parameters.AddWithValue("@retryable", OutboxRecordState.Retryable.ToString());
        command.Parameters.AddWithValue("@claimed", OutboxRecordState.Claimed.ToString());
        command.Parameters.AddWithValue("@claimed_at", request.ClaimedAt);
        command.Parameters.AddWithValue("@claimed_until", request.ClaimedAt.Add(request.LeaseDuration));

        var records = new List<OutboxWrite>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            records.Add(new OutboxWrite(
                new OutboxRecordId(reader.GetGuid(0)),
                reader.GetString(1),
                (byte[])reader[2]));
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return records;
    }

    private async Task<Option<OutboxRecordState>> GetOutboxStateCoreAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            "select state from dbo.orcacore_outbox where outbox_record_id = @outbox_record_id;",
            connection);
        command.Parameters.AddWithValue("@outbox_record_id", outboxRecordId.Value);
        var state = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return state is null
            ? Option<OutboxRecordState>.None
            : Option<OutboxRecordState>.Some(Enum.Parse<OutboxRecordState>((string)state));
    }

    private async Task MarkCoreAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_outbox
            set
                state = @state,
                claimed_until = case
                    when @state = @claimed then claimed_until
                    else null
                end
            where outbox_record_id = @outbox_record_id;
            """,
            connection);
        command.Parameters.AddWithValue("@outbox_record_id", outboxRecordId.Value);
        command.Parameters.AddWithValue("@state", state.ToString());
        command.Parameters.AddWithValue("@claimed", OutboxRecordState.Claimed.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReleaseOutboxCoreAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_outbox
            set
                state = @retryable,
                claimed_until = null
            where outbox_record_id = @outbox_record_id
              and state = @claimed;
            """,
            connection);
        command.Parameters.AddWithValue("@outbox_record_id", outboxRecordId.Value);
        command.Parameters.AddWithValue("@retryable", OutboxRecordState.Retryable.ToString());
        command.Parameters.AddWithValue("@claimed", OutboxRecordState.Claimed.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyCoreAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await ApplyProjectionOperationsAsync(connection, transaction, operations, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListCoreAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            select instance_id, parent_instance_id, root_instance_id, definition_id, definition_version,
                   status, created_at, updated_at, error_summary, outcome_name,
                   continue_as_new_generation, archived_at, saga_audits
            from dbo.orcacore_instance_projections
            order by instance_id;
            """,
            connection);

        var snapshots = new List<WorkflowInstanceSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            snapshots.Add(new WorkflowInstanceSnapshot
            {
                InstanceId = new InstanceId(reader.GetGuid(0)),
                ParentInstanceId = reader.IsDBNull(1) ? null : new InstanceId(reader.GetGuid(1)),
                RootInstanceId = reader.IsDBNull(2) ? null : new InstanceId(reader.GetGuid(2)),
                DefinitionId = new DefinitionId(reader.GetGuid(3)),
                DefinitionVersion = new DefinitionVersion(reader.GetInt32(4)),
                Status = Enum.Parse<WorkflowStatus>(reader.GetString(5)),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(6),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(7),
                ErrorSummary = reader.IsDBNull(8) ? null : reader.GetString(8),
                EndOutcomeName = reader.IsDBNull(9) ? null : reader.GetString(9),
                ContinueAsNewGeneration = reader.GetInt32(10),
                ArchivedAt = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
                SagaAudits = reader.IsDBNull(12)
                    ? []
                    : JsonSerializer.Deserialize<IReadOnlyList<SagaAuditScopeSnapshot>>(reader.GetString(12), JsonOptions) ?? []
            });
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        var waits = await LoadActiveWaitsAsync(connection, snapshots.Select(snapshot => snapshot.InstanceId).ToArray(), cancellationToken)
            .ConfigureAwait(false);
        return snapshots
            .Select(snapshot => snapshot with
            {
                ActiveWaits = waits.TryGetValue(snapshot.InstanceId, out var activeWaits) ? activeWaits : []
            })
            .Where(snapshot => Matches(snapshot, query))
            .Select(CloneSnapshot)
            .ToArray();
    }

    private async Task<int> CountCoreAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        var snapshots = await ListCoreAsync(query, cancellationToken).ConfigureAwait(false);
        return snapshots.Count;
    }

    private async Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsCoreAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        var snapshots = await ListCoreAsync(query, cancellationToken).ConfigureAwait(false);
        return snapshots.SelectMany(snapshot => snapshot.ActiveWaits).ToArray();
    }

    private async Task<WorkflowStatistics> GetStatisticsCoreAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        var snapshots = await ListCoreAsync(query, cancellationToken).ConfigureAwait(false);
        return new WorkflowStatistics
        {
            Groups = snapshots
                .GroupBy(snapshot => new
                {
                    snapshot.DefinitionId,
                    snapshot.DefinitionVersion,
                    snapshot.Status
                })
                .Select(group => new WorkflowStatisticsGroup
                {
                    DefinitionId = group.Key.DefinitionId,
                    DefinitionVersion = group.Key.DefinitionVersion,
                    Status = group.Key.Status,
                    Count = group.Count()
                })
                .ToArray()
        };
    }

    private async Task ScheduleCoreAsync(TimerScheduleRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await UpsertTimerScheduleAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<FireTimerCommand>> ClaimDueCoreAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            ;with due as (
                select top (@max_count) timer_id
                from dbo.orcacore_timers with (updlock, readpast, rowlock)
                where fire_at <= @due_at
                  and (claimed = 0 or claimed_until is null or claimed_until <= @claimed_at)
                order by fire_at, timer_id
            )
            update dbo.orcacore_timers
            set
                claimed = 1,
                claimed_until = @claimed_until
            output inserted.timer_id, inserted.instance_id, inserted.command_id
            where timer_id in (select timer_id from due);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@max_count", request.MaxCount);
        command.Parameters.AddWithValue("@due_at", request.DueAtOrBefore);
        command.Parameters.AddWithValue("@claimed_at", request.ClaimedAt);
        command.Parameters.AddWithValue("@claimed_until", request.ClaimedAt.Add(request.LeaseDuration));

        var commands = new List<FireTimerCommand>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            commands.Add(new FireTimerCommand
            {
                TimerId = new TimerId(reader.GetGuid(0)),
                InstanceId = new InstanceId(reader.GetGuid(1)),
                CommandId = new CommandId(reader.GetGuid(2)),
                RequestedAt = request.ClaimedAt
            });
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return commands;
    }

    private async Task CompleteTimerCoreAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            "delete from dbo.orcacore_timers where timer_id = @timer_id;",
            connection);
        command.Parameters.AddWithValue("@timer_id", timerId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReleaseTimerCoreAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_timers
            set
                claimed = 0,
                claimed_until = null
            where timer_id = @timer_id;
            """,
            connection);
        command.Parameters.AddWithValue("@timer_id", timerId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task<StreamVersion> LoadActualVersionAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        WorkflowStreamId streamId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "select isnull(max(version), 0) from dbo.orcacore_events where stream_id = @stream_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@stream_id", streamId.InstanceId.Value);
        var actual = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return new StreamVersion(Convert.ToInt64(actual));
    }

    private static async Task RollbackQuietlyAsync(SqlTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
        catch (SqlException)
        {
        }
    }

    private static async Task InsertEventAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        WorkflowStreamId streamId,
        StreamVersion version,
        WorkflowEvent workflowEvent,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            insert into dbo.orcacore_events (stream_id, version, event_id, event_type, occurred_at, payload)
            values (@stream_id, @version, @event_id, @event_type, @occurred_at, @payload);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@stream_id", streamId.InstanceId.Value);
        command.Parameters.AddWithValue("@version", version.Value);
        command.Parameters.AddWithValue("@event_id", workflowEvent.EventId.Value);
        command.Parameters.AddWithValue("@event_type", WorkflowEventCodec.ToEventType(workflowEvent));
        command.Parameters.AddWithValue("@occurred_at", workflowEvent.OccurredAt);
        command.Parameters.AddWithValue("@payload", WorkflowEventCodec.Serialize(workflowEvent));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertCheckpointAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CheckpointWrite checkpoint,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_checkpoints
            set stream_version = @stream_version,
                content_type = @content_type,
                payload = @payload,
                definition_id = @definition_id,
                definition_version = @definition_version,
                status = @status,
                last_step_path = @last_step_path,
                error_summary = @error_summary,
                outcome_name = @outcome_name,
                continue_as_new_generation = @continue_as_new_generation
            where instance_id = @instance_id;
            if @@rowcount = 0
            begin
                insert into dbo.orcacore_checkpoints (
                    instance_id, stream_version, content_type, payload, definition_id, definition_version,
                    status, last_step_path, error_summary, outcome_name, continue_as_new_generation)
                values (
                    @instance_id, @stream_version, @content_type, @payload, @definition_id, @definition_version,
                    @status, @last_step_path, @error_summary, @outcome_name, @continue_as_new_generation);
            end;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@instance_id", checkpoint.InstanceId.Value);
        command.Parameters.AddWithValue("@stream_version", checkpoint.StreamVersion.Value);
        command.Parameters.AddWithValue("@content_type", checkpoint.ContentType);
        command.Parameters.AddWithValue("@payload", checkpoint.Payload);
        AddNullable(command, "@definition_id", checkpoint.DefinitionId?.Value);
        AddNullable(command, "@definition_version", checkpoint.DefinitionVersion?.Value);
        AddNullable(command, "@status", checkpoint.Status?.ToString());
        AddNullable(command, "@last_step_path", checkpoint.LastStepPath);
        AddNullable(command, "@error_summary", checkpoint.ErrorSummary);
        AddNullable(command, "@outcome_name", checkpoint.OutcomeName);
        command.Parameters.AddWithValue("@continue_as_new_generation", checkpoint.ContinueAsNewGeneration);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyInboxOperationsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<InboxWrite> operations,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            await using var command = new SqlCommand(
                """
                update dbo.orcacore_inbox
                set state = case when state = @applied then state else @state end
                where event_id = @event_id;
                if @@rowcount = 0
                begin
                    insert into dbo.orcacore_inbox (event_id, state)
                    values (@event_id, @state);
                end;
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("@event_id", operation.EventId.Value);
            command.Parameters.AddWithValue("@state", operation.State.ToString());
            command.Parameters.AddWithValue("@applied", InboxRecordState.Applied.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ApplyStartIdempotencyOperationsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<StartIdempotencyWrite> operations,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            await using var command = new SqlCommand(
                """
                if not exists (
                    select 1 from dbo.orcacore_start_idempotency where idempotency_key = @idempotency_key)
                begin
                    insert into dbo.orcacore_start_idempotency (
                        idempotency_key, instance_id, definition_id, definition_version)
                    values (
                        @idempotency_key, @instance_id, @definition_id, @definition_version);
                end;
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("@idempotency_key", operation.IdempotencyKey);
            command.Parameters.AddWithValue("@instance_id", operation.InstanceId.Value);
            command.Parameters.AddWithValue("@definition_id", operation.DefinitionId.Value);
            command.Parameters.AddWithValue("@definition_version", operation.DefinitionVersion.Value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task InsertOutboxRecordsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        InstanceId instanceId,
        IEnumerable<OutboxWrite> records,
        CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            await using var command = new SqlCommand(
                """
                insert into dbo.orcacore_outbox (outbox_record_id, instance_id, kind, payload, state)
                values (@outbox_record_id, @instance_id, @kind, @payload, @state);
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("@outbox_record_id", record.OutboxRecordId.Value);
            command.Parameters.AddWithValue("@instance_id", instanceId.Value);
            command.Parameters.AddWithValue("@kind", record.Kind);
            command.Parameters.AddWithValue("@payload", record.Payload);
            command.Parameters.AddWithValue("@state", OutboxRecordState.Pending.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ApplyProjectionOperationsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<ProjectionWrite> operations,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            switch (operation.Kind)
            {
                case ProjectionOperationKind.UpsertSummary when operation.InstanceSnapshot is { } snapshot:
                    await UpsertSummaryProjectionAsync(connection, transaction, snapshot, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case ProjectionOperationKind.UpsertActiveWait when operation.ActiveWait is { } activeWait:
                    await UpsertActiveWaitProjectionAsync(connection, transaction, operation.InstanceId, activeWait, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case ProjectionOperationKind.RemoveActiveWait when operation.WaitId is { } waitId:
                    await using (var command = new SqlCommand(
                        "delete from dbo.orcacore_active_wait_projections where wait_id = @wait_id;",
                        connection,
                        transaction))
                    {
                        command.Parameters.AddWithValue("@wait_id", waitId.Value);
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    break;
            }
        }
    }

    private static async Task UpsertSummaryProjectionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        WorkflowInstanceSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_instance_projections
            set parent_instance_id = @parent_instance_id,
                root_instance_id = @root_instance_id,
                definition_id = @definition_id,
                definition_version = @definition_version,
                status = @status,
                created_at = @created_at,
                updated_at = @updated_at,
                error_summary = @error_summary,
                outcome_name = @outcome_name,
                continue_as_new_generation = @continue_as_new_generation,
                archived_at = @archived_at,
                saga_audits = @saga_audits
            where instance_id = @instance_id;
            if @@rowcount = 0
            begin
                insert into dbo.orcacore_instance_projections (
                    instance_id, parent_instance_id, root_instance_id, definition_id, definition_version,
                    status, created_at, updated_at, error_summary, outcome_name,
                    continue_as_new_generation, archived_at, saga_audits)
                values (
                    @instance_id, @parent_instance_id, @root_instance_id, @definition_id, @definition_version,
                    @status, @created_at, @updated_at, @error_summary, @outcome_name,
                    @continue_as_new_generation, @archived_at, @saga_audits);
            end;
            delete from dbo.orcacore_active_wait_projections where instance_id = @instance_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@instance_id", snapshot.InstanceId.Value);
        AddNullable(command, "@parent_instance_id", snapshot.ParentInstanceId?.Value);
        AddNullable(command, "@root_instance_id", snapshot.RootInstanceId?.Value);
        command.Parameters.AddWithValue("@definition_id", snapshot.DefinitionId.Value);
        command.Parameters.AddWithValue("@definition_version", snapshot.DefinitionVersion.Value);
        command.Parameters.AddWithValue("@status", snapshot.Status.ToString());
        command.Parameters.AddWithValue("@created_at", snapshot.CreatedAt);
        command.Parameters.AddWithValue("@updated_at", snapshot.UpdatedAt);
        AddNullable(command, "@error_summary", snapshot.ErrorSummary);
        AddNullable(command, "@outcome_name", snapshot.EndOutcomeName);
        command.Parameters.AddWithValue("@continue_as_new_generation", snapshot.ContinueAsNewGeneration);
        AddNullable(command, "@archived_at", snapshot.ArchivedAt);
        command.Parameters.AddWithValue("@saga_audits", JsonSerializer.Serialize(snapshot.SagaAudits, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        foreach (var wait in snapshot.ActiveWaits)
        {
            await UpsertActiveWaitProjectionAsync(connection, transaction, snapshot.InstanceId, wait, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task UpsertActiveWaitProjectionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        InstanceId instanceId,
        ActiveWaitSnapshot wait,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_active_wait_projections
            set instance_id = @instance_id,
                event_name = @event_name,
                correlation_id = @correlation_id,
                registered_at = @registered_at,
                status = @status,
                mode = @mode
            where wait_id = @wait_id;
            if @@rowcount = 0
            begin
                insert into dbo.orcacore_active_wait_projections (
                    wait_id, instance_id, event_name, correlation_id, registered_at, status, mode)
                values (
                    @wait_id, @instance_id, @event_name, @correlation_id, @registered_at, @status, @mode);
            end;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@wait_id", wait.WaitId.Value);
        command.Parameters.AddWithValue("@instance_id", instanceId.Value);
        command.Parameters.AddWithValue("@event_name", wait.EventName);
        command.Parameters.AddWithValue("@correlation_id", wait.CorrelationId.Value);
        command.Parameters.AddWithValue("@registered_at", wait.RegisteredAt);
        command.Parameters.AddWithValue("@status", wait.Status);
        command.Parameters.AddWithValue("@mode", wait.Mode);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertTimerSchedulesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<TimerScheduleRequest> requests,
        CancellationToken cancellationToken)
    {
        foreach (var request in requests)
        {
            await UpsertTimerScheduleAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task UpsertTimerScheduleAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        TimerScheduleRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_timers
            set instance_id = @instance_id,
                command_id = @command_id,
                fire_at = @fire_at,
                wakeup_name = @wakeup_name,
                claimed = 0,
                claimed_until = null
            where timer_id = @timer_id;
            if @@rowcount = 0
            begin
                insert into dbo.orcacore_timers (
                    timer_id,
                    instance_id,
                    command_id,
                    fire_at,
                    wakeup_name,
                    claimed,
                    claimed_until)
                values (
                    @timer_id,
                    @instance_id,
                    @command_id,
                    @fire_at,
                    @wakeup_name,
                    0,
                    null);
            end;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@timer_id", request.TimerId.Value);
        command.Parameters.AddWithValue("@instance_id", request.InstanceId.Value);
        command.Parameters.AddWithValue("@command_id", request.CommandId.Value);
        command.Parameters.AddWithValue("@fire_at", request.FireAt);
        command.Parameters.AddWithValue("@wakeup_name", request.WakeupName);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyDictionary<InstanceId, IReadOnlyList<ActiveWaitSnapshot>>> LoadActiveWaitsAsync(
        SqlConnection connection,
        IReadOnlyList<InstanceId> instanceIds,
        CancellationToken cancellationToken)
    {
        if (instanceIds.Count == 0)
        {
            return new Dictionary<InstanceId, IReadOnlyList<ActiveWaitSnapshot>>();
        }

        var parameterNames = instanceIds.Select((_, index) => $"@instance_id_{index}").ToArray();
        await using var command = new SqlCommand(
            $"""
            select instance_id, wait_id, event_name, correlation_id, registered_at, status, mode
            from dbo.orcacore_active_wait_projections
            where instance_id in ({string.Join(", ", parameterNames)})
            order by instance_id, registered_at, wait_id;
            """,
            connection);
        for (var index = 0; index < instanceIds.Count; index++)
        {
            command.Parameters.AddWithValue(parameterNames[index], instanceIds[index].Value);
        }

        var waits = new Dictionary<InstanceId, List<ActiveWaitSnapshot>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var instanceId = new InstanceId(reader.GetGuid(0));
            if (!waits.TryGetValue(instanceId, out var instanceWaits))
            {
                instanceWaits = [];
                waits.Add(instanceId, instanceWaits);
            }

            instanceWaits.Add(new ActiveWaitSnapshot
            {
                WaitId = new WaitId(reader.GetGuid(1)),
                EventName = reader.GetString(2),
                CorrelationId = new CorrelationId(reader.GetString(3)),
                RegisteredAt = reader.GetFieldValue<DateTimeOffset>(4),
                Status = reader.GetString(5),
                Mode = reader.GetString(6)
            });
        }

        return waits.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<ActiveWaitSnapshot>)pair.Value);
    }

    private static void AddNullable(SqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static void ThrowIfInvalidLease(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be positive.");
        }
    }

    private static bool Matches(WorkflowInstanceSnapshot snapshot, WorkflowProjectionQuery query)
    {
        return (query.InstanceId is null || snapshot.InstanceId == query.InstanceId) &&
            (query.ParentInstanceId is null || snapshot.ParentInstanceId == query.ParentInstanceId) &&
            (query.RootInstanceId is null || snapshot.RootInstanceId == query.RootInstanceId) &&
            (query.DefinitionId is null || snapshot.DefinitionId == query.DefinitionId) &&
            (query.DefinitionVersion is null || snapshot.DefinitionVersion == query.DefinitionVersion) &&
            (query.Status is null || snapshot.Status == query.Status) &&
            (query.ActiveWaitEventName is null || snapshot.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, query.ActiveWaitEventName, StringComparison.Ordinal))) &&
            (query.ActiveWaitCorrelationId is null || snapshot.ActiveWaits.Any(wait =>
                wait.CorrelationId == query.ActiveWaitCorrelationId));
    }

    private static WorkflowInstanceSnapshot CloneSnapshot(WorkflowInstanceSnapshot snapshot)
    {
        return snapshot with
        {
            ActiveWaits = snapshot.ActiveWaits.Select(wait => wait with { }).ToArray(),
            SagaAudits = snapshot.SagaAudits.Select(scope => scope with
            {
                ForwardActions = scope.ForwardActions.Select(action => action with { }).ToArray(),
                CompensationActions = scope.CompensationActions.Select(action => action with { }).ToArray(),
                RecoveryInterventions = scope.RecoveryInterventions.Select(intervention => intervention with { }).ToArray()
            }).ToArray()
        };
    }

    private sealed record SqlServerOutboxRecord(OutboxWrite Write, OutboxRecordState State);
}
