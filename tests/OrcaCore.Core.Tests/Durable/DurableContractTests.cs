using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using Xunit;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Core.Tests.Durable;

public sealed class DurableContractTests
{
    [Fact]
    public void WorkflowEventCatalog_AllEventsCarryInstanceAndCausationMetadata()
    {
        var eventTypes = typeof(DurableWorkflowEvent).Assembly.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(DurableWorkflowEvent)) && !type.IsAbstract)
            .ToArray();

        eventTypes.Should().NotBeEmpty();
        eventTypes.Should().OnlyContain(type =>
            type.GetProperty(nameof(DurableWorkflowEvent.EventId)) != null &&
            type.GetProperty(nameof(DurableWorkflowEvent.InstanceId)) != null &&
            type.GetProperty(nameof(DurableWorkflowEvent.CommandId)) != null &&
            type.GetProperty(nameof(DurableWorkflowEvent.CausationId)) != null &&
            type.GetProperty(nameof(DurableWorkflowEvent.OccurredAt)) != null);
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
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString()),
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
