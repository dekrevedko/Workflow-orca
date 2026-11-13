using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Serialization;

namespace OrcaCore.Abstractions.Providers.Dag.Internal;

internal sealed class DagTransactionClock
{
    internal Func<DateTimeOffset>? TestUtcNow { get; set; }
}

internal interface IDagRunWorkflowTransaction
{
    Task<bool> WorkflowExistsAsync(Guid id, CancellationToken cancellationToken);
    Task AddOutboxAsync(Guid runId, IReadOnlyList<OutboxWrite> writes, CancellationToken cancellationToken);
    Task<bool> PruneObserverAsync(DagRunObserverRow observer, DateTimeOffset now, CancellationToken cancellationToken);
}

internal sealed class DagRunSqlStore(
    Func<DbConnection> createConnection,
    bool sqlServer,
    string tablePrefix,
    DagTransactionClock clock,
    Func<DbException, bool> uniqueViolation,
    Func<CancellationToken, Task>? initialize = null,
    Func<DbConnection, DbTransaction, CancellationToken, Task<IDagRunWorkflowTransaction>>? workflowFactory = null)
{
    private string Table(string name) => tablePrefix + (sqlServer ? "[" + name + "]" : name);
    private string Limit(int count) => sqlServer ? "top (" + count.ToString(CultureInfo.InvariantCulture) + ") " : "";
    private string TailLimit(int count) => sqlServer ? "" : " limit " + count.ToString(CultureInfo.InvariantCulture);

    internal async Task<WorkflowOperationalPressure> ReadPressureAsync(DbConnection connection,
        DbTransaction transaction,
        WorkflowOperationalPressure workflow, CancellationToken cancellationToken)
    {
        var now = clock.TestUtcNow?.Invoke() ??
                  await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, transaction,
            $"select (select count(*) from {Table("dag_runs")} where state in ('Running','CancellationRequested','IntegrityFault'))," +
            $"(select count(*) from {Table("dag_runs")} where is_stuck={(sqlServer ? "1" : "true")})," +
            $"(select coalesce(sum(version),0) from {Table("dag_runs")})," +
            $"(select count(*) from {Table("dag_runs")})," +
            $"(select count(*) from {Table("dag_run_subscriptions")} where lease_expires_at>@expiresAt)," +
            $"(select count(*) from {Table("dag_run_subscriptions")} where lease_expires_at<=@expiresAt)",
            ("expiresAt", now));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidDataException("Missing DAG pressure projection.");
        long Number(int index) => Convert.ToInt64(reader.GetValue(index), CultureInfo.InvariantCulture);
        var events = Number(2);
        var checkpoints = Number(3);
        var result = workflow with
        {
            ActiveDagRunCount = Number(0), StuckDagRunCount = Number(1), DagRunStreamEventCount = events,
            DagRunCheckpointCount = checkpoints, DagRunCheckpointLag = 0,
            ActiveDagObserverCount = Number(4), ExpiredDagObserverCount = Number(5),
            StreamEventCount = checked(workflow.StreamEventCount + events),
            CheckpointCount = checked(workflow.CheckpointCount + checkpoints)
        };
        await reader.DisposeAsync().ConfigureAwait(false);
        if (sqlServer) return result;
        var counts = new Dictionary<(bool Observer, string State), long>();
        await using var outbox = Command(connection, transaction,
            "select (recipient_host_id is not null),state,count(*) from orcacore_outbox " +
            "where kind in ('dag-child-start-v1','dag-child-join-v1','dag-child-cancel-v1','dag-run-ready-v1') " +
            "group by (recipient_host_id is not null),state");
        await using var rows = await outbox.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await rows.ReadAsync(cancellationToken).ConfigureAwait(false))
            counts[(rows.GetBoolean(0), rows.GetString(1))] = rows.GetInt64(2);
        long Count(bool observer, string state) => counts.GetValueOrDefault((observer, state));
        return result with
        {
            InternalDagPendingCount = Count(false, "Pending"), InternalDagRetryableCount = Count(false, "Retryable"),
            InternalDagClaimedCount = Count(false, "Claimed"), InternalDagPoisonedCount = Count(false, "Poisoned"),
            DagObserverPendingCount = Count(true, "Pending"), DagObserverRetryableCount = Count(true, "Retryable"),
            DagObserverClaimedCount = Count(true, "Claimed"), DagObserverPoisonedCount = Count(true, "Poisoned")
        };
    }

    internal async Task RefreshStuckAsync(WorkflowOperatorStatisticsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction,
            $"update {Table("dag_runs")} set is_stuck=case when state in ('Running','CancellationRequested','IntegrityFault') " +
            $"and last_progress_at<=@cutoff and exists (select 1 from {Table("dag_run_nodes")} n where n.run_id={Table("dag_runs")}.run_id and n.state='Ready') " +
            $"then {(sqlServer ? "1" : "true")} else {(sqlServer ? "0" : "false")} end",
            cancellationToken, ("cutoff", request.ObservedAt.Subtract(request.StuckThreshold))).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task<Result<AppendEventsResult>> AppendAsync(ProviderCommitBatch batch,
        CancellationToken cancellationToken)
    {
        try
        {
            DagRunStoreRules.ValidateBatch(batch);
        }
        catch (InvalidDataException exception)
        {
            return DagRunStoreRules.Failure("DAG_PROTOCOL_INVALID", exception.Message);
        }

        try
        {
            return await ChangeAsync(batch.StreamId.DagRunId!.Value, batch.DagRun!.Fence?.RequestId,
                batch.DagRun.OperationId,
                async (state, now, workflow) =>
                {
                    if (state is null && await workflow
                            .WorkflowExistsAsync(batch.StreamId.DagRunId.Value, cancellationToken)
                            .ConfigureAwait(false))
                        return (
                            DagRunStoreRules.Failure("DAG_INTEGRITY_CONFLICT", "workflow owns this aggregate identity"),
                            state);
                    var result = DagRunStoreRules.Append(state, batch, now, 0, out var updated);
                    return (result, result.IsSuccess ? updated : null);
                }, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException exception) when (uniqueViolation(exception))
        {
            var creation = batch.DagRun!.Records[0].CreationSnapshot;
            if (creation is not null &&
                (await GetByStartKeyAsync(creation.StartKey, cancellationToken).ConfigureAwait(false)).HasValue)
                return DagRunStoreRules.Failure("DAG_START_KEY_CONFLICT", "caller key is already bound");
            return DagRunStoreRules.Failure("DAG_INTEGRITY_CONFLICT", "persistent record identity collision");
        }
        catch (InvalidDataException exception)
        {
            return DagRunStoreRules.Failure(
                exception.Message.StartsWith("DAG_INTEGRITY_CONFLICT:", StringComparison.Ordinal)
                    ? "DAG_INTEGRITY_CONFLICT"
                    : "DAG_PROTOCOL_INVALID", exception.Message);
        }
    }

    internal Task<DagRunLeaseResultV1> AcquireAsync(DagRunLeaseRequestV1 request, CancellationToken cancellationToken)
    {
        DagRunStoreRules.ValidateLeaseRequest(request);
        return ChangeAsync(request.RunId, request.RequestId, null, (state, now, _) =>
                Task.FromResult((DagRunStoreRules.Acquire(state, request, now, () => long.MaxValue), state)),
            cancellationToken);
    }

    internal Task<DagRunLeaseResultV1> RenewAsync(DagRunLeaseRequestV1 request, CancellationToken cancellationToken)
    {
        DagRunStoreRules.ValidateLeaseRequest(request);
        return ChangeAsync(request.RunId, request.RequestId, null, (state, now, _) =>
            Task.FromResult((DagRunStoreRules.Renew(state, request, now), state)), cancellationToken);
    }

    internal Task<DagRunLeaseResultV1> ReleaseAsync(DagRunFenceV1 fence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fence);
        return ChangeAsync(fence.RunId, fence.RequestId, null, (state, now, _) =>
            Task.FromResult((DagRunStoreRules.Release(state, fence, now), state)), cancellationToken);
    }

    internal async Task CancelRequestAsync(Guid runId, Guid ownerId, Guid requestId,
        CancellationToken cancellationToken)
    {
        await ChangeAsync(runId, requestId, null, (state, now, _) =>
        {
            DagRunStoreRules.CancelRequest(state, ownerId, requestId, now);
            return Task.FromResult((true, state));
        }, cancellationToken).ConfigureAwait(false);
    }

    internal Task<DagRunSubscribeResultV1> SubscribeAsync(DagRunSubscriptionV1 subscription,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        // Validate even when the run is absent, before any connection is opened.
        _ = DagRunStoreRules.Subscribe(null, subscription, default);
        return ChangeAsync(subscription.RunId, null, null, (state, now, _) =>
            Task.FromResult((DagRunStoreRules.Subscribe(state, subscription, now), state)), cancellationToken);
    }

    internal async Task<Option<DagRunCheckpointV1>> LoadCheckpointAsync(WorkflowStreamId streamId,
        CancellationToken cancellationToken)
    {
        var id = DagRunStoreRules.RequireDagStream(streamId);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null,
            $"select version, checkpoint_payload, checkpoint_sha256 from {Table("dag_runs")} where run_id=@runId",
            ("runId", id));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return Option<DagRunCheckpointV1>.None;
        var checkpoint = new DagRunCheckpointV1(1, id, new StreamVersion(reader.GetInt64(0)),
            DagRunStoreRules.CheckpointContentType, reader.GetFieldValue<byte[]>(1), reader.GetString(2));
        _ = DagRunStoreRules.ReadCheckpoint(checkpoint);
        return Option<DagRunCheckpointV1>.Some(checkpoint);
    }

    internal async Task<Option<DagRunDiscoveryV1>> GetByStartKeyAsync(DagRunStartKeyV1 key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        _ = new DagRunStartKeyV1(key.Value);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null,
            $"select creation_sequence,run_id,version,definition_id,definition_version,definition_fingerprint,state,start_key_text " +
            $"from {Table("dag_runs")} where start_key_digest=@keyDigest",
            ("keyDigest", DagRunStoreRules.Hash(Encoding.UTF8.GetBytes(key.Value))));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return Option<DagRunDiscoveryV1>.None;
        if (!string.Equals(reader.GetString(7), key.Value, StringComparison.Ordinal))
            throw new InvalidDataException("DAG_INTEGRITY_CONFLICT: start-key digest collision.");
        return Option<DagRunDiscoveryV1>.Some(Discovery(reader));
    }

    internal async Task<IReadOnlyList<DagRunDiscoveryV1>> DiscoverAsync(DagRunDiscoveryRequestV1 request,
        CancellationToken cancellationToken)
    {
        DagRunStoreRules.ValidateDiscovery(request);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null,
            $"select {Limit(request.MaxCount)}creation_sequence,run_id,version,definition_id,definition_version,definition_fingerprint,state " +
            $"from {Table("dag_runs")} where creation_sequence>@afterSequence and state in ('Running','CancellationRequested','IntegrityFault') " +
            $"order by creation_sequence{TailLimit(request.MaxCount)}",
            ("afterSequence", request.AfterCreationSequence));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<DagRunDiscoveryV1>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(Discovery(reader));
        return result;
    }

    internal async Task<IReadOnlyList<DagRunRecordV1>> LoadTailAsync(WorkflowStreamId streamId,
        StreamVersion afterVersion, CancellationToken cancellationToken)
    {
        var id = DagRunStoreRules.RequireDagStream(streamId);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null,
            $"select stream_version,operation_id,payload,checksum from {Table("dag_run_events")} " +
            "where run_id=@runId and stream_version>@afterVersion order by stream_version",
            ("runId", id), ("afterVersion", afterVersion.Value));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<DagRunRecordV1>();
        var version = afterVersion.Value;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var bytes = reader.GetFieldValue<byte[]>(2);
            if (DagRunStoreRules.Hash(bytes) != reader.GetString(3))
                throw new InvalidDataException("DAG_PROTOCOL_INVALID: event checksum.");
            var record = DagRunRecordCodecV1.Deserialize(bytes);
            if (record.RunId != id || record.OperationId != reader.GetGuid(1) ||
                record.StreamVersion.Value != reader.GetInt64(0) ||
                record.StreamVersion.Value != ++version)
                throw new InvalidDataException("DAG_PROTOCOL_INVALID: tail coordinates/gap.");
            result.Add(record);
        }

        return result;
    }

    private async Task<T> ChangeAsync<T>(Guid runId, Guid? requestId, Guid? operationId,
        Func<DagRunStoredState?, DateTimeOffset, IDagRunWorkflowTransaction, Task<(T Result, DagRunStoredState? State)>>
            operation,
        CancellationToken cancellationToken)
    {
        if (runId == Guid.Empty) throw new ArgumentException("DAG run identity is empty.", nameof(runId));
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(
                sqlServer ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        IDagRunWorkflowTransaction workflow;
        if (workflowFactory is not null)
            workflow = await workflowFactory(connection, transaction, cancellationToken).ConfigureAwait(false);
        else
        {
            await ExecuteAsync(connection, transaction, "select pg_advisory_xact_lock(hashtextextended(@lockKey,0))",
                cancellationToken, ("lockKey", "orcacore:aggregate:" + runId.ToString("D"))).ConfigureAwait(false);
            workflow = new PostgreSqlOutboxTransaction(connection, transaction);
        }

        var state = await ReadStateAsync(connection, transaction, runId, requestId, operationId, cancellationToken)
            .ConfigureAwait(false);
        // Read the transaction clock only AFTER ownership locks, including the absent-run advisory lock.
        var now = clock.TestUtcNow?.Invoke() ??
                  await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var beforeVersion = state?.Snapshot.StreamVersion.Value;
        var knownRequests = state?.Requests.ToDictionary(p => p.Key, p => p.Value) ?? [];
        var knownOperations = state?.Operations.Keys.ToHashSet() ?? [];
        var (result, updated) = await operation(state, now, workflow).ConfigureAwait(false);
        if (updated is not null)
        {
            await SaveStateAsync(connection, transaction, updated, beforeVersion, knownRequests, knownOperations,
                cancellationToken).ConfigureAwait(false);
            if (updated.NewOutbox.Count != 0)
                await workflow.AddOutboxAsync(runId, updated.NewOutbox, cancellationToken).ConfigureAwait(false);
            if (result is DagRunLeaseResultV1 { QueueSequence: long.MaxValue } lease)
                result = (T)(object)(lease with { QueueSequence = updated.Requests[requestId!.Value].QueueSequence });
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (initialize is not null) await initialize(cancellationToken).ConfigureAwait(false);
        var connection = createConnection();
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<DateTimeOffset> DatabaseTimeAsync(DbConnection connection, DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction,
            sqlServer ? "select SYSUTCDATETIME()" : "select clock_timestamp()");
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value switch
        {
            DateTimeOffset timestamp => timestamp.ToUniversalTime(),
            DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
            _ => throw new InvalidDataException("Provider returned no transaction clock.")
        };
    }

    private async Task<DagRunStoredState?> ReadStateAsync(DbConnection connection, DbTransaction transaction,
        Guid runId,
        Guid? requestId, Guid? operationId, CancellationToken cancellationToken)
    {
        DagRunStoredState state;
        await using (var command = Command(connection, transaction,
                         $"select creation_sequence,version,owner_fence,lease_request_id,last_operational_at,is_stuck,checkpoint_payload,checkpoint_sha256 " +
                         $"from {Table("dag_runs")}{(sqlServer ? " with (updlock,holdlock)" : "")} where run_id=@runId{(sqlServer ? "" : " for update")}",
                         ("runId", runId)))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
            var checkpoint = new DagRunCheckpointV1(1, runId, new StreamVersion(reader.GetInt64(1)),
                DagRunStoreRules.CheckpointContentType, reader.GetFieldValue<byte[]>(6), reader.GetString(7));
            state = new DagRunStoredState
            {
                Snapshot = DagRunStoreRules.ReadCheckpoint(checkpoint), Checkpoint = checkpoint,
                CreationSequence = reader.GetInt64(0), Fence = reader.GetInt64(2),
                ActiveRequest = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                LastOperationalAt = Timestamp(reader, 4), IsStuck = reader.GetBoolean(5)
            };
        }

        await using (var command = Command(connection, transaction,
                         $"select request_id,owner_id,purpose,queue_sequence,state,wait_expires_at,granted_at,lease_expires_at,hard_deadline,granted_token,append_committed " +
                         $"from {Table("dag_run_lease_requests")} where run_id=@runId and (state in ('Waiting','Granted')" +
                         (requestId is null ? ")" : " or request_id=@requestId)"),
                         requestId is null ? [("runId", runId)] : [("runId", runId), ("requestId", requestId.Value)]))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new DagRunLeaseRow(reader.GetGuid(0), reader.GetGuid(1),
                    Enum.Parse<DagRunLeasePurposeV1>(reader.GetString(2)), reader.GetInt64(3), reader.GetString(4),
                    Timestamp(reader, 5),
                    NullableTimestamp(reader, 6), NullableTimestamp(reader, 7), NullableTimestamp(reader, 8),
                    reader.IsDBNull(9) ? null : reader.GetInt64(9), reader.GetBoolean(10));
                state.Requests.Add(row.RequestId, row);
            }

        if (operationId is not null)
        {
            await using var command = Command(connection, transaction,
                $"select receipt_payload from {Table("dag_run_operations")} where run_id=@runId and operation_id=@operationId",
                ("runId", runId), ("operationId", operationId.Value));
            var bytes = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as byte[];
            if (bytes is not null)
            {
                var receipt = JsonSerializer.Deserialize(bytes, DagRunStoreJsonContext.Default.DagRunOperationReceipt)
                              ?? throw new InvalidDataException("DAG_PROTOCOL_INVALID: operation receipt.");
                state.Operations.Add(receipt.OperationId, receipt);
            }
        }

        await using (var command = Command(connection, transaction,
                         $"select subscription_id,recipient_host_id,after_version,lease_expires_at from {Table("dag_run_subscriptions")} where run_id=@runId",
                         ("runId", runId)))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var subscription = new DagRunSubscriptionV1(reader.GetGuid(0), reader.GetGuid(1), runId,
                    new StreamVersion(reader.GetInt64(2)), TimeSpan.FromSeconds(30));
                state.Observers.Add(subscription.SubscriptionId, new(subscription, Timestamp(reader, 3)));
            }

        return state;
    }

    private static DagRunDiscoveryV1 Discovery(DbDataReader reader) => new(reader.GetInt64(0), reader.GetGuid(1),
        new StreamVersion(reader.GetInt64(2)), new DagRunBindingV1(DefinitionId.Parse(reader.GetString(3)),
            new DefinitionVersion(checked((int)reader.GetInt64(4))), reader.GetString(5)),
        Enum.Parse<DagRunStateV1>(reader.GetString(6)));

    private static DateTimeOffset Timestamp(DbDataReader reader, int ordinal) => reader.GetValue(ordinal) switch
    {
        DateTimeOffset value => value.ToUniversalTime(),
        DateTime value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
        _ => throw new InvalidDataException("DAG_PROTOCOL_INVALID: stored UTC time.")
    };

    private static DateTimeOffset? NullableTimestamp(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Timestamp(reader, ordinal);

    private static DbCommand Command(DbConnection connection, DbTransaction? transaction, string text,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = text;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@" + name;
            if (value is null)
                parameter.DbType = name is "ownerId" or "requestId" or "childId" or "recipientId" or "subscriptionId"
                    ? DbType.Guid
                    : name is "grantedAt" or "expiresAt" or "hardDeadline" or "completedAt" or "readyAt" or "startedAt"
                        ? DbType.DateTimeOffset
                        : name == "grantedToken"
                            ? DbType.Int64
                            : name.EndsWith("Bytes", StringComparison.Ordinal)
                                ? DbType.Binary
                                : DbType.String;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static async Task<int> ExecuteAsync(DbConnection connection, DbTransaction transaction, string text,
        CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var command = Command(connection, transaction, text, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveStateAsync(DbConnection connection, DbTransaction transaction, DagRunStoredState state,
        long? beforeVersion, IReadOnlyDictionary<Guid, DagRunLeaseRow> knownRequests,
        IReadOnlySet<Guid> knownOperations,
        CancellationToken cancellationToken)
    {
        var snapshot = state.Snapshot;
        var active = state.ActiveRequest is { } id ? state.Requests.GetValueOrDefault(id) : null;
        var parameters = new (string, object?)[]
        {
            ("runId", snapshot.RunId), ("version", snapshot.StreamVersion.Value), ("fence", state.Fence),
            ("ownerId", active?.OwnerId), ("requestId", active?.RequestId), ("grantedAt", active?.GrantedAt),
            ("expiresAt", active?.ExpiresAt), ("hardDeadline", active?.HardDeadline),
            ("operationalAt", state.LastOperationalAt), ("isStuck", state.IsStuck),
            ("state", snapshot.State.ToString()), ("updatedAt", snapshot.UpdatedAt),
            ("completedAt", snapshot.CompletedAt),
            ("checkpointBytes", state.Checkpoint.Payload), ("checkpointHash", state.Checkpoint.Sha256),
            ("definitionId", snapshot.Graph.Definition.DefinitionId.ToString()),
            ("definitionVersion", (long)snapshot.Graph.Definition.DefinitionVersion.Value),
            ("definitionFingerprint", snapshot.Graph.Definition.Fingerprint),
            ("startKey", snapshot.StartKey.Value),
            ("keyDigest", DagRunStoreRules.Hash(Encoding.UTF8.GetBytes(snapshot.StartKey.Value))),
            ("graphBytes",
                JsonSerializer.SerializeToUtf8Bytes(snapshot.Graph, DagRunStoreJsonContext.Default.DagRunGraphV1)),
            ("inputBytes",
                JsonSerializer.SerializeToUtf8Bytes(snapshot.RunInput, DagRunStoreJsonContext.Default.DagRunValueV1)),
            ("createdAt", snapshot.CreatedAt)
        };
        if (beforeVersion is null)
        {
            var output = sqlServer ? " output inserted.creation_sequence" : "";
            var returning = sqlServer ? "" : " returning creation_sequence";
            await using var command = Command(connection, transaction,
                $"insert into {Table("dag_runs")} (run_id,start_key_digest,start_key_text,version,definition_id,definition_version," +
                "definition_fingerprint,state,last_operational_at,is_stuck,last_progress_at,graph_payload,input_payload,checkpoint_payload," +
                "checkpoint_sha256,created_at,updated_at,completed_at,owner_id,owner_fence,lease_request_id,lease_granted_at,lease_expires_at,lease_hard_deadline)" +
                output +
                " values (@runId,@keyDigest,@startKey,@version,@definitionId,@definitionVersion,@definitionFingerprint,@state," +
                "@operationalAt,@isStuck,@updatedAt,@graphBytes,@inputBytes,@checkpointBytes,@checkpointHash,@createdAt,@updatedAt,@completedAt," +
                "@ownerId,@fence,@requestId,@grantedAt,@expiresAt,@hardDeadline)" + returning, parameters);
            state.CreationSequence =
                Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                    CultureInfo.InvariantCulture);
        }
        else
        {
            var business = beforeVersion.Value != snapshot.StreamVersion.Value
                ? ",version=@version,state=@state,last_progress_at=@updatedAt,updated_at=@updatedAt,completed_at=@completedAt," +
                  "checkpoint_payload=@checkpointBytes,checkpoint_sha256=@checkpointHash"
                : "";
            await ExecuteAsync(connection, transaction,
                $"update {Table("dag_runs")} set owner_id=@ownerId,owner_fence=@fence,lease_request_id=@requestId," +
                "lease_granted_at=@grantedAt,lease_expires_at=@expiresAt,lease_hard_deadline=@hardDeadline," +
                "last_operational_at=@operationalAt,is_stuck=@isStuck" + business + " where run_id=@runId",
                cancellationToken, parameters).ConfigureAwait(false);
        }

        foreach (var record in state.NewRecords)
        {
            var bytes = DagRunRecordCodecV1.Serialize(record);
            using var document = JsonDocument.Parse(bytes);
            await ExecuteAsync(connection, transaction,
                $"insert into {Table("dag_run_events")} (run_id,stream_version,operation_id,record_kind,record_version,payload,checksum,created_at) " +
                "values (@runId,@version,@operationId,@kind,1,@recordBytes,@checksum,@createdAt)", cancellationToken,
                ("runId", snapshot.RunId), ("version", record.StreamVersion.Value), ("operationId", record.OperationId),
                ("kind", document.RootElement.GetProperty("kind").GetString()), ("recordBytes", bytes),
                ("checksum", DagRunStoreRules.Hash(bytes)), ("createdAt", record.UpdatedAt)).ConfigureAwait(false);
        }

        foreach (var operation in state.Operations.Values.Where(o => !knownOperations.Contains(o.OperationId)))
            await ExecuteAsync(connection, transaction,
                $"insert into {Table("dag_run_operations")} (run_id,operation_id,operation_fingerprint,result_version,receipt_payload) " +
                "values (@runId,@operationId,@fingerprint,@version,@receiptBytes)", cancellationToken,
                ("runId", snapshot.RunId), ("operationId", operation.OperationId),
                ("fingerprint", operation.OperationFingerprint),
                ("version", operation.ResultVersion),
                ("receiptBytes",
                    JsonSerializer.SerializeToUtf8Bytes(operation,
                        DagRunStoreJsonContext.Default.DagRunOperationReceipt))).ConfigureAwait(false);
        var ordinals = beforeVersion is null
            ? snapshot.Nodes.Select(n => n.Ordinal)
            : state.NewRecords.SelectMany(r => r.ChangedNodes.Select(n => n.Ordinal)).Distinct();
        foreach (var ordinal in ordinals)
            await SaveNodeAsync(connection, transaction, state, ordinal, cancellationToken).ConfigureAwait(false);
        foreach (var row in state.Requests.Values.ToArray())
        {
            if (knownRequests.TryGetValue(row.RequestId, out var original) && original == row) continue;
            var args = new (string, object?)[]
            {
                ("runId", snapshot.RunId), ("requestId", row.RequestId), ("ownerId", row.OwnerId),
                ("purpose", row.Purpose.ToString()), ("state", row.State), ("waitExpiresAt", row.WaitExpiresAt),
                ("grantedAt", row.GrantedAt), ("expiresAt", row.ExpiresAt), ("hardDeadline", row.HardDeadline),
                ("grantedToken", row.GrantedToken), ("appendCommitted", row.AppendCommitted)
            };
            if (!knownRequests.ContainsKey(row.RequestId))
            {
                var output = sqlServer ? " output inserted.queue_sequence" : "";
                var returning = sqlServer ? "" : " returning queue_sequence";
                await using var command = Command(connection, transaction,
                    $"insert into {Table("dag_run_lease_requests")} (run_id,request_id,owner_id,purpose,state,wait_expires_at,granted_at," +
                    "lease_expires_at,hard_deadline,granted_token,append_committed)" + output +
                    " values (@runId,@requestId,@ownerId,@purpose,@state,@waitExpiresAt,@grantedAt,@expiresAt,@hardDeadline,@grantedToken,@appendCommitted)" +
                    returning, args);
                var sequence =
                    Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                        CultureInfo.InvariantCulture);
                state.Requests[row.RequestId] = row with { QueueSequence = sequence };
            }
            else
                await ExecuteAsync(connection, transaction,
                    $"update {Table("dag_run_lease_requests")} set state=@state,wait_expires_at=@waitExpiresAt,granted_at=@grantedAt," +
                    "lease_expires_at=@expiresAt,hard_deadline=@hardDeadline,granted_token=@grantedToken,append_committed=@appendCommitted " +
                    "where run_id=@runId and request_id=@requestId", cancellationToken, args).ConfigureAwait(false);
        }

        foreach (var observer in state.Observers.Values)
        {
            var sub = observer.Subscription;
            var args = new (string, object?)[]
            {
                ("runId", snapshot.RunId), ("subscriptionId", sub.SubscriptionId),
                ("recipientId", sub.RecipientHostId), ("afterVersion", sub.AfterVersion.Value),
                ("expiresAt", observer.ExpiresAt)
            };
            var count = await ExecuteAsync(connection, transaction,
                $"update {Table("dag_run_subscriptions")} set after_version=@afterVersion,lease_expires_at=@expiresAt " +
                "where subscription_id=@subscriptionId and run_id=@runId and recipient_host_id=@recipientId",
                cancellationToken, args).ConfigureAwait(false);
            if (count == 0)
                await ExecuteAsync(connection, transaction,
                        $"insert into {Table("dag_run_subscriptions")} (subscription_id,recipient_host_id,run_id,after_version,lease_expires_at) " +
                        "values (@subscriptionId,@recipientId,@runId,@afterVersion,@expiresAt)", cancellationToken,
                        args)
                    .ConfigureAwait(false);
        }
    }

    private async Task SaveNodeAsync(DbConnection connection, DbTransaction transaction, DagRunStoredState state,
        int ordinal,
        CancellationToken cancellationToken)
    {
        var node = state.Snapshot.Nodes[ordinal];
        var definition = state.Snapshot.Graph.Nodes[ordinal];
        var args = new (string, object?)[]
        {
            ("runId", state.Snapshot.RunId), ("ordinal", ordinal), ("nodeId", definition.NodeId),
            ("bindingBytes",
                JsonSerializer.SerializeToUtf8Bytes(definition.Workflow,
                    DagRunStoreJsonContext.Default.DagRunBindingV1)),
            ("dependenciesBytes",
                JsonSerializer.SerializeToUtf8Bytes(definition.DirectDependencies.ToArray(),
                    DagRunStoreJsonContext.Default.Int32Array)),
            ("state", node.State.ToString()), ("childId", node.ChildInstanceId?.Value),
            ("inputPresent", node.Input is not null), ("inputBytes", node.Input?.Bytes),
            ("inputContract", node.Input?.ContractKey),
            ("inputCodec", node.Input?.CodecFormat), ("inputFingerprint", node.Input?.Fingerprint),
            ("outputPresent", node.Output is not null), ("outputBytes", node.Output?.Bytes),
            ("outputContract", node.Output?.ContractKey),
            ("outputCodec", node.Output?.CodecFormat), ("outputFingerprint", node.Output?.Fingerprint),
            ("failureCode", node.FailureCode), ("readyAt", node.ReadyAt), ("startedAt", node.StartedAt),
            ("completedAt", node.CompletedAt)
        };
        var columns =
            "node_id,binding_payload,dependencies_payload,state,child_id,input_present,input_bytes,input_contract,input_codec,input_fingerprint," +
            "output_present,output_bytes,output_contract,output_codec,output_fingerprint,failure_code,ready_at,started_at,completed_at";
        var values =
            "@nodeId,@bindingBytes,@dependenciesBytes,@state,@childId,@inputPresent,@inputBytes,@inputContract,@inputCodec,@inputFingerprint," +
            "@outputPresent,@outputBytes,@outputContract,@outputCodec,@outputFingerprint,@failureCode,@readyAt,@startedAt,@completedAt";
        var assignments = string.Join(",",
            columns.Split(',').Zip(values.Split(','), (column, value) => column + "=" + value));
        var count = await ExecuteAsync(connection, transaction,
            $"update {Table("dag_run_nodes")} set {assignments} where run_id=@runId and ordinal=@ordinal",
            cancellationToken, args).ConfigureAwait(false);
        if (count == 0)
            await ExecuteAsync(connection, transaction,
                $"insert into {Table("dag_run_nodes")} (run_id,ordinal,{columns}) values (@runId,@ordinal,{values})",
                cancellationToken, args).ConfigureAwait(false);
    }

    internal async Task UnsubscribeAsync(Guid subscriptionId, Guid recipientHostId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        Guid? runId;
        await using (var command = Command(connection, null,
                         $"select run_id from {Table("dag_run_subscriptions")} where subscription_id=@subscriptionId and recipient_host_id=@recipientId",
                         ("subscriptionId", subscriptionId), ("recipientId", recipientHostId)))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            runId = value is Guid id ? id : null;
        }

        if (runId is null) return;
        await ChangeAsync(runId.Value, null, null, (state, now, _) =>
        {
            if (state?.Observers.TryGetValue(subscriptionId, out var observer) == true &&
                observer.Subscription.RecipientHostId == recipientHostId)
                state.Observers[subscriptionId] = observer with { ExpiresAt = DagRunStoreRules.Clamp(state, now) };
            return Task.FromResult((true, state));
        }, cancellationToken).ConfigureAwait(false);
        await PruneObserversAsync(1000, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<int> PruneObserversAsync(int maxCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        if (maxCount > 1000) throw new ArgumentOutOfRangeException(nameof(maxCount));
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var sqlNow = sqlServer ? "SYSUTCDATETIME()" : "clock_timestamp()";
        var now = clock.TestUtcNow?.Invoke();
        var runs = new List<Guid>();
        await using (var command = Command(connection, null,
                         $"select distinct {Limit(maxCount)}run_id from {Table("dag_run_subscriptions")} where lease_expires_at<=" +
                         (now is null ? sqlNow : "@expiresAt") + $" order by run_id{TailLimit(maxCount)}",
                         now is null ? [] : [("expiresAt", now.Value)]))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                runs.Add(reader.GetGuid(0));
        var removed = 0;
        foreach (var runId in runs)
        {
            if (removed == maxCount) break;
            removed += await PruneRunAsync(runId, maxCount - removed, cancellationToken).ConfigureAwait(false);
        }

        return removed;
    }

    private async Task<int> PruneRunAsync(Guid runId, int remaining, CancellationToken cancellationToken)
    {
        // Cleanup needs the same ownership lock, clock sample and shared outbox transaction.
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(
                sqlServer ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        IDagRunWorkflowTransaction workflow;
        if (workflowFactory is not null)
            workflow = await workflowFactory(connection, transaction, cancellationToken).ConfigureAwait(false);
        else
        {
            await ExecuteAsync(connection, transaction, "select pg_advisory_xact_lock(hashtextextended(@lockKey,0))",
                cancellationToken,
                ("lockKey", "orcacore:aggregate:" + runId.ToString("D"))).ConfigureAwait(false);
            workflow = new PostgreSqlOutboxTransaction(connection, transaction);
        }

        var state = await ReadStateAsync(connection, transaction, runId, null, null, cancellationToken)
            .ConfigureAwait(false);
        if (state is null) return 0;
        var now = DagRunStoreRules.Clamp(state, clock.TestUtcNow?.Invoke() ??
                                                await DatabaseTimeAsync(connection, transaction, cancellationToken)
                                                    .ConfigureAwait(false));
        var count = 0;
        foreach (var observer in state.Observers.Values.Where(o => o.ExpiresAt <= now).Take(remaining))
        {
            if (!await workflow.PruneObserverAsync(observer, now, cancellationToken).ConfigureAwait(false)) continue;
            await ExecuteAsync(connection, transaction,
                $"delete from {Table("dag_run_subscriptions")} where subscription_id=@subscriptionId and recipient_host_id=@recipientId and lease_expires_at<=@expiresAt",
                cancellationToken, ("subscriptionId", observer.Subscription.SubscriptionId),
                ("recipientId", observer.Subscription.RecipientHostId), ("expiresAt", now)).ConfigureAwait(false);
            count++;
        }

        await ExecuteAsync(connection, transaction,
            $"update {Table("dag_runs")} set last_operational_at=@operationalAt where run_id=@runId",
            cancellationToken, ("operationalAt", now), ("runId", runId)).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return count;
    }

    private sealed class PostgreSqlOutboxTransaction(DbConnection connection, DbTransaction transaction)
        : IDagRunWorkflowTransaction
    {
        public async Task<bool> WorkflowExistsAsync(Guid id, CancellationToken cancellationToken)
        {
            await using var command = Command(connection, transaction,
                "select 1 from orcacore_stream_heads where instance_id=@runId", ("runId", id));
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
        }

        public async Task AddOutboxAsync(Guid runId, IReadOnlyList<OutboxWrite> writes,
            CancellationToken cancellationToken)
        {
            foreach (var write in writes)
            {
                var parameters = new (string, object?)[]
                {
                    ("outboxId", write.OutboxRecordId.Value), ("runId", runId),
                    ("kind", write.Kind), ("recordBytes", write.Payload), ("recipientId", write.RecipientHostId),
                    ("subscriptionId", write.DagRunSubscriptionId)
                };
                var count = await ExecuteAsync(connection, transaction,
                    "insert into orcacore_outbox(outbox_record_id,instance_id,kind,payload,state,dag_run_owner,recipient_host_id,dag_run_subscription_id) " +
                    "values (@outboxId,@runId,@kind,@recordBytes,'Pending',@runId,@recipientId,@subscriptionId) on conflict(outbox_record_id) do nothing",
                    cancellationToken, parameters).ConfigureAwait(false);
                if (count != 0) continue;
                await using var command = Command(connection, transaction,
                    "select 1 from orcacore_outbox where outbox_record_id=@outboxId and dag_run_owner=@runId and kind=@kind and payload=@recordBytes " +
                    "and recipient_host_id is not distinct from @recipientId and dag_run_subscription_id is not distinct from @subscriptionId",
                    parameters);
                if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                    throw new InvalidDataException("DAG_INTEGRITY_CONFLICT: outbox identity collision.");
            }
        }

        public async Task<bool> PruneObserverAsync(DagRunObserverRow observer, DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var args = new (string, object?)[]
            {
                ("runId", observer.Subscription.RunId), ("subscriptionId", observer.Subscription.SubscriptionId),
                ("recipientId", observer.Subscription.RecipientHostId), ("expiresAt", now)
            };
            await using (var command = Command(connection, transaction,
                             "select state,claimed_until from orcacore_outbox where dag_run_owner=@runId and dag_run_subscription_id=@subscriptionId " +
                             "and recipient_host_id=@recipientId and kind='dag-run-ready-v1' for update", args))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                // Lock every notice, not just notices that were already claimed at query
                // start. A claimant cannot slip between the eligibility test and delete.
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    if (reader.GetString(0) == "Poisoned" || (reader.GetString(0) == "Claimed" &&
                                                              (reader.IsDBNull(1) || Timestamp(reader, 1) > now)))
                        return false;
            }

            await ExecuteAsync(connection, transaction,
                "delete from orcacore_outbox where dag_run_owner=@runId and dag_run_subscription_id=@subscriptionId " +
                "and recipient_host_id=@recipientId and kind='dag-run-ready-v1' and state<>'Poisoned' and (state<>'Claimed' or claimed_until<=@expiresAt)",
                cancellationToken, args).ConfigureAwait(false);
            return true;
        }
    }
}
