using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

internal sealed class WhileStep<TState>(
    Func<TState, bool> condition,
    IReadOnlyList<IStep<TState>> bodySteps) : IStep<TState>
{
    public string StepId => "While";
    public Func<TState, bool> Condition { get; } = condition;
    public IReadOnlyList<IStep<TState>> BodySteps { get; } = bodySteps;

    public Task<StepResult> ExecuteAsync(StepContext<TState> context)
    {
        throw new NotSupportedException("WhileStep is handled by the interpreter, not executed directly.");
    }
}
