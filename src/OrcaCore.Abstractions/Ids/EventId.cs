using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore;

[JsonConverter(typeof(EventIdJsonConverter))]
public sealed class EventId : IEquatable<EventId>
{
    private EventId(string value) => Value = value;

    public string Value { get; }

    public static EventId Create(string value) =>
        new(StrongValueValidation.CallerCreated(value, nameof(value)));

    public bool Equals(EventId? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is EventId other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
