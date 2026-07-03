namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// The result of an instance-targeted event delivery attempt (EV-020, EV-023). Returned
/// instead of thrown for the routine "no match" case (EV matching rule) — only genuinely
/// exceptional conditions throw.
/// </summary>
public enum RaiseEventOutcome
{
    /// <summary>The event matched the instance's active wait; the instance resumed exactly once.</summary>
    Resumed,

    /// <summary>The instance has no active wait matching this event's name and correlation id.</summary>
    NoMatch,

    /// <summary>No instance is registered for the given <c>InstanceId</c>.</summary>
    InstanceNotFound,
}
