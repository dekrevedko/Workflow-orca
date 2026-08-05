using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Ephemeral.Execution;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class EphemeralRoutingIndexTests
{
    [Fact]
    public void IndexSnapshot_ReplacesPreviousWaitKeysForInstance()
    {
        var index = new EphemeralRoutingIndex();
        var instanceId = InstanceIdValue(1);
        var firstCorrelation = CorrelationId.Create("first");
        var secondCorrelation = CorrelationId.Create("second");

        index.IndexSnapshot(Snapshot(instanceId, Wait("Approved", firstCorrelation)));
        index.IndexSnapshot(Snapshot(instanceId, Wait("Rejected", secondCorrelation)));

        index.FindCandidates(Event("Approved", firstCorrelation)).Should().BeEmpty();
        index.FindCandidates(Event("Rejected", secondCorrelation)).Should().ContainSingle()
            .Which.Should().Be(instanceId);
    }

    [Fact]
    public void IndexSnapshot_DeduplicatesRepeatedWaitKeysForInstance()
    {
        var index = new EphemeralRoutingIndex();
        var instanceId = InstanceIdValue(1);
        var correlation = CorrelationId.Create("same");

        index.IndexSnapshot(Snapshot(
            instanceId,
            Wait("Ready", correlation, 1),
            Wait("Ready", correlation, 2)));

        index.FindCandidates(Event("Ready", correlation)).Should().ContainSingle()
            .Which.Should().Be(instanceId);
    }

    private static WorkflowInstanceSnapshot Snapshot(InstanceId instanceId, params ActiveWaitSnapshot[] waits)
    {
        return new WorkflowInstanceSnapshot
        {
            InstanceId = instanceId,
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial,
            Status = waits.Length == 0 ? WorkflowStatus.Running : WorkflowStatus.Waiting,
            CreatedAt = Timestamp(0),
            UpdatedAt = Timestamp(0),
            ActiveWaits = waits
        };
    }

    private static ActiveWaitSnapshot Wait(string eventName, CorrelationId correlationId, int waitId = 1)
    {
        return new ActiveWaitSnapshot
        {
            WaitId = WaitIdValue(waitId),
            EventName = eventName,
            CorrelationId = correlationId,
            RegisteredAt = Timestamp(waitId),
            Status = "Active",
            Mode = "Resident"
        };
    }

    private static EventEnvelope Event(string eventName, CorrelationId correlationId)
    {
        return new EventEnvelope
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            EventName = eventName,
            CorrelationId = correlationId,
            OccurredAt = Timestamp(0)
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 22, 0, 0, TimeSpan.Zero).AddSeconds(seconds);
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return DefinitionId.Parse(GuidValue(value).ToString());
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
    }

    private static WaitId WaitIdValue(int value)
    {
        return WaitId.Parse(GuidValue(value).ToString());
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
