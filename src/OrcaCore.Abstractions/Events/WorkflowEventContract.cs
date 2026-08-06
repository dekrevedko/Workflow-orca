namespace OrcaCore;

/// <summary>
/// Identifies one positive version of an application-owned workflow event contract.
/// </summary>
public sealed class EventContractVersion : IEquatable<EventContractVersion>
{
    /// <summary>Initializes an event-contract version from a positive integer.</summary>
    public EventContractVersion(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Event contract version must be positive.");
        }

        Value = value;
    }

    /// <summary>Gets the numeric event-contract version.</summary>
    public int Value { get; }

    /// <summary>Gets the first event-contract version.</summary>
    public static EventContractVersion Initial => new(1);

    /// <inheritdoc />
    public bool Equals(EventContractVersion? other) => other is not null && Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EventContractVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value;

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Provides the stable application-owned name and version of a workflow event.
/// </summary>
public class WorkflowEventContract : IEquatable<WorkflowEventContract>
{
    private protected WorkflowEventContract(EventName eventName, EventContractVersion version)
    {
        EventName = eventName ?? throw new ArgumentNullException(nameof(eventName));
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }

    /// <summary>Gets the stable application-owned event name.</summary>
    public EventName EventName { get; }

    /// <summary>Gets the stable application-owned event-contract version.</summary>
    public EventContractVersion Version { get; }

    internal virtual Type? BoundPayloadType => null;

    /// <summary>Creates a payloadless event descriptor.</summary>
    public static WorkflowEventContract Create(EventName eventName, EventContractVersion version) =>
        new(eventName, version);

    /// <inheritdoc />
    public bool Equals(WorkflowEventContract? other) =>
        other is not null && EventName.Equals(other.EventName) && Version.Equals(other.Version);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is WorkflowEventContract other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(EventName, Version);

}

/// <summary>
/// Binds a fixed-codec payload type to a stable workflow-event name and version.
/// </summary>
public sealed class WorkflowEventContract<TPayload> : WorkflowEventContract
{
    private WorkflowEventContract(EventName eventName, EventContractVersion version)
        : base(eventName, version)
    {
    }

    internal override Type BoundPayloadType => typeof(TPayload);

    /// <summary>Creates a typed event descriptor.</summary>
    public static new WorkflowEventContract<TPayload> Create(
        EventName eventName,
        EventContractVersion version) => new(eventName, version);

}
