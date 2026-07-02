using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Events;

/// <summary>
/// Normalizes an inbound event for routing, matching, and deduplication.
/// </summary>
public sealed record EventEnvelope
{
    /// <summary>
    /// Gets the event identity used for deduplication.
    /// </summary>
    public required EventId EventId { get; init; }

    /// <summary>
    /// Gets the event name matched against active waits.
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Gets the request-reply correlation identity.
    /// </summary>
    public required CorrelationId CorrelationId { get; init; }

    /// <summary>
    /// Gets the business payload carried by the event.
    /// </summary>
    public object? Payload { get; init; }

    /// <summary>
    /// Gets when the event occurred.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
