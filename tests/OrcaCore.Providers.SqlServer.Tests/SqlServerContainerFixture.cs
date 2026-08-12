using OrcaCore.Providers.SqlServer.Internal;
using Testcontainers.MsSql;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "SQL Server provider";
}

public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private const string ContainerPassword = "OrcaCore!123";
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword(ContainerPassword)
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync() =>
        await container.StartAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    internal async Task<SqlServerStateDocumentStore> CreateDocumentsAsync()
    {
        var schema = CreateSchemaName();
        return await CreateDocumentsAsync(schema);
    }

    internal async Task<SqlServerStateDocumentStore> CreateDocumentsAsync(string schema)
    {
        var documents = new SqlServerStateDocumentStore(ConnectionString, schema);
        await documents.InitializeAsync(TestContext.Current.CancellationToken);
        return documents;
    }

    internal static string CreateSchemaName() => "orcacore_" + Guid.NewGuid().ToString("N");
}
