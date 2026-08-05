using OrcaCore.Abstractions.Providers;

namespace OrcaCore.ProviderCertification;

public sealed class InMemoryProviderCertificationTests : ContinueAsNewCertificationTests
{
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
