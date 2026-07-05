using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.PostgreSql;

/// <summary>
/// Stores durable resource-pool state in PostgreSQL.
/// </summary>
public sealed class PostgreSqlResourcePoolStore : IResourcePoolStore, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;

    /// <summary>
    /// Initializes a PostgreSQL resource-pool store from a connection string.
    /// </summary>
    public PostgreSqlResourcePoolStore(string connectionString)
        : this(CreateDataSource(connectionString), ownsDataSource: true)
    {
    }

    /// <summary>
    /// Initializes a PostgreSQL resource-pool store from an existing caller-owned data source.
    /// </summary>
    public PostgreSqlResourcePoolStore(NpgsqlDataSource dataSource)
        : this(dataSource, ownsDataSource: false)
    {
    }

    private PostgreSqlResourcePoolStore(NpgsqlDataSource dataSource, bool ownsDataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>
    /// Creates the provider-owned resource-pool tables.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            create table if not exists orcacore_resource_pools (
                pool_name text primary key,
                capacity integer not null,
                lease_duration_seconds integer null
            );

            create table if not exists orcacore_resource_tickets (
                ticket_id uuid primary key,
                pool_name text not null references orcacore_resource_pools(pool_name) on delete cascade,
                holder_instance_id uuid not null,
                holder_key text not null,
                ticket_count integer not null,
                acquired_at timestamp with time zone not null,
                expires_at timestamp with time zone null
            );

            create index if not exists ix_orcacore_resource_tickets_pool
                on orcacore_resource_tickets (pool_name);

            create index if not exists ix_orcacore_resource_tickets_holder
                on orcacore_resource_tickets (holder_instance_id, holder_key);

            create table if not exists orcacore_resource_waiters (
                waiter_id uuid primary key,
                holder_instance_id uuid not null,
                holder_key text not null,
                requirements jsonb not null,
                requested_at timestamp with time zone not null,
                expires_at timestamp with time zone null,
                unique (holder_instance_id, holder_key)
            );

            create index if not exists ix_orcacore_resource_waiters_requested
                on orcacore_resource_waiters (requested_at, waiter_id);

            create table if not exists orcacore_resource_expired_tickets (
                ticket_id uuid primary key,
                pool_name text not null,
                ticket jsonb not null,
                expired_at timestamp with time zone not null
            );

            create table if not exists orcacore_resource_audit (
                audit_id uuid primary key,
                pool_name text not null,
                action text not null,
                reason text not null,
                occurred_at timestamp with time zone not null,
                ticket jsonb null
            );
            """,
            connection);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidatePool(definition);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_resource_pools (pool_name, capacity, lease_duration_seconds)
            values (@pool_name, @capacity, @lease_duration_seconds)
            on conflict (pool_name) do update set
                capacity = excluded.capacity,
                lease_duration_seconds = excluded.lease_duration_seconds;
            """,
            connection);
        command.Parameters.AddWithValue("pool_name", definition.Name);
        command.Parameters.AddWithValue("capacity", definition.Capacity);
        command.Parameters.Add("lease_duration_seconds", NpgsqlDbType.Integer).Value =
            definition.LeaseDuration is null ? DBNull.Value : (int)definition.LeaseDuration.Value.TotalSeconds;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ResourcePoolAcquireResult> AcquireAsync(
        ResourcePoolAcquireRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAcquireRequest(request);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await LockPoolTablesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

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
        if (CanGrant(request.Requirements, pools, heldCounts))
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

        var waiter = await UpsertWaiterAsync(connection, transaction, request, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ResourcePoolAcquireResult(ResourcePoolAcquireStatus.Queued, [], waiter, null);
    }

    /// <inheritdoc />
    public async Task<ResourcePoolReleaseResult> ReleaseAsync(
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await LockPoolTablesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        var released = await DeleteTicketsAsync(connection, transaction, request, cancellationToken)
            .ConfigureAwait(false);
        var granted = await GrantQueuedWaitersAsync(connection, transaction, request.ReleasedAt, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ResourcePoolReleaseResult(released, granted);
    }

    /// <inheritdoc />
    public async Task<Option<ResourcePoolSnapshot>> GetPoolAsync(
        string poolName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var pools = await LoadPoolDefinitionsAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (!pools.TryGetValue(poolName, out var definition))
        {
            return Option<ResourcePoolSnapshot>.None;
        }

        var tickets = await LoadTicketsAsync(connection, null, poolName, cancellationToken).ConfigureAwait(false);
        var expiredTickets = await LoadExpiredTicketsAsync(connection, null, poolName, cancellationToken)
            .ConfigureAwait(false);
        var auditRecords = await LoadAuditRecordsAsync(connection, null, poolName, cancellationToken)
            .ConfigureAwait(false);
        var waiters = (await LoadWaitersAsync(connection, null, cancellationToken).ConfigureAwait(false))
            .Where(waiter => waiter.Requirements.Any(requirement =>
                string.Equals(requirement.PoolName, poolName, StringComparison.Ordinal)))
            .ToArray();
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResourcePoolSnapshot>> ListPoolsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var pools = await LoadPoolDefinitionsAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var waiters = await LoadWaitersAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var snapshots = new List<ResourcePoolSnapshot>(pools.Count);

        foreach (var definition in pools.Values.OrderBy(pool => pool.Name, StringComparer.Ordinal))
        {
            var tickets = await LoadTicketsAsync(connection, null, definition.Name, cancellationToken)
                .ConfigureAwait(false);
            var expiredTickets = await LoadExpiredTicketsAsync(connection, null, definition.Name, cancellationToken)
                .ConfigureAwait(false);
            var auditRecords = await LoadAuditRecordsAsync(connection, null, definition.Name, cancellationToken)
                .ConfigureAwait(false);
            var poolWaiters = waiters
                .Where(waiter => waiter.Requirements.Any(requirement =>
                    string.Equals(requirement.PoolName, definition.Name, StringComparison.Ordinal)))
                .ToArray();

            snapshots.Add(new ResourcePoolSnapshot(
                definition.Name,
                definition.Capacity,
                Math.Max(0, definition.Capacity - tickets.Sum(ticket => ticket.Count)),
                tickets,
                poolWaiters)
            {
                ExpiredTickets = expiredTickets,
                AuditRecords = auditRecords
            });
        }

        return snapshots;
    }

    /// <inheritdoc />
    public async Task ResizePoolAsync(string poolName, int capacity, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            update orcacore_resource_pools
            set capacity = @capacity
            where pool_name = @pool_name;
            """,
            connection);
        command.Parameters.AddWithValue("pool_name", poolName);
        command.Parameters.AddWithValue("capacity", capacity);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await LockPoolTablesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

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

    /// <inheritdoc />
    public async Task<ResourcePoolForceReleaseResult> ForceReleaseTicketAsync(
        Guid ticketId,
        string reason,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await LockPoolTablesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        var ticket = await DeleteTicketAsync(connection, transaction, ticketId, cancellationToken)
            .ConfigureAwait(false);
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
        var granted = await GrantQueuedWaitersAsync(connection, transaction, releasedAt, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ResourcePoolForceReleaseResult(ticket, granted, audit);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (ownsDataSource)
        {
            await dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return NpgsqlDataSource.Create(connectionString);
    }

    private static async Task LockPoolTablesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            lock table orcacore_resource_pools in exclusive mode;
            lock table orcacore_resource_tickets in exclusive mode;
            lock table orcacore_resource_waiters in exclusive mode;
            lock table orcacore_resource_expired_tickets in exclusive mode;
            lock table orcacore_resource_audit in exclusive mode;
            """,
            connection,
            transaction);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, ResourcePoolDefinition>> LoadPoolDefinitionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select pool_name, capacity, lease_duration_seconds
            from orcacore_resource_pools;
            """,
            connection,
            transaction);

        var pools = new Dictionary<string, ResourcePoolDefinition>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            pools[reader.GetString(0)] = new ResourcePoolDefinition(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : TimeSpan.FromSeconds(reader.GetInt32(2)));
        }

        return pools;
    }

    private static async Task<Dictionary<string, int>> LoadHeldCountsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select pool_name, coalesce(sum(ticket_count), 0)::integer
            from orcacore_resource_tickets
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
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string poolName,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select ticket_id, pool_name, ticket_count, holder_instance_id, holder_key, acquired_at, expires_at
            from orcacore_resource_tickets
            where pool_name = @pool_name
            order by acquired_at, ticket_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("pool_name", poolName);

        var tickets = new List<ResourcePoolTicket>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tickets.Add(ReadTicket(reader));
        }

        return tickets;
    }

    private static async Task<IReadOnlyList<ResourcePoolTicket>> LoadAllTicketsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select ticket_id, pool_name, ticket_count, holder_instance_id, holder_key, acquired_at, expires_at
            from orcacore_resource_tickets
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
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string poolName,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select ticket, expired_at
            from orcacore_resource_expired_tickets
            where pool_name = @pool_name
            order by expired_at, ticket_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("pool_name", poolName);

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
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string poolName,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select audit_id, action, reason, occurred_at, ticket
            from orcacore_resource_audit
            where pool_name = @pool_name
            order by occurred_at, audit_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("pool_name", poolName);

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
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select waiter_id, holder_instance_id, holder_key, requirements, requested_at, expires_at
            from orcacore_resource_waiters
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
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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
            await using var command = new NpgsqlCommand(
                """
                insert into orcacore_resource_tickets (
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
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_resource_waiters (
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
        command.Parameters.AddWithValue("waiter_id", waiter.WaiterId);
        command.Parameters.AddWithValue("holder_instance_id", waiter.HolderInstanceId.Value);
        command.Parameters.AddWithValue("holder_key", waiter.HolderKey);
        command.Parameters.Add("requirements", NpgsqlDbType.Jsonb).Value = SerializeRequirements(waiter.Requirements);
        command.Parameters.AddWithValue("requested_at", waiter.RequestedAt);
        command.Parameters.Add("expires_at", NpgsqlDbType.TimestampTz).Value =
            (object?)waiter.ExpiresAt ?? DBNull.Value;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return waiter;
    }

    private static async Task<IReadOnlyList<ResourcePoolTicket>> DeleteTicketsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            delete from orcacore_resource_tickets
            where holder_instance_id = @holder_instance_id
              and holder_key = @holder_key
            returning ticket_id, pool_name, ticket_count, holder_instance_id, holder_key, acquired_at, expires_at;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("holder_instance_id", request.HolderInstanceId.Value);
        command.Parameters.AddWithValue("holder_key", request.HolderKey);

        var released = new List<ResourcePoolTicket>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            released.Add(ReadTicket(reader));
        }

        return released;
    }

    private static async Task<ResourcePoolTicket?> DeleteTicketAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            delete from orcacore_resource_tickets
            where ticket_id = @ticket_id
            returning ticket_id, pool_name, ticket_count, holder_instance_id, holder_key, acquired_at, expires_at;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("ticket_id", ticketId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadTicket(reader)
            : null;
    }

    private static async Task<bool> ExpiredTicketExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select exists (
                select 1
                from orcacore_resource_expired_tickets
                where ticket_id = @ticket_id);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("ticket_id", ticketId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task InsertExpiredTicketAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ResourcePoolExpiredTicket expiredTicket,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_resource_expired_tickets (ticket_id, pool_name, ticket, expired_at)
            values (@ticket_id, @pool_name, @ticket, @expired_at);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("ticket_id", expiredTicket.Ticket.TicketId);
        command.Parameters.AddWithValue("pool_name", expiredTicket.Ticket.PoolName);
        command.Parameters.Add("ticket", NpgsqlDbType.Jsonb).Value = SerializeTicket(expiredTicket.Ticket);
        command.Parameters.AddWithValue("expired_at", expiredTicket.ExpiredAt);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteExpiredTicketAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "delete from orcacore_resource_expired_tickets where ticket_id = @ticket_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("ticket_id", ticketId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertAuditRecordAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ResourcePoolAuditRecord audit,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_resource_audit (
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
        command.Parameters.AddWithValue("audit_id", audit.AuditId);
        command.Parameters.AddWithValue("pool_name", audit.Ticket?.PoolName ?? string.Empty);
        command.Parameters.AddWithValue("action", audit.Action);
        command.Parameters.AddWithValue("reason", audit.Reason);
        command.Parameters.AddWithValue("occurred_at", audit.OccurredAt);
        command.Parameters.Add("ticket", NpgsqlDbType.Jsonb).Value =
            audit.Ticket is null ? DBNull.Value : SerializeTicket(audit.Ticket);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ResourcePoolWaiter>> GrantQueuedWaitersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset grantedAt,
        CancellationToken cancellationToken)
    {
        var granted = new List<ResourcePoolWaiter>();
        var pools = await LoadPoolDefinitionsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var heldCounts = await LoadHeldCountsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var waiters = await LoadWaitersAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        foreach (var waiter in waiters)
        {
            if (!CanGrant(waiter.Requirements, pools, heldCounts))
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
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid waiterId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "delete from orcacore_resource_waiters where waiter_id = @waiter_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("waiter_id", waiterId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool CanGrant(
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

    private static ResourcePoolTicket ReadTicket(NpgsqlDataReader reader)
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

    private static void AddTicketParameters(NpgsqlCommand command, ResourcePoolTicket ticket)
    {
        command.Parameters.AddWithValue("ticket_id", ticket.TicketId);
        command.Parameters.AddWithValue("pool_name", ticket.PoolName);
        command.Parameters.AddWithValue("holder_instance_id", ticket.HolderInstanceId.Value);
        command.Parameters.AddWithValue("holder_key", ticket.HolderKey);
        command.Parameters.AddWithValue("ticket_count", ticket.Count);
        command.Parameters.AddWithValue("acquired_at", ticket.AcquiredAt);
        command.Parameters.Add("expires_at", NpgsqlDbType.TimestampTz).Value =
            (object?)ticket.ExpiresAt ?? DBNull.Value;
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

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    private static void ValidatePool(ResourcePoolDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        ArgumentOutOfRangeException.ThrowIfNegative(definition.Capacity);
    }

    private static void ValidateAcquireRequest(ResourcePoolAcquireRequest request)
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
