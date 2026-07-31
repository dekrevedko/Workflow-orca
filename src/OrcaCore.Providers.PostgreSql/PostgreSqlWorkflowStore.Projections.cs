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

internal sealed class PostgreSqlProjectionStore(NpgsqlDataSource dataSource)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
    public async Task<IReadOnlyList<LegacyWorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var snapshots = new List<LegacyWorkflowInstanceSnapshot>();
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
                   saga_audits,
                   stream_version
            from orcacore_instance_projections summary
            where (@instance_id is null or summary.instance_id = @instance_id)
              and (@parent_instance_id is null or summary.parent_instance_id = @parent_instance_id)
              and (@root_instance_id is null or summary.root_instance_id = @root_instance_id)
              and (@definition_id is null or summary.definition_id = @definition_id)
              and (@definition_version is null or summary.definition_version = @definition_version)
              and (@status is null or summary.status = @status)
              and ((@wait_event_name is null and @wait_correlation_id is null) or exists (
                  select 1
                  from orcacore_active_wait_projections wait
                  where wait.instance_id = summary.instance_id
                    and (@wait_event_name is null or wait.event_name = @wait_event_name)
                    and (@wait_correlation_id is null or wait.correlation_id = @wait_correlation_id)))
            order by instance_id;
            """,
            connection);
        AddProjectionQueryParameters(command, query);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var instanceId = InstanceId.Parse(reader.GetGuid(0).ToString());
            snapshots.Add(new LegacyWorkflowInstanceSnapshot
            {
                InstanceId = instanceId,
                ParentInstanceId = reader.IsDBNull(1) ? null : InstanceId.Parse(reader.GetGuid(1).ToString()),
                RootInstanceId = reader.IsDBNull(2) ? null : InstanceId.Parse(reader.GetGuid(2).ToString()),
                DefinitionId = DefinitionId.Parse(reader.GetGuid(3).ToString()),
                DefinitionVersion = new DefinitionVersion(reader.GetInt32(4)),
                Status = Enum.Parse<LegacyWorkflowStatus>(reader.GetString(5)),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(6),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(7),
                ErrorSummary = reader.IsDBNull(8) ? null : reader.GetString(8),
                EndOutcomeName = reader.IsDBNull(9) ? null : reader.GetString(9),
                ContinueAsNewGeneration = reader.GetInt32(10),
                ArchivedAt = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
                SagaAudits = reader.IsDBNull(12) ? [] : DeserializeSagaAudits(reader.GetString(12)),
                StreamVersion = reader.IsDBNull(13) ? null : reader.GetInt64(13)
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
              and ((@wait_event_name is null and @wait_correlation_id is null) or exists (
                  select 1
                  from orcacore_active_wait_projections wait
                  where wait.instance_id = summary.instance_id
                    and (@wait_event_name is null or wait.event_name = @wait_event_name)
                    and (@wait_correlation_id is null or wait.correlation_id = @wait_correlation_id)));
            """,
            connection);
        AddProjectionQueryParameters(command, query);

        var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return checked((int)(long)(count ?? 0L));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LegacyActiveWaitSnapshot>> ListActiveWaitsAsync(
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
                (select count(*) from orcacore_outbox where state = @pending) as outbox_pending,
                (select count(*) from orcacore_outbox where state = @retryable) as outbox_retryable,
                (select count(*) from orcacore_outbox where state = @claimed) as outbox_claimed,
                (select count(*) from orcacore_outbox where kind = @continue_kind and state = @pending)
                    as continuation_pending,
                (select count(*) from orcacore_outbox where kind = @continue_kind and state = @retryable)
                    as continuation_retryable,
                (select count(*) from orcacore_outbox where kind = @continue_kind and state = @claimed)
                    as continuation_claimed,
                (select count(*) from orcacore_outbox where kind <> @continue_kind and state = @pending)
                    as external_pending,
                (select count(*) from orcacore_outbox where kind <> @continue_kind and state = @retryable)
                    as external_retryable,
                (select count(*) from orcacore_outbox where kind <> @continue_kind and state = @claimed)
                    as external_claimed,
                (select coalesce(max(events.max_version - coalesce(checkpoints.stream_version, 0)), 0)
                    from (
                        select stream_id, max(version) as max_version
                        from orcacore_events
                        group by stream_id
                    ) events
                    left join orcacore_checkpoints checkpoints on checkpoints.instance_id = events.stream_id)
                    as checkpoint_lag,
                (select count(*) from orcacore_instance_projections
                    where status in (@running, @waiting, @paused)) as active_instances;
            """,
            connection);
        command.Parameters.AddWithValue("pending", OutboxRecordState.Pending.ToString());
        command.Parameters.AddWithValue("retryable", OutboxRecordState.Retryable.ToString());
        command.Parameters.AddWithValue("claimed", OutboxRecordState.Claimed.ToString());
        command.Parameters.AddWithValue("continue_kind", OutboxKinds.Continue);
        command.Parameters.AddWithValue("running", LegacyWorkflowStatus.Running.ToString());
        command.Parameters.AddWithValue("waiting", LegacyWorkflowStatus.Waiting.ToString());
        command.Parameters.AddWithValue("paused", LegacyWorkflowStatus.Paused.ToString());

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
            OutboxPendingCount = checked((int)reader.GetInt64(3)),
            OutboxRetryableCount = checked((int)reader.GetInt64(4)),
            OutboxClaimedCount = checked((int)reader.GetInt64(5)),
            ContinuationPendingCount = checked((int)reader.GetInt64(6)),
            ContinuationRetryableCount = checked((int)reader.GetInt64(7)),
            ContinuationClaimedCount = checked((int)reader.GetInt64(8)),
            ExternalOutboxPendingCount = checked((int)reader.GetInt64(9)),
            ExternalOutboxRetryableCount = checked((int)reader.GetInt64(10)),
            ExternalOutboxClaimedCount = checked((int)reader.GetInt64(11)),
            CheckpointLag = reader.GetInt64(12),
            ActiveInstanceCount = checked((int)reader.GetInt64(13))
        };
    }

    /// <inheritdoc />

    internal static async Task ApplyProjectionOperationsAsync(
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
        LegacyWorkflowInstanceSnapshot snapshot,
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
                stream_version,
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
                @stream_version,
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
                stream_version = excluded.stream_version,
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
        command.Parameters.Add("stream_version", NpgsqlDbType.Bigint).Value =
            (object?)snapshot.StreamVersion ?? DBNull.Value;
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
        LegacyActiveWaitSnapshot activeWait,
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

    private static async Task<IReadOnlyDictionary<InstanceId, IReadOnlyList<LegacyActiveWaitSnapshot>>> LoadActiveWaitsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<InstanceId> instanceIds,
        CancellationToken cancellationToken)
    {
        if (instanceIds.Count == 0)
        {
            return new Dictionary<InstanceId, IReadOnlyList<LegacyActiveWaitSnapshot>>();
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

        var waits = new Dictionary<InstanceId, List<LegacyActiveWaitSnapshot>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var instanceId = InstanceId.Parse(reader.GetGuid(0).ToString());
            if (!waits.TryGetValue(instanceId, out var instanceWaits))
            {
                instanceWaits = [];
                waits.Add(instanceId, instanceWaits);
            }

            instanceWaits.Add(new LegacyActiveWaitSnapshot
            {
                WaitId = WaitId.Parse(reader.GetGuid(1).ToString()),
                EventName = reader.GetString(2),
                CorrelationId = CorrelationId.Create(reader.GetString(3)),
                RegisteredAt = reader.GetFieldValue<DateTimeOffset>(4),
                Status = reader.GetString(5),
                Mode = reader.GetString(6)
            });
        }

        return waits.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<LegacyActiveWaitSnapshot>)pair.Value);
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

    private static IReadOnlyList<SagaAuditScopeSnapshot> DeserializeSagaAudits(string payload)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<SagaAuditScopeSnapshot>>(payload, JsonOptions) ?? [];
    }
}
