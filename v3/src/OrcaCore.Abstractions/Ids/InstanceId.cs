namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Globally unique, stable identity of a workflow instance. Identifies the logical
/// execution, never the current in-memory activation.
/// </summary>
public readonly record struct InstanceId(Guid Value)
{
    /// <summary>Creates a new, time-ordered instance identity.</summary>
    public static InstanceId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
