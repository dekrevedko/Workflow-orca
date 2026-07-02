using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.PostgreSql;

/// <summary>
/// Stores durable workflow events and checkpoints in PostgreSQL.
/// </summary>
public sealed class PostgreSqlWorkflowStore :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    IWorkflowRetentionStore,
    IAsyncDisposable
{
    private const string StartedEventType = nameof(WorkflowStartedEvent);
    private const string StepCompletedEventType = nameof(WorkflowStepCompletedEvent);
    private const string StepFailedEventType = nameof(WorkflowStepFailedEvent);
    private const string WaitRegisteredEventType = nameof(WorkflowWaitRegisteredEvent);
    private const string WaitMatchedEventType = nameof(WorkflowWaitMatchedEvent);
    private const string PausedEventType = nameof(WorkflowPausedEvent);
    private const string ResumedEventType = nameof(WorkflowResumedEvent);
    private const string DeliveryBufferedEventType = nameof(WorkflowDeliveryBufferedEvent);
    private const string DeliveryDiscardedEventType = nameof(WorkflowDeliveryDiscardedEvent);
    private const string CompletedEventType = nameof(WorkflowCompletedEvent);
    private const string TerminalEventType = nameof(WorkflowTerminalEvent);

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly NpgsqlDataSource dataSource;

    /// <summary>
    /// Initializes a PostgreSQL workflow store from a connection string.
    /// </summary>
    public PostgreSqlWorkflowStore(string connectionString)
        : this(NpgsqlDataSource.Create(connectionString))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
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
        await using var command = new NpgsqlCommand(
            """
            create table if not exists orcacore_events (
                stream_id uuid not null,
                version bigint not null,
                event_id uuid not null,
                event_type text not null,
                occurred_at timestamp with time zone not null,
                payload jsonb not null,
                primary key (stream_id, version),
                unique (event_id)
            );

            create index if not exists ix_orcacore_events_stream_id_version
                on orcacore_events (stream_id, version);

            create table if not exists orcacore_checkpoints (
                instance_id uuid primary key,
                stream_version bigint not null,
                content_type text not null,
                payload bytea not null,
                definition_id uuid null,
                definition_version integer null,
                status text null,
                last_step_path text null,
                error_summary text null,
                outcome_name text null
            );

            create table if not exists orcacore_inbox (
                event_id uuid primary key,
                state text not null
            );

            create table if not exists orcacore_outbox (
                outbox_record_id uuid primary key,
                instance_id uuid not null,
                kind text not null,
                payload bytea not null,
                state text not null
            );

            create index if not exists ix_orcacore_outbox_state
                on orcacore_outbox (state);

            create table if not exists orcacore_instance_projections (
                instance_id uuid primary key,
                definition_id uuid not null,
                definition_version integer not null,
                status text not null,
                created_at timestamp with time zone not null,
                updated_at timestamp with time zone not null,
                error_summary text null,
                outcome_name text null
            );

            create table if not exists orcacore_active_wait_projections (
                wait_id uuid primary key,
                instance_id uuid not null,
                event_name text not null,
                correlation_id text not null,
                registered_at timestamp with time zone not null,
                status text not null,
                mode text not null
            );

            create index if not exists ix_orcacore_active_wait_lookup
                on orcacore_active_wait_projections (event_name, correlation_id);
            """,
            connection);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
                   outcome_name
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
            OutcomeName = reader.IsDBNull(8) ? null : reader.GetString(8)
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
            await InsertOutboxRecordsAsync(connection, transaction, batch.StreamId.InstanceId, batch.OutboxRecords, cancellationToken)
                .ConfigureAwait(false);
            await ApplyProjectionOperationsAsync(connection, transaction, batch.ProjectionOperations, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result<AppendEventsResult>.Success(new AppendEventsResult(new StreamVersion(nextVersion)));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return EventStoreConflict.ExpectedVersionMismatch(batch.ExpectedVersion, batch.ExpectedVersion);
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
            events.Add(DeserializeEvent(reader.GetString(0), reader.GetString(1)));
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
    public async Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxCount);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            update orcacore_outbox
            set state = @claimed
            where outbox_record_id in (
                select outbox_record_id
                from orcacore_outbox
                where state in (@pending, @retryable)
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
        command.Parameters.AddWithValue("max_count", maxCount);

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
            set state = @state
            where outbox_record_id = @outbox_record_id;
            """,
            connection);
        command.Parameters.AddWithValue("outbox_record_id", outboxRecordId.Value);
        command.Parameters.AddWithValue("state", state.ToString());

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
                   definition_id,
                   definition_version,
                   status,
                   created_at,
                   updated_at,
                   error_summary,
                   outcome_name
            from orcacore_instance_projections summary
            where (@instance_id is null or summary.instance_id = @instance_id)
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
                DefinitionId = new DefinitionId(reader.GetGuid(1)),
                DefinitionVersion = new DefinitionVersion(reader.GetInt32(2)),
                Status = Enum.Parse<WorkflowStatus>(reader.GetString(3)),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(4),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(5),
                ErrorSummary = reader.IsDBNull(6) ? null : reader.GetString(6),
                EndOutcomeName = reader.IsDBNull(7) ? null : reader.GetString(7),
                ActiveWaits = await LoadActiveWaitsAsync(instanceId, cancellationToken).ConfigureAwait(false)
            });
        }

        return snapshots;
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        return (await ListAsync(query, cancellationToken).ConfigureAwait(false)).Count;
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

        return new WorkflowStatistics { Groups = groups };
    }

    /// <inheritdoc />
    public async Task<PurgeResult> PurgeAsync(InstanceId instanceId, CancellationToken cancellationToken)
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

    private static async Task<StreamVersion> LoadActualVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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
        command.Parameters.AddWithValue("event_type", ToEventType(workflowEvent));
        command.Parameters.AddWithValue("occurred_at", workflowEvent.OccurredAt);
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = SerializeEvent(workflowEvent);

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
                outcome_name)
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
                @outcome_name)
            on conflict (instance_id) do update set
                stream_version = excluded.stream_version,
                content_type = excluded.content_type,
                payload = excluded.payload,
                definition_id = excluded.definition_id,
                definition_version = excluded.definition_version,
                status = excluded.status,
                last_step_path = excluded.last_step_path,
                error_summary = excluded.error_summary,
                outcome_name = excluded.outcome_name;
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
                    break;
                default:
                    break;
            }
        }
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
                definition_id,
                definition_version,
                status,
                created_at,
                updated_at,
                error_summary,
                outcome_name)
            values (
                @instance_id,
                @definition_id,
                @definition_version,
                @status,
                @created_at,
                @updated_at,
                @error_summary,
                @outcome_name)
            on conflict (instance_id) do update set
                definition_id = excluded.definition_id,
                definition_version = excluded.definition_version,
                status = excluded.status,
                created_at = excluded.created_at,
                updated_at = excluded.updated_at,
                error_summary = excluded.error_summary,
                outcome_name = excluded.outcome_name;
            delete from orcacore_active_wait_projections
            where instance_id = @instance_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("instance_id", snapshot.InstanceId.Value);
        command.Parameters.AddWithValue("definition_id", snapshot.DefinitionId.Value);
        command.Parameters.AddWithValue("definition_version", snapshot.DefinitionVersion.Value);
        command.Parameters.AddWithValue("status", snapshot.Status.ToString());
        command.Parameters.AddWithValue("created_at", snapshot.CreatedAt);
        command.Parameters.AddWithValue("updated_at", snapshot.UpdatedAt);
        command.Parameters.AddWithValue("error_summary", (object?)snapshot.ErrorSummary ?? DBNull.Value);
        command.Parameters.AddWithValue("outcome_name", (object?)snapshot.EndOutcomeName ?? DBNull.Value);

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

    private async Task<IReadOnlyList<ActiveWaitSnapshot>> LoadActiveWaitsAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select wait_id,
                   event_name,
                   correlation_id,
                   registered_at,
                   status,
                   mode
            from orcacore_active_wait_projections
            where instance_id = @instance_id
            order by registered_at, wait_id;
            """,
            connection);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);

        var waits = new List<ActiveWaitSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            waits.Add(new ActiveWaitSnapshot
            {
                WaitId = new WaitId(reader.GetGuid(0)),
                EventName = reader.GetString(1),
                CorrelationId = new CorrelationId(reader.GetString(2)),
                RegisteredAt = reader.GetFieldValue<DateTimeOffset>(3),
                Status = reader.GetString(4),
                Mode = reader.GetString(5)
            });
        }

        return waits;
    }

    private static void AddProjectionQueryParameters(NpgsqlCommand command, WorkflowProjectionQuery query)
    {
        AddNullableParameter(command, "instance_id", NpgsqlDbType.Uuid, query.InstanceId?.Value);
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
            "delete from orcacore_instance_projections where instance_id = @instance_id;",
            "delete from orcacore_checkpoints where instance_id = @instance_id;",
            "delete from orcacore_outbox where instance_id = @instance_id;",
            "delete from orcacore_events where stream_id = @instance_id;"
        })
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("instance_id", instanceId.Value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string SerializeEvent(WorkflowEvent workflowEvent)
    {
        return JsonSerializer.Serialize(workflowEvent, workflowEvent.GetType(), JsonOptions);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new EventIdJsonConverter());
        options.Converters.Add(new InstanceIdJsonConverter());
        options.Converters.Add(new CommandIdJsonConverter());
        options.Converters.Add(new CausationIdJsonConverter());
        options.Converters.Add(new DefinitionIdJsonConverter());
        options.Converters.Add(new DefinitionVersionJsonConverter());
        options.Converters.Add(new WaitIdJsonConverter());
        options.Converters.Add(new CorrelationIdJsonConverter());
        return options;
    }

    private static WorkflowEvent DeserializeEvent(string eventType, string payload)
    {
        return eventType switch
        {
            StartedEventType => Required(JsonSerializer.Deserialize<WorkflowStartedEvent>(payload, JsonOptions)),
            StepCompletedEventType => Required(JsonSerializer.Deserialize<WorkflowStepCompletedEvent>(payload, JsonOptions)),
            StepFailedEventType => Required(JsonSerializer.Deserialize<WorkflowStepFailedEvent>(payload, JsonOptions)),
            WaitRegisteredEventType => Required(JsonSerializer.Deserialize<WorkflowWaitRegisteredEvent>(payload, JsonOptions)),
            WaitMatchedEventType => Required(JsonSerializer.Deserialize<WorkflowWaitMatchedEvent>(payload, JsonOptions)),
            PausedEventType => Required(JsonSerializer.Deserialize<WorkflowPausedEvent>(payload, JsonOptions)),
            ResumedEventType => Required(JsonSerializer.Deserialize<WorkflowResumedEvent>(payload, JsonOptions)),
            DeliveryBufferedEventType => Required(JsonSerializer.Deserialize<WorkflowDeliveryBufferedEvent>(payload, JsonOptions)),
            DeliveryDiscardedEventType => Required(JsonSerializer.Deserialize<WorkflowDeliveryDiscardedEvent>(payload, JsonOptions)),
            CompletedEventType => Required(JsonSerializer.Deserialize<WorkflowCompletedEvent>(payload, JsonOptions)),
            TerminalEventType => Required(JsonSerializer.Deserialize<WorkflowTerminalEvent>(payload, JsonOptions)),
            _ => throw new InvalidOperationException($"Workflow event type '{eventType}' is not supported.")
        };
    }

    private static string ToEventType(WorkflowEvent workflowEvent)
    {
        return workflowEvent switch
        {
            WorkflowStartedEvent => StartedEventType,
            WorkflowStepCompletedEvent => StepCompletedEventType,
            WorkflowStepFailedEvent => StepFailedEventType,
            WorkflowWaitRegisteredEvent => WaitRegisteredEventType,
            WorkflowWaitMatchedEvent => WaitMatchedEventType,
            WorkflowPausedEvent => PausedEventType,
            WorkflowResumedEvent => ResumedEventType,
            WorkflowDeliveryBufferedEvent => DeliveryBufferedEventType,
            WorkflowDeliveryDiscardedEvent => DeliveryDiscardedEventType,
            WorkflowCompletedEvent => CompletedEventType,
            WorkflowTerminalEvent => TerminalEventType,
            _ => throw new InvalidOperationException(
                $"Workflow event '{workflowEvent.GetType().Name}' is not supported.")
        };
    }

    private static TEvent Required<TEvent>(TEvent? workflowEvent)
        where TEvent : WorkflowEvent
    {
        return workflowEvent ?? throw new JsonException("Workflow event payload could not be deserialized.");
    }
}
