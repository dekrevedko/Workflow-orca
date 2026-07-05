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

internal sealed class SqlServerProjectionStore(string connectionString)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await ApplyProjectionOperationsAsync(connection, transaction, operations, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"""
            select instance_id, parent_instance_id, root_instance_id, definition_id, definition_version,
                   status, created_at, updated_at, error_summary, outcome_name,
                   continue_as_new_generation, archived_at, saga_audits, stream_version
            from dbo.orcacore_instance_projections summary
            {SqlServerProjectionQueryBuilder.SummaryWhereClause}
            order by summary.instance_id;
            """,
            connection);
        SqlServerProjectionQueryBuilder.AddParameters(command, query);

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
                    : JsonSerializer.Deserialize<IReadOnlyList<SagaAuditScopeSnapshot>>(reader.GetString(12), JsonOptions) ?? [],
                StreamVersion = reader.IsDBNull(13) ? null : reader.GetInt64(13)
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
            .ToArray();
    }

    public async Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"""
            select count(*)
            from dbo.orcacore_instance_projections summary
            {SqlServerProjectionQueryBuilder.SummaryWhereClause};
            """,
            connection);
        SqlServerProjectionQueryBuilder.AddParameters(command, query);

        var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(count);
    }

    public async Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"""
            select wait.wait_id, wait.event_name, wait.correlation_id, wait.registered_at, wait.status, wait.mode
            from dbo.orcacore_active_wait_projections wait
            join dbo.orcacore_instance_projections summary on summary.instance_id = wait.instance_id
            {SqlServerProjectionQueryBuilder.SummaryWhereClause}
            order by wait.instance_id, wait.registered_at, wait.wait_id;
            """,
            connection);
        SqlServerProjectionQueryBuilder.AddParameters(command, query);

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

    public async Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            $"""
            select summary.definition_id, summary.definition_version, summary.status, count(*)
            from dbo.orcacore_instance_projections summary
            {SqlServerProjectionQueryBuilder.SummaryWhereClause}
            group by summary.definition_id, summary.definition_version, summary.status
            order by summary.definition_id, summary.definition_version, summary.status;
            """,
            connection);
        SqlServerProjectionQueryBuilder.AddParameters(command, query);

        var groups = new List<WorkflowStatisticsGroup>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            groups.Add(new WorkflowStatisticsGroup
            {
                DefinitionId = new DefinitionId(reader.GetGuid(0)),
                DefinitionVersion = new DefinitionVersion(reader.GetInt32(1)),
                Status = Enum.Parse<WorkflowStatus>(reader.GetString(2)),
                Count = Convert.ToInt32(reader.GetValue(3))
            });
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        return new WorkflowStatistics
        {
            Groups = groups,
            Pressure = await LoadPressureMetricsAsync(connection, cancellationToken).ConfigureAwait(false)
        };
    }

    private static async Task<WorkflowPressureMetrics> LoadPressureMetricsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select
                (select count(*) from dbo.orcacore_events) as stream_events,
                (select count(*) from dbo.orcacore_checkpoints) as checkpoints,
                (select count(*) from dbo.orcacore_outbox where state in (@pending, @retryable)) as pending_outbox,
                (select count(*) from dbo.orcacore_outbox where state = @pending) as outbox_pending,
                (select count(*) from dbo.orcacore_outbox where state = @retryable) as outbox_retryable,
                (select count(*) from dbo.orcacore_outbox where state = @claimed) as outbox_claimed,
                (select isnull(max(events.max_version - isnull(checkpoints.stream_version, 0)), 0)
                    from (
                        select stream_id, max(version) as max_version
                        from dbo.orcacore_events
                        group by stream_id
                    ) events
                    left join dbo.orcacore_checkpoints checkpoints on checkpoints.instance_id = events.stream_id)
                    as checkpoint_lag,
                (select count(*) from dbo.orcacore_instance_projections
                    where status in (@running, @waiting, @paused)) as active_instances;
            """,
            connection);
        command.Parameters.AddWithValue("@pending", OutboxRecordState.Pending.ToString());
        command.Parameters.AddWithValue("@retryable", OutboxRecordState.Retryable.ToString());
        command.Parameters.AddWithValue("@claimed", OutboxRecordState.Claimed.ToString());
        command.Parameters.AddWithValue("@running", WorkflowStatus.Running.ToString());
        command.Parameters.AddWithValue("@waiting", WorkflowStatus.Waiting.ToString());
        command.Parameters.AddWithValue("@paused", WorkflowStatus.Paused.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new WorkflowPressureMetrics();
        }

        return new WorkflowPressureMetrics
        {
            TotalStreamEvents = Convert.ToInt64(reader.GetValue(0)),
            CheckpointCount = Convert.ToInt32(reader.GetValue(1)),
            PendingOutboxCount = Convert.ToInt32(reader.GetValue(2)),
            OutboxPendingCount = Convert.ToInt32(reader.GetValue(3)),
            OutboxRetryableCount = Convert.ToInt32(reader.GetValue(4)),
            OutboxClaimedCount = Convert.ToInt32(reader.GetValue(5)),
            CheckpointLag = Convert.ToInt64(reader.GetValue(6)),
            ActiveInstanceCount = Convert.ToInt32(reader.GetValue(7))
        };
    }

    internal static async Task ApplyProjectionOperationsAsync(
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
            }
        }
    }

    private static async Task AppendHistoryProjectionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        InstanceId instanceId,
        ProjectionHistoryWrite history,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            if not exists (
                select 1 from dbo.orcacore_history_projections where history_id = @history_id)
            begin
                insert into dbo.orcacore_history_projections (
                    history_id, instance_id, recorded_at, kind, payload)
                values (
                    @history_id, @instance_id, @recorded_at, @kind, @payload);
            end;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@history_id", history.HistoryId);
        command.Parameters.AddWithValue("@instance_id", instanceId.Value);
        command.Parameters.AddWithValue("@recorded_at", history.RecordedAt);
        command.Parameters.AddWithValue("@kind", history.Kind);
        command.Parameters.AddWithValue("@payload", history.PayloadJson);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
                stream_version = @stream_version,
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
                    status, stream_version, created_at, updated_at, error_summary, outcome_name,
                    continue_as_new_generation, archived_at, saga_audits)
                values (
                    @instance_id, @parent_instance_id, @root_instance_id, @definition_id, @definition_version,
                    @status, @stream_version, @created_at, @updated_at, @error_summary, @outcome_name,
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
        AddNullable(command, "@stream_version", snapshot.StreamVersion);
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

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
