namespace OrcaCore;

/// <summary>
/// Defines one user-authored business step in a workflow.
/// </summary>
public interface IStep<TState>
{
    /// <summary>
    /// Executes the step by mutating business state through the context and returning control intent.
    /// </summary>
    ValueTask<StepResult> ExecuteAsync(
        StepContext<TState> context,
        CancellationToken cancellationToken);
}
