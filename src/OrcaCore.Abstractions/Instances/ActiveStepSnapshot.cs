namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Provides immutable metadata for the step currently executing on an instance.
/// </summary>
public sealed record ActiveStepSnapshot
{
    /// <summary>
    /// Gets the workflow node path for the running step.
    /// </summary>
    public required string StepPath { get; init; }

    /// <summary>
    /// Gets when the step attempt started.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets the configured timeout for this step attempt, when any.
    /// </summary>
    public TimeSpan? ExpectedTimeout { get; init; }
}
