namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Carries first-class request-reply correlation identity across events.
/// </summary>
public readonly record struct CorrelationId
{
    /// <summary>
    /// Initializes a correlation identifier from non-empty text.
    /// </summary>
    public CorrelationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value;
    }

    /// <summary>
    /// Gets the correlation value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Returns the correlation value.
    /// </summary>
    public override string ToString()
    {
        return Value;
    }
}
