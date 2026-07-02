namespace OrcaCore.Abstractions.Ids;

/// <summary>Globally unique dedup identity of an inbound event (EV-001).</summary>
public readonly record struct EventId(Guid Value)
{
    /// <summary>Creates a new, time-ordered event identity.</summary>
    public static EventId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
