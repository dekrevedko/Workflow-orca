using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Steps;

/// <summary>
/// Immutable control-intent outcome returned by a business step (CR-011). The runtime
/// interprets these variants; steps must not drive sequencing by other means.
/// </summary>
public abstract record StepResult
{
    /// <summary>The step finished successfully and the runtime may advance.</summary>
    public sealed record Completed : StepResult;

    /// <summary>
    /// The step failed. The runtime moves the instance to a failure terminal path (CR-014).
    /// </summary>
    /// <param name="Error">Operational error describing the failure.</param>
    public sealed record Failed(OrcaCoreException Error) : StepResult;

    /// <summary>
    /// The step requests suspension until an inbound event with the given name and correlation
    /// identity arrives.
    /// </summary>
    /// <param name="EventName">Logical event name to match.</param>
    /// <param name="CorrelationId">Request-reply identity paired with this wait.</param>
    public sealed record WaitForEvent(string EventName, CorrelationId CorrelationId) : StepResult;

    /// <summary>
    /// Cooperative checkpoint: commit business-state progress, release the execution lane, and
    /// reschedule continuation of the same step while status remains running (CR-017).
    /// </summary>
    public sealed record Yield : StepResult;
}
