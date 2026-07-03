using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Tests.Durable;

public class DurableContractTests
{
    [Fact]
    public void WorkflowEventCatalog_AllEventsCarryInstanceAndCausationMetadata()
    {
        var instanceId = InstanceId.New();
        var streamVersion = new StreamVersion(1);
        var causationId = new CausationId(CommandId.New());
        var occurredAt = DateTimeOffset.UtcNow;

        WorkflowEvent[] events =
        [
            new WorkflowEvent.WorkflowStarted(instanceId, streamVersion, causationId, occurredAt, new DefinitionId("def"), new DefinitionVersion(1), null),
            new WorkflowEvent.VersionBound(instanceId, streamVersion, causationId, occurredAt, new DefinitionId("def"), new DefinitionVersion(1)),
            new WorkflowEvent.StepEntered(instanceId, streamVersion, causationId, occurredAt, "Step1"),
            new WorkflowEvent.StepSucceeded(instanceId, streamVersion, causationId, occurredAt, "Step1"),
            new WorkflowEvent.StepFailed(instanceId, streamVersion, causationId, occurredAt, "Step1", "boom"),
            new WorkflowEvent.WaitRegistered(instanceId, streamVersion, causationId, occurredAt, new WaitId(Guid.NewGuid()), "Approved", new CorrelationId("corr-1")),
            new WorkflowEvent.WaitMatched(instanceId, streamVersion, causationId, occurredAt, new WaitId(Guid.NewGuid()), new EventId(Guid.NewGuid())),
            new WorkflowEvent.EventBuffered(instanceId, streamVersion, causationId, occurredAt, new EventId(Guid.NewGuid()), "Approved"),
            new WorkflowEvent.EventConsumed(instanceId, streamVersion, causationId, occurredAt, new EventId(Guid.NewGuid()), "Approved"),
            new WorkflowEvent.DuplicateEventDiscarded(instanceId, streamVersion, causationId, occurredAt, new EventId(Guid.NewGuid())),
            new WorkflowEvent.WorkflowCompleted(instanceId, streamVersion, causationId, occurredAt, null),
            new WorkflowEvent.WorkflowFailed(instanceId, streamVersion, causationId, occurredAt, "boom"),
            new WorkflowEvent.InstanceDeleted(instanceId, streamVersion, causationId, occurredAt),
        ];

        foreach (var @event in events)
        {
            @event.InstanceId.Should().Be(instanceId);
            @event.StreamVersion.Should().Be(streamVersion);
            @event.CausationId.Should().Be(causationId);
            @event.OccurredAt.Should().Be(occurredAt);
        }
    }

    [Fact]
    public void WorkflowCommandCatalog_AllCommandsCarryCommandIdAndInstanceIdentity()
    {
        var commandId = CommandId.New();
        var instanceId = InstanceId.New();
        var envelope = new EventEnvelope(new EventId(Guid.NewGuid()), "Approved", new CorrelationId("corr-1"), null, DateTimeOffset.UtcNow);

        WorkflowCommand[] commands =
        [
            new WorkflowCommand.StartWorkflow(commandId, instanceId, new DefinitionId("def"), new DefinitionVersion(1), null),
            new WorkflowCommand.DeliverEvent(commandId, instanceId, envelope),
        ];

        foreach (var command in commands)
        {
            command.CommandId.Should().Be(commandId);
            command.InstanceId.Should().Be(instanceId);
        }
    }

    [Fact]
    public void StreamVersion_NegativeValuesRejected()
    {
        var act = () => new StreamVersion(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();

        var zero = new StreamVersion(0);
        zero.Value.Should().Be(0);
    }

    [Fact]
    public void WorkflowStartedEvent_CarriesBoundDefinitionVersion()
    {
        var definitionVersion = new DefinitionVersion(3);

        var started = new WorkflowEvent.WorkflowStarted(
            InstanceId.New(),
            new StreamVersion(0),
            new CausationId(CommandId.New()),
            DateTimeOffset.UtcNow,
            new DefinitionId("def"),
            definitionVersion,
            null);

        started.DefinitionVersion.Should().Be(definitionVersion);
    }
}
