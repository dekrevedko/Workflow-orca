using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;

namespace OrcaCore.Core.Definitions;

internal sealed record InitNode<TState> : WorkflowNode<TState>
{
    internal InitNode(
        string nodeId,
        Func<object?, TState> createState,
        Func<SerializedPayload, IWorkflowPayloadSerializer, object?>? rehydrateInput = null)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(createState);

        CreateState = createState;
        RehydrateInput = rehydrateInput;
    }

    internal Func<object?, TState> CreateState { get; }

    /// <summary>
    /// Rebuilds the typed start input from its serialized form so a durable host can run
    /// Init after the starting process is gone. Captured at build time to stay AOT-safe.
    /// </summary>
    internal Func<SerializedPayload, IWorkflowPayloadSerializer, object?>? RehydrateInput { get; }
}

internal sealed record BusinessStepNode<TState> : WorkflowNode<TState>
{
    internal BusinessStepNode(
        string nodeId,
        Func<IStep<TState>> stepFactory,
        WorkflowPolicySet? policies = null)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(stepFactory);

        StepFactory = stepFactory;
        Policies = policies ?? WorkflowPolicySet.Empty;
    }

    internal Func<IStep<TState>> StepFactory { get; }

    internal WorkflowPolicySet Policies { get; }
}

internal sealed record EndNode<TState> : WorkflowNode<TState>
{
    internal EndNode(
        string nodeId,
        string? outcomeName,
        Func<TState, string?>? outcomeSelector = null)
        : base(nodeId)
    {
        OutcomeName = outcomeName;
        OutcomeSelector = outcomeSelector;
    }

    internal string? OutcomeName { get; }

    private Func<TState, string?>? OutcomeSelector { get; }

    internal string? ResolveOutcome(TState state)
    {
        return OutcomeSelector is null ? OutcomeName : OutcomeSelector(state);
    }
}

internal sealed record BranchReturnNode<TState> : WorkflowNode<TState>
{
    internal BranchReturnNode(
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

    internal Type ResultType { get; }

    internal Func<TState, object?> ResultSelector { get; }
}

internal sealed record ContinueAsNewNode<TState> : WorkflowNode<TState>
{
    internal ContinueAsNewNode(string nodeId, Func<TState, TState> stateSelector)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(stateSelector);
        StateSelector = stateSelector;
    }

    internal Func<TState, TState> StateSelector { get; }
}

internal sealed record CompiledScopeNode<TState> : WorkflowNode<TState>
{
    internal CompiledScopeNode(string nodeId, string scopePlanId)
        : base(nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopePlanId);
        ScopePlanId = scopePlanId;
    }

    internal string ScopePlanId { get; }
}

internal sealed record IfNode<TState> : WorkflowNode<TState>
{
    internal IfNode(
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

    internal Func<TState, bool> Condition { get; }

    internal SequenceNode<TState> Then { get; }

    internal SequenceNode<TState> Else { get; }
}

internal sealed record WhileNode<TState> : WorkflowNode<TState>
{
    internal WhileNode(string nodeId, Func<TState, bool> condition, SequenceNode<TState> body)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(body);

        Condition = condition;
        Body = body;
    }

    internal Func<TState, bool> Condition { get; }

    internal SequenceNode<TState> Body { get; }
}

internal sealed record RunChildNode<TState> : WorkflowNode<TState>
{
    internal RunChildNode(
        string nodeId,
        DefinitionId childDefinitionId,
        DefinitionVersion childDefinitionVersion,
        RunChildFailurePolicy failurePolicy)
        : base(nodeId)
    {
        ChildDefinitionId = childDefinitionId;
        ChildDefinitionVersion = childDefinitionVersion;
        FailurePolicy = failurePolicy;
    }

    internal DefinitionId ChildDefinitionId { get; }

    internal DefinitionVersion ChildDefinitionVersion { get; }

    internal RunChildFailurePolicy FailurePolicy { get; }
}

internal sealed record RunChildrenNode<TState> : WorkflowNode<TState>
{
    internal RunChildrenNode(
        string nodeId,
        DefinitionId childDefinitionId,
        DefinitionVersion childDefinitionVersion,
        Func<TState, IReadOnlyList<string>> itemSnapshotSelector,
        RunChildFailurePolicy failurePolicy,
        int? maxConcurrency,
        RunChildrenJoinPolicy joinPolicy,
        RunChildrenResidualPolicy residualPolicy)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(itemSnapshotSelector);

        ChildDefinitionId = childDefinitionId;
        ChildDefinitionVersion = childDefinitionVersion;
        ItemSnapshotSelector = itemSnapshotSelector;
        FailurePolicy = failurePolicy;
        MaxConcurrency = maxConcurrency;
        JoinPolicy = joinPolicy;
        ResidualPolicy = residualPolicy;
    }

    internal DefinitionId ChildDefinitionId { get; }

    internal DefinitionVersion ChildDefinitionVersion { get; }

    internal Func<TState, IReadOnlyList<string>> ItemSnapshotSelector { get; }

    internal RunChildFailurePolicy FailurePolicy { get; }

    internal int? MaxConcurrency { get; }

    internal RunChildrenJoinPolicy JoinPolicy { get; }

    internal RunChildrenResidualPolicy ResidualPolicy { get; }
}

internal sealed record WaitNode<TState> : WorkflowNode<TState>
{
    internal WaitNode(
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

    internal string EventName { get; }

    internal Func<TState, CorrelationId> CorrelationSelector { get; }

    internal TimeSpan? Timeout { get; }
}

internal sealed record DelayNode<TState> : WorkflowNode<TState>
{
    internal DelayNode(string nodeId, TimeSpan duration)
        : base(nodeId)
    {
        Duration = duration;
    }

    internal TimeSpan Duration { get; }
}

internal sealed record SequenceNode<TState> : WorkflowNode<TState>
{
    internal SequenceNode(string nodeId, IEnumerable<WorkflowNode<TState>> children)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(children);

        Children = new ReadOnlyList<WorkflowNode<TState>>(children);
    }

    internal IReadOnlyList<WorkflowNode<TState>> Children { get; }
}
