namespace OrcaCore;

/// <summary>
/// Describes the closed set of control intents a business step may return.
/// </summary>
public abstract record StepResult
{
    private protected StepResult()
    {
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
        public WaitForEvent(EventName eventName, CorrelationId correlationId)
        {
            ArgumentNullException.ThrowIfNull(eventName);
            ArgumentNullException.ThrowIfNull(correlationId);
            EventName = eventName;
            CorrelationId = correlationId;
        }

        public EventName EventName { get; }

        public CorrelationId CorrelationId { get; }
    }

}

// Transitional engine-only intents remain non-public and deliberately are not nested under the
// portable StepResult contract. Staged authoring replaces each one before the package split.
internal sealed record EngineYieldStepResult : StepResult;

internal sealed record EngineContinueAsNewStepResult<TState>(TState State) : StepResult;

internal sealed record EngineExternalJobStepResult(
    string ExternalJobId,
    byte[] Payload,
    IReadOnlyList<Abstractions.Providers.ResourcePoolRequirement>? Requirements = null,
    TimeSpan? Timeout = null) : StepResult;

internal sealed record EngineAcquireResourcesStepResult(
    string HolderKey,
    IReadOnlyList<Abstractions.Providers.ResourcePoolRequirement> Requirements,
    TimeSpan? LeaseDuration = null) : StepResult;
