using AwesomeAssertions;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.ProviderCertification;

public sealed class InMemoryProviderCertificationTests : EventStoreCertificationTests
{
    [Fact]
    public void InMemoryEventStore_PassesEventStoreCertification()
    {
        CreateFixture().EventStore.Should().BeAssignableTo<IWorkflowEventStore>();
    }

    [Fact]
    public void InMemoryInboxStore_PassesInboxCertification()
    {
        CreateFixture().InboxStore.Should().BeAssignableTo<IWorkflowInboxStore>();
    }

    [Fact]
    public void InMemoryOutboxStore_PassesOutboxCertification()
    {
        CreateFixture().OutboxStore.Should().BeAssignableTo<IWorkflowOutboxStore>();
    }

    [Fact]
    public void InMemoryProjectionStore_PassesProjectionCertification()
    {
        CreateFixture().ProjectionStore.Should().BeAssignableTo<IWorkflowProjectionStore>();
    }

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
