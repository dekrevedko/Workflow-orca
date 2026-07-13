using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Abstractions.Steps;

/// <summary>
/// Describes the closed set of control intents a business step may return.
/// </summary>
public abstract record StepResult
{
    /// <summary>
    /// Indicates that the current step completed and execution may advance.
    /// </summary>
    public sealed record Completed : StepResult;

    /// <summary>
    /// Indicates that the current step failed with an expected OrcaCore error.
    /// </summary>
    public sealed record Failed(OrcaCoreException Error) : StepResult;

    /// <summary>
    /// Indicates that execution should wait for a matching event.
    /// </summary>
    public sealed record WaitForEvent(string EventName, CorrelationId CorrelationId) : StepResult;

    /// <summary>
    /// Indicates that the step's committed progress should be rescheduled cooperatively.
    /// </summary>
    public sealed record Yield : StepResult;

    /// <summary>
    /// Replaces durable business state and restarts execution as a fresh generation while
    /// preserving the logical instance identity (durable engine only).
    /// </summary>
    public sealed record ContinueAsNew<TState>(TState State) : StepResult;

    /// <summary>
    /// Dispatches external work and waits durably for its reported completion (durable engine
    /// only). The next step observes the completion event; a timeout fails the instance. Pool
    /// requirements that cannot be granted queue the instance until a grant signal re-runs
    /// this step (DR-031 external-job / resource-pool triggers).
    /// </summary>
    public sealed record RunExternalJob(
        string ExternalJobId,
        byte[] Payload,
        IReadOnlyList<ResourcePoolRequirement>? Requirements = null,
        TimeSpan? Timeout = null) : StepResult;

    /// <summary>
    /// Acquires durable resource-pool tickets for a guarded holder before execution continues
    /// (durable engine only). When capacity is exhausted the instance queues durably; a
    /// <c>ResourcePoolGranted</c> signal re-runs this step to re-attempt the acquisition
    /// (DR-031 resource-pool grant trigger).
    /// </summary>
    public sealed record AcquireResources(
        string HolderKey,
        IReadOnlyList<ResourcePoolRequirement> Requirements,
        TimeSpan? LeaseDuration = null) : StepResult;
}
