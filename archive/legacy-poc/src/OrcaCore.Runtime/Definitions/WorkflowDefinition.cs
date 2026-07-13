namespace OrcaCore.Runtime.Definitions;

public sealed class WorkflowDefinition<TState>
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<IWorkflowNode>> _nodeListsByPath;

    internal WorkflowDefinition(string definitionId, IReadOnlyList<IWorkflowNode> nodes)
    {
        DefinitionId = definitionId;
        Nodes = nodes;
        _nodeListsByPath = BuildNodeListIndex(nodes);
    }

    public string DefinitionId { get; }

    internal IReadOnlyList<IWorkflowNode> Nodes { get; }

    internal IReadOnlyList<IWorkflowNode> ResolveNodes(string nodePath)
    {
        if (_nodeListsByPath.TryGetValue(nodePath, out var nodes))
            return nodes;

        throw new InvalidOperationException(
            $"Workflow definition '{DefinitionId}' does not contain a node list at path '{nodePath}'.");
    }

    internal IReadOnlyCollection<string> GetNodeListPaths() => _nodeListsByPath.Keys.ToArray();

    private static IReadOnlyDictionary<string, IReadOnlyList<IWorkflowNode>> BuildNodeListIndex(
        IReadOnlyList<IWorkflowNode> rootNodes)
    {
        var index = new Dictionary<string, IReadOnlyList<IWorkflowNode>>(StringComparer.Ordinal)
        {
            [string.Empty] = rootNodes
        };

        RegisterChildLists(rootNodes, string.Empty, index);
        return index;
    }

    private static void RegisterChildLists(
        IReadOnlyList<IWorkflowNode> nodes,
        string listPath,
        IDictionary<string, IReadOnlyList<IWorkflowNode>> index)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var nodePath = CombineNodePath(listPath, i.ToString());
            switch (nodes[i])
            {
                case IfNode<TState> ifNode:
                    RegisterNodeList($"{nodePath}/then", ifNode.ThenNodes, index);
                    RegisterNodeList($"{nodePath}/else", ifNode.ElseNodes, index);
                    break;

                case WhileNode<TState> whileNode:
                    RegisterNodeList($"{nodePath}/body", whileNode.BodyNodes, index);
                    break;

                case ParallelNode<TState> parallelNode:
                    foreach (var branch in parallelNode.Branches)
                        RegisterNodeList($"{nodePath}/branch/{branch.BranchId}", branch.Nodes, index);
                    break;
            }
        }
    }

    private static void RegisterNodeList(
        string path,
        IReadOnlyList<IWorkflowNode> nodes,
        IDictionary<string, IReadOnlyList<IWorkflowNode>> index)
    {
        if (index.ContainsKey(path))
            throw new InvalidOperationException($"Duplicate node-list path '{path}' detected.");

        index[path] = nodes;
        RegisterChildLists(nodes, path, index);
    }

    private static string CombineNodePath(string parentPath, string segment) =>
        string.IsNullOrEmpty(parentPath) ? segment : $"{parentPath}/{segment}";
}
