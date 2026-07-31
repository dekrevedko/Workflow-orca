using Npgsql;
using NpgsqlTypes;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Providers.PostgreSql;

/// <summary>
/// Persists one serialized resource-governance aggregate stream per partition.
/// </summary>
public sealed class PostgreSqlResourceGovernanceStore(NpgsqlDataSource dataSource)
    : IDurableResourceGovernanceStore
{
    private readonly SemaphoreSlim initializeGate = new(1, 1);
    private int initialized;

    /// <inheritdoc />
    public async ValueTask<ResourceGovernanceStream> LoadAsync(
        ResourceGovernancePartitionId partitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitionId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await dataSource
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            select sequence, format_id, payload, checksum
            from orcacore_resource_governance_records
            where partition_id = @partition_id
            order by sequence
            """,
            connection);
        command.Parameters.AddWithValue("partition_id", NpgsqlDbType.Text, partitionId.Value);
        var records = new List<ResourceGovernanceRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            records.Add(ResourceGovernanceRecord.FromPersisted(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetFieldValue<byte[]>(2),
                reader.GetString(3)));
        }

        return ResourceGovernanceStream.Create(records.Count, records);
    }

    /// <inheritdoc />
    public async ValueTask<ResourceGovernanceAppendResult> AppendAsync(
        ResourceGovernancePartitionId partitionId,
        long expectedVersion,
        IReadOnlyList<ResourceGovernanceRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
        {
            throw new ArgumentException("A governance append batch cannot be empty.", nameof(records));
        }

        var copied = records.ToArray();
        if (copied.Any(record => record is null))
        {
            throw new ArgumentException("A governance append batch cannot contain null.", nameof(records));
        }

        for (var index = 0; index < copied.Length; index++)
        {
            if (copied[index].Sequence != expectedVersion + index + 1L)
            {
                throw new ArgumentException(
                    "A governance append batch must be consecutive after its expected version.",
                    nameof(records));
            }
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await dataSource
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using (var ensureHead = new NpgsqlCommand(
                         """
                         insert into orcacore_resource_governance_streams (partition_id, version)
                         values (@partition_id, 0)
                         on conflict (partition_id) do nothing
                         """,
                         connection,
                         transaction))
        {
            ensureHead.Parameters.AddWithValue(
                "partition_id",
                NpgsqlDbType.Text,
                partitionId.Value);
            await ensureHead.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        long actualVersion;
        await using (var lockHead = new NpgsqlCommand(
                         """
                         select version
                         from orcacore_resource_governance_streams
                         where partition_id = @partition_id
                         for update
                         """,
                         connection,
                         transaction))
        {
            lockHead.Parameters.AddWithValue(
                "partition_id",
                NpgsqlDbType.Text,
                partitionId.Value);
            actualVersion = (long)(await lockHead
                .ExecuteScalarAsync(cancellationToken)
                .ConfigureAwait(false))!;
        }

        if (actualVersion != expectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ResourceGovernanceAppendResult.Conflict(actualVersion);
        }

        foreach (var record in copied)
        {
            await using var insert = new NpgsqlCommand(
                """
                insert into orcacore_resource_governance_records
                    (partition_id, sequence, format_id, payload, checksum)
                values
                    (@partition_id, @sequence, @format_id, @payload, @checksum)
                """,
                connection,
                transaction);
            insert.Parameters.AddWithValue("partition_id", NpgsqlDbType.Text, partitionId.Value);
            insert.Parameters.AddWithValue("sequence", NpgsqlDbType.Bigint, record.Sequence);
            insert.Parameters.AddWithValue("format_id", NpgsqlDbType.Text, record.FormatId);
            insert.Parameters.AddWithValue("payload", NpgsqlDbType.Bytea, record.Payload.ToArray());
            insert.Parameters.AddWithValue("checksum", NpgsqlDbType.Text, record.Checksum);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var committedVersion = expectedVersion + copied.Length;
        await using (var updateHead = new NpgsqlCommand(
                         """
                         update orcacore_resource_governance_streams
                         set version = @version
                         where partition_id = @partition_id
                         """,
                         connection,
                         transaction))
        {
            updateHead.Parameters.AddWithValue("version", NpgsqlDbType.Bigint, committedVersion);
            updateHead.Parameters.AddWithValue(
                "partition_id",
                NpgsqlDbType.Text,
                partitionId.Value);
            await updateHead.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ResourceGovernanceAppendResult.Committed(committedVersion);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref initialized) != 0)
        {
            return;
        }

        await initializeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref initialized) != 0)
            {
                return;
            }

            await using var connection = await dataSource
                .OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var command = new NpgsqlCommand(
                """
                create table if not exists orcacore_resource_governance_streams (
                    partition_id text primary key,
                    version bigint not null check (version >= 0)
                );

                create table if not exists orcacore_resource_governance_records (
                    partition_id text not null references orcacore_resource_governance_streams(partition_id)
                        on delete cascade,
                    sequence bigint not null check (sequence > 0),
                    format_id text not null,
                    payload bytea not null,
                    checksum text not null,
                    primary key (partition_id, sequence)
                );
                """,
                connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref initialized, 1);
        }
        finally
        {
            initializeGate.Release();
        }
    }
}
