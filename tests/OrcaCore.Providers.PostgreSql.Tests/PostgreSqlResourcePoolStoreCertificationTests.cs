using AwesomeAssertions;
using Npgsql;
using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.TestSupport;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Providers.PostgreSql.Tests;

[Trait(Traits.Container, "PostgreSql")]
public sealed class PostgreSqlResourcePoolStoreCertificationTests :
    ResourcePoolStoreCertificationTests,
    IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("orcacore")
        .WithUsername("orcacore")
        .WithPassword("orcacore")
        .Build();
    private PostgreSqlResourcePoolStore? certificationStore;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        certificationStore = new PostgreSqlResourcePoolStore(container.GetConnectionString());
        await certificationStore.InitializeAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (certificationStore is not null)
        {
            await certificationStore.DisposeAsync();
        }

        await container.DisposeAsync();
    }

    protected override IResourcePoolStore CreateStore()
    {
        return certificationStore ??
            throw new InvalidOperationException("PostgreSQL resource-pool certification store is not initialized.");
    }

    [Fact]
    public async Task InitializeAsync_CreatesOwnershipColumnsFromFirstCreate()
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            select count(*)
            from information_schema.columns
            where table_schema = current_schema()
              and (
                (table_name = 'orcacore_resource_tickets' and column_name in ('fiber_id', 'scope_id'))
                or
                (table_name = 'orcacore_resource_waiters' and column_name in ('fiber_id', 'scope_id'))
              );
            """,
            connection);

        var ownershipColumnCount = (long)(await command.ExecuteScalarAsync(
            TestContext.Current.CancellationToken) ?? throw new InvalidOperationException());

        ownershipColumnCount.Should().Be(4);
    }
}
