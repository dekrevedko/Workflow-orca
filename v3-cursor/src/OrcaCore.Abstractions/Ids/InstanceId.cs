namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Globally unique, stable identity for one logical workflow execution (CR-022).
/// Identifies the execution itself, never the current in-memory activation.
/// </summary>
public readonly record struct InstanceId(Guid Value)
{
    /// <summary>
    /// Creates a new time-ordered identifier using UUID version 7.
    /// </summary>
    public static InstanceId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
