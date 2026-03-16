using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

public sealed class WorkflowBuilder<TState>(string definitionId)
{
    private readonly List<IStep<TState>> _steps = [];
    private bool _initCalled;
    private bool _endCalled;

    public WorkflowBuilder<TState> Init()
    {
        if (_initCalled)
            throw new InvalidOperationException("Init() has already been called.");
        _initCalled = true;
        return this;
    }

    public WorkflowBuilder<TState> Step<TStep>() where TStep : IStep<TState>, new()
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called before adding steps.");
        if (_endCalled)
            throw new InvalidOperationException("Cannot add steps after End().");
        _steps.Add(new TStep());
        return this;
    }

    public WorkflowBuilder<TState> Parallel(Action<ParallelBuilder<TState>> configure)
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called before adding steps.");
        if (_endCalled)
            throw new InvalidOperationException("Cannot add steps after End().");

        var parallelBuilder = new ParallelBuilder<TState>();
        configure(parallelBuilder);
        _steps.Add(new ParallelStep<TState>(parallelBuilder.GetBranches()));
        return this;
    }

    public WorkflowBuilder<TState> While(
        Func<TState, bool> condition,
        Action<BranchBuilder<TState>> body)
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called before adding steps.");
        if (_endCalled)
            throw new InvalidOperationException("Cannot add steps after End().");

        var bodyBuilder = new BranchBuilder<TState>();
        body(bodyBuilder);
        _steps.Add(new WhileStep<TState>(condition, bodyBuilder.GetSteps()));
        return this;
    }

    public WorkflowBuilder<TState> If(
        Func<TState, bool> condition,
        Action<BranchBuilder<TState>> then,
        Action<BranchBuilder<TState>>? @else = null)
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called before adding steps.");
        if (_endCalled)
            throw new InvalidOperationException("Cannot add steps after End().");

        var thenBuilder = new BranchBuilder<TState>();
        then(thenBuilder);
        var elseBuilder = new BranchBuilder<TState>();
        @else?.Invoke(elseBuilder);
        _steps.Add(new IfStep<TState>(condition, thenBuilder.GetSteps(), elseBuilder.GetSteps()));
        return this;
    }

    public WorkflowBuilder<TState> Wait(string eventName, Func<TState, string> correlationSelector)
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called before adding steps.");
        if (_endCalled)
            throw new InvalidOperationException("Cannot add steps after End().");
        _steps.Add(new WaitStep<TState>(eventName, correlationSelector));
        return this;
    }

    public WorkflowBuilder<TState> End()
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called before End().");
        if (_endCalled)
            throw new InvalidOperationException("End() has already been called.");
        _endCalled = true;
        return this;
    }

    public WorkflowDefinition<TState> Build()
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called.");
        if (!_endCalled)
            throw new InvalidOperationException("End() must be called.");
        return new WorkflowDefinition<TState>(definitionId, _steps.AsReadOnly());
    }
}
