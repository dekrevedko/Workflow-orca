using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.ProviderCertification;

public sealed class InMemoryProviderCertificationTests : ContinueAsNewCertificationTests
{
    protected override IProviderCertificationFixture CreateFixture()
    {
        return new InMemoryProviderCertificationFixture(new InMemoryWorkflowProvider());
    }

    private sealed class InMemoryProviderCertificationFixture(InMemoryWorkflowProvider provider)
        : IProviderCertificationFixture
    {
        public IWorkflowEventStore EventStore => provider;

        public IWorkflowInboxStore InboxStore => provider;

        public IWorkflowOutboxStore OutboxStore => provider;

        public IWorkflowProjectionStore ProjectionStore => provider;
    }
}
