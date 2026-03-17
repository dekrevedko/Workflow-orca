namespace OrcaCore.Runtime.Execution.Nodes;

internal sealed class WaitLongNode<TState> : IWorkflowNode
{
    public WaitLongNode(string eventName, Func<TState, string> correlationSelector)
    {
        EventName = eventName;
        CorrelationSelector = correlationSelector;
    }

    public string NodeId => $"WaitLong({EventName})";
    public string EventName { get; }
    public Func<TState, string> CorrelationSelector { get; }
}
