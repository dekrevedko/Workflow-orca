
namespace OrcaCore.Runtime.Execution.Nodes;

internal sealed class BusinessStepNode<TState> : IWorkflowNode
{
    public BusinessStepNode(IStep<TState> step)
    {
        Step = step;
    }

    public string NodeId => Step.StepId;
    public IStep<TState> Step { get; }
}
