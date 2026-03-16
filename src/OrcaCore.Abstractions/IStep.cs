namespace OrcaCore.Abstractions;

public interface IStep<TState>
{
    string StepId { get; }
    Task<StepResult> ExecuteAsync(StepContext<TState> context);
}
