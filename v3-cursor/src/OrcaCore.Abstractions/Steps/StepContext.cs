using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Steps;

/// <summary>
/// Execution context for a business step. Exposes no runtime surface (CR-012): steps express
/// orchestration intent only through <see cref="StepResult"/>.
/// </summary>
/// <typeparam name="TState">Workflow-owned business state mutated in place by the step.</typeparam>
public sealed class StepContext<TState>
{
    /// <summary>
    /// Initializes a new step context.
    /// </summary>
    /// <param name="state">Mutable business state; the only channel for business-state mutation.</param>
    /// <param name="timeProvider">Clock used for time-aware step logic.</param>
    /// <param name="resumedEvent">
    /// When non-null, the event that resumed this step after a wait. Set only on the first
    /// step invocation following wait resume (EV-022).
    /// </param>
    public StepContext(
        TState state,
        TimeProvider timeProvider,
        EventEnvelope? resumedEvent = null)
    {
        State = state;
        TimeProvider = timeProvider;
        ResumedEvent = resumedEvent;
    }

    /// <summary>
    /// Mutable business state. Steps mutate this object directly; results never carry state
    /// changes (CR-011).
    /// </summary>
    public TState State { get; }

    /// <summary>
    /// Event that resumed execution after a wait, or null when this is not a post-wait step.
    /// </summary>
    public EventEnvelope? ResumedEvent { get; init; }

    /// <summary>
    /// Injectable clock for deterministic time access in steps and tests.
    /// </summary>
    public TimeProvider TimeProvider { get; }
}
