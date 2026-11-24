using Microsoft.Extensions.DependencyInjection;
using OrcaCore.ProviderCertification;
using OrcaCore.TestSupport;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Providers.PostgreSql.Tests;

[Trait(Traits.Container, "PostgreSql")]
public sealed class PostgreSqlGovernanceRetryTests : GovernanceRetryCertificationTests, IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync() => await container.StartAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => container.DisposeAsync();

    protected override IServiceCollection CreateProviderServices() =>
        new ServiceCollection().AddOrcaCorePostgreSqlDurableProvider(new(container.GetConnectionString(), "public"));
}
