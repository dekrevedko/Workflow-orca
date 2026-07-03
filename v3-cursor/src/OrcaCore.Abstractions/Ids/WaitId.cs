namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identity for an engine-owned wait record registered by the runtime.
/// </summary>
public readonly record struct WaitId(Guid Value)
{
    /// <summary>
    /// Creates a new time-ordered identifier using UUID version 7.
    /// </summary>
    public static WaitId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
