using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.SqlServer.Internal;
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
    private SqlServerResourceGovernanceStore? store;

    public SqlServerResourceGovernanceCertificationTests(SqlServerContainerFixture container)
    {
        this.container = container;
    }

    public async ValueTask InitializeAsync() =>
        store = new SqlServerResourceGovernanceStore(await container.CreateDocumentsAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected override IDurableResourceGovernanceStore CreateStore() =>
        store ?? throw new InvalidOperationException("SQL Server resource-governance store is not initialized.");
}
