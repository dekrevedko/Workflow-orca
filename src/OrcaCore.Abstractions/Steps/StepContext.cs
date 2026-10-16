namespace OrcaCore;

/// <summary>
/// Provides a business step with state, resume data, and deterministic time only.
/// </summary>
public sealed class StepContext<TState>
{
    private TState state;

    internal StepContext(
        TState state,
        StepExecutionContext execution,
        EventEnvelope? resumedEvent,
        TimeProvider timeProvider,
        ForEachItemContext? forEachItem = null,
        ResourceLeaseExecutionContext? resourceLease = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.state = state;
        Execution = execution;
        ResumedEvent = resumedEvent;
        TimeProvider = timeProvider;
        ForEachItem = forEachItem;
        ResourceLease = resourceLease;
    }

    /// <summary>
    /// Gets the mutable business state, which is the only mutation channel available to steps.
    /// </summary>
    public TState State => state;

    /// <summary>Replaces the attempt-local state value committed when the step completes.</summary>
    public void ReplaceState(TState replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        state = replacement;
    }

    /// <summary>Gets stable occurrence identity and the current diagnostic attempt.</summary>
    public StepExecutionContext Execution { get; }

    /// <summary>
    /// Gets the event that resumed the instance after a wait, only on the first resumed step.
    /// </summary>
    public EventEnvelope? ResumedEvent { get; }

    /// <summary>
    /// Gets the deterministic time provider for the step.
    /// </summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the current ForEach work item when this step runs inside a ForEach body; null otherwise.
    /// </summary>
    public ForEachItemContext? ForEachItem { get; }

    /// <summary>Gets the current durable lease protection context, when lexically leased.</summary>
    public ResourceLeaseExecutionContext? ResourceLease { get; }
}
