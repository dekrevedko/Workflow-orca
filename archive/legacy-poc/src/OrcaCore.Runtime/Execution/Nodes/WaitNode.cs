namespace OrcaCore.Runtime.Execution.Nodes;

internal sealed class WaitNode<TState> : IWorkflowNode
{
    public WaitNode(string eventName, Func<TState, string> correlationSelector)
    {
        EventName = eventName;
        CorrelationSelector = correlationSelector;
    }

    public string NodeId => $"Wait({EventName})";
    public string EventName { get; }
    public Func<TState, string> CorrelationSelector { get; }
}
