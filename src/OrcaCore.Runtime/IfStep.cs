using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

internal sealed class IfStep<TState>(
    Func<TState, bool> condition,
    IReadOnlyList<IStep<TState>> thenSteps,
    IReadOnlyList<IStep<TState>> elseSteps) : IStep<TState>
{
    public string StepId => "If";
    public Func<TState, bool> Condition { get; } = condition;
    public IReadOnlyList<IStep<TState>> ThenSteps { get; } = thenSteps;
    public IReadOnlyList<IStep<TState>> ElseSteps { get; } = elseSteps;

    public Task<StepResult> ExecuteAsync(StepContext<TState> context)
    {
        throw new NotSupportedException("IfStep is handled by the interpreter, not executed directly.");
    }
}
