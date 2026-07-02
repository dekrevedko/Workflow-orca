namespace OrcaCore.Abstractions.Steps;

/// <summary>
/// A passive unit of workflow structure (CR-010): it executes and returns a result; it
/// never drives its own sequencing.
/// </summary>
public interface IStep<TState>
{
    ValueTask<StepResult> ExecuteAsync(StepContext<TState> context, CancellationToken cancellationToken);
}
