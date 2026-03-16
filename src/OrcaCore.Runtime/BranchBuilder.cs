using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

public sealed class BranchBuilder<TState>
{
    private readonly List<IStep<TState>> _steps = [];

    internal IReadOnlyList<IStep<TState>> GetSteps() => _steps.AsReadOnly();

    public BranchBuilder<TState> Step<TStep>() where TStep : IStep<TState>, new()
    {
        _steps.Add(new TStep());
        return this;
    }

    public BranchBuilder<TState> Wait(string eventName, Func<TState, string> correlationSelector)
    {
        _steps.Add(new WaitStep<TState>(eventName, correlationSelector));
        return this;
    }

    public BranchBuilder<TState> While(
        Func<TState, bool> condition,
        Action<BranchBuilder<TState>> body)
    {
        var bodyBuilder = new BranchBuilder<TState>();
        body(bodyBuilder);
        _steps.Add(new WhileStep<TState>(condition, bodyBuilder.GetSteps()));
        return this;
    }

    public BranchBuilder<TState> If(
        Func<TState, bool> condition,
        Action<BranchBuilder<TState>> then,
        Action<BranchBuilder<TState>>? @else = null)
    {
        var thenBuilder = new BranchBuilder<TState>();
        then(thenBuilder);
        var elseBuilder = new BranchBuilder<TState>();
        @else?.Invoke(elseBuilder);
        _steps.Add(new IfStep<TState>(condition, thenBuilder.GetSteps(), elseBuilder.GetSteps()));
        return this;
    }
}
