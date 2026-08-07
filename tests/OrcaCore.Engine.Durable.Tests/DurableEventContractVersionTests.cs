using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests;

public sealed class DurableEventContractVersionTests
{
    [Fact]
    public void WaitState_PersistsAndMatchesTheExactEventContractVersion()
    {
        var waitId = WaitId.Parse(Guid.CreateVersion7().ToString());
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var state = DurableWaitState.FromSnapshot([], []);
        state.Apply(new WorkflowWaitRegisteredEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = instanceId,
            CommandId = CommandId.New(),
            CausationId = CausationId.New(),
            OccurredAt = DateTimeOffset.UtcNow,
            WaitId = waitId,
            EventName = "Approved",
            EventContractVersion = 2,
            CorrelationId = CorrelationId.Create("order-1")
        });

        state.CreateCheckpointActiveWaits().Should().ContainSingle()
            .Which.EventContractVersion.Should().Be(2);
        state.FindActiveWait(Envelope(1)).Should().BeNull();
        state.FindActiveWait(Envelope(2)).Should().NotBeNull();

        state.Apply(new WorkflowWaitMatchedEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = instanceId,
            CommandId = CommandId.New(),
            CausationId = CausationId.New(),
            OccurredAt = DateTimeOffset.UtcNow,
            WaitId = waitId,
            MatchedEventId = EventId.Create(Guid.CreateVersion7().ToString()),
            EventName = "Approved",
            EventContractVersion = 2,
            CorrelationId = CorrelationId.Create("order-1")
        });

        state.CreateCheckpointPendingResumes().Should().ContainSingle()
            .Which.EventContractVersion.Should().Be(2);
    }

    private static DurableEventEnvelope Envelope(int version) => new()
    {
        EventId = EventId.Create(Guid.CreateVersion7().ToString()),
        EventName = "Approved",
        EventContractVersion = version,
        CorrelationId = CorrelationId.Create("order-1"),
        OccurredAt = DateTimeOffset.UtcNow,
        Route = new DurableEventRouteEnvelope
        {
            Kind = "direct",
            InstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString())
        }
    };
}
