using OrcaCore.Abstractions.Events;

namespace OrcaCore.Abstractions.Steps;

/// <summary>
/// Provides a business step with state, resume data, and deterministic time only.
/// </summary>
public sealed class StepContext<TState>
{
    /// <summary>
    /// Initializes a step context.
    /// </summary>
    public StepContext(TState state, EventEnvelope? resumedEvent, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        State = state;
        ResumedEvent = resumedEvent;
        TimeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the mutable business state, which is the only mutation channel available to steps.
    /// </summary>
    public TState State { get; }

    /// <summary>
    /// Gets the event that resumed the instance after a wait, only on the first resumed step.
    /// </summary>
    public EventEnvelope? ResumedEvent { get; }

    /// <summary>
    /// Gets the deterministic time provider for the step.
    /// </summary>
    public TimeProvider TimeProvider { get; }
}
