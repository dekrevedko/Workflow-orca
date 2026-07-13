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
    private readonly SqlServerProjectionStore projectionStore;
    private readonly SqlServerTimerScheduler timerScheduler;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes the SQL Server workflow store from a connection string.
    /// </summary>
    public SqlServerWorkflowStore(string connectionString, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        retentionStore = new SqlServerRetentionStore(connectionString);
        resourcePoolStore = new SqlServerResourcePoolStore(connectionString);
        projectionStore = new SqlServerProjectionStore(connectionString);
        timerScheduler = new SqlServerTimerScheduler(connectionString);
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
                cancellationToken,
                timeProvider)
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
            new OutboxClaimRequest(maxCount, timeProvider.GetUtcNow(), DefaultLeaseDuration),
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
        return projectionStore.ApplyAsync(operations, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        return projectionStore.ListAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        return projectionStore.CountAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        return projectionStore.ListActiveWaitsAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        return projectionStore.GetStatisticsAsync(query, cancellationToken);
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

        return timerScheduler.ScheduleAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken)
    {
        return timerScheduler.ClaimDueAsync(dueAtOrBefore, maxCount, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxCount);
        cancellationToken.ThrowIfCancellationRequested();

        return timerScheduler.ClaimDueAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        return timerScheduler.CompleteAsync(timerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        return timerScheduler.ReleaseAsync(timerId, cancellationToken);
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
    public Task<IReadOnlyList<ResourcePoolSnapshot>> ListPoolsAsync(CancellationToken cancellationToken)
    {
        return resourcePoolStore.ListPoolsAsync(cancellationToken);
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
                   last_step_path, error_summary, outcome_name, continue_as_new_generation, runtime_state
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
            ContinueAsNewGeneration = reader.GetInt32(9),
            RuntimeState = ReadRuntimeState(reader, 10)
        });
    }

    private static WorkflowRuntimeCheckpointState ReadRuntimeState(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return WorkflowRuntimeCheckpointState.Empty;
        }

        return JsonSerializer.Deserialize(
            (byte[])reader[ordinal],
            OrcaCoreJsonSerializerContext.Default.WorkflowRuntimeCheckpointState)
            ?? WorkflowRuntimeCheckpointState.Empty;
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
            await SqlServerProjectionStore.ApplyProjectionOperationsAsync(
                    connection,
                    transaction,
                    batch.ProjectionOperations,
                    cancellationToken)
                .ConfigureAwait(false);
            await SqlServerTimerScheduler.UpsertTimerSchedulesAsync(
                    connection,
                    transaction,
                    batch.TimerSchedules,
                    cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result<AppendEventsResult>.Success(new AppendEventsResult(new StreamVersion(nextVersion)));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken).ConfigureAwait(false);
            if (batch.StartIdempotencyOperations.FirstOrDefault() is { } startWrite)
            {
                var existingStart = await GetStartedAsync(startWrite.IdempotencyKey, cancellationToken)
                    .ConfigureAwait(false);
                if (existingStart.HasValue)
                {
                    return EventStoreConflict.StartIdempotencyKeyAlreadyExists(startWrite.IdempotencyKey);
                }
            }

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
                continue_as_new_generation = @continue_as_new_generation,
                runtime_state = @runtime_state
            where instance_id = @instance_id;
            if @@rowcount = 0
            begin
                insert into dbo.orcacore_checkpoints (
                    instance_id, stream_version, content_type, payload, definition_id, definition_version,
                    status, last_step_path, error_summary, outcome_name, continue_as_new_generation,
                    runtime_state)
                values (
                    @instance_id, @stream_version, @content_type, @payload, @definition_id, @definition_version,
                    @status, @last_step_path, @error_summary, @outcome_name, @continue_as_new_generation,
                    @runtime_state);
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
        command.Parameters.AddWithValue("@runtime_state", JsonSerializer.SerializeToUtf8Bytes(
            checkpoint.RuntimeState,
            OrcaCoreJsonSerializerContext.Default.WorkflowRuntimeCheckpointState));
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
                insert into dbo.orcacore_start_idempotency (
                    idempotency_key, instance_id, definition_id, definition_version)
                values (
                    @idempotency_key, @instance_id, @definition_id, @definition_version);
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

    private sealed record SqlServerOutboxRecord(OutboxWrite Write, OutboxRecordState State);
}
