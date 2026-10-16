using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.SqlServer.Internal;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

[Trait(Traits.Container, "SqlServer")]
[Collection(SqlServerCollection.Name)]
public sealed class SqlServerResourcePoolCertificationTests : ResourcePoolStoreCertificationTests, IAsyncLifetime
{
    private readonly SqlServerContainerFixture container;
    private SqlServerResourcePoolStore? store;

    public SqlServerResourcePoolCertificationTests(SqlServerContainerFixture container)
    {
        this.container = container;
    }

    public async ValueTask InitializeAsync() =>
        store = new SqlServerResourcePoolStore(await container.CreateDocumentsAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected override IResourcePoolStore CreateStore() =>
        store ?? throw new InvalidOperationException("SQL Server resource-pool store is not initialized.");
}
