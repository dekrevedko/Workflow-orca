using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.SqlServer;

internal sealed class SqlServerResourcePoolStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string connectionString;

    public SqlServerResourcePoolStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
    }

    public async Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateResourcePool(definition);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_resource_pools
            set capacity = @capacity,
                lease_duration_seconds = @lease_duration_seconds
            where pool_name = @pool_name;
            if @@rowcount = 0
            begin
                insert into dbo.orcacore_resource_pools (pool_name, capacity, lease_duration_seconds)
                values (@pool_name, @capacity, @lease_duration_seconds);
            end;
            """,
            connection);
        command.Parameters.AddWithValue("@pool_name", definition.Name);
        command.Parameters.AddWithValue("@capacity", definition.Capacity);
        AddNullable(command, "@lease_duration_seconds", definition.LeaseDuration is null
            ? null
            : (int)definition.LeaseDuration.Value.TotalSeconds);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResourcePoolAcquireResult> AcquireAsync(
        ResourcePoolAcquireRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateResourcePoolAcquireRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await LockResourcePoolStateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        var pools = await LoadPoolDefinitionsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (request.Requirements.Any(requirement => !pools.ContainsKey(requirement.PoolName)))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ResourcePoolAcquireResult(
                ResourcePoolAcquireStatus.Rejected,
                [],
                null,
                "One or more resource pools do not exist.");
        }

        var heldCounts = await LoadHeldCountsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (CanGrantResourceTickets(request.Requirements, pools, heldCounts))
        {
            var tickets = await InsertTicketsAsync(
                connection,
                transaction,
                request.HolderInstanceId,
                request.HolderKey,
                request.Requirements,
                request.RequestedAt,
                request.ExpiresAt,
                cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ResourcePoolAcquireResult(ResourcePoolAcquireStatus.Granted, tickets, null, null);
        }

        var waiter = await UpsertWaiterAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ResourcePoolAcquireResult(ResourcePoolAcquireStatus.Queued, [], waiter, null);
    }

    public async Task<ResourcePoolReleaseResult> ReleaseAsync(
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await LockResourcePoolStateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        var released = await DeleteTicketsAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
        var granted = await GrantQueuedResourceWaitersAsync(
            connection,
            transaction,
            request.ReleasedAt,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ResourcePoolReleaseResult(released, granted);
    }

    public async Task<Option<ResourcePoolSnapshot>> GetPoolAsync(string poolName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var definition = await LoadPoolDefinitionAsync(connection, null, poolName, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return Option<ResourcePoolSnapshot>.None;
        }

        var tickets = await LoadTicketsAsync(connection, null, poolName, cancellationToken).ConfigureAwait(false);
        var waiters = (await LoadWaitersAsync(connection, null, cancellationToken).ConfigureAwait(false))
            .Where(waiter => waiter.Requirements.Any(requirement =>
                string.Equals(requirement.PoolName, poolName, StringComparison.Ordinal)))
            .ToArray();
        var expiredTickets = await LoadExpiredTicketsAsync(connection, null, poolName, cancellationToken).ConfigureAwait(false);
        var auditRecords = await LoadAuditRecordsAsync(connection, null, poolName, cancellationToken).ConfigureAwait(false);

        return Option<ResourcePoolSnapshot>.Some(new ResourcePoolSnapshot(
            definition.Name,
            definition.Capacity,
            Math.Max(0, definition.Capacity - tickets.Sum(ticket => ticket.Count)),
            tickets,
            waiters)
        {
            ExpiredTickets = expiredTickets,
            AuditRecords = auditRecords
        });
    }

    public async Task ResizePoolAsync(string poolName, int capacity, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_resource_pools
            set capacity = @capacity
            where pool_name = @pool_name;
            """,
            connection);
        command.Parameters.AddWithValue("@pool_name", poolName);
        command.Parameters.AddWithValue("@capacity", capacity);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await LockResourcePoolStateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        var tickets = await LoadAllTicketsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var expired = new List<ResourcePoolExpiredTicket>();
        foreach (var ticket in tickets.Where(ticket => ticket.ExpiresAt <= now))
        {
            if (await ExpiredTicketExistsAsync(connection, transaction, ticket.TicketId, cancellationToken)
                    .ConfigureAwait(false))
            {
                continue;
            }

            var expiredTicket = new ResourcePoolExpiredTicket(ticket, now);
            await InsertExpiredTicketAsync(connection, transaction, expiredTicket, cancellationToken)
                .ConfigureAwait(false);
            expired.Add(expiredTicket);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ResourcePoolExpiryResult(expired);
    }

    public async Task<ResourcePoolForceReleaseResult> ForceReleaseTicketAsync(
        Guid ticketId,
        string reason,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await LockResourcePoolStateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        var ticket = await DeleteTicketAsync(connection, transaction, ticketId, cancellationToken).ConfigureAwait(false);
        if (ticket is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ResourcePoolForceReleaseResult(null, [], null);
        }

        await DeleteExpiredTicketAsync(connection, transaction, ticketId, cancellationToken).ConfigureAwait(false);
        var audit = new ResourcePoolAuditRecord(
            Guid.CreateVersion7(),
            "ForceRelease",
            reason,
            releasedAt,
            ticket);
        await InsertAuditRecordAsync(connection, transaction, audit, cancellationToken).ConfigureAwait(false);
        var granted = await GrantQueuedResourceWaitersAsync(
            connection,
            transaction,
            releasedAt,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ResourcePoolForceReleaseResult(ticket, granted, audit);
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task LockResourcePoolStateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            declare @lock_result int;
            exec @lock_result = sys.sp_getapplock
                @Resource = N'orcacore_resource_pool_state',
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction';
            if @lock_result < 0
                throw 51000, 'Could not acquire SQL Server resource-pool state lock.', 1;
            """,
            connection,
            transaction);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, ResourcePoolDefinition>> LoadPoolDefinitionsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select pool_name, capacity, lease_duration_seconds
            from dbo.orcacore_resource_pools;
            """,
            connection,
            transaction);

        var pools = new Dictionary<string, ResourcePoolDefinition>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.GetString(0);
            pools[name] = new ResourcePoolDefinition(
                name,
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : TimeSpan.FromSeconds(reader.GetInt32(2)));
        }

        return pools;
    }

    private static async Task<ResourcePoolDefinition?> LoadPoolDefinitionAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string poolName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select pool_name, capacity, lease_duration_seconds
            from dbo.orcacore_resource_pools
            where pool_name = @pool_name;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@pool_name", poolName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ResourcePoolDefinition(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.IsDBNull(2) ? null : TimeSpan.FromSeconds(reader.GetInt32(2)));
    }

    private static async Task<Dictionary<string, int>> LoadHeldCountsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select pool_name, coalesce(sum(ticket_count), 0)
            from dbo.orcacore_resource_tickets
            group by pool_name;
            """,
            connection,
            transaction);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            counts[reader.GetString(0)] = reader.GetInt32(1);
        }

        return counts;
    }

    private static async Task<IReadOnlyList<ResourcePoolTicket>> LoadTicketsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string poolName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select ticket_id, pool_name, ticket_count, holder_instance_id, holder_key, acquired_at, expires_at
            from dbo.orcacore_resource_tickets
            where pool_name = @pool_name
            order by acquired_at, ticket_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@pool_name", poolName);

        var tickets = new List<ResourcePoolTicket>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tickets.Add(ReadTicket(reader));
        }

        return tickets;
    }

    private static async Task<IReadOnlyList<ResourcePoolTicket>> LoadAllTicketsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select ticket_id, pool_name, ticket_count, holder_instance_id, holder_key, acquired_at, expires_at
            from dbo.orcacore_resource_tickets
            order by acquired_at, ticket_id;
            """,
            connection,
            transaction);

        var tickets = new List<ResourcePoolTicket>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tickets.Add(ReadTicket(reader));
        }

        return tickets;
    }

    private static async Task<IReadOnlyList<ResourcePoolExpiredTicket>> LoadExpiredTicketsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string poolName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select ticket, expired_at
            from dbo.orcacore_resource_expired_tickets
            where pool_name = @pool_name
            order by expired_at, ticket_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@pool_name", poolName);

        var expired = new List<ResourcePoolExpiredTicket>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            expired.Add(new ResourcePoolExpiredTicket(
                DeserializeTicket(reader.GetString(0)),
                reader.GetFieldValue<DateTimeOffset>(1)));
        }

        return expired;
    }

    private static async Task<IReadOnlyList<ResourcePoolAuditRecord>> LoadAuditRecordsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string poolName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select audit_id, action, reason, occurred_at, ticket
            from dbo.orcacore_resource_audit
            where pool_name = @pool_name
            order by occurred_at, audit_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@pool_name", poolName);

        var auditRecords = new List<ResourcePoolAuditRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            auditRecords.Add(new ResourcePoolAuditRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : DeserializeTicket(reader.GetString(4))));
        }

        return auditRecords;
    }

    private static async Task<IReadOnlyList<ResourcePoolWaiter>> LoadWaitersAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select waiter_id, holder_instance_id, holder_key, requirements, requested_at, expires_at
            from dbo.orcacore_resource_waiters
            order by requested_at, waiter_id;
            """,
            connection,
            transaction);

        var waiters = new List<ResourcePoolWaiter>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            waiters.Add(new ResourcePoolWaiter(
                reader.GetGuid(0),
                new InstanceId(reader.GetGuid(1)),
                reader.GetString(2),
                DeserializeRequirements(reader.GetString(3)),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return waiters;
    }

    private static async Task<IReadOnlyList<ResourcePoolTicket>> InsertTicketsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        InstanceId holderInstanceId,
        string holderKey,
        IReadOnlyList<ResourcePoolRequirement> requirements,
        DateTimeOffset acquiredAt,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        var tickets = requirements
            .Select(requirement => new ResourcePoolTicket(
                Guid.CreateVersion7(),
                requirement.PoolName,
                requirement.Count,
                holderInstanceId,
                holderKey,
                acquiredAt,
                expiresAt))
            .ToArray();

        foreach (var ticket in tickets)
        {
            await using var command = new SqlCommand(
                """
                insert into dbo.orcacore_resource_tickets (
                    ticket_id,
                    pool_name,
                    holder_instance_id,
                    holder_key,
                    ticket_count,
                    acquired_at,
                    expires_at)
                values (
                    @ticket_id,
                    @pool_name,
                    @holder_instance_id,
                    @holder_key,
                    @ticket_count,
                    @acquired_at,
                    @expires_at);
                """,
                connection,
                transaction);
            AddTicketParameters(command, ticket);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return tickets;
    }

    private static async Task<ResourcePoolWaiter> UpsertWaiterAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ResourcePoolAcquireRequest request,
        CancellationToken cancellationToken)
    {
        var existing = (await LoadWaitersAsync(connection, transaction, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(waiter => waiter.HolderInstanceId == request.HolderInstanceId &&
                string.Equals(waiter.HolderKey, request.HolderKey, StringComparison.Ordinal));
        if (existing is not null)
        {
            return existing;
        }

        var waiter = new ResourcePoolWaiter(
            Guid.CreateVersion7(),
            request.HolderInstanceId,
            request.HolderKey,
            request.Requirements.ToArray(),
            request.RequestedAt,
            request.ExpiresAt);
        await using var command = new SqlCommand(
            """
            insert into dbo.orcacore_resource_waiters (
                waiter_id,
                holder_instance_id,
                holder_key,
                requirements,
                requested_at,
                expires_at)
            values (
                @waiter_id,
                @holder_instance_id,
                @holder_key,
                @requirements,
                @requested_at,
                @expires_at);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@waiter_id", waiter.WaiterId);
        command.Parameters.AddWithValue("@holder_instance_id", waiter.HolderInstanceId.Value);
        command.Parameters.AddWithValue("@holder_key", waiter.HolderKey);
        command.Parameters.AddWithValue("@requirements", SerializeRequirements(waiter.Requirements));
        command.Parameters.AddWithValue("@requested_at", waiter.RequestedAt);
        AddNullable(command, "@expires_at", waiter.ExpiresAt);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return waiter;
    }

    private static async Task<IReadOnlyList<ResourcePoolTicket>> DeleteTicketsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            delete from dbo.orcacore_resource_tickets
            output deleted.ticket_id, deleted.pool_name, deleted.ticket_count,
                   deleted.holder_instance_id, deleted.holder_key, deleted.acquired_at, deleted.expires_at
            where holder_instance_id = @holder_instance_id
              and holder_key = @holder_key;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@holder_instance_id", request.HolderInstanceId.Value);
        command.Parameters.AddWithValue("@holder_key", request.HolderKey);

        var released = new List<ResourcePoolTicket>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            released.Add(ReadTicket(reader));
        }

        return released;
    }

    private static async Task<ResourcePoolTicket?> DeleteTicketAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            delete from dbo.orcacore_resource_tickets
            output deleted.ticket_id, deleted.pool_name, deleted.ticket_count,
                   deleted.holder_instance_id, deleted.holder_key, deleted.acquired_at, deleted.expires_at
            where ticket_id = @ticket_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@ticket_id", ticketId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadTicket(reader)
            : null;
    }

    private static async Task<bool> ExpiredTicketExistsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            select case when exists (
                select 1
                from dbo.orcacore_resource_expired_tickets
                where ticket_id = @ticket_id)
            then cast(1 as bit) else cast(0 as bit) end;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@ticket_id", ticketId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task InsertExpiredTicketAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ResourcePoolExpiredTicket expiredTicket,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            insert into dbo.orcacore_resource_expired_tickets (ticket_id, pool_name, ticket, expired_at)
            values (@ticket_id, @pool_name, @ticket, @expired_at);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@ticket_id", expiredTicket.Ticket.TicketId);
        command.Parameters.AddWithValue("@pool_name", expiredTicket.Ticket.PoolName);
        command.Parameters.AddWithValue("@ticket", SerializeTicket(expiredTicket.Ticket));
        command.Parameters.AddWithValue("@expired_at", expiredTicket.ExpiredAt);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteExpiredTicketAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "delete from dbo.orcacore_resource_expired_tickets where ticket_id = @ticket_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@ticket_id", ticketId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertAuditRecordAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ResourcePoolAuditRecord audit,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            insert into dbo.orcacore_resource_audit (
                audit_id,
                pool_name,
                action,
                reason,
                occurred_at,
                ticket)
            values (
                @audit_id,
                @pool_name,
                @action,
                @reason,
                @occurred_at,
                @ticket);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@audit_id", audit.AuditId);
        command.Parameters.AddWithValue("@pool_name", audit.Ticket?.PoolName ?? string.Empty);
        command.Parameters.AddWithValue("@action", audit.Action);
        command.Parameters.AddWithValue("@reason", audit.Reason);
        command.Parameters.AddWithValue("@occurred_at", audit.OccurredAt);
        AddNullable(command, "@ticket", audit.Ticket is null ? null : SerializeTicket(audit.Ticket));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ResourcePoolWaiter>> GrantQueuedResourceWaitersAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        DateTimeOffset grantedAt,
        CancellationToken cancellationToken)
    {
        var granted = new List<ResourcePoolWaiter>();
        var pools = await LoadPoolDefinitionsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var heldCounts = await LoadHeldCountsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var waiters = await LoadWaitersAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        foreach (var waiter in waiters)
        {
            if (!CanGrantResourceTickets(waiter.Requirements, pools, heldCounts))
            {
                continue;
            }

            await InsertTicketsAsync(
                connection,
                transaction,
                waiter.HolderInstanceId,
                waiter.HolderKey,
                waiter.Requirements,
                grantedAt,
                waiter.ExpiresAt,
                cancellationToken).ConfigureAwait(false);
            await DeleteWaiterAsync(connection, transaction, waiter.WaiterId, cancellationToken).ConfigureAwait(false);

            foreach (var requirement in waiter.Requirements)
            {
                heldCounts[requirement.PoolName] = heldCounts.GetValueOrDefault(requirement.PoolName) + requirement.Count;
            }

            granted.Add(waiter);
        }

        return granted;
    }

    private static async Task DeleteWaiterAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid waiterId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "delete from dbo.orcacore_resource_waiters where waiter_id = @waiter_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@waiter_id", waiterId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool CanGrantResourceTickets(
        IEnumerable<ResourcePoolRequirement> requirements,
        IReadOnlyDictionary<string, ResourcePoolDefinition> pools,
        IReadOnlyDictionary<string, int> heldCounts)
    {
        foreach (var requirement in requirements)
        {
            var held = heldCounts.GetValueOrDefault(requirement.PoolName);
            if (pools[requirement.PoolName].Capacity - held < requirement.Count)
            {
                return false;
            }
        }

        return true;
    }

    private static ResourcePoolTicket ReadTicket(SqlDataReader reader)
    {
        return new ResourcePoolTicket(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt32(2),
            new InstanceId(reader.GetGuid(3)),
            reader.GetString(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6));
    }

    private static void AddTicketParameters(SqlCommand command, ResourcePoolTicket ticket)
    {
        command.Parameters.AddWithValue("@ticket_id", ticket.TicketId);
        command.Parameters.AddWithValue("@pool_name", ticket.PoolName);
        command.Parameters.AddWithValue("@holder_instance_id", ticket.HolderInstanceId.Value);
        command.Parameters.AddWithValue("@holder_key", ticket.HolderKey);
        command.Parameters.AddWithValue("@ticket_count", ticket.Count);
        command.Parameters.AddWithValue("@acquired_at", ticket.AcquiredAt);
        AddNullable(command, "@expires_at", ticket.ExpiresAt);
    }

    private static string SerializeRequirements(IReadOnlyList<ResourcePoolRequirement> requirements)
    {
        return JsonSerializer.Serialize(requirements, JsonOptions);
    }

    private static IReadOnlyList<ResourcePoolRequirement> DeserializeRequirements(string requirements)
    {
        return JsonSerializer.Deserialize<ResourcePoolRequirement[]>(requirements, JsonOptions) ?? [];
    }

    private static string SerializeTicket(ResourcePoolTicket ticket)
    {
        return JsonSerializer.Serialize(ticket, JsonOptions);
    }

    private static ResourcePoolTicket DeserializeTicket(string ticket)
    {
        return JsonSerializer.Deserialize<ResourcePoolTicket>(ticket, JsonOptions)
            ?? throw new JsonException("Resource-pool ticket payload could not be deserialized.");
    }

    private static void AddNullable(SqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static void ValidateResourcePool(ResourcePoolDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        ArgumentOutOfRangeException.ThrowIfNegative(definition.Capacity);
    }

    private static void ValidateResourcePoolAcquireRequest(ResourcePoolAcquireRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
        if (request.Requirements.Count == 0)
        {
            throw new ArgumentException("At least one resource requirement is required.", nameof(request));
        }

        foreach (var requirement in request.Requirements)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(requirement.PoolName);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requirement.Count, 0);
        }
    }
}
