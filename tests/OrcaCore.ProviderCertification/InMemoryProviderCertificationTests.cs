using AwesomeAssertions;
using OrcaCore.Abstractions.Providers;
using Xunit;

namespace OrcaCore.ProviderCertification;

public sealed class InMemoryProviderCertificationTests : ContinueAsNewCertificationTests
{
    [Fact]
    public Task DefinitionFanoutInbox_UsesAtomicStablePerTargetOwnership() =>
        DefinitionFanoutInboxCertification.RunAsync(CreateFixture());

    [Fact]
    public Task StartOrDeliverInbox_UsesAtomicInputBoundIntentOwnership() =>
        StartOrDeliverInboxCertification.RunAsync(CreateFixture());

    [Fact]
    public async Task OperationalStatisticsAndMaintenance_AreProviderAuthoritativeAndReferenceSafe()
    {
        using var provider = InMemoryProviderPorts.Create();
        await OperationalMaintenanceCertification.RunAsync(
            provider.EventStore,
            provider.InboxStore,
            provider.OutboxStore,
            provider.OperationalStore,
            provider.MaintenanceStore,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ActiveWaitQuery_ImplementsTheVersionAwareProviderOverload()
    {
        var projectionStore = InMemoryProviderPorts.Create().ProjectionStore;

        projectionStore.GetType().GetMethod(
                nameof(IWorkflowProjectionStore.FindActiveWaitsAsync),
                [
                    typeof(global::OrcaCore.DefinitionId),
                    typeof(global::OrcaCore.EventName),
                    typeof(global::OrcaCore.EventContractVersion),
                    typeof(global::OrcaCore.CorrelationId),
                    typeof(CancellationToken)
                ])
            .Should().NotBeNull(
                "the selected in-memory provider must filter version identity without the interface fallback");
    }

    protected override IProviderCertificationFixture CreateFixture()
    {
        return new InMemoryProviderCertificationFixture(InMemoryProviderPorts.Create());
    }

    private sealed class InMemoryProviderCertificationFixture(InMemoryProviderPorts provider)
        : IProviderCertificationFixture
    {
        public IWorkflowEventStore EventStore => provider.EventStore;

        public IWorkflowInboxStore InboxStore => provider.InboxStore;

        public IWorkflowStartIdempotencyStore StartIdempotencyStore => provider.StartIdempotencyStore;

        public IWorkflowOutboxStore OutboxStore => provider.OutboxStore;

        public IWorkflowProjectionStore ProjectionStore => provider.ProjectionStore;
    }
}
