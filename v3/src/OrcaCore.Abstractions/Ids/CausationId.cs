namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identity of whatever caused an event to be appended (DU-011 causation metadata). In this
/// architecture, events are always caused by the durable command that decided them, so this
/// wraps <see cref="CommandId"/> rather than introducing a separate identity space.
/// </summary>
public readonly record struct CausationId(CommandId CommandId)
{
    public override string ToString() => CommandId.ToString();
}
