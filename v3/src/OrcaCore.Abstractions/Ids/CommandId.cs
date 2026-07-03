namespace OrcaCore.Abstractions.Ids;

/// <summary>Globally unique identity of a durable command (DU-011).</summary>
public readonly record struct CommandId(Guid Value)
{
    /// <summary>Creates a new, time-ordered command identity.</summary>
    public static CommandId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
