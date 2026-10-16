
namespace OrcaCore.Runtime.Durable.Builders;

public sealed class DurableBranchBuilder<TState>
{
    private readonly List<IWorkflowNode> _nodes = [];

    internal IReadOnlyList<IWorkflowNode> GetNodes() => _nodes.AsReadOnly();

    public DurableBranchBuilder<TState> Then<TStep>() where TStep : IStep<TState>, new()
    {
        _nodes.Add(new BusinessStepNode<TState>(new TStep()));
        return this;
    }

    public DurableBranchBuilder<TState> Step<TStep>() where TStep : IStep<TState>, new() => Then<TStep>();

    public DurableBranchBuilder<TState> Wait(string eventName, Func<TState, string> correlationSelector)
    {
        _nodes.Add(new WaitNode<TState>(eventName, correlationSelector));
        return this;
    }

    public DurableBranchBuilder<TState> WaitLong(string eventName, Func<TState, string> correlationSelector)
    {
        _nodes.Add(new WaitLongNode<TState>(eventName, correlationSelector));
        return this;
    }

    public DurableBranchBuilder<TState> While(
        Func<TState, bool> condition,
        Action<DurableBranchBuilder<TState>> body)
    {
        var bodyBuilder = new DurableBranchBuilder<TState>();
        body(bodyBuilder);
        _nodes.Add(new WhileNode<TState>(condition, bodyBuilder.GetNodes()));
        return this;
    }

    public DurableBranchBuilder<TState> If(
        Func<TState, bool> condition,
        Action<DurableBranchBuilder<TState>> then,
        Action<DurableBranchBuilder<TState>>? @else = null)
    {
        var thenBuilder = new DurableBranchBuilder<TState>();
        then(thenBuilder);
        var elseBuilder = new DurableBranchBuilder<TState>();
        @else?.Invoke(elseBuilder);
        _nodes.Add(new IfNode<TState>(condition, thenBuilder.GetNodes(), elseBuilder.GetNodes()));
        return this;
    }
}
