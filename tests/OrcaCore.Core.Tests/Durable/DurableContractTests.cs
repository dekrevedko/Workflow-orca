using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using Xunit;

namespace OrcaCore.Core.Tests.Durable;

public sealed class DurableContractTests
{
    [Fact]
    public void WorkflowEventCatalog_AllEventsCarryInstanceAndCausationMetadata()
    {
        var eventTypes = typeof(WorkflowEvent).Assembly.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(WorkflowEvent)) && !type.IsAbstract)
            .ToArray();

        eventTypes.Should().NotBeEmpty();
        eventTypes.Should().OnlyContain(type =>
            type.GetProperty(nameof(WorkflowEvent.EventId)) != null &&
            type.GetProperty(nameof(WorkflowEvent.InstanceId)) != null &&
            type.GetProperty(nameof(WorkflowEvent.CommandId)) != null &&
            type.GetProperty(nameof(WorkflowEvent.CausationId)) != null &&
            type.GetProperty(nameof(WorkflowEvent.OccurredAt)) != null);
    }

    [Fact]
    public void WorkflowCommandCatalog_AllCommandsCarryCommandIdAndInstanceIdentity()
    {
        var commandTypes = typeof(WorkflowCommand).Assembly.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(WorkflowCommand)) && !type.IsAbstract)
            .ToArray();

        commandTypes.Should().NotBeEmpty();
        commandTypes.Should().OnlyContain(type =>
            type.GetProperty(nameof(WorkflowCommand.CommandId)) != null &&
            type.GetProperty(nameof(WorkflowCommand.InstanceId)) != null &&
            type.GetProperty(nameof(WorkflowCommand.RequestedAt)) != null);
    }

    [Fact]
    public void StreamVersion_NegativeValuesRejected()
    {
        var act = () => new StreamVersion(-1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public void WorkflowStartedEvent_CarriesBoundDefinitionVersion()
    {
        var definitionId = DefinitionId.New();
        var definitionVersion = new DefinitionVersion(7);
        var started = new WorkflowStartedEvent
        {
            EventId = EventId.New(),
            InstanceId = InstanceId.New(),
            CommandId = CommandId.New(),
            CausationId = CausationId.New(),
            OccurredAt = DateTimeOffset.UtcNow,
            DefinitionId = definitionId,
            DefinitionVersion = definitionVersion
        };

        started.DefinitionId.Should().Be(definitionId);
        started.DefinitionVersion.Should().Be(definitionVersion);
    }
}
