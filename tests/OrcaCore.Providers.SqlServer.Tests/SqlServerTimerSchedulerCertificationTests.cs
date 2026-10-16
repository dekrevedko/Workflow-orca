using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.SqlServer.Internal;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

[Trait(Traits.Container, "SqlServer")]
[Collection(SqlServerCollection.Name)]
public sealed class SqlServerTimerSchedulerCertificationTests : TimerSchedulerCertificationTests, IAsyncLifetime
{
    private readonly SqlServerContainerFixture container;
    private SqlServerWorkflowStore? store;

    public SqlServerTimerSchedulerCertificationTests(SqlServerContainerFixture container)
    {
        this.container = container;
    }

    public async ValueTask InitializeAsync() =>
        store = new SqlServerWorkflowStore(await container.CreateDocumentsAsync(), TimeProvider.System);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected override ITimerScheduler CreateTimerScheduler() =>
        store ?? throw new InvalidOperationException("SQL Server timer scheduler is not initialized.");
}
