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

internal sealed class PostgreSqlWorkflowRetentionStore(NpgsqlDataSource dataSource)
{
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
}
