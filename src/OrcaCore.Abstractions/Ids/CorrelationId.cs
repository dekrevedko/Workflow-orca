using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore;

[JsonConverter(typeof(CorrelationIdJsonConverter))]
public sealed class CorrelationId : IEquatable<CorrelationId>
{
    private CorrelationId(string value) => Value = value;

    public string Value { get; }

    public static CorrelationId Create(string value) =>
        new(StrongValueValidation.CallerCreated(value, nameof(value)));

    public bool Equals(CorrelationId? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is CorrelationId other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
