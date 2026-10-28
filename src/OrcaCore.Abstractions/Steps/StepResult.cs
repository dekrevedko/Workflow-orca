namespace OrcaCore;

/// <summary>
/// Describes the closed set of control intents a business step may return.
/// </summary>
public abstract record StepResult
{
    private protected StepResult()
    {
    }

    internal virtual bool TryReadWait(out WorkflowEventContract contract, out CorrelationId correlation)
    {
        contract = null!;
        correlation = null!;
        return false;
    }

    /// <summary>
    /// Indicates that the current step completed and execution may advance.
    /// </summary>
    public sealed record Completed : StepResult;

    /// <summary>
    /// Indicates that the current step failed with an expected OrcaCore error.
    /// </summary>
    public sealed record Failed : StepResult
    {
        public Failed(OrcaCoreException error)
        {
            ArgumentNullException.ThrowIfNull(error);
            Error = error;
        }

        public OrcaCoreException Error { get; }
    }

    /// <summary>
    /// Indicates that execution should wait for a matching event.
    /// </summary>
    public sealed record WaitForEvent : StepResult
    {
        public WaitForEvent(WorkflowEventContract eventContract, CorrelationId correlationId)
        {
            ArgumentNullException.ThrowIfNull(eventContract);
            ArgumentNullException.ThrowIfNull(correlationId);
            EventContract = eventContract;
            CorrelationId = correlationId;
        }

        public WorkflowEventContract EventContract { get; }

        public CorrelationId CorrelationId { get; }

        internal override bool TryReadWait(out WorkflowEventContract contract, out CorrelationId correlation)
        {
            contract = EventContract;
            correlation = CorrelationId;
            return true;
        }
    }

    /// <summary>
    /// Indicates that execution should wait for a matching typed event.
    /// </summary>
    public sealed record WaitForEvent<TPayload> : StepResult
    {
        public WaitForEvent(
            WorkflowEventContract<TPayload> eventContract,
            CorrelationId correlationId)
        {
            ArgumentNullException.ThrowIfNull(eventContract);
            ArgumentNullException.ThrowIfNull(correlationId);
            EventContract = eventContract;
            CorrelationId = correlationId;
        }

        public WorkflowEventContract<TPayload> EventContract { get; }

        public CorrelationId CorrelationId { get; }

        internal override bool TryReadWait(out WorkflowEventContract contract, out CorrelationId correlation)
        {
            contract = EventContract;
            correlation = CorrelationId;
            return true;
        }
    }

}
