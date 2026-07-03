namespace OrcaCore.Abstractions.Steps;

/// <summary>
/// Contract for a user-defined business step (CR-011, CR-013).
/// </summary>
/// <typeparam name="TState">Workflow-owned business state type.</typeparam>
public interface IStep<TState>
{
    /// <summary>
    /// Executes the step, mutating business state through the context and returning control
    /// intent only.
    /// </summary>
    /// <param name="context">Execution context with mutable state and optional resumed event.</param>
    /// <param name="cancellationToken">Cooperative cancellation signal from the runtime.</param>
    /// <returns>Control-intent result interpreted by the runtime.</returns>
    ValueTask<StepResult> ExecuteAsync(StepContext<TState> context, CancellationToken cancellationToken);
}
