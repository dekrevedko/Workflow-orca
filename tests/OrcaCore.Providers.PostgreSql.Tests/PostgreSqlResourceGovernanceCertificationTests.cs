using Npgsql;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.ProviderCertification;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using OrcaCore.TestSupport;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Providers.PostgreSql.Tests;

[Trait(Traits.Container, "PostgreSql")]
public sealed class PostgreSqlResourceGovernanceCertificationTests(PostgreSqlResourceGovernanceCertificationTests.Database database) :
    ResourceGovernanceStoreCertificationTests, IClassFixture<PostgreSqlResourceGovernanceCertificationTests.Database>
{
    protected override IDurableResourceGovernanceStore CreateStore() =>
        new PostgreSqlResourceGovernanceStore(database.DataSource);

    [Fact]
    public async Task AbsentPartitionAndStoredZeroVersionBothHaveAnEmptyHead()
    {
        var token = TestContext.Current.CancellationToken;
        var partition = ResourceGovernancePartitionId.Create("stored-zero");
        var store = CreateStore();
        Assert.Null((await store.LoadLatestAsync(partition, token)).Record);
        await using var insert = database.DataSource.CreateCommand(
            "insert into orcacore_resource_governance_streams (partition_id, version) values ($1, 0)");
        insert.Parameters.Add(new NpgsqlParameter<string> { TypedValue = partition.Value });
        Assert.Equal(1, await insert.ExecuteNonQueryAsync(token));
        var head = await store.LoadLatestAsync(partition, token);
        Assert.Equal(partition, head.PartitionId);
        Assert.Equal(0, head.Version);
        Assert.Null(head.Record);
        Assert.Equal(new ResourceGovernanceAppendResult.Committed(1),
            await store.AppendAsync(partition, head.Version, [Record(1, "first")], token));
    }

    [Theory]
    [InlineData("missing-record")]
    [InlineData("no-records")]
    [InlineData("version-ahead")]
    [InlineData("record-ahead")]
    [InlineData("zero-with-records")]
    [InlineData("checksum")]
    [InlineData("format")]
    public async Task InvalidLatestRecordOrHeadFailsClosed(string damage)
    {
        var token = TestContext.Current.CancellationToken;
        var partition = ResourceGovernancePartitionId.Create(damage);
        var store = CreateStore();
        await store.AppendAsync(partition, 0, [Record(1, "older"), Record(2, "current")], token);
        Assert.Equal(2, (await store.LoadLatestAsync(partition, token)).Version);
        var sql = damage switch
        {
            "missing-record" => "delete from orcacore_resource_governance_records where partition_id = $1 and sequence = 2",
            "no-records" => "delete from orcacore_resource_governance_records where partition_id = $1",
            "version-ahead" => "update orcacore_resource_governance_streams set version = 3 where partition_id = $1",
            "record-ahead" => "update orcacore_resource_governance_streams set version = 1 where partition_id = $1",
            "zero-with-records" => "update orcacore_resource_governance_streams set version = 0 where partition_id = $1",
            "checksum" => "update orcacore_resource_governance_records set checksum = 'damaged' where partition_id = $1 and sequence = 2",
            "format" => "update orcacore_resource_governance_records set format_id = 'unknown' where partition_id = $1 and sequence = 2",
            _ => throw new ArgumentOutOfRangeException(nameof(damage))
        };
        await using var command = database.DataSource.CreateCommand(sql);
        command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = partition.Value });
        Assert.Equal(damage == "no-records" ? 2 : 1, await command.ExecuteNonQueryAsync(token));

        var reconnected = CreateStore();
        await Assert.ThrowsAnyAsync<Exception>(() => reconnected.LoadLatestAsync(partition, token).AsTask());
        await Assert.ThrowsAnyAsync<Exception>(() => reconnected.LoadAsync(partition, token).AsTask());
        Assert.Null((await reconnected.LoadLatestAsync(ResourceGovernancePartitionId.Create("absent"), token)).Record);
    }

    [Fact]
    public async Task DamagedHistoricalChecksumIsDetectedByAuditAndDoesNotInvalidateCurrentState()
    {
        var token = TestContext.Current.CancellationToken;
        var partition = ResourceGovernancePartitionId.Create("historical-checksum");
        var store = CreateStore();
        await store.AppendAsync(partition, 0, [Record(1, "older"), Record(2, "current")], token);
        await using var corrupt = database.DataSource.CreateCommand(
            "update orcacore_resource_governance_records set checksum = 'damaged' where partition_id = $1 and sequence = 1");
        corrupt.Parameters.Add(new NpgsqlParameter<string> { TypedValue = partition.Value });
        Assert.Equal(1, await corrupt.ExecuteNonQueryAsync(token));

        var reconnected = CreateStore();
        var head = await reconnected.LoadLatestAsync(partition, token);
        Assert.Equal(2, head.Version);
        Assert.Equal("current"u8.ToArray(), head.Record!.Payload.ToArray());
        await Assert.ThrowsAnyAsync<ArgumentException>(() => reconnected.LoadAsync(partition, token).AsTask());
    }

    public sealed class Database : IAsyncLifetime
    {
        private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        internal NpgsqlDataSource DataSource { get; private set; } = null!;

        public async ValueTask InitializeAsync()
        {
            await container.StartAsync(TestContext.Current.CancellationToken);
            DataSource = NpgsqlDataSource.Create(container.GetConnectionString());
        }

        public async ValueTask DisposeAsync()
        {
            if (DataSource is not null) await DataSource.DisposeAsync();
            await container.DisposeAsync();
        }
    }
}
