using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.SqlServer.Internal;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

[Trait(Traits.Container, "SqlServer")]
[Collection(SqlServerCollection.Name)]
public sealed class SqlServerRetentionCertificationTests : RetentionCertificationTests, IAsyncLifetime
{
    private readonly SqlServerContainerFixture container;
    private SqlServerWorkflowStore? store;

    public SqlServerRetentionCertificationTests(SqlServerContainerFixture container)
    {
        this.container = container;
    }

    public async ValueTask InitializeAsync() =>
        store = new SqlServerWorkflowStore(await container.CreateDocumentsAsync(), TimeProvider.System);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected override IRetentionCertificationFixture CreateFixture() =>
        new Fixture(store ?? throw new InvalidOperationException("SQL Server retention store is not initialized."));

    private sealed class Fixture(SqlServerWorkflowStore store) : IRetentionCertificationFixture
    {
        public IWorkflowEventStore EventStore => store;

        public IWorkflowOutboxStore OutboxStore => store;

        public IWorkflowProjectionStore ProjectionStore => store;

        public ITimerScheduler TimerScheduler => store;

        public async Task<(bool Purged, string? Reason)> PurgeForRetentionAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken)
        {
            var result = await store.PurgeForMaintenanceAsync(
                new WorkflowProviderMaintenanceRequest(instanceId, DateTimeOffset.UnixEpoch),
                cancellationToken).ConfigureAwait(false);
            return result.Disposition == WorkflowProviderMaintenanceDisposition.Purged
                ? (true, null)
                : (false, Explain(result.Blocker));
        }

        private static string? Explain(WorkflowProviderMaintenanceBlocker? blocker) => blocker switch
        {
            WorkflowProviderMaintenanceBlocker.ActiveInstance => "Instance is active.",
            WorkflowProviderMaintenanceBlocker.ClaimedOutboxDispatch => "Instance has claimed outbox records.",
            _ => blocker?.ToString()
        };
    }
}
