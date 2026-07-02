using OrcaCore.Abstractions.Ids;
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
    internal BusinessStepNode(string nodeId, Func<IStep<TState>> stepFactory)
        : base(nodeId)
    {
        ArgumentNullException.ThrowIfNull(stepFactory);

        StepFactory = stepFactory;
    }

    internal Func<IStep<TState>> StepFactory { get; }
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
    internal WaitNode(string nodeId, string eventName, Func<TState, CorrelationId> correlationSelector)
        : base(nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(correlationSelector);

        EventName = eventName;
        CorrelationSelector = correlationSelector;
    }

    internal string EventName { get; }

    internal Func<TState, CorrelationId> CorrelationSelector { get; }
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
