namespace OrcaCore.Abstractions.Ids;

/// <summary>Identity of an engine-owned wait record.</summary>
public readonly record struct WaitId(Guid Value)
{
    /// <summary>Creates a new, time-ordered wait identity.</summary>
    public static WaitId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
