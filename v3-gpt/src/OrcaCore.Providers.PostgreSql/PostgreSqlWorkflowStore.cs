using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;
using OrcaCore.Providers.Relational;

namespace OrcaCore.Providers.PostgreSql;

/// <summary>
/// Stores durable workflow events and checkpoints in PostgreSQL.
/// </summary>
public sealed class PostgreSqlWorkflowStore :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowStartIdempotencyStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    ITimerScheduler,
    IWorkflowRetentionStore,
    IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;
    private readonly PostgreSqlProjectionStore projectionStore;
    private readonly PostgreSqlTimerScheduler timerScheduler;
    private readonly PostgreSqlWorkflowRetentionStore retentionStore;
    private readonly PostgreSqlWorkflowStoreOptions options;

    /// <summary>
    /// Initializes a PostgreSQL workflow store from a connection string.
    /// </summary>
    public PostgreSqlWorkflowStore(
        string connectionString,
        TimeProvider? timeProvider = null,
        PostgreSqlWorkflowStoreOptions? options = null)
        : this(CreateDataSource(connectionString), ownsDataSource: true, timeProvider, options)
    {
    }

    /// <summary>
    /// Initializes a PostgreSQL workflow store from an existing caller-owned data source.
    /// </summary>
    public PostgreSqlWorkflowStore(
        NpgsqlDataSource dataSource,
        TimeProvider? timeProvider = null,
        PostgreSqlWorkflowStoreOptions? options = null)
        : this(dataSource, ownsDataSource: false, timeProvider, options)
    {
    }

    private PostgreSqlWorkflowStore(
        NpgsqlDataSource dataSource,
        bool ownsDataSource,
        TimeProvider? timeProvider,
        PostgreSqlWorkflowStoreOptions? options)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.options = options ?? new PostgreSqlWorkflowStoreOptions();
        projectionStore = new PostgreSqlProjectionStore(dataSource);
        timerScheduler = new PostgreSqlTimerScheduler(dataSource);
        retentionStore = new PostgreSqlWorkflowRetentionStore(dataSource);
    }

    /// <summary>
    /// Creates the provider schema when it is not already present.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await RelationalMigrationRunner
            .ApplyAsync(
                connection,
                PostgreSqlWorkflowStoreMigrations.Journal,
                PostgreSqlWorkflowStoreMigrations.All,
                cancellationToken,
                timeProvider)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select stream_version,
                   content_type,
                   payload,
                   definition_id,
                   definition_version,
                   status,
                   last_step_path,
                   error_summary,
                   outcome_name,
                   continue_as_new_generation,
                   runtime_state
            from orcacore_checkpoints
            where instance_id = @instance_id;
            """,
            connection);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return Option<CheckpointWrite>.None;
        }

        return Option<CheckpointWrite>.Some(new CheckpointWrite(
            instanceId,
            new StreamVersion(reader.GetInt64(0)),
            reader.GetString(1),
            reader.GetFieldValue<byte[]>(2))
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

    private static WorkflowRuntimeCheckpointState ReadRuntimeState(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return WorkflowRuntimeCheckpointState.Empty;
        }

        return JsonSerializer.Deserialize(
            reader.GetString(ordinal),
            OrcaCoreJsonSerializerContext.Default.WorkflowRuntimeCheckpointState)
            ?? WorkflowRuntimeCheckpointState.Empty;
    }

    /// <inheritdoc />
    public async Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var actualVersion = await LoadActualVersionAsync(
                connection,
                transaction,
                batch.StreamId,
                cancellationToken).ConfigureAwait(false);
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
                await UpsertCheckpointAsync(connection, transaction, checkpoint, cancellationToken)
                    .ConfigureAwait(false);
            }

            await ApplyInboxOperationsAsync(connection, transaction, batch.InboxOperations, cancellationToken)
                .ConfigureAwait(false);
            await ApplyStartIdempotencyOperationsAsync(connection, transaction, batch.StartIdempotencyOperations, cancellationToken)
                .ConfigureAwait(false);
            await InsertOutboxRecordsAsync(connection, transaction, batch.StreamId.InstanceId, batch.OutboxRecords, cancellationToken)
                .ConfigureAwait(false);
            await PostgreSqlProjectionStore.ApplyProjectionOperationsAsync(
                    connection,
                    transaction,
                    batch.ProjectionOperations,
                    cancellationToken)
                .ConfigureAwait(false);
            await PostgreSqlTimerScheduler.UpsertTimerSchedulesAsync(
                    connection,
                    transaction,
                    batch.TimerSchedules,
                    cancellationToken)
                .ConfigureAwait(false);

            await options
                .InvokeBeforeCommitAsync(CreateAppendContext(batch, new StreamVersion(nextVersion)), cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result<AppendEventsResult>.Success(new AppendEventsResult(new StreamVersion(nextVersion)));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken).ConfigureAwait(false);
            var actualVersion = await LoadActualVersionAsync(connection, null, batch.StreamId, cancellationToken)
                .ConfigureAwait(false);
            return EventStoreConflict.ExpectedVersionMismatch(batch.ExpectedVersion, actualVersion);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select event_type, payload
            from orcacore_events
            where stream_id = @stream_id and version > @after_version
            order by version;
            """,
            connection);
        command.Parameters.AddWithValue("stream_id", streamId.InstanceId.Value);
        command.Parameters.AddWithValue("after_version", afterVersion.Value);

        var events = new List<WorkflowEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(WorkflowEventCodec.Deserialize(reader.GetString(0), reader.GetString(1)));
        }

        return events;
    }

    /// <inheritdoc />
    public async Task<Option<InboxRecordState>> GetAsync(EventId eventId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select state
            from orcacore_inbox
            where event_id = @event_id;
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId.Value);

        var state = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return state is null
            ? Option<InboxRecordState>.None
            : Option<InboxRecordState>.Some(Enum.Parse<InboxRecordState>((string)state));
    }

    /// <inheritdoc />
    public async Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select instance_id, definition_id, definition_version
            from orcacore_start_idempotency
            where idempotency_key = @idempotency_key;
            """,
            connection);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await ClaimAsync(
            connection,
            new OutboxClaimRequest(maxCount, timeProvider.GetUtcNow(), DefaultLeaseDuration),
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        OutboxClaimRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await ClaimAsync(connection, request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        NpgsqlConnection connection,
        OutboxClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxCount);
        ThrowIfInvalidLease(request.LeaseDuration);

        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            update orcacore_outbox
            set
                state = @claimed,
                claimed_until = @claimed_until
            where outbox_record_id in (
                select outbox_record_id
                from orcacore_outbox
                where state in (@pending, @retryable)
                   or (state = @claimed and (claimed_until is null or claimed_until <= @claimed_at))
                order by outbox_record_id
                for update skip locked
                limit @max_count
            )
            returning outbox_record_id, kind, payload;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("claimed", OutboxRecordState.Claimed.ToString());
        command.Parameters.AddWithValue("pending", OutboxRecordState.Pending.ToString());
        command.Parameters.AddWithValue("retryable", OutboxRecordState.Retryable.ToString());
        command.Parameters.AddWithValue("claimed_at", request.ClaimedAt);
        command.Parameters.AddWithValue("claimed_until", request.ClaimedAt.Add(request.LeaseDuration));
        command.Parameters.AddWithValue("max_count", request.MaxCount);

        var records = new List<OutboxWrite>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            records.Add(new OutboxWrite(
                new OutboxRecordId(reader.GetGuid(0)),
                reader.GetString(1),
                reader.GetFieldValue<byte[]>(2)));
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return records;
    }

    /// <inheritdoc />
    public async Task<Option<OutboxRecordState>> GetStateAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select state
            from orcacore_outbox
            where outbox_record_id = @outbox_record_id;
            """,
            connection);
        command.Parameters.AddWithValue("outbox_record_id", outboxRecordId.Value);

        var state = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return state is null
            ? Option<OutboxRecordState>.None
            : Option<OutboxRecordState>.Some(Enum.Parse<OutboxRecordState>((string)state));
    }

    /// <inheritdoc />
    public async Task MarkAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            update orcacore_outbox
            set
                state = @state,
                claimed_until = case
                    when @state = @claimed then claimed_until
                    else null
                end
            where outbox_record_id = @outbox_record_id;
            """,
            connection);
        command.Parameters.AddWithValue("outbox_record_id", outboxRecordId.Value);
        command.Parameters.AddWithValue("state", state.ToString());
        command.Parameters.AddWithValue("claimed", OutboxRecordState.Claimed.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(OutboxRecordId outboxRecordId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            update orcacore_outbox
            set
                state = @retryable,
                claimed_until = null
            where outbox_record_id = @outbox_record_id
              and state = @claimed;
            """,
            connection);
        command.Parameters.AddWithValue("outbox_record_id", outboxRecordId.Value);
        command.Parameters.AddWithValue("retryable", OutboxRecordState.Retryable.ToString());
        command.Parameters.AddWithValue("claimed", OutboxRecordState.Claimed.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        return projectionStore.ApplyAsync(operations, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        return projectionStore.ListAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        return projectionStore.CountAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        return projectionStore.ListActiveWaitsAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        return projectionStore.GetStatisticsAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken)
    {
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
    public Task<ArchiveResult> ArchiveAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        return retentionStore.ArchiveAsync(policy, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PurgeResult> PurgeAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        return retentionStore.PurgeAsync(policy, cancellationToken);
    }

    public Task<PurgeResult> PurgeAsync(InstanceId instanceId, CancellationToken cancellationToken)
    {
        return retentionStore.PurgeAsync(instanceId, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ownsDataSource
            ? dataSource.DisposeAsync()
            : ValueTask.CompletedTask;
    }

    private static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return NpgsqlDataSource.Create(connectionString);
    }

    private static PostgreSqlAppendContext CreateAppendContext(
        ProviderCommitBatch batch,
        StreamVersion newVersion)
    {
        return new PostgreSqlAppendContext(
            batch.StreamId,
            batch.ExpectedVersion,
            newVersion,
            batch.Events.Count,
            batch.InboxOperations.Count,
            batch.OutboxRecords.Count,
            batch.ProjectionOperations.Count,
            batch.TimerSchedules.Count);
    }

    private static async Task<StreamVersion> LoadActualVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        WorkflowStreamId streamId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select coalesce(max(version), 0)
            from orcacore_events
            where stream_id = @stream_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("stream_id", streamId.InstanceId.Value);

        var actual = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return new StreamVersion((long)(actual ?? 0L));
    }

    private static async Task RollbackQuietlyAsync(
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
        catch (PostgresException)
        {
        }
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkflowStreamId streamId,
        StreamVersion version,
        WorkflowEvent workflowEvent,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_events (
                stream_id,
                version,
                event_id,
                event_type,
                occurred_at,
                payload)
            values (
                @stream_id,
                @version,
                @event_id,
                @event_type,
                @occurred_at,
                @payload);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("stream_id", streamId.InstanceId.Value);
        command.Parameters.AddWithValue("version", version.Value);
        command.Parameters.AddWithValue("event_id", workflowEvent.EventId.Value);
        command.Parameters.AddWithValue("event_type", WorkflowEventCodec.ToEventType(workflowEvent));
        command.Parameters.AddWithValue("occurred_at", workflowEvent.OccurredAt);
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = WorkflowEventCodec.Serialize(workflowEvent);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertCheckpointAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CheckpointWrite checkpoint,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_checkpoints (
                instance_id,
                stream_version,
                content_type,
                payload,
                definition_id,
                definition_version,
                status,
                last_step_path,
                error_summary,
                outcome_name,
                continue_as_new_generation,
                runtime_state)
            values (
                @instance_id,
                @stream_version,
                @content_type,
                @payload,
                @definition_id,
                @definition_version,
                @status,
                @last_step_path,
                @error_summary,
                @outcome_name,
                @continue_as_new_generation,
                @runtime_state)
            on conflict (instance_id) do update set
                stream_version = excluded.stream_version,
                content_type = excluded.content_type,
                payload = excluded.payload,
                definition_id = excluded.definition_id,
                definition_version = excluded.definition_version,
                status = excluded.status,
                last_step_path = excluded.last_step_path,
                error_summary = excluded.error_summary,
                outcome_name = excluded.outcome_name,
                continue_as_new_generation = excluded.continue_as_new_generation,
                runtime_state = excluded.runtime_state;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("instance_id", checkpoint.InstanceId.Value);
        command.Parameters.AddWithValue("stream_version", checkpoint.StreamVersion.Value);
        command.Parameters.AddWithValue("content_type", checkpoint.ContentType);
        command.Parameters.AddWithValue("payload", checkpoint.Payload);
        command.Parameters.AddWithValue("definition_id", (object?)checkpoint.DefinitionId?.Value ?? DBNull.Value);
        command.Parameters.AddWithValue("definition_version", (object?)checkpoint.DefinitionVersion?.Value ?? DBNull.Value);
        command.Parameters.AddWithValue("status", (object?)checkpoint.Status?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("last_step_path", (object?)checkpoint.LastStepPath ?? DBNull.Value);
        command.Parameters.AddWithValue("error_summary", (object?)checkpoint.ErrorSummary ?? DBNull.Value);
        command.Parameters.AddWithValue("outcome_name", (object?)checkpoint.OutcomeName ?? DBNull.Value);
        command.Parameters.AddWithValue("continue_as_new_generation", checkpoint.ContinueAsNewGeneration);
        command.Parameters.Add("runtime_state", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(
            checkpoint.RuntimeState,
            OrcaCoreJsonSerializerContext.Default.WorkflowRuntimeCheckpointState);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyInboxOperationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<InboxWrite> operations,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            await using var command = new NpgsqlCommand(
                """
                insert into orcacore_inbox (event_id, state)
                values (@event_id, @state)
                on conflict (event_id) do update set
                    state = case
                        when orcacore_inbox.state = @applied then orcacore_inbox.state
                        else excluded.state
                    end;
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("event_id", operation.EventId.Value);
            command.Parameters.AddWithValue("state", operation.State.ToString());
            command.Parameters.AddWithValue("applied", InboxRecordState.Applied.ToString());

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ApplyStartIdempotencyOperationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<StartIdempotencyWrite> operations,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            await using var command = new NpgsqlCommand(
                """
                insert into orcacore_start_idempotency (
                    idempotency_key,
                    instance_id,
                    definition_id,
                    definition_version)
                values (
                    @idempotency_key,
                    @instance_id,
                    @definition_id,
                    @definition_version);
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("idempotency_key", operation.IdempotencyKey);
            command.Parameters.AddWithValue("instance_id", operation.InstanceId.Value);
            command.Parameters.AddWithValue("definition_id", operation.DefinitionId.Value);
            command.Parameters.AddWithValue("definition_version", operation.DefinitionVersion.Value);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task InsertOutboxRecordsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        IEnumerable<OutboxWrite> records,
        CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            await using var command = new NpgsqlCommand(
                """
                insert into orcacore_outbox (
                    outbox_record_id,
                    instance_id,
                    kind,
                    payload,
                    state)
                values (
                    @outbox_record_id,
                    @instance_id,
                    @kind,
                    @payload,
                    @state);
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("outbox_record_id", record.OutboxRecordId.Value);
            command.Parameters.AddWithValue("instance_id", instanceId.Value);
            command.Parameters.AddWithValue("kind", record.Kind);
            command.Parameters.AddWithValue("payload", record.Payload);
            command.Parameters.AddWithValue("state", OutboxRecordState.Pending.ToString());

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ThrowIfInvalidLease(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be positive.");
        }
    }

    private static IReadOnlyList<SagaAuditScopeSnapshot> DeserializeSagaAudits(string payload)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<SagaAuditScopeSnapshot>>(payload, JsonOptions) ?? [];
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

}
