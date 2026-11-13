using AwesomeAssertions;
using OrcaCore.Abstractions.Providers;
using Xunit;

namespace OrcaCore.ProviderCertification;

public sealed class InMemoryProviderCertificationTests : ContinueAsNewCertificationTests
{
    [Fact]
    public async Task DagCreationAndReplay_PersistAnIndependentAggregate()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.CreationAndReplayAsync(provider.EventStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagFencedAppend_AtomicallyPersistsInputsAndAddressedNotices()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.FencedAppendAndObserversAsync(provider.EventStore, provider.OutboxStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagLeaseQueue_UsesFifoAndAnAbsoluteTurnDeadline()
    {
        var clock = new DagCertificationTimeProvider();
        using var provider = InMemoryProviderPorts.Create(clock);
        await DagRunCertification.LeaseQueueAsync(provider.EventStore, clock.GetUtcNow, clock.Advance, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagFailureClosure_PreservesIndependentWorkAndDerivesTerminalOutcome()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.FailureClosureAndTerminalAsync(provider.EventStore, TestContext.Current.CancellationToken);
    }

    private sealed class DagCertificationTimeProvider : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        internal void Advance(TimeSpan duration) => now += duration;
    }

    [Fact]
    public async Task DagObservers_CleanExpiredNoticesButRetainClaimsPoisonAndBusinessWork()
    {
        var clock = new DagCertificationTimeProvider();
        using var provider = InMemoryProviderPorts.Create(clock);
        await DagRunCertification.ObserverCleanupAsync(provider.EventStore, provider.OutboxStore, clock.GetUtcNow, clock.Advance, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagConcurrentLeaseRequests_AreSerialized()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.ConcurrentLeasesAsync(provider.EventStore, provider.EventStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagCancellationAndIntegrity_PreserveAdmissionsAndDeriveCancelledTerminal()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.CancellationAndIntegrityAsync(provider.EventStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagAndWorkflowIdentities_CannotOwnTheSameGuid()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.CrossAggregateIdentityAsync(provider.EventStore, provider.OutboxStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("AC", "AC-108")]
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
            provider.ProjectionStore,
            provider.MaintenanceStore,
            OrcaCore.Abstractions.Diagnostics.OrcaCoreDiagnostics.InMemoryProviderName,
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
