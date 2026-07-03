using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Abstractions.Events;

/// <summary>
/// Outcome of delivering an <see cref="EventEnvelope"/> to an instance-targeted wait
/// (EV-020). A non-matching event is a normal, clearly represented outcome — never an
/// exception (see T1-08 implementation notes).
/// </summary>
public abstract record RaiseEventResult
{
    /// <summary>The event matched an active wait and the instance resumed (EV-023).</summary>
    /// <param name="Snapshot">Instance snapshot taken immediately after resume.</param>
    public sealed record Resumed(WorkflowInstanceSnapshot Snapshot) : RaiseEventResult;

    /// <summary>
    /// No active wait matched this event's <c>EventName</c>/<c>CorrelationId</c> pair; the
    /// instance is left exactly as it was.
    /// </summary>
    public sealed record NoMatch : RaiseEventResult;
}
