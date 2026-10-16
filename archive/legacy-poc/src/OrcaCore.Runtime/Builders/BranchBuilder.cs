
namespace OrcaCore.Runtime.Builders;

public sealed class BranchBuilder<TState>
{
    private readonly List<IWorkflowNode> _nodes = [];

    internal IReadOnlyList<IWorkflowNode> GetNodes() => _nodes.AsReadOnly();

    public BranchBuilder<TState> Then<TStep>() where TStep : IStep<TState>, new()
    {
        _nodes.Add(new BusinessStepNode<TState>(new TStep()));
        return this;
    }

    public BranchBuilder<TState> Step<TStep>() where TStep : IStep<TState>, new() => Then<TStep>();

    public BranchBuilder<TState> Wait(string eventName, Func<TState, string> correlationSelector)
    {
        _nodes.Add(new WaitNode<TState>(eventName, correlationSelector));
        return this;
    }

    public BranchBuilder<TState> While(
        Func<TState, bool> condition,
        Action<BranchBuilder<TState>> body)
    {
        var bodyBuilder = new BranchBuilder<TState>();
        body(bodyBuilder);
        _nodes.Add(new WhileNode<TState>(condition, bodyBuilder.GetNodes()));
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
        _nodes.Add(new IfNode<TState>(condition, thenBuilder.GetNodes(), elseBuilder.GetNodes()));
        return this;
    }
}
