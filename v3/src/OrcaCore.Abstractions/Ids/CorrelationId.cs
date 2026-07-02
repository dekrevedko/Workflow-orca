namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Request-reply identity (EV-002): the same value leaves with an outbound request and
/// returns on the response event that resolves a matching wait.
/// </summary>
public readonly record struct CorrelationId(string Value)
{
    public string Value { get; } = !string.IsNullOrWhiteSpace(Value)
        ? Value
        : throw new ArgumentException("CorrelationId must not be empty or whitespace.", nameof(Value));

    public override string ToString() => Value;
}
