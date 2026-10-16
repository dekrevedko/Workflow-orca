using System.Data;
using Npgsql;
using NpgsqlTypes;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;
using OrcaCore.Providers.PostgreSql.Internal;

namespace OrcaCore.Providers.PostgreSql;

internal sealed class PostgreSqlProjectionStore(NpgsqlDataSource dataSource)
{

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
    public async Task<Option<WorkflowProjectionSnapshot>> GetAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        var snapshots = await LoadAsync(
            instanceId,
            definitionId: null,
            eventName: null,
            eventContractVersion: null,
            correlationId: null,
            cancellationToken).ConfigureAwait(false);
        return snapshots.Count == 0
            ? Option<WorkflowProjectionSnapshot>.None
            : Option<WorkflowProjectionSnapshot>.Some(snapshots[0]);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(correlationId);
        return LoadAsync(
            instanceId: null,
            definitionId,
            eventName,
            eventContractVersion: null,
            correlationId,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        EventContractVersion eventContractVersion,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(eventContractVersion);
        ArgumentNullException.ThrowIfNull(correlationId);
        return LoadAsync(
            instanceId: null,
            definitionId,
            eventName,
            eventContractVersion,
            correlationId,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> ListLeaseRecoveryCandidatesAsync(
        CancellationToken cancellationToken) =>
        LoadAsync(
            instanceId: null,
            definitionId: null,
            eventName: null,
            eventContractVersion: null,
            correlationId: null,
            cancellationToken);

    private async Task<IReadOnlyList<WorkflowProjectionSnapshot>> LoadAsync(
        InstanceId? instanceId,
        DefinitionId? definitionId,
        EventName? eventName,
        EventContractVersion? eventContractVersion,
        CorrelationId? correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var snapshots = new List<WorkflowProjectionSnapshot>();
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
                   last_active_at,
                   is_stuck,
                   stuck_detected_at,
                   saga_audits,
                   stream_version
            from orcacore_instance_projections summary
            where (@instance_id is null or summary.instance_id = @instance_id)
              and (@definition_id is null or summary.definition_id = @definition_id)
              and ((@wait_event_name is null and @wait_event_contract_version is null and @wait_correlation_id is null) or exists (
                  select 1
                  from orcacore_active_wait_projections wait
                  where wait.instance_id = summary.instance_id
                    and wait.event_name = @wait_event_name
                    and (@wait_event_contract_version is null or wait.event_contract_version = @wait_event_contract_version)
                    and wait.correlation_id = @wait_correlation_id))
            order by instance_id;
            """,
            connection);
        AddNullableParameter(command, "instance_id", NpgsqlDbType.Uuid, instanceId?.Value);
        AddNullableParameter(command, "definition_id", NpgsqlDbType.Uuid, definitionId?.Value);
        AddNullableParameter(command, "wait_event_name", NpgsqlDbType.Text, eventName?.Value);
        AddNullableParameter(
            command,
            "wait_event_contract_version",
            NpgsqlDbType.Integer,
            eventContractVersion?.Value);
        AddNullableParameter(command, "wait_correlation_id", NpgsqlDbType.Text, correlationId?.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            snapshots.Add(new WorkflowProjectionSnapshot
            {
                InstanceId = InstanceId.Parse(reader.GetGuid(0).ToString()),
                ParentInstanceId = reader.IsDBNull(1) ? null : InstanceId.Parse(reader.GetGuid(1).ToString()),
                RootInstanceId = reader.IsDBNull(2) ? null : InstanceId.Parse(reader.GetGuid(2).ToString()),
                DefinitionId = DefinitionId.Parse(reader.GetGuid(3).ToString()),
                DefinitionVersion = new DefinitionVersion(reader.GetInt32(4)),
                Status = Enum.Parse<global::OrcaCore.WorkflowInstanceStatus>(reader.GetString(5)),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(6),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(7),
                ArchivedAt = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
                LastActiveAt = reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
                IsStuck = reader.GetBoolean(13),
                StuckDetectedAt = reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
                ErrorSummary = reader.IsDBNull(8) ? null : reader.GetString(8),
                EndOutcomeName = reader.IsDBNull(9) ? null : reader.GetString(9),
                ContinueAsNewGeneration = reader.GetInt32(10),
                StreamVersion = reader.IsDBNull(16) ? null : reader.GetInt64(16)
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
        WorkflowProjectionSnapshot snapshot,
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
                last_active_at,
                is_stuck,
                stuck_detected_at,
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
                @last_active_at,
                @is_stuck,
                @stuck_detected_at,
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
                last_active_at = excluded.last_active_at,
                is_stuck = excluded.is_stuck,
                stuck_detected_at = excluded.stuck_detected_at,
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
        command.Parameters.AddWithValue("last_active_at", (object?)snapshot.LastActiveAt ?? DBNull.Value);
        command.Parameters.AddWithValue("is_stuck", snapshot.IsStuck);
        command.Parameters.AddWithValue("stuck_detected_at", (object?)snapshot.StuckDetectedAt ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "saga_audits",
            NpgsqlDbType.Jsonb,
            "[]");

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
        WorkflowProjectionActiveWaitSnapshot activeWait,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_active_wait_projections (
                wait_id,
                instance_id,
                event_name,
                event_contract_version,
                correlation_id,
                registered_at,
                status,
                mode)
            values (
                @wait_id,
                @instance_id,
                @event_name,
                @event_contract_version,
                @correlation_id,
                @registered_at,
                @status,
                @mode)
            on conflict (wait_id) do update set
                instance_id = excluded.instance_id,
                event_name = excluded.event_name,
                event_contract_version = excluded.event_contract_version,
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
        command.Parameters.AddWithValue("event_contract_version", activeWait.EventContractVersion);
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

    private static async Task<IReadOnlyDictionary<InstanceId, IReadOnlyList<WorkflowProjectionActiveWaitSnapshot>>> LoadActiveWaitsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<InstanceId> instanceIds,
        CancellationToken cancellationToken)
    {
        if (instanceIds.Count == 0)
        {
            return new Dictionary<InstanceId, IReadOnlyList<WorkflowProjectionActiveWaitSnapshot>>();
        }

        await using var command = new NpgsqlCommand(
            """
            select instance_id,
                   wait_id,
                   event_name,
                   event_contract_version,
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

        var waits = new Dictionary<InstanceId, List<WorkflowProjectionActiveWaitSnapshot>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var instanceId = InstanceId.Parse(reader.GetGuid(0).ToString());
            if (!waits.TryGetValue(instanceId, out var instanceWaits))
            {
                instanceWaits = [];
                waits.Add(instanceId, instanceWaits);
            }

            instanceWaits.Add(new WorkflowProjectionActiveWaitSnapshot
            {
                WaitId = WaitId.Parse(reader.GetGuid(1).ToString()),
                EventName = reader.GetString(2),
                EventContractVersion = reader.GetInt32(3),
                CorrelationId = CorrelationId.Create(reader.GetString(4)),
                RegisteredAt = reader.GetFieldValue<DateTimeOffset>(5),
                Status = reader.GetString(6),
                Mode = reader.GetString(7)
            });
        }

        return waits.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<WorkflowProjectionActiveWaitSnapshot>)pair.Value);
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

}
