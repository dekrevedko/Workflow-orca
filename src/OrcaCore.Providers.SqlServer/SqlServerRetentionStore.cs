using System.Data;
using Microsoft.Data.SqlClient;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.SqlServer;

internal sealed class SqlServerRetentionStore
{
    private readonly string connectionString;

    public SqlServerRetentionStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
    }

    public async Task<ArchiveResult> ArchiveAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
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

    public async Task<PurgeResult> PurgeAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        if (await IsActiveInstanceAsync(connection, transaction, policy.InstanceId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new PurgeResult { Purged = false, Reason = "Instance is active." };
        }

        if (await HasClaimedOutboxAsync(connection, transaction, policy.InstanceId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new PurgeResult { Purged = false, Reason = "Instance has claimed outbox records." };
        }

        await DeleteInstanceDataAsync(connection, transaction, policy.InstanceId, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new PurgeResult { Purged = true };
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task<bool> IsActiveInstanceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select status
            from dbo.orcacore_instance_projections
            where instance_id = @instance_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@instance_id", instanceId.Value);

        var status = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (status is null)
        {
            return false;
        }

        var workflowStatus = Enum.Parse<WorkflowStatus>(status);
        return workflowStatus is WorkflowStatus.Running or WorkflowStatus.Waiting or WorkflowStatus.Paused;
    }

    private static async Task<bool> ArchiveInstanceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        InstanceId instanceId,
        DateTimeOffset archivedAt,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_instance_projections
            set archived_at = @archived_at
            where instance_id = @instance_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@instance_id", instanceId.Value);
        command.Parameters.AddWithValue("@archived_at", archivedAt);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    private static async Task<bool> HasClaimedOutboxAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select case when exists (
                select 1
                from dbo.orcacore_outbox
                where instance_id = @instance_id
                  and state = @claimed)
            then cast(1 as bit) else cast(0 as bit) end;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@instance_id", instanceId.Value);
        command.Parameters.AddWithValue("@claimed", OutboxRecordState.Claimed.ToString());

        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task DeleteInstanceDataAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        foreach (var sql in new[]
        {
            "delete from dbo.orcacore_active_wait_projections where instance_id = @instance_id;",
            "delete from dbo.orcacore_history_projections where instance_id = @instance_id;",
            "delete from dbo.orcacore_instance_projections where instance_id = @instance_id;",
            "delete from dbo.orcacore_checkpoints where instance_id = @instance_id;",
            "delete from dbo.orcacore_timers where instance_id = @instance_id;",
            "delete from dbo.orcacore_outbox where instance_id = @instance_id;",
            "delete from dbo.orcacore_events where stream_id = @instance_id;"
        })
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@instance_id", instanceId.Value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
