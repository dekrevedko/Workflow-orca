namespace OrcaCore.Abstractions.Contracts;

public interface IStep<TState>
{
    string StepId { get; }
    Task<StepResult> ExecuteAsync(StepContext<TState> context);
}
