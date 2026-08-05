using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Definitions;

internal sealed record InitNode<TState> : WorkflowNode<TState>
{
    public InitNode(
        string nodeId,
        Type inputType,
        Func<object?, TState> createState)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(inputType);
        ArgumentNullException.ThrowIfNull(createState);

        InputType = inputType;
        CreateState = createState;
    }

    public Type InputType { get; }

    public Func<object?, TState> CreateState { get; }
}

internal sealed record BusinessStepNode<TState> : WorkflowNode<TState>
{
    public BusinessStepNode(
        string nodeId,
        Func<IStep<TState>> stepFactory,
        Type? stepType,
        WorkflowPolicySet? policies = null)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(stepFactory);

        StepFactory = stepFactory;
        StepType = stepType;
        Policies = policies ?? WorkflowPolicySet.Empty;
    }

    public Func<IStep<TState>> StepFactory { get; }

    public Type? StepType { get; }

    public WorkflowPolicySet Policies { get; }
}

internal sealed record EndNode<TState> : WorkflowNode<TState>
{
    public EndNode(
        string nodeId,
        string? outcomeName,
        Func<TState, string?>? outcomeSelector = null)
        : base(nodeId)
    {
        OutcomeName = outcomeName;
        OutcomeSelector = outcomeSelector;
    }

    public string? OutcomeName { get; }

    private Func<TState, string?>? OutcomeSelector { get; }

    public string? ResolveOutcome(TState state)
    {
        return OutcomeSelector is null ? OutcomeName : OutcomeSelector(state);
    }
}

internal sealed record BranchReturnNode<TState> : WorkflowNode<TState>
{
    public BranchReturnNode(
        string nodeId,
        Type resultType,
        Func<TState, object?> resultSelector)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(resultType);
        ArgumentNullException.ThrowIfNull(resultSelector);

        ResultType = resultType;
        ResultSelector = resultSelector;
    }

    public Type ResultType { get; }

    public Func<TState, object?> ResultSelector { get; }
}

internal sealed record ContinueAsNewNode<TState> : WorkflowNode<TState>
{
    public ContinueAsNewNode(string nodeId, Func<TState, TState> stateSelector)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(stateSelector);
        StateSelector = stateSelector;
    }

    public Func<TState, TState> StateSelector { get; }
}

internal sealed record CompiledScopeNode<TState> : WorkflowNode<TState>
{
    public CompiledScopeNode(string nodeId, string scopePlanId)
        : base(nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopePlanId);
        ScopePlanId = scopePlanId;
    }

    public string ScopePlanId { get; }
}

internal sealed record IfNode<TState> : WorkflowNode<TState>
{
    public IfNode(
        string nodeId,
        Func<TState, bool> condition,
        SequenceNode<TState> then,
        SequenceNode<TState> otherwise)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        ArgumentNullException.ThrowIfNull(otherwise);

        Condition = condition;
        Then = then;
        Else = otherwise;
    }

    public Func<TState, bool> Condition { get; }

    public SequenceNode<TState> Then { get; }

    public SequenceNode<TState> Else { get; }
}

internal sealed record WhileNode<TState> : WorkflowNode<TState>
{
    public WhileNode(string nodeId, Func<TState, bool> condition, SequenceNode<TState> body)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(body);

        Condition = condition;
        Body = body;
    }

    public Func<TState, bool> Condition { get; }

    public SequenceNode<TState> Body { get; }
}

internal sealed record WaitNode<TState> : WorkflowNode<TState>
{
    public WaitNode(
        string nodeId,
        string eventName,
        Func<TState, CorrelationId> correlationSelector,
        TimeSpan? timeout = null)
        : base(nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(correlationSelector);

        EventName = eventName;
        CorrelationSelector = correlationSelector;
        Timeout = timeout;
    }

    public string EventName { get; }

    public Func<TState, CorrelationId> CorrelationSelector { get; }

    public TimeSpan? Timeout { get; }
}

internal sealed record DelayNode<TState> : WorkflowNode<TState>
{
    public DelayNode(string nodeId, TimeSpan duration)
        : base(nodeId)
    {
        Duration = duration;
    }

    public TimeSpan Duration { get; }
}

internal sealed record SequenceNode<TState> : WorkflowNode<TState>
{
    public SequenceNode(string nodeId, IEnumerable<WorkflowNode<TState>> children)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(children);

        Children = new ReadOnlyList<WorkflowNode<TState>>(children);
    }

    public IReadOnlyList<WorkflowNode<TState>> Children { get; }
}
