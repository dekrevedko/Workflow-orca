using Microsoft.Extensions.DependencyInjection;
using OrcaCore.ProviderCertification;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

[Trait(Traits.Container, "SqlServer")]
[Collection(SqlServerCollection.Name)]
public sealed class SqlServerGovernanceRetryTests(SqlServerContainerFixture container) : GovernanceRetryCertificationTests
{
    private readonly string schema = SqlServerContainerFixture.CreateSchemaName();

    protected override IServiceCollection CreateProviderServices() =>
        new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(new(container.ConnectionString, schema));
}
