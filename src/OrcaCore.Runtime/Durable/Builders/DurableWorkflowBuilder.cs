
namespace OrcaCore.Runtime.Durable.Builders;

public sealed class DurableWorkflowBuilder<TState>
{
    private readonly string _definitionId;
    private readonly string _definitionVersion;
    private readonly List<IWorkflowNode> _nodes = [];
    private bool _initCalled;
    private bool _endCalled;

    public DurableWorkflowBuilder(string definitionId, string definitionVersion)
    {
        _definitionId = definitionId;
        _definitionVersion = definitionVersion;
    }

    public DurableWorkflowBuilder<TState> Init()
    {
        if (_initCalled)
            throw new InvalidOperationException("Init() has already been called.");

        _initCalled = true;
        return this;
    }

    public DurableWorkflowBuilder<TState> Then<TStep>() where TStep : IStep<TState>, new()
    {
        EnsureCanAddNodes();
        _nodes.Add(new BusinessStepNode<TState>(new TStep()));
        return this;
    }

    public DurableWorkflowBuilder<TState> Step<TStep>() where TStep : IStep<TState>, new() => Then<TStep>();

    public DurableWorkflowBuilder<TState> Wait(string eventName, Func<TState, string> correlationSelector)
    {
        EnsureCanAddNodes();
        _nodes.Add(new WaitNode<TState>(eventName, correlationSelector));
        return this;
    }

    public DurableWorkflowBuilder<TState> WaitLong(string eventName, Func<TState, string> correlationSelector)
    {
        EnsureCanAddNodes();
        _nodes.Add(new WaitLongNode<TState>(eventName, correlationSelector));
        return this;
    }

    public DurableWorkflowBuilder<TState> If(
        Func<TState, bool> condition,
        Action<DurableBranchBuilder<TState>> then,
        Action<DurableBranchBuilder<TState>>? @else = null)
    {
        EnsureCanAddNodes();

        var thenBuilder = new DurableBranchBuilder<TState>();
        then(thenBuilder);
        var elseBuilder = new DurableBranchBuilder<TState>();
        @else?.Invoke(elseBuilder);
        _nodes.Add(new IfNode<TState>(condition, thenBuilder.GetNodes(), elseBuilder.GetNodes()));
        return this;
    }

    public DurableWorkflowBuilder<TState> While(
        Func<TState, bool> condition,
        Action<DurableBranchBuilder<TState>> body)
    {
        EnsureCanAddNodes();

        var bodyBuilder = new DurableBranchBuilder<TState>();
        body(bodyBuilder);
        _nodes.Add(new WhileNode<TState>(condition, bodyBuilder.GetNodes()));
        return this;
    }

    public DurableWorkflowBuilder<TState> Parallel(Action<DurableParallelBuilder<TState>> configure)
    {
        EnsureCanAddNodes();

        var parallelBuilder = new DurableParallelBuilder<TState>();
        configure(parallelBuilder);
        _nodes.Add(new ParallelNode<TState>(parallelBuilder.GetBranches()));
        return this;
    }

    public DurableWorkflowBuilder<TState> End()
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called before End().");
        if (_endCalled)
            throw new InvalidOperationException("End() has already been called.");

        _endCalled = true;
        return this;
    }

    public DurableWorkflowDefinition<TState> Build()
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called.");
        if (!_endCalled)
            throw new InvalidOperationException("End() must be called.");

        return new DurableWorkflowDefinition<TState>(_definitionId, _definitionVersion, _nodes.AsReadOnly());
    }

    private void EnsureCanAddNodes()
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called before adding steps.");
        if (_endCalled)
            throw new InvalidOperationException("Cannot add steps after End().");
    }
}
