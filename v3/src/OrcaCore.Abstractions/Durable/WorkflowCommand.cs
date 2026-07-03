using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Durable;

/// <summary>
/// The closed vocabulary of intents to mutate one durable instance (DU-011). Commands are not
/// durable truth — events are; commands are the write-path request that decides them. Every
/// variant carries <see cref="CommandId"/> and <see cref="InstanceId"/>, declared once here so
/// the whole catalog structurally carries command identity and instance routing.
/// </summary>
public abstract record WorkflowCommand
{
    private WorkflowCommand(CommandId commandId, InstanceId instanceId)
    {
        CommandId = commandId;
        InstanceId = instanceId;
    }

    public CommandId CommandId { get; }

    public InstanceId InstanceId { get; }

    /// <summary>Starts a new instance bound to a definition version (DU-040) from untyped start input.</summary>
    public sealed record StartWorkflow(
        CommandId CommandId,
        InstanceId InstanceId,
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        object? Input) : WorkflowCommand(CommandId, InstanceId);

    /// <summary>Delivers an inbound event to a durable instance for matching against its waits.</summary>
    public sealed record DeliverEvent(
        CommandId CommandId,
        InstanceId InstanceId,
        EventEnvelope Envelope) : WorkflowCommand(CommandId, InstanceId);
}
