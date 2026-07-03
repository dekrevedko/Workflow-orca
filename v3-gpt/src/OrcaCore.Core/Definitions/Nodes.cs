using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;

namespace OrcaCore.Core.Definitions;

internal sealed record InitNode<TState> : WorkflowNode<TState>
{
    internal InitNode(string nodeId, Func<object?, TState> createState)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(createState);

        CreateState = createState;
    }

    internal Func<object?, TState> CreateState { get; }
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
    internal EndNode(string nodeId, string? outcomeName)
        : base(nodeId)
    {
        OutcomeName = outcomeName;
    }

    internal string? OutcomeName { get; }
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

internal sealed record ParallelNode<TState> : WorkflowNode<TState>
{
    internal ParallelNode(string nodeId, IEnumerable<ParallelBranch<TState>> branches)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(branches);

        Branches = new ReadOnlyList<ParallelBranch<TState>>(branches);
    }

    internal IReadOnlyList<ParallelBranch<TState>> Branches { get; }
}

internal sealed record WhenFirstNode<TState> : WorkflowNode<TState>
{
    internal WhenFirstNode(
        string nodeId,
        WhenFirstResidualPolicy residualPolicy,
        IEnumerable<ParallelBranch<TState>> branches)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(branches);

        ResidualPolicy = residualPolicy;
        Branches = new ReadOnlyList<ParallelBranch<TState>>(branches);
    }

    internal WhenFirstResidualPolicy ResidualPolicy { get; }

    internal IReadOnlyList<ParallelBranch<TState>> Branches { get; }
}

internal abstract record ForEachNode<TState> : WorkflowNode<TState>
{
    protected ForEachNode(
        string nodeId,
        SequenceNode<TState> body,
        int? maxConcurrency,
        ForEachJoinPolicy joinPolicy,
        ForEachFailurePolicy failurePolicy,
        ForEachResidualPolicy residualPolicy)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(body);

        Body = body;
        MaxConcurrency = maxConcurrency;
        JoinPolicy = joinPolicy;
        FailurePolicy = failurePolicy;
        ResidualPolicy = residualPolicy;
    }

    internal SequenceNode<TState> Body { get; }

    internal int? MaxConcurrency { get; }

    internal ForEachJoinPolicy JoinPolicy { get; }

    internal ForEachFailurePolicy FailurePolicy { get; }

    internal ForEachResidualPolicy ResidualPolicy { get; }

    internal abstract IReadOnlyList<ForEachWorkItemSnapshot> MaterializeWorkItems(TState state);
}

internal sealed record ForEachNode<TState, TItem> : ForEachNode<TState>
{
    internal ForEachNode(
        string nodeId,
        Func<TState, IReadOnlyList<TItem>> itemSelector,
        WorkflowPartitioner<TItem> partitioner,
        SequenceNode<TState> body,
        int? maxConcurrency,
        ForEachJoinPolicy joinPolicy,
        ForEachFailurePolicy failurePolicy,
        ForEachResidualPolicy residualPolicy)
        : base(nodeId, body, maxConcurrency, joinPolicy, failurePolicy, residualPolicy)
    {
        ArgumentNullException.ThrowIfNull(itemSelector);
        ArgumentNullException.ThrowIfNull(partitioner);

        ItemSelector = itemSelector;
        Partitioner = partitioner;
    }

    private Func<TState, IReadOnlyList<TItem>> ItemSelector { get; }

    private WorkflowPartitioner<TItem> Partitioner { get; }

    internal override IReadOnlyList<ForEachWorkItemSnapshot> MaterializeWorkItems(TState state)
    {
        var items = ItemSelector(state);
        var partitions = Partitioner.Partition(items);
        return partitions
            .Select(partition => new ForEachWorkItemSnapshot
            {
                Index = partition.Index,
                Items = partition.Items.Cast<object?>().ToArray(),
                Status = ForEachWorkItemStatus.Pending
            })
            .ToArray();
    }
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

internal sealed record ParallelBranch<TState>
{
    internal ParallelBranch(BranchId branchId, SequenceNode<TState> sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        BranchId = branchId;
        Sequence = sequence;
    }

    internal BranchId BranchId { get; }

    internal SequenceNode<TState> Sequence { get; }
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
