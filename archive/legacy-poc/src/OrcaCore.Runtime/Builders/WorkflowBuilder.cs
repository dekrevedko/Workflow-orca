using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Runtime.Builders;

public sealed class WorkflowBuilder<TState>(string definitionId)
{
    private readonly List<IWorkflowNode> _nodes = [];
    private bool _initCalled;
    private bool _endCalled;

    public WorkflowBuilder<TState> Init()
    {
        if (_initCalled)
            throw new InvalidOperationException("Init() has already been called.");
        _initCalled = true;
        return this;
    }

    public WorkflowBuilder<TState> Then<TStep>() where TStep : IStep<TState>, new()
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called before adding steps.");
        if (_endCalled)
            throw new InvalidOperationException("Cannot add steps after End().");
        _nodes.Add(new BusinessStepNode<TState>(new TStep()));
        return this;
    }

    public WorkflowBuilder<TState> Step<TStep>() where TStep : IStep<TState>, new() => Then<TStep>();

    public WorkflowBuilder<TState> Parallel(Action<ParallelBuilder<TState>> configure)
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called before adding steps.");
        if (_endCalled)
            throw new InvalidOperationException("Cannot add steps after End().");

        var parallelBuilder = new ParallelBuilder<TState>();
        configure(parallelBuilder);
        _nodes.Add(new ParallelNode<TState>(parallelBuilder.GetBranches()));
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
        _nodes.Add(new WhileNode<TState>(condition, bodyBuilder.GetNodes()));
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
        _nodes.Add(new IfNode<TState>(condition, thenBuilder.GetNodes(), elseBuilder.GetNodes()));
        return this;
    }

    public WorkflowBuilder<TState> Wait(string eventName, Func<TState, string> correlationSelector)
    {
        if (!_initCalled)
            throw new InvalidOperationException("Init() must be called before adding steps.");
        if (_endCalled)
            throw new InvalidOperationException("Cannot add steps after End().");
        _nodes.Add(new WaitNode<TState>(eventName, correlationSelector));
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

    public Validation<WorkflowDefinition<TState>> TryValidate()
    {
        var errors = new List<ValidationError>();
        if (!_initCalled)
            errors.Add(new ValidationError("BUILD_INIT_REQUIRED", "Init() must be called."));
        if (!_endCalled)
            errors.Add(new ValidationError("BUILD_END_REQUIRED", "End() must be called."));

        if (errors.Count > 0)
            return Validation<WorkflowDefinition<TState>>.Invalid(errors);

        return Validation<WorkflowDefinition<TState>>.Valid(new WorkflowDefinition<TState>(definitionId, _nodes.AsReadOnly()));
    }

    public WorkflowDefinition<TState> Build()
    {
        var validation = TryValidate();
        if (!validation.IsValid)
        {
            var message = string.Join(Environment.NewLine, validation.Errors.Select(static e => e.Message));
            throw new InvalidOperationException(message);
        }

        return validation.Value!;
    }
}
