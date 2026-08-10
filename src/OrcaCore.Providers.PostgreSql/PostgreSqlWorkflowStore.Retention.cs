using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;
using OrcaCore.Providers.PostgreSql.Internal;

namespace OrcaCore.Providers.PostgreSql;

internal sealed class PostgreSqlWorkflowRetentionStore(NpgsqlDataSource dataSource)
{
    private const string MaintenanceTableLockSql =
        """
        lock table
            orcacore_events,
            orcacore_checkpoints,
            orcacore_inbox,
            orcacore_inbox_fanout_targets,
            orcacore_outbox,
            orcacore_instance_projections,
            orcacore_active_wait_projections,
            orcacore_timers,
            orcacore_history_projections
        in share row exclusive mode;
        """;

    private static readonly string[] InstanceDataDeleteStatements =
    [
        "delete from orcacore_active_wait_projections where instance_id = @instance_id;",
        "delete from orcacore_history_projections where instance_id = @instance_id;",
        "delete from orcacore_instance_projections where instance_id = @instance_id;",
        "delete from orcacore_checkpoints where instance_id = @instance_id;",
        "delete from orcacore_timers where instance_id = @instance_id;",
        "delete from orcacore_outbox where instance_id = @instance_id;",
        "delete from orcacore_events where stream_id = @instance_id;"
    ];

    internal async Task<WorkflowProviderMaintenanceInspection> InspectForMaintenanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            .ConfigureAwait(false);
        var exists = await InstanceArtifactExistsAsync(
            connection,
            transaction,
            instanceId,
            cancellationToken).ConfigureAwait(false);
        var archivedAt = exists
            ? await LoadArchivedAtAsync(connection, transaction, instanceId, cancellationToken).ConfigureAwait(false)
            : null;
        var blocker = exists
            ? await FindMaintenanceBlockerAsync(
                connection,
                transaction,
                instanceId,
                cancellationToken).ConfigureAwait(false)
            : null;
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new WorkflowProviderMaintenanceInspection(exists, archivedAt, blocker);
    }

    internal async Task<WorkflowProviderMaintenanceResult> ArchiveForMaintenanceAsync(
        WorkflowProviderMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await LockMaintenanceTablesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        if (!await ProjectionExistsAsync(
                connection,
                transaction,
                request.InstanceId,
                cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new WorkflowProviderMaintenanceResult(WorkflowProviderMaintenanceDisposition.NotFound);
        }

        if (await IsActiveInstanceAsync(
                connection,
                transaction,
                request.InstanceId,
                cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Rejected(WorkflowProviderMaintenanceBlocker.ActiveInstance);
        }

        await using var command = new NpgsqlCommand(
            """
            update orcacore_instance_projections
            set archived_at = @archived_at
            where instance_id = @instance_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("archived_at", request.RequestedAt);
        command.Parameters.AddWithValue("instance_id", request.InstanceId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new WorkflowProviderMaintenanceResult(WorkflowProviderMaintenanceDisposition.Archived);
    }

    private static async Task<bool> ProjectionExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select exists (select 1 from orcacore_instance_projections where instance_id = @instance_id);",
            connection,
            transaction);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    internal async Task<WorkflowProviderMaintenanceResult> PurgeForMaintenanceAsync(
        WorkflowProviderMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await LockMaintenanceTablesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        if (!await InstanceArtifactExistsAsync(
                connection,
                transaction,
                request.InstanceId,
                cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new WorkflowProviderMaintenanceResult(WorkflowProviderMaintenanceDisposition.NotFound);
        }

        if (await FindMaintenanceBlockerAsync(
                connection,
                transaction,
                request.InstanceId,
                cancellationToken).ConfigureAwait(false) is { } blocker)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Rejected(blocker);
        }

        await DeleteInstanceDataAsync(
            connection,
            transaction,
            request.InstanceId,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new WorkflowProviderMaintenanceResult(WorkflowProviderMaintenanceDisposition.Purged);
    }

    private static async Task<bool> InstanceArtifactExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select exists (
                select 1 from orcacore_instance_projections where instance_id = @instance_id
                union all
                select 1 from orcacore_events where stream_id = @instance_id);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task<DateTimeOffset?> LoadArchivedAtAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select archived_at from orcacore_instance_projections where instance_id = @instance_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value switch
        {
            DateTimeOffset timestamp => timestamp,
            DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
            _ => null
        };
    }

    private static async Task<WorkflowProviderMaintenanceBlocker?> FindMaintenanceBlockerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        if (await IsActiveInstanceAsync(connection, transaction, instanceId, cancellationToken).ConfigureAwait(false))
        {
            return WorkflowProviderMaintenanceBlocker.ActiveInstance;
        }

        if (await HasPendingInboxAsync(connection, transaction, instanceId, cancellationToken).ConfigureAwait(false))
        {
            return WorkflowProviderMaintenanceBlocker.PendingInboxDelivery;
        }

        var states = await LoadOutboxStatesAsync(connection, transaction, instanceId, cancellationToken)
            .ConfigureAwait(false);
        if (states.Contains(OutboxRecordState.Claimed))
        {
            return WorkflowProviderMaintenanceBlocker.ClaimedOutboxDispatch;
        }

        if (states.Contains(OutboxRecordState.Pending) || states.Contains(OutboxRecordState.Retryable))
        {
            return WorkflowProviderMaintenanceBlocker.PendingOutboxDispatch;
        }

        return states.Contains(OutboxRecordState.Poisoned)
            ? WorkflowProviderMaintenanceBlocker.PoisonedOutboxDispatch
            : null;
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
        return status is string statusText && Enum.Parse<global::OrcaCore.WorkflowInstanceStatus>(statusText) is
            global::OrcaCore.WorkflowInstanceStatus.Pending or
            global::OrcaCore.WorkflowInstanceStatus.Running or
            global::OrcaCore.WorkflowInstanceStatus.Waiting or
            global::OrcaCore.WorkflowInstanceStatus.CancellationRequested;
    }

    private static async Task<bool> HasPendingInboxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select exists (
                select 1
                from orcacore_inbox
                where instance_id = @instance_id and state = @received
                union all
                select 1
                from orcacore_inbox_fanout_targets
                where instance_id = @instance_id and state = @received);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        command.Parameters.AddWithValue("received", InboxRecordState.Received.ToString());

        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task<HashSet<OutboxRecordState>> LoadOutboxStatesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select distinct state from orcacore_outbox where instance_id = @instance_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        var states = new HashSet<OutboxRecordState>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            states.Add(Enum.Parse<OutboxRecordState>(reader.GetString(0)));
        }

        return states;
    }

    private static WorkflowProviderMaintenanceResult Rejected(WorkflowProviderMaintenanceBlocker blocker) =>
        new(WorkflowProviderMaintenanceDisposition.Rejected, blocker);

    private static async Task LockMaintenanceTablesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(MaintenanceTableLockSql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteInstanceDataAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        foreach (var sql in InstanceDataDeleteStatements)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("instance_id", instanceId.Value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
