using System.Data;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;
using OrcaCore.Providers.Relational;
using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Providers.PostgreSql;

/// <summary>
/// Stores durable workflow events and checkpoints in PostgreSQL.
/// </summary>
internal sealed class PostgreSqlWorkflowStore :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowStartIdempotencyStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    ITimerScheduler,
    IAsyncDisposable
{
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
            DefinitionId = reader.IsDBNull(3) ? null : DefinitionId.Parse(reader.GetGuid(3).ToString()),
            DefinitionVersion = reader.IsDBNull(4) ? null : new DefinitionVersion(reader.GetInt32(4)),
            Status = reader.IsDBNull(5)
                ? null
                : Enum.Parse<global::OrcaCore.WorkflowInstanceStatus>(reader.GetString(5)),
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
            PostgreSqlJsonSerializerContext.Default.WorkflowRuntimeCheckpointState)
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
            var targetStorageKeys = batch.InboxTargetPoisonOperations
                .Select(operation => InboxTargetStorageKey(operation.InstanceId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var lockedStorageRevisions = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var storageKey in batch.InboxRouteMutations
                         .Select(mutation => InboxRouteStorageKey(mutation.Route))
                         .Concat(targetStorageKeys)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(key => key, StringComparer.Ordinal))
            {
                lockedStorageRevisions[storageKey] = await LockInboxStorageKeyAsync(
                    connection,
                    transaction,
                    storageKey,
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (var routeMutation in batch.InboxRouteMutations)
            {
                var actualRevision = lockedStorageRevisions[InboxRouteStorageKey(routeMutation.Route)];
                if (actualRevision != routeMutation.ExpectedRevision)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return EventStoreConflict.InboxRouteChanged(routeMutation.Route);
                }
            }

            foreach (var inboxWrite in batch.InboxOperations.Where(operation =>
                         operation.EnvelopeFingerprint is not null))
            {
                var existing = await GetInboxIdentityAsync(
                    connection,
                    transaction,
                    inboxWrite.EventId,
                    cancellationToken).ConfigureAwait(false);
                if (existing is not null &&
                    (existing.InstanceId is null || !existing.InstanceId.Equals(batch.StreamId.InstanceId) ||
                     !string.Equals(
                         existing.EnvelopeFingerprint,
                         inboxWrite.EnvelopeFingerprint,
                         StringComparison.Ordinal)))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return EventStoreConflict.EventIdAlreadyExists(inboxWrite.EventId);
                }
            }

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

            await ApplyInboxOperationsAsync(
                    connection,
                    transaction,
                    batch.StreamId.InstanceId,
                    batch.InboxOperations,
                    cancellationToken)
                .ConfigureAwait(false);
            await AdvanceInboxRoutesAsync(
                connection,
                transaction,
                batch.InboxRouteMutations,
                cancellationToken).ConfigureAwait(false);
            await ApplyInboxTargetPoisonOperationsAsync(
                connection,
                transaction,
                batch.InboxTargetPoisonOperations,
                cancellationToken).ConfigureAwait(false);
            foreach (var targetStorageKey in targetStorageKeys)
            {
                await SetInboxStorageRevisionAsync(
                    connection,
                    transaction,
                    targetStorageKey,
                    lockedStorageRevisions[targetStorageKey] + 1,
                    cancellationToken).ConfigureAwait(false);
            }

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
            if (batch.StartIdempotencyOperations.FirstOrDefault() is { } startWrite)
            {
                var existingStart = await GetStartedAsync(startWrite.IdempotencyKey, cancellationToken)
                    .ConfigureAwait(false);
                if (existingStart.HasValue)
                {
                    return EventStoreConflict.StartIdempotencyKeyAlreadyExists(startWrite.IdempotencyKey);
                }
            }

            if (batch.InboxOperations.FirstOrDefault(operation => operation.EnvelopeFingerprint is not null) is { } inboxWrite)
            {
                var existingEvent = await GetByEventIdAsync(inboxWrite.EventId, cancellationToken)
                    .ConfigureAwait(false);
                if (existingEvent.HasValue)
                {
                    return EventStoreConflict.EventIdAlreadyExists(inboxWrite.EventId);
                }
            }

            var actualVersion = await LoadActualVersionAsync(connection, null, batch.StreamId, cancellationToken)
                .ConfigureAwait(false);
            return EventStoreConflict.ExpectedVersionMismatch(batch.ExpectedVersion, actualVersion);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
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

        var events = new List<DurableWorkflowEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(WorkflowEventCodec.Deserialize(reader.GetString(0), reader.GetString(1)));
        }

        return events;
    }

    /// <inheritdoc />
    public async Task<InboxAcceptanceCommitResult> AcceptAsync(
        InboxAcceptance acceptance,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        var existing = await GetByEventIdAsync(acceptance.Envelope.EventId, cancellationToken).ConfigureAwait(false);
        if (existing.HasValue)
        {
            return ClassifyInboxAcceptance(existing.Value, acceptance.EnvelopeFingerprint);
        }

        var route = CreateInboxRoute(acceptance.Envelope);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var routeStorageKey = InboxRouteStorageKey(route);
            var targetStorageKey = route.Kind == "direct"
                ? InboxTargetStorageKey(route.InstanceId ?? throw new InvalidOperationException(
                    "A direct inbox route requires an instance."))
                : null;
            var lockedStorageRevisions = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var storageKey in new[] { routeStorageKey, targetStorageKey }
                         .Where(key => key is not null)
                         .Cast<string>()
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(key => key, StringComparer.Ordinal))
            {
                lockedStorageRevisions[storageKey] = await LockInboxStorageKeyAsync(
                    connection,
                    transaction,
                    storageKey,
                    cancellationToken).ConfigureAwait(false);
            }

            if (route.InstanceId is { } targetInstanceId)
            {
                var targetStatus = await LoadInboxTargetStatusAsync(
                    connection,
                    transaction,
                    targetInstanceId,
                    cancellationToken).ConfigureAwait(false);
                if (targetStatus is null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new InboxAcceptanceCommitResult(
                        InboxAcceptanceCommitDisposition.DirectInstanceNotFound,
                        null);
                }

                if (IsTerminal(targetStatus.Value))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new InboxAcceptanceCommitResult(
                        InboxAcceptanceCommitDisposition.DirectInstanceTerminal,
                        null);
                }
            }

            await using var command = new NpgsqlCommand(
                """
                insert into orcacore_inbox (
                    instance_id, event_id, envelope_fingerprint, state, route_key, accepted_at,
                    event_name, event_contract_version, correlation_id, causation_event_id, occurred_at,
                    route_kind, route_instance_id, route_definition_id, route_definition_version,
                    start_idempotency_key, workflow_input_content_type, workflow_input_payload,
                    payload_content_type, payload)
                values (
                    @instance_id, @event_id, @envelope_fingerprint, @state, @route_key, @accepted_at,
                    @event_name, @event_contract_version, @correlation_id, @causation_event_id, @occurred_at,
                    @route_kind, @route_instance_id, @route_definition_id, @route_definition_version,
                    @start_idempotency_key, @workflow_input_content_type, @workflow_input_payload,
                    @payload_content_type, @payload)
                returning acceptance_sequence;
                """,
                connection,
                transaction);
            command.Parameters.Add("instance_id", NpgsqlDbType.Uuid).Value =
                (object?)acceptance.Envelope.Route.InstanceId?.Value ?? DBNull.Value;
            command.Parameters.AddWithValue("event_id", acceptance.Envelope.EventId.Value);
            command.Parameters.AddWithValue("envelope_fingerprint", acceptance.EnvelopeFingerprint);
            command.Parameters.AddWithValue("state", InboxRecordState.Received.ToString());
            command.Parameters.AddWithValue("route_key", InboxRouteStorageKey(route));
            command.Parameters.AddWithValue("accepted_at", acceptance.AcceptedAt);
            AddEnvelopeParameters(command, acceptance.Envelope);
            var sequence = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("PostgreSQL did not return an inbox acceptance sequence."));
            await SetInboxStorageRevisionAsync(
                connection,
                transaction,
                routeStorageKey,
                lockedStorageRevisions[routeStorageKey] + 1,
                cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new InboxAcceptanceCommitResult(
                InboxAcceptanceCommitDisposition.Accepted,
                new InboxRecord(
                    acceptance.Envelope.Route.InstanceId,
                    acceptance.Envelope.EventId,
                    acceptance.EnvelopeFingerprint,
                    InboxRecordState.Received)
                {
                    Envelope = acceptance.Envelope,
                    Route = route,
                    AcceptanceSequence = sequence,
                    AcceptedAt = acceptance.AcceptedAt
                });
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken).ConfigureAwait(false);
            var raced = await GetByEventIdAsync(acceptance.Envelope.EventId, cancellationToken).ConfigureAwait(false);
            if (!raced.HasValue)
            {
                throw;
            }

            return ClassifyInboxAcceptance(raced.Value, acceptance.EnvelopeFingerprint);
        }
    }

    /// <inheritdoc />
    public async Task<InboxMatchSnapshot> GetMatchSnapshotAsync(
        InboxMatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        var revisions = new List<InboxRouteRevision>(request.Routes.Count);
        foreach (var route in request.Routes.OrderBy(InboxRouteStorageKey, StringComparer.Ordinal))
        {
            revisions.Add(new InboxRouteRevision(
                route,
                await LockInboxRouteAsync(connection, transaction, route, cancellationToken).ConfigureAwait(false)));
        }

        await using var command = new NpgsqlCommand(
            """
            select event_id
            from orcacore_inbox
            where state = @state
              and route_key = any(@route_keys)
            order by acceptance_sequence, event_id
            limit 1;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("state", InboxRecordState.Received.ToString());
        command.Parameters.AddWithValue(
            "route_keys",
            request.Routes.Select(InboxRouteStorageKey).ToArray());
        var eventIdValue = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        InboxRecord? pending = null;
        if (eventIdValue is not null)
        {
            var loaded = await GetByEventIdAsync(EventId.Create(eventIdValue), cancellationToken).ConfigureAwait(false);
            pending = loaded.HasValue ? loaded.Value : null;
        }

        return new InboxMatchSnapshot(pending, revisions);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InboxRecord>> ListReceivedAsync(
        long afterAcceptanceSequence,
        int maxCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterAcceptanceSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select event_id
            from orcacore_inbox
            where state = @state
              and acceptance_sequence > @after_acceptance_sequence
            order by acceptance_sequence, event_id
            limit @max_count;
            """,
            connection);
        command.Parameters.AddWithValue("state", InboxRecordState.Received.ToString());
        command.Parameters.AddWithValue("after_acceptance_sequence", afterAcceptanceSequence);
        command.Parameters.AddWithValue("max_count", maxCount);
        var eventIds = new List<EventId>(maxCount);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                eventIds.Add(EventId.Create(reader.GetString(0)));
            }
        }

        var records = new List<InboxRecord>(eventIds.Count);
        foreach (var eventId in eventIds)
        {
            var record = await GetByEventIdAsync(eventId, cancellationToken).ConfigureAwait(false);
            if (record.HasValue && record.Value.State == InboxRecordState.Received)
            {
                records.Add(record.Value);
            }
        }

        return records;
    }

    Task<IReadOnlyList<InboxRecord>> IWorkflowInboxStore.ListReceivedAsync(
        long afterAcceptanceSequence,
        int maxCount,
        CancellationToken cancellationToken) =>
        ListReceivedAsync(afterAcceptanceSequence, maxCount, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<InboxRecord>> ListHandoffRetriesAsync(
        DateTimeOffset eligibleAt,
        int maxCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select event_id
            from orcacore_inbox
            where state = @state
              and handoff_failure_count > 0
              and handoff_retry_not_before <= @eligible_at
            order by handoff_retry_not_before, acceptance_sequence, event_id
            limit @max_count;
            """,
            connection);
        command.Parameters.AddWithValue("state", InboxRecordState.Received.ToString());
        command.Parameters.AddWithValue("eligible_at", eligibleAt);
        command.Parameters.AddWithValue("max_count", maxCount);
        var eventIds = new List<EventId>(maxCount);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                eventIds.Add(EventId.Create(reader.GetString(0)));
            }
        }

        var records = new List<InboxRecord>(eventIds.Count);
        foreach (var eventId in eventIds)
        {
            var record = await GetByEventIdAsync(eventId, cancellationToken).ConfigureAwait(false);
            if (record.HasValue && record.Value.State == InboxRecordState.Received)
            {
                records.Add(record.Value);
            }
        }

        return records;
    }

    /// <inheritdoc />
    public async Task MarkPoisonedAsync(
        EventId eventId,
        InboxRecordState expectedState,
        string code,
        string? detail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var existing = await GetByEventIdAsync(eventId, cancellationToken).ConfigureAwait(false);
        if (!existing.HasValue)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        var route = existing.Value.Route;
        var revision = route is null
            ? (long?)null
            : await LockInboxRouteAsync(connection, transaction, route, cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            update orcacore_inbox
            set state = @state,
                poison_code = @poison_code,
                poison_detail = @poison_detail
            where event_id = @event_id
              and state = @expected_state;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("state", InboxRecordState.Poisoned.ToString());
        command.Parameters.AddWithValue("poison_code", code);
        command.Parameters.Add("poison_detail", NpgsqlDbType.Text).Value = (object?)detail ?? DBNull.Value;
        command.Parameters.AddWithValue("event_id", eventId.Value);
        command.Parameters.AddWithValue("expected_state", expectedState.ToString());
        var changed = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (changed != 0 && route is not null && revision is not null)
        {
            await SetInboxRouteRevisionAsync(
                connection,
                transaction,
                route,
                revision.Value + 1,
                cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecordHandoffFailureAsync(
        EventId eventId,
        InboxRecordState expectedState,
        int expectedFailureCount,
        int maxFailureCount,
        DateTimeOffset retryNotBefore,
        string code,
        string? detail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedFailureCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFailureCount);
        if (expectedFailureCount >= maxFailureCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedFailureCount),
                expectedFailureCount,
                "The observed failure count must be below the terminal failure count.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var existing = await GetByEventIdAsync(eventId, cancellationToken).ConfigureAwait(false);
        if (!existing.HasValue)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        var route = existing.Value.Route;
        var revision = route is null
            ? (long?)null
            : await LockInboxRouteAsync(connection, transaction, route, cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            update orcacore_inbox
            set handoff_failure_count = handoff_failure_count + 1,
                handoff_retry_not_before = case
                    when handoff_failure_count + 1 >= @max_failure_count then null
                    else @retry_not_before
                end,
                state = case
                    when handoff_failure_count + 1 >= @max_failure_count then @poisoned_state
                    else state
                end,
                poison_code = case
                    when handoff_failure_count + 1 >= @max_failure_count then @poison_code
                    else poison_code
                end,
                poison_detail = case
                    when handoff_failure_count + 1 >= @max_failure_count then @poison_detail
                    else poison_detail
                end
            where event_id = @event_id
              and state = @expected_state
              and handoff_failure_count = @expected_failure_count
            returning state;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("max_failure_count", maxFailureCount);
        command.Parameters.AddWithValue("retry_not_before", retryNotBefore);
        command.Parameters.AddWithValue("poisoned_state", InboxRecordState.Poisoned.ToString());
        command.Parameters.AddWithValue("poison_code", code);
        command.Parameters.Add("poison_detail", NpgsqlDbType.Text).Value = (object?)detail ?? DBNull.Value;
        command.Parameters.AddWithValue("event_id", eventId.Value);
        command.Parameters.AddWithValue("expected_state", expectedState.ToString());
        command.Parameters.AddWithValue("expected_failure_count", expectedFailureCount);
        var resultingState = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (string.Equals(resultingState, InboxRecordState.Poisoned.ToString(), StringComparison.Ordinal) &&
            route is not null &&
            revision is not null)
        {
            await SetInboxRouteRevisionAsync(
                connection,
                transaction,
                route,
                revision.Value + 1,
                cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Option<InboxRecord>> GetAsync(
        InstanceId instanceId,
        EventId eventId,
        CancellationToken cancellationToken)
    {
        var record = await GetByEventIdAsync(eventId, cancellationToken).ConfigureAwait(false);
        return record.HasValue && record.Value.InstanceId?.Equals(instanceId) == true
            ? record
            : Option<InboxRecord>.None;
    }

    /// <inheritdoc />
    public async Task<Option<InboxRecord>> GetByEventIdAsync(
        EventId eventId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select
                instance_id,
                envelope_fingerprint,
                state,
                event_name,
                event_contract_version,
                correlation_id,
                causation_event_id,
                occurred_at,
                route_kind,
                route_instance_id,
                route_definition_id,
                route_definition_version,
                start_idempotency_key,
                workflow_input_content_type,
                workflow_input_payload,
                payload_content_type,
                payload,
                acceptance_sequence,
                accepted_at,
                poison_code,
                poison_detail,
                handoff_failure_count,
                handoff_retry_not_before
            from orcacore_inbox
            where event_id = @event_id;
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return Option<InboxRecord>.None;
        }

        var instanceId = reader.IsDBNull(0) ? null : InstanceId.Parse(reader.GetGuid(0).ToString());
        var envelope = reader.IsDBNull(3)
            ? null
            : new DurableEventEnvelope
            {
                EventId = eventId,
                EventName = reader.GetString(3),
                EventContractVersion = reader.GetInt32(4),
                CorrelationId = CorrelationId.Create(reader.GetString(5)),
                CausationEventId = reader.IsDBNull(6) ? null : EventId.Create(reader.GetString(6)),
                OccurredAt = reader.GetFieldValue<DateTimeOffset>(7),
                Route = new DurableEventRouteEnvelope
                {
                    Kind = reader.GetString(8),
                    InstanceId = reader.IsDBNull(9) ? null : InstanceId.Parse(reader.GetGuid(9).ToString()),
                    DefinitionId = reader.IsDBNull(10) ? null : DefinitionId.Parse(reader.GetGuid(10).ToString()),
                    DefinitionVersion = reader.IsDBNull(11) ? null : new DefinitionVersion(reader.GetInt32(11)),
                    StartIdempotencyKey = reader.IsDBNull(12) ? null : reader.GetString(12),
                    WorkflowInputContentType = reader.IsDBNull(13) ? null : reader.GetString(13),
                    WorkflowInputPayload = reader.IsDBNull(14) ? null : reader.GetFieldValue<byte[]>(14)
                },
                PayloadContentType = reader.IsDBNull(15) ? null : reader.GetString(15),
                Payload = reader.IsDBNull(16) ? null : reader.GetFieldValue<byte[]>(16)
            };
        return Option<InboxRecord>.Some(new InboxRecord(
            instanceId,
            eventId,
            reader.GetString(1),
            Enum.Parse<InboxRecordState>(reader.GetString(2)))
        {
            Envelope = envelope,
            Route = envelope is null ? null : CreateInboxRoute(envelope),
            AcceptanceSequence = reader.IsDBNull(17) ? 0 : reader.GetInt64(17),
            AcceptedAt = reader.GetFieldValue<DateTimeOffset>(18),
            PoisonCode = reader.IsDBNull(19) ? null : reader.GetString(19),
            PoisonDetail = reader.IsDBNull(20) ? null : reader.GetString(20),
            HandoffFailureCount = reader.GetInt32(21),
            HandoffRetryNotBefore = reader.IsDBNull(22) ? null : reader.GetFieldValue<DateTimeOffset>(22)
        });
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
            select
                instance_id,
                definition_id,
                definition_version,
                definition_fingerprint,
                input_fingerprint
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
            InstanceId.Parse(reader.GetGuid(0).ToString()),
            DefinitionId.Parse(reader.GetGuid(1).ToString()),
            new DefinitionVersion(reader.GetInt32(2)),
            reader.GetString(3),
            reader.GetString(4)));
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
        // DR-037: a kind selector partitions the outbox so disjoint pumps claim disjoint
        // record kinds; the ix_orcacore_outbox_kind index keeps the partitioned scan cheap.
        var kindPredicate = request.KindSelector switch
        {
            { Include: not null } => " and kind = any(@kinds)",
            { Exclude: not null } => " and kind <> all(@kinds)",
            _ => string.Empty
        };
        await using var command = new NpgsqlCommand(
            $"""
            update orcacore_outbox
            set
                state = @claimed,
                claimed_until = @claimed_until
            where outbox_record_id in (
                select outbox_record_id
                from orcacore_outbox
                where (state in (@pending, @retryable)
                   or (state = @claimed and (claimed_until is null or claimed_until <= @claimed_at))){kindPredicate}
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
        if (request.KindSelector is { } selector)
        {
            command.Parameters.AddWithValue(
                "kinds",
                (selector.Include ?? selector.Exclude!).ToArray());
        }

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
    public Task<Option<WorkflowProjectionSnapshot>> GetAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        projectionStore.GetAsync(instanceId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken) =>
        projectionStore.FindActiveWaitsAsync(definitionId, eventName, correlationId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        EventContractVersion eventContractVersion,
        CorrelationId correlationId,
        CancellationToken cancellationToken) =>
        projectionStore.FindActiveWaitsAsync(
            definitionId,
            eventName,
            eventContractVersion,
            correlationId,
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> ListLeaseRecoveryCandidatesAsync(
        CancellationToken cancellationToken) =>
        projectionStore.ListLeaseRecoveryCandidatesAsync(cancellationToken);

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

    internal Task<(bool Purged, string? Reason)> PurgeForRetentionAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        return retentionStore.PurgeForRetentionAsync(instanceId, cancellationToken);
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
        DurableWorkflowEvent workflowEvent,
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
            PostgreSqlJsonSerializerContext.Default.WorkflowRuntimeCheckpointState);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ExistingInboxIdentity?> GetInboxIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        EventId eventId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select instance_id, envelope_fingerprint
            from orcacore_inbox
            where event_id = @event_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("event_id", eventId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new ExistingInboxIdentity(
                reader.IsDBNull(0) ? null : InstanceId.Parse(reader.GetGuid(0).ToString()),
                reader.GetString(1))
            : null;
    }

    private static async Task ApplyInboxOperationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        IEnumerable<InboxWrite> operations,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            if (operation.EnvelopeFingerprint is null)
            {
                await using var update = new NpgsqlCommand(
                    """
                    update orcacore_inbox
                    set state = case
                        when state = @applied then state
                        else @state
                    end,
                        instance_id = coalesce(@target_instance_id, instance_id)
                    where event_id = @event_id
                      and (instance_id = @instance_id or
                           (instance_id is null and @target_instance_id = @instance_id))
                      and (@expected_state is null or state = @expected_state);
                    """,
                    connection,
                    transaction);
                update.Parameters.AddWithValue("instance_id", instanceId.Value);
                update.Parameters.AddWithValue("event_id", operation.EventId.Value);
                update.Parameters.AddWithValue("state", operation.State.ToString());
                update.Parameters.AddWithValue("applied", InboxRecordState.Applied.ToString());
                update.Parameters.Add("target_instance_id", NpgsqlDbType.Uuid).Value =
                    (object?)operation.TargetInstanceId?.Value ?? DBNull.Value;
                update.Parameters.Add("expected_state", NpgsqlDbType.Text).Value =
                    (object?)operation.ExpectedState?.ToString() ?? DBNull.Value;
                if (await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                {
                    throw new InvalidOperationException(
                        $"Inbox transition '{operation.EventId}' for instance '{instanceId}' " +
                        "has no accepted envelope record.");
                }

                continue;
            }

            await using (var transition = new NpgsqlCommand(
                """
                update orcacore_inbox
                set state = case
                    when state = @applied then state
                    else @state
                end,
                    instance_id = coalesce(@target_instance_id, instance_id)
                where event_id = @event_id
                  and envelope_fingerprint = @envelope_fingerprint;
                """,
                connection,
                transaction))
            {
                transition.Parameters.AddWithValue("instance_id", instanceId.Value);
                transition.Parameters.AddWithValue("event_id", operation.EventId.Value);
                transition.Parameters.AddWithValue("envelope_fingerprint", operation.EnvelopeFingerprint);
                transition.Parameters.AddWithValue("state", operation.State.ToString());
                transition.Parameters.AddWithValue("applied", InboxRecordState.Applied.ToString());
                transition.Parameters.Add("target_instance_id", NpgsqlDbType.Uuid).Value =
                    (object?)operation.TargetInstanceId?.Value ?? DBNull.Value;
                if (await transition.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1)
                {
                    continue;
                }
            }

            await using var command = new NpgsqlCommand(
                """
                insert into orcacore_inbox (
                    instance_id,
                    event_id,
                    envelope_fingerprint,
                    state,
                    event_name,
                    event_contract_version,
                    correlation_id,
                    causation_event_id,
                    occurred_at,
                    route_kind,
                    route_instance_id,
                    route_definition_id,
                    route_definition_version,
                    start_idempotency_key,
                    workflow_input_content_type,
                    workflow_input_payload,
                    payload_content_type,
                    payload)
                values (
                    @instance_id,
                    @event_id,
                    @envelope_fingerprint,
                    @state,
                    @event_name,
                    @event_contract_version,
                    @correlation_id,
                    @causation_event_id,
                    @occurred_at,
                    @route_kind,
                    @route_instance_id,
                    @route_definition_id,
                    @route_definition_version,
                    @start_idempotency_key,
                    @workflow_input_content_type,
                    @workflow_input_payload,
                    @payload_content_type,
                    @payload);
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("instance_id", instanceId.Value);
            command.Parameters.AddWithValue("event_id", operation.EventId.Value);
            command.Parameters.AddWithValue("envelope_fingerprint", operation.EnvelopeFingerprint);
            command.Parameters.AddWithValue("state", operation.State.ToString());
            AddEnvelopeParameters(command, operation.Envelope);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void AddEnvelopeParameters(NpgsqlCommand command, DurableEventEnvelope? envelope)
    {
        command.Parameters.Add("event_name", NpgsqlDbType.Text).Value =
            (object?)envelope?.EventName ?? DBNull.Value;
        command.Parameters.Add("event_contract_version", NpgsqlDbType.Integer).Value =
            (object?)envelope?.EventContractVersion ?? DBNull.Value;
        command.Parameters.Add("correlation_id", NpgsqlDbType.Text).Value =
            (object?)envelope?.CorrelationId.Value ?? DBNull.Value;
        command.Parameters.Add("causation_event_id", NpgsqlDbType.Text).Value =
            (object?)envelope?.CausationEventId?.Value ?? DBNull.Value;
        command.Parameters.Add("occurred_at", NpgsqlDbType.TimestampTz).Value =
            (object?)envelope?.OccurredAt ?? DBNull.Value;
        command.Parameters.Add("route_kind", NpgsqlDbType.Text).Value =
            (object?)envelope?.Route.Kind ?? DBNull.Value;
        command.Parameters.Add("route_instance_id", NpgsqlDbType.Uuid).Value =
            (object?)envelope?.Route.InstanceId?.Value ?? DBNull.Value;
        command.Parameters.Add("route_definition_id", NpgsqlDbType.Uuid).Value =
            (object?)envelope?.Route.DefinitionId?.Value ?? DBNull.Value;
        command.Parameters.Add("route_definition_version", NpgsqlDbType.Integer).Value =
            (object?)envelope?.Route.DefinitionVersion?.Value ?? DBNull.Value;
        command.Parameters.Add("start_idempotency_key", NpgsqlDbType.Text).Value =
            (object?)envelope?.Route.StartIdempotencyKey ?? DBNull.Value;
        command.Parameters.Add("workflow_input_content_type", NpgsqlDbType.Text).Value =
            (object?)envelope?.Route.WorkflowInputContentType ?? DBNull.Value;
        command.Parameters.Add("workflow_input_payload", NpgsqlDbType.Bytea).Value =
            (object?)envelope?.Route.WorkflowInputPayload ?? DBNull.Value;
        command.Parameters.Add("payload_content_type", NpgsqlDbType.Text).Value =
            (object?)envelope?.PayloadContentType ?? DBNull.Value;
        command.Parameters.Add("payload", NpgsqlDbType.Bytea).Value = (object?)envelope?.Payload ?? DBNull.Value;
    }

    private static InboxAcceptanceCommitResult ClassifyInboxAcceptance(
        InboxRecord existing,
        string fingerprint) =>
        new(
            string.Equals(existing.EnvelopeFingerprint, fingerprint, StringComparison.Ordinal)
                ? InboxAcceptanceCommitDisposition.Duplicate
                : InboxAcceptanceCommitDisposition.Conflict,
            existing);

    private static InboxRouteKey CreateInboxRoute(DurableEventEnvelope envelope) =>
        envelope.Route.Kind switch
        {
            "direct" => InboxRouteKey.Direct(
                envelope.Route.InstanceId ?? throw new InvalidOperationException("A direct inbox route requires an instance."),
                EventName.Create(envelope.EventName),
                new EventContractVersion(envelope.EventContractVersion),
                envelope.CorrelationId),
            "correlation" => InboxRouteKey.Correlation(
                envelope.Route.DefinitionId ?? throw new InvalidOperationException("A correlation inbox route requires a definition."),
                EventName.Create(envelope.EventName),
                new EventContractVersion(envelope.EventContractVersion),
                envelope.CorrelationId),
            _ => throw new NotSupportedException(
                $"Inbox buffering for route '{envelope.Route.Kind}' is owned by a later Section 7B task.")
        };

    private static string InboxRouteStorageKey(InboxRouteKey route)
    {
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        return route.Kind == "direct"
            ? $"direct|{route.InstanceId!.Value:D}|{route.EventContractVersion.Value}|" +
              $"{Encode(route.EventName.Value)}|{Encode(route.CorrelationId.Value)}"
            : $"correlation|{route.DefinitionId!.Value:D}|{route.EventContractVersion.Value}|" +
              $"{Encode(route.EventName.Value)}|{Encode(route.CorrelationId.Value)}";
    }

    private static string InboxTargetStorageKey(InstanceId instanceId) =>
        $"direct-target|{instanceId.Value:D}";

    private static Task<long> LockInboxRouteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InboxRouteKey route,
        CancellationToken cancellationToken)
        => LockInboxStorageKeyAsync(connection, transaction, InboxRouteStorageKey(route), cancellationToken);

    private static async Task<long> LockInboxStorageKeyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string storageKey,
        CancellationToken cancellationToken)
    {
        await using (var insert = new NpgsqlCommand(
            "insert into orcacore_inbox_routes (route_key, revision) values (@route_key, 0) on conflict do nothing;",
            connection,
            transaction))
        {
            insert.Parameters.AddWithValue("route_key", storageKey);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var select = new NpgsqlCommand(
            "select revision from orcacore_inbox_routes where route_key = @route_key for update;",
            connection,
            transaction);
        select.Parameters.AddWithValue("route_key", storageKey);
        return (long)(await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The inbox route revision was not materialized."));
    }

    private static Task SetInboxRouteRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InboxRouteKey route,
        long revision,
        CancellationToken cancellationToken)
        => SetInboxStorageRevisionAsync(
            connection,
            transaction,
            InboxRouteStorageKey(route),
            revision,
            cancellationToken);

    private static async Task SetInboxStorageRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string storageKey,
        long revision,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "update orcacore_inbox_routes set revision = @revision where route_key = @route_key;",
            connection,
            transaction);
        command.Parameters.AddWithValue("revision", revision);
        command.Parameters.AddWithValue("route_key", storageKey);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<global::OrcaCore.WorkflowInstanceStatus?> LoadInboxTargetStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select status from orcacore_instance_projections where instance_id = @instance_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        return value is null
            ? null
            : Enum.Parse<global::OrcaCore.WorkflowInstanceStatus>(value, ignoreCase: false);
    }

    private static bool IsTerminal(global::OrcaCore.WorkflowInstanceStatus status) =>
        status is global::OrcaCore.WorkflowInstanceStatus.Completed or
            global::OrcaCore.WorkflowInstanceStatus.Failed or
            global::OrcaCore.WorkflowInstanceStatus.TimedOut or
            global::OrcaCore.WorkflowInstanceStatus.Cancelled or
            global::OrcaCore.WorkflowInstanceStatus.Terminated;

    private static async Task AdvanceInboxRoutesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<InboxRouteMutation> mutations,
        CancellationToken cancellationToken)
    {
        foreach (var mutation in mutations)
        {
            await SetInboxRouteRevisionAsync(
                connection,
                transaction,
                mutation.Route,
                mutation.ExpectedRevision + 1,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ApplyInboxTargetPoisonOperationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<InboxTargetPoisonWrite> operations,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            await using var command = new NpgsqlCommand(
                """
                update orcacore_inbox
                set state = @poisoned,
                    poison_code = @poison_code,
                    poison_detail = @poison_detail
                where state = @received
                  and route_kind = 'direct'
                  and route_instance_id = @instance_id;
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("poisoned", InboxRecordState.Poisoned.ToString());
            command.Parameters.AddWithValue("received", InboxRecordState.Received.ToString());
            command.Parameters.AddWithValue("poison_code", operation.Code);
            command.Parameters.Add("poison_detail", NpgsqlDbType.Text).Value =
                (object?)operation.Detail ?? DBNull.Value;
            command.Parameters.AddWithValue("instance_id", operation.InstanceId.Value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record ExistingInboxIdentity(
        InstanceId? InstanceId,
        string EnvelopeFingerprint);

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
                    definition_version,
                    definition_fingerprint,
                    input_fingerprint)
                values (
                    @idempotency_key,
                    @instance_id,
                    @definition_id,
                    @definition_version,
                    @definition_fingerprint,
                    @input_fingerprint);
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("idempotency_key", operation.IdempotencyKey);
            command.Parameters.AddWithValue("instance_id", operation.InstanceId.Value);
            command.Parameters.AddWithValue("definition_id", operation.DefinitionId.Value);
            command.Parameters.AddWithValue("definition_version", operation.DefinitionVersion.Value);
            command.Parameters.AddWithValue("definition_fingerprint", operation.DefinitionFingerprint);
            command.Parameters.AddWithValue("input_fingerprint", operation.InputFingerprint);

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

}
