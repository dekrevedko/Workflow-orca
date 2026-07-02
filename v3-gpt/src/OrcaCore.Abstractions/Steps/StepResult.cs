using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;

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
}
