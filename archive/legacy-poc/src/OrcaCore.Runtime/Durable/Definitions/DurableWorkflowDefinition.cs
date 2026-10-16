namespace OrcaCore.Runtime.Durable.Definitions;

public sealed class DurableWorkflowDefinition<TState>
{
    internal DurableWorkflowDefinition(
        string definitionId,
        string definitionVersion,
        IReadOnlyList<IWorkflowNode> nodes)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        InnerDefinition = new WorkflowDefinition<TState>(definitionId, nodes);
    }

    public string DefinitionId { get; }

    public string DefinitionVersion { get; }

    internal WorkflowDefinition<TState> InnerDefinition { get; }

    internal IReadOnlyList<IWorkflowNode> ResolveNodes(string nodePath) =>
        InnerDefinition.ResolveNodes(nodePath);

    internal IReadOnlyCollection<string> GetNodeListPaths() =>
        InnerDefinition.GetNodeListPaths();
}
