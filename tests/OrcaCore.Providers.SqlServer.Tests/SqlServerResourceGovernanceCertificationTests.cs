using System.Data;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.SqlServer.Internal;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

[Trait(Traits.Container, "SqlServer")]
[Collection(SqlServerCollection.Name)]
public sealed class SqlServerResourceGovernanceCertificationTests :
    ResourceGovernanceStoreCertificationTests,
    IAsyncLifetime
{
    private readonly SqlServerContainerFixture container;
    private readonly string schema = SqlServerContainerFixture.CreateSchemaName();
    private SqlServerResourceGovernanceStore? store;

    public SqlServerResourceGovernanceCertificationTests(SqlServerContainerFixture container)
    {
        this.container = container;
    }

    public async ValueTask InitializeAsync() =>
        store = new SqlServerResourceGovernanceStore(await container.CreateDocumentsAsync(schema));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected override IDurableResourceGovernanceStore CreateStore() =>
        store ?? throw new InvalidOperationException("SQL Server resource-governance store is not initialized.");

    [Fact]
    public async Task ReadingEmptyOrPopulatedHeadDoesNotCreateOrRewriteTheStateDocument()
    {
        var token = TestContext.Current.CancellationToken;
        var partition = ResourceGovernancePartitionId.Create("read-only");
        var writer = CreateStore();
        await using var connection = new SqlConnection(container.ConnectionString);
        await connection.OpenAsync(token);
        await using var revision = connection.CreateCommand();
        revision.CommandText = $"select state_revision from [{schema}].[orcacore_provider_state] where state_key = N'resource-governance'";
        Assert.Null(await revision.ExecuteScalarAsync(token));
        Assert.Null((await writer.LoadLatestAsync(partition, token)).Record);
        Assert.Null(await revision.ExecuteScalarAsync(token));
        await writer.AppendAsync(partition, 0, [Record(1, "first"), Record(2, "last")], token);
        var committedRevision = Assert.IsType<long>(await revision.ExecuteScalarAsync(token));

        var restarted = new SqlServerResourceGovernanceStore(await container.CreateDocumentsAsync(schema));
        var head = await restarted.LoadLatestAsync(partition, token);
        Assert.Equal(2, head.Version);
        Assert.Equal("last"u8.ToArray(), head.Record!.Payload.ToArray());
        Assert.Equal(committedRevision, Assert.IsType<long>(await revision.ExecuteScalarAsync(token)));
    }

    [Theory]
    [InlineData("sequence")]
    [InlineData("format")]
    [InlineData("checksum")]
    [InlineData("missing-last")]
    [InlineData("missing-all")]
    [InlineData("null-version")]
    [InlineData("text-version")]
    [InlineData("negative-version")]
    public async Task InvalidCurrentRecordInThePersistedDocumentFailsClosed(string damage)
    {
        var token = TestContext.Current.CancellationToken;
        var partition = ResourceGovernancePartitionId.Create("damaged");
        var writer = CreateStore();
        await writer.AppendAsync(partition, 0, [Record(1, "older"), Record(2, "current")], token);
        Assert.Equal(2, (await writer.LoadLatestAsync(partition, token)).Version);
        await RewriteStreamAsync(stream =>
        {
            Assert.Equal(2, stream["version"]!.GetValue<long>());
            var records = stream["records"]!.AsArray();
            switch (damage)
            {
                case "sequence": records[1]!["sequence"] = 3; break;
                case "format": records[1]!["formatId"] = "unknown"; break;
                case "checksum": records[1]!["checksum"] = "damaged"; break;
                case "missing-last": records.RemoveAt(1); break;
                case "missing-all": records.Clear(); break;
                case "null-version": stream["version"] = null; break;
                case "text-version": stream["version"] = "2"; break;
                case "negative-version": stream["version"] = -1; break;
                default: throw new ArgumentOutOfRangeException(nameof(damage));
            }
        }, token);

        var restarted = new SqlServerResourceGovernanceStore(await container.CreateDocumentsAsync(schema));
        var damaged = await ReadStoredRowAsync(token);
        await Assert.ThrowsAnyAsync<Exception>(() => restarted.LoadLatestAsync(partition, token).AsTask());
        await Assert.ThrowsAnyAsync<Exception>(() => restarted.LoadAsync(partition, token).AsTask());
        Assert.Equal(damaged, await ReadStoredRowAsync(token));
    }

    [Fact]
    public async Task LegacyDocumentReadsItsCountAsVersionAndIsOnlyUpgradedByAWrite()
    {
        var token = TestContext.Current.CancellationToken;
        var partition = ResourceGovernancePartitionId.Create("legacy");
        await CreateStore().AppendAsync(partition, 0, [Record(1, "first"), Record(2, "last")], token);
        await RewriteStreamAsync(stream => Assert.True(stream.Remove("version")), token);
        var restarted = new SqlServerResourceGovernanceStore(await container.CreateDocumentsAsync(schema));
        var legacy = await ReadStoredRowAsync(token);
        var head = await restarted.LoadLatestAsync(partition, token);
        Assert.Equal(2, head.Version);
        Assert.Equal("last"u8.ToArray(), head.Record!.Payload.ToArray());
        Assert.Equal(legacy, await ReadStoredRowAsync(token));

        Assert.Equal(new ResourceGovernanceAppendResult.Committed(3),
            await restarted.AppendAsync(partition, head.Version, [Record(3, "next")], token));
        var upgraded = JsonNode.Parse((await ReadStoredRowAsync(token)).Payload)!["streams"]![0]!;
        Assert.Equal(3, upgraded["version"]!.GetValue<long>());
        Assert.Equal(new long[] { 1, 2, 3 }, (await restarted.LoadAsync(partition, token)).Records.Select(r => r.Sequence));
    }

    [Fact]
    public async Task EarlierDamagedRecordIsOnlyValidatedByTheFullAudit()
    {
        var token = TestContext.Current.CancellationToken;
        var partition = ResourceGovernancePartitionId.Create("bad-history");
        var store = CreateStore();
        await store.AppendAsync(partition, 0, [Record(1, "first"), Record(2, "latest")], token);
        await RewriteStreamAsync(stream => stream["records"]![0]!["checksum"] = "damaged", token);
        var before = await ReadStoredRowAsync(token);
        var restarted = new SqlServerResourceGovernanceStore(await container.CreateDocumentsAsync(schema));
        var head = await restarted.LoadLatestAsync(partition, token);
        Assert.Equal(2, head.Version);
        Assert.Equal("latest"u8.ToArray(), head.Record!.Payload.ToArray());
        await Assert.ThrowsAnyAsync<ArgumentException>(() => restarted.LoadAsync(partition, token).AsTask());
        Assert.Equal(before, await ReadStoredRowAsync(token));
    }

    private async Task RewriteStreamAsync(Action<JsonObject> change, CancellationToken token)
    {
        var document = JsonNode.Parse((await ReadStoredRowAsync(token)).Payload)!;
        change(document["streams"]!.AsArray().Single()!.AsObject());
        await using var connection = new SqlConnection(container.ConnectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"update [{schema}].[orcacore_provider_state] set payload_json = @payload where state_key = N'resource-governance'";
        command.Parameters.Add("@payload", SqlDbType.NVarChar, -1).Value = document.ToJsonString();
        Assert.Equal(1, await command.ExecuteNonQueryAsync(token));
    }

    private async Task<(long Revision, string Payload)> ReadStoredRowAsync(CancellationToken token)
    {
        await using var connection = new SqlConnection(container.ConnectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"select state_revision, payload_json from [{schema}].[orcacore_provider_state] where state_key = N'resource-governance'";
        await using var reader = await command.ExecuteReaderAsync(token);
        Assert.True(await reader.ReadAsync(token));
        return (reader.GetInt64(0), reader.GetString(1));
    }
}
