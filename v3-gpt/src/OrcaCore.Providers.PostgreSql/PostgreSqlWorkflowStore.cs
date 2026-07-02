using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.PostgreSql;

/// <summary>
/// Stores durable workflow events and checkpoints in PostgreSQL.
/// </summary>
public sealed class PostgreSqlWorkflowStore : IWorkflowEventStore, IAsyncDisposable
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
            Status = reader.IsDBNull(5) ? null : Enum.Parse<Abstractions.Instances.WorkflowStatus>(reader.GetString(5)),
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
