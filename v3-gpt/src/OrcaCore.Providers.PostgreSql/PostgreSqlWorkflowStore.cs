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

    /// <summary>
    /// Initializes a PostgreSQL workflow store from a connection string.
    /// </summary>
    public PostgreSqlWorkflowStore(string connectionString)
        : this(CreateDataSource(connectionString))
    {
    }

    /// <summary>
    /// Initializes a PostgreSQL workflow store from an existing data source.
    /// </summary>
    public PostgreSqlWorkflowStore(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
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
                cancellationToken)
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
                   continue_as_new_generation
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
            ContinueAsNewGeneration = reader.GetInt32(9)
        });
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
            await ApplyProjectionOperationsAsync(connection, transaction, batch.ProjectionOperations, cancellationToken)
                .ConfigureAwait(false);
            await UpsertTimerSchedulesAsync(connection, transaction, batch.TimerSchedules, cancellationToken)
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
            new OutboxClaimRequest(maxCount, DateTimeOffset.UtcNow, DefaultLeaseDuration),
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
    public async Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await ApplyProjectionOperationsAsync(connection, transaction, operations, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var snapshots = new List<WorkflowInstanceSnapshot>();
        await using var command = new NpgsqlCommand(
            """
            select instance_id,
                   parent_instance_id,
                   root_instance_id,
                   definition_id,
                   definition_version,
                   status,
                   created_at,
                   updated_at,
                   error_summary,
                   outcome_name,
                   continue_as_new_generation,
                   archived_at,
                   saga_audits
            from orcacore_instance_projections summary
            where (@instance_id is null or summary.instance_id = @instance_id)
              and (@parent_instance_id is null or summary.parent_instance_id = @parent_instance_id)
              and (@root_instance_id is null or summary.root_instance_id = @root_instance_id)
              and (@definition_id is null or summary.definition_id = @definition_id)
              and (@definition_version is null or summary.definition_version = @definition_version)
              and (@status is null or summary.status = @status)
              and (@wait_event_name is null or exists (
                  select 1
                  from orcacore_active_wait_projections wait
                  where wait.instance_id = summary.instance_id
                    and wait.event_name = @wait_event_name))
              and (@wait_correlation_id is null or exists (
                  select 1
                  from orcacore_active_wait_projections wait
                  where wait.instance_id = summary.instance_id
                    and wait.correlation_id = @wait_correlation_id))
            order by instance_id;
            """,
            connection);
        AddProjectionQueryParameters(command, query);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var instanceId = new InstanceId(reader.GetGuid(0));
            snapshots.Add(new WorkflowInstanceSnapshot
            {
                InstanceId = instanceId,
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
                SagaAudits = reader.IsDBNull(12) ? [] : DeserializeSagaAudits(reader.GetString(12))
            });
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        var activeWaits = await LoadActiveWaitsAsync(
            connection,
            snapshots.Select(snapshot => snapshot.InstanceId).ToArray(),
            cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            snapshots[index] = snapshot with
            {
                ActiveWaits = activeWaits.TryGetValue(snapshot.InstanceId, out var waits) ? waits : []
            };
        }

        return snapshots;
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select count(*)
            from orcacore_instance_projections summary
            where (@instance_id is null or summary.instance_id = @instance_id)
              and (@parent_instance_id is null or summary.parent_instance_id = @parent_instance_id)
              and (@root_instance_id is null or summary.root_instance_id = @root_instance_id)
              and (@definition_id is null or summary.definition_id = @definition_id)
              and (@definition_version is null or summary.definition_version = @definition_version)
              and (@status is null or summary.status = @status)
              and (@wait_event_name is null or exists (
                  select 1
                  from orcacore_active_wait_projections wait
                  where wait.instance_id = summary.instance_id
                    and wait.event_name = @wait_event_name))
              and (@wait_correlation_id is null or exists (
                  select 1
                  from orcacore_active_wait_projections wait
                  where wait.instance_id = summary.instance_id
                    and wait.correlation_id = @wait_correlation_id));
            """,
            connection);
        AddProjectionQueryParameters(command, query);

        var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return checked((int)(long)(count ?? 0L));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        return (await ListAsync(query, cancellationToken).ConfigureAwait(false))
            .SelectMany(snapshot => snapshot.ActiveWaits)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        var groups = (await ListAsync(query, cancellationToken).ConfigureAwait(false))
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
            .ToArray();

        return new WorkflowStatistics
        {
            Groups = groups,
            Pressure = await LoadPressureMetricsAsync(cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task<WorkflowPressureMetrics> LoadPressureMetricsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select
                (select count(*) from orcacore_events) as stream_events,
                (select count(*) from orcacore_checkpoints) as checkpoints,
                (select count(*) from orcacore_outbox where state in (@pending, @retryable)) as pending_outbox,
                (select count(*) from orcacore_instance_projections
                    where status in (@running, @waiting, @paused)) as active_instances;
            """,
            connection);
        command.Parameters.AddWithValue("pending", OutboxRecordState.Pending.ToString());
        command.Parameters.AddWithValue("retryable", OutboxRecordState.Retryable.ToString());
        command.Parameters.AddWithValue("running", WorkflowStatus.Running.ToString());
        command.Parameters.AddWithValue("waiting", WorkflowStatus.Waiting.ToString());
        command.Parameters.AddWithValue("paused", WorkflowStatus.Paused.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new WorkflowPressureMetrics();
        }

        return new WorkflowPressureMetrics
        {
            TotalStreamEvents = reader.GetInt64(0),
            CheckpointCount = checked((int)reader.GetInt64(1)),
            PendingOutboxCount = checked((int)reader.GetInt64(2)),
            ActiveInstanceCount = checked((int)reader.GetInt64(3))
        };
    }

    /// <inheritdoc />
    public async Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_timers (
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
                false,
                null)
            on conflict (timer_id) do update set
                instance_id = excluded.instance_id,
                command_id = excluded.command_id,
                fire_at = excluded.fire_at,
                wakeup_name = excluded.wakeup_name,
                claimed = false,
                claimed_until = null;
            """,
            connection);
        command.Parameters.AddWithValue("timer_id", request.TimerId.Value);
        command.Parameters.AddWithValue("instance_id", request.InstanceId.Value);
        command.Parameters.AddWithValue("command_id", request.CommandId.Value);
        command.Parameters.AddWithValue("fire_at", request.FireAt);
        command.Parameters.AddWithValue("wakeup_name", request.WakeupName);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken)
    {
        return await ClaimDueAsync(
            new TimerClaimRequest(dueAtOrBefore, maxCount, dueAtOrBefore, DefaultLeaseDuration),
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxCount);
        ThrowIfInvalidLease(request.LeaseDuration);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            update orcacore_timers
            set
                claimed = true,
                claimed_until = @claimed_until
            where timer_id in (
                select timer_id
                from orcacore_timers
                where fire_at <= @due_at
                  and (claimed = false or claimed_until is null or claimed_until <= @claimed_at)
                order by fire_at, timer_id
                for update skip locked
                limit @max_count
            )
            returning timer_id, instance_id, command_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("due_at", request.DueAtOrBefore);
        command.Parameters.AddWithValue("claimed_at", request.ClaimedAt);
        command.Parameters.AddWithValue("claimed_until", request.ClaimedAt.Add(request.LeaseDuration));
        command.Parameters.AddWithValue("max_count", request.MaxCount);

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

    /// <inheritdoc />
    public async Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            delete from orcacore_timers
            where timer_id = @timer_id;
            """,
            connection);
        command.Parameters.AddWithValue("timer_id", timerId.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            update orcacore_timers
            set
                claimed = false,
                claimed_until = null
            where timer_id = @timer_id;
            """,
            connection);
        command.Parameters.AddWithValue("timer_id", timerId.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ArchiveResult> ArchiveAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        if (await IsActiveInstanceAsync(connection, transaction, policy.InstanceId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ArchiveResult { Archived = false, Reason = "Instance is active." };
        }

        var archived = await ArchiveInstanceAsync(
            connection,
            transaction,
            policy.InstanceId,
            policy.RequestedAt,
            cancellationToken).ConfigureAwait(false);
        if (!archived)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ArchiveResult { Archived = false, Reason = "Instance projection was not found." };
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ArchiveResult { Archived = true };
    }

    /// <inheritdoc />
    public async Task<PurgeResult> PurgeAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return await PurgeCoreAsync(policy.InstanceId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PurgeResult> PurgeAsync(InstanceId instanceId, CancellationToken cancellationToken)
    {
        return await PurgeCoreAsync(instanceId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PurgeResult> PurgeCoreAsync(InstanceId instanceId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        if (await IsActiveInstanceAsync(connection, transaction, instanceId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new PurgeResult { Purged = false, Reason = "Instance is active." };
        }

        if (await HasClaimedOutboxAsync(connection, transaction, instanceId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new PurgeResult { Purged = false, Reason = "Instance has claimed outbox records." };
        }

        await DeleteInstanceDataAsync(connection, transaction, instanceId, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new PurgeResult { Purged = true };
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return dataSource.DisposeAsync();
    }

    private static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return NpgsqlDataSource.Create(connectionString);
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
                continue_as_new_generation)
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
                @continue_as_new_generation)
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
                continue_as_new_generation = excluded.continue_as_new_generation;
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

    private static async Task UpsertTimerSchedulesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<TimerScheduleRequest> requests,
        CancellationToken cancellationToken)
    {
        foreach (var request in requests)
        {
            await UpsertTimerScheduleAsync(connection, transaction, request, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task UpsertTimerScheduleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TimerScheduleRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_timers (
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
                false,
                null)
            on conflict (timer_id) do update set
                instance_id = excluded.instance_id,
                command_id = excluded.command_id,
                fire_at = excluded.fire_at,
                wakeup_name = excluded.wakeup_name,
                claimed = false,
                claimed_until = null;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("timer_id", request.TimerId.Value);
        command.Parameters.AddWithValue("instance_id", request.InstanceId.Value);
        command.Parameters.AddWithValue("command_id", request.CommandId.Value);
        command.Parameters.AddWithValue("fire_at", request.FireAt);
        command.Parameters.AddWithValue("wakeup_name", request.WakeupName);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyProjectionOperationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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
                    await UpsertActiveWaitProjectionAsync(
                        connection,
                        transaction,
                        operation.InstanceId,
                        activeWait,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case ProjectionOperationKind.RemoveActiveWait when operation.WaitId is { } waitId:
                    await RemoveActiveWaitProjectionAsync(connection, transaction, waitId, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case ProjectionOperationKind.AppendHistory:
                    if (operation.History is { } history)
                    {
                        await AppendHistoryProjectionAsync(
                            connection,
                            transaction,
                            operation.InstanceId,
                            history,
                            cancellationToken).ConfigureAwait(false);
                    }

                    break;
                default:
                    break;
            }
        }
    }

    private static async Task AppendHistoryProjectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        ProjectionHistoryWrite history,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_history_projections (
                history_id,
                instance_id,
                recorded_at,
                kind,
                payload)
            values (
                @history_id,
                @instance_id,
                @recorded_at,
                @kind,
                @payload)
            on conflict (history_id) do nothing;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("history_id", history.HistoryId);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        command.Parameters.AddWithValue("recorded_at", history.RecordedAt);
        command.Parameters.AddWithValue("kind", history.Kind);
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = history.PayloadJson;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertSummaryProjectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkflowInstanceSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_instance_projections (
                instance_id,
                parent_instance_id,
                root_instance_id,
                definition_id,
                definition_version,
                status,
                created_at,
                updated_at,
                error_summary,
                outcome_name,
                continue_as_new_generation,
                archived_at,
                saga_audits)
            values (
                @instance_id,
                @parent_instance_id,
                @root_instance_id,
                @definition_id,
                @definition_version,
                @status,
                @created_at,
                @updated_at,
                @error_summary,
                @outcome_name,
                @continue_as_new_generation,
                @archived_at,
                @saga_audits)
            on conflict (instance_id) do update set
                parent_instance_id = excluded.parent_instance_id,
                root_instance_id = excluded.root_instance_id,
                definition_id = excluded.definition_id,
                definition_version = excluded.definition_version,
                status = excluded.status,
                created_at = excluded.created_at,
                updated_at = excluded.updated_at,
                error_summary = excluded.error_summary,
                outcome_name = excluded.outcome_name,
                continue_as_new_generation = excluded.continue_as_new_generation,
                archived_at = excluded.archived_at,
                saga_audits = excluded.saga_audits;
            delete from orcacore_active_wait_projections
            where instance_id = @instance_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("instance_id", snapshot.InstanceId.Value);
        command.Parameters.Add("parent_instance_id", NpgsqlDbType.Uuid).Value =
            (object?)snapshot.ParentInstanceId?.Value ?? DBNull.Value;
        command.Parameters.Add("root_instance_id", NpgsqlDbType.Uuid).Value =
            (object?)snapshot.RootInstanceId?.Value ?? DBNull.Value;
        command.Parameters.AddWithValue("definition_id", snapshot.DefinitionId.Value);
        command.Parameters.AddWithValue("definition_version", snapshot.DefinitionVersion.Value);
        command.Parameters.AddWithValue("status", snapshot.Status.ToString());
        command.Parameters.AddWithValue("created_at", snapshot.CreatedAt);
        command.Parameters.AddWithValue("updated_at", snapshot.UpdatedAt);
        command.Parameters.AddWithValue("error_summary", (object?)snapshot.ErrorSummary ?? DBNull.Value);
        command.Parameters.AddWithValue("outcome_name", (object?)snapshot.EndOutcomeName ?? DBNull.Value);
        command.Parameters.AddWithValue("continue_as_new_generation", snapshot.ContinueAsNewGeneration);
        command.Parameters.AddWithValue("archived_at", (object?)snapshot.ArchivedAt ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "saga_audits",
            NpgsqlDbType.Jsonb,
            JsonSerializer.Serialize(snapshot.SagaAudits, JsonOptions));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        foreach (var activeWait in snapshot.ActiveWaits)
        {
            await UpsertActiveWaitProjectionAsync(
                connection,
                transaction,
                snapshot.InstanceId,
                activeWait,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task UpsertActiveWaitProjectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        ActiveWaitSnapshot activeWait,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_active_wait_projections (
                wait_id,
                instance_id,
                event_name,
                correlation_id,
                registered_at,
                status,
                mode)
            values (
                @wait_id,
                @instance_id,
                @event_name,
                @correlation_id,
                @registered_at,
                @status,
                @mode)
            on conflict (wait_id) do update set
                instance_id = excluded.instance_id,
                event_name = excluded.event_name,
                correlation_id = excluded.correlation_id,
                registered_at = excluded.registered_at,
                status = excluded.status,
                mode = excluded.mode;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("wait_id", activeWait.WaitId.Value);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        command.Parameters.AddWithValue("event_name", activeWait.EventName);
        command.Parameters.AddWithValue("correlation_id", activeWait.CorrelationId.Value);
        command.Parameters.AddWithValue("registered_at", activeWait.RegisteredAt);
        command.Parameters.AddWithValue("status", activeWait.Status);
        command.Parameters.AddWithValue("mode", activeWait.Mode);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RemoveActiveWaitProjectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WaitId waitId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            delete from orcacore_active_wait_projections
            where wait_id = @wait_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("wait_id", waitId.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyDictionary<InstanceId, IReadOnlyList<ActiveWaitSnapshot>>> LoadActiveWaitsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<InstanceId> instanceIds,
        CancellationToken cancellationToken)
    {
        if (instanceIds.Count == 0)
        {
            return new Dictionary<InstanceId, IReadOnlyList<ActiveWaitSnapshot>>();
        }

        await using var command = new NpgsqlCommand(
            """
            select instance_id,
                   wait_id,
                   event_name,
                   correlation_id,
                   registered_at,
                   status,
                   mode
            from orcacore_active_wait_projections
            where instance_id = any(@instance_ids)
            order by instance_id, registered_at, wait_id;
            """,
            connection);
        command.Parameters.Add("instance_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            .Value = instanceIds.Select(instanceId => instanceId.Value).ToArray();

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

    private static void AddProjectionQueryParameters(NpgsqlCommand command, WorkflowProjectionQuery query)
    {
        AddNullableParameter(command, "instance_id", NpgsqlDbType.Uuid, query.InstanceId?.Value);
        AddNullableParameter(command, "parent_instance_id", NpgsqlDbType.Uuid, query.ParentInstanceId?.Value);
        AddNullableParameter(command, "root_instance_id", NpgsqlDbType.Uuid, query.RootInstanceId?.Value);
        AddNullableParameter(command, "definition_id", NpgsqlDbType.Uuid, query.DefinitionId?.Value);
        AddNullableParameter(command, "definition_version", NpgsqlDbType.Integer, query.DefinitionVersion?.Value);
        AddNullableParameter(command, "status", NpgsqlDbType.Text, query.Status?.ToString());
        AddNullableParameter(command, "wait_event_name", NpgsqlDbType.Text, query.ActiveWaitEventName);
        AddNullableParameter(command, "wait_correlation_id", NpgsqlDbType.Text, query.ActiveWaitCorrelationId?.Value);
    }

    private static void AddNullableParameter(
        NpgsqlCommand command,
        string name,
        NpgsqlDbType type,
        object? value)
    {
        var parameter = command.Parameters.Add(name, type);
        parameter.Value = value ?? DBNull.Value;
    }

    private static void ThrowIfInvalidLease(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be positive.");
        }
    }

    private static async Task<bool> IsActiveInstanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select status
            from orcacore_instance_projections
            where instance_id = @instance_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);

        var status = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return status is string statusText &&
            Enum.Parse<WorkflowStatus>(statusText) is
                WorkflowStatus.Running or
                WorkflowStatus.Waiting or
                WorkflowStatus.Paused;
    }

    private static async Task<bool> ArchiveInstanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        DateTimeOffset archivedAt,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            update orcacore_instance_projections
            set archived_at = @archived_at
            where instance_id = @instance_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        command.Parameters.AddWithValue("archived_at", archivedAt);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    private static async Task<bool> HasClaimedOutboxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select exists (
                select 1
                from orcacore_outbox
                where instance_id = @instance_id
                  and state = @claimed);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        command.Parameters.AddWithValue("claimed", OutboxRecordState.Claimed.ToString());

        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task DeleteInstanceDataAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        foreach (var sql in new[]
        {
            "delete from orcacore_active_wait_projections where instance_id = @instance_id;",
            "delete from orcacore_history_projections where instance_id = @instance_id;",
            "delete from orcacore_instance_projections where instance_id = @instance_id;",
            "delete from orcacore_checkpoints where instance_id = @instance_id;",
            "delete from orcacore_timers where instance_id = @instance_id;",
            "delete from orcacore_outbox where instance_id = @instance_id;",
            "delete from orcacore_events where stream_id = @instance_id;"
        })
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("instance_id", instanceId.Value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
