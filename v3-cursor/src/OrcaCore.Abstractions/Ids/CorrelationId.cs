namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// First-class request-reply identity: the same value leaves with an outbound request and
/// returns on the matching response event (EV-002).
/// </summary>
public readonly record struct CorrelationId(string Value)
{
    /// <summary>
    /// Creates a new correlation identifier backed by a time-ordered UUID string.
    /// </summary>
    public static CorrelationId New() => new(Guid.CreateVersion7().ToString("D"));

    /// <inheritdoc />
    public override string ToString() => Value;
}
