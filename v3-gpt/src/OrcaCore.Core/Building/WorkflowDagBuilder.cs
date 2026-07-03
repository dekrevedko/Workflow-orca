using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Core.Building;

/// <summary>
/// Builds a DAG authoring front-end that compiles to durable child orchestration batches.
/// </summary>
public sealed class WorkflowDagBuilder
{
    private readonly List<WorkflowDagNode> nodes = [];
    private readonly List<WorkflowDagEdge> edges = [];

    /// <summary>
    /// Adds a durable child node to the DAG.
    /// </summary>
    public WorkflowDagBuilder Node(
        string nodeId,
        DefinitionId childDefinitionId,
        DefinitionVersion childDefinitionVersion,
        RunChildFailurePolicy failurePolicy = RunChildFailurePolicy.PropagateFailure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        nodes.Add(new WorkflowDagNode(nodeId, childDefinitionId, childDefinitionVersion, [], failurePolicy));
        return this;
    }

    /// <summary>
    /// Adds an edge stating that dependentNodeId requires prerequisiteNodeId.
    /// </summary>
    public WorkflowDagBuilder DependsOn(string dependentNodeId, string prerequisiteNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependentNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(prerequisiteNodeId);
        edges.Add(new WorkflowDagEdge(prerequisiteNodeId, dependentNodeId));
        return this;
    }

    /// <summary>
    /// Builds a validated DAG plan.
    /// </summary>
    public Validation<WorkflowDagPlan> BuildValidated()
    {
        var errors = new List<ValidationError>();
        foreach (var duplicate in nodes
                     .GroupBy(node => node.NodeId, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.DagDuplicateNode,
                $"DAG node '{duplicate}' is duplicated.",
                duplicate));
        }

        var nodeIds = nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (!nodeIds.Contains(edge.PrerequisiteNodeId))
            {
                errors.Add(new ValidationError(
                    BuilderValidationCodes.DagMissingNode,
                    $"DAG edge references missing prerequisite node '{edge.PrerequisiteNodeId}'.",
                    edge.PrerequisiteNodeId));
            }

            if (!nodeIds.Contains(edge.DependentNodeId))
            {
                errors.Add(new ValidationError(
                    BuilderValidationCodes.DagMissingNode,
                    $"DAG edge references missing dependent node '{edge.DependentNodeId}'.",
                    edge.DependentNodeId));
            }
        }

        if (errors.Count == 0 && FindCycle(nodeIds, edges) is { Count: > 0 } cycle)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.DagCycle,
                $"DAG cycle detected: {string.Join(" -> ", cycle)}.",
                string.Join("/", cycle)));
        }

        if (errors.Count > 0)
        {
            return Validation<WorkflowDagPlan>.Invalid(errors);
        }

        var nodesById = nodes
            .GroupBy(node => node.NodeId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(node => node with
            {
                Dependencies = edges
                    .Where(edge => string.Equals(edge.DependentNodeId, node.NodeId, StringComparison.Ordinal))
                    .Select(edge => edge.PrerequisiteNodeId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()
            })
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        return Validation<WorkflowDagPlan>.Valid(new WorkflowDagPlan(nodesById, edges.ToArray()));
    }

    private static IReadOnlyList<string> FindCycle(
        IReadOnlySet<string> nodeIds,
        IReadOnlyList<WorkflowDagEdge> dagEdges)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var outgoing = dagEdges
            .GroupBy(edge => edge.PrerequisiteNodeId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(edge => edge.DependentNodeId).ToArray(),
                StringComparer.Ordinal);

        foreach (var nodeId in nodeIds.Order(StringComparer.Ordinal))
        {
            if (Visit(nodeId, outgoing, visiting, visited, stack) is { Count: > 0 } cycle)
            {
                return cycle;
            }
        }

        return [];
    }

    private static IReadOnlyList<string> Visit(
        string nodeId,
        IReadOnlyDictionary<string, string[]> outgoing,
        HashSet<string> visiting,
        HashSet<string> visited,
        Stack<string> stack)
    {
        if (visited.Contains(nodeId))
        {
            return [];
        }

        if (!visiting.Add(nodeId))
        {
            return stack.Reverse().SkipWhile(candidate => !string.Equals(candidate, nodeId, StringComparison.Ordinal))
                .Append(nodeId)
                .ToArray();
        }

        stack.Push(nodeId);
        if (outgoing.TryGetValue(nodeId, out var nextNodes))
        {
            foreach (var next in nextNodes.Order(StringComparer.Ordinal))
            {
                if (Visit(next, outgoing, visiting, visited, stack) is { Count: > 0 } cycle)
                {
                    return cycle;
                }
            }
        }

        stack.Pop();
        visiting.Remove(nodeId);
        visited.Add(nodeId);
        return [];
    }
}

/// <summary>
/// Represents a compiled DAG plan.
/// </summary>
public sealed record WorkflowDagPlan(
    IReadOnlyList<WorkflowDagNode> Nodes,
    IReadOnlyList<WorkflowDagEdge> Edges)
{
    /// <summary>
    /// Gets one compiled node by id.
    /// </summary>
    public WorkflowDagNode Node(string nodeId)
    {
        return Nodes.Single(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets nodes whose dependencies have completed and whose failed prerequisites do not block them.
    /// </summary>
    public IReadOnlyList<WorkflowDagNode> GetRunnableNodes(
        IReadOnlyCollection<string> completedNodeIds,
        IReadOnlyCollection<string> failedNodeIds)
    {
        var completed = completedNodeIds.ToHashSet(StringComparer.Ordinal);
        var failed = failedNodeIds.ToHashSet(StringComparer.Ordinal);
        return Nodes
            .Where(node => !completed.Contains(node.NodeId))
            .Where(node => !failed.Contains(node.NodeId))
            .Where(node => node.Dependencies.All(completed.Contains))
            .Where(node => node.Dependencies.All(dependency => !failed.Contains(dependency)))
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Gets nodes blocked by failed prerequisites.
    /// </summary>
    public IReadOnlyList<WorkflowDagNode> GetBlockedByFailures(IReadOnlyCollection<string> failedNodeIds)
    {
        var failed = failedNodeIds.ToHashSet(StringComparer.Ordinal);
        return Nodes
            .Where(node => node.Dependencies.Any(failed.Contains))
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Creates a neutral child batch that durable orchestration can submit through RunChildren.
    /// </summary>
    public WorkflowDagChildBatch CreateChildBatch(IReadOnlyList<WorkflowDagNode> runnableNodes)
    {
        ArgumentNullException.ThrowIfNull(runnableNodes);
        if (runnableNodes.Count == 0)
        {
            throw new ArgumentException("At least one runnable DAG node is required.", nameof(runnableNodes));
        }

        var first = runnableNodes[0];
        return new WorkflowDagChildBatch(
            first.ChildDefinitionId,
            first.ChildDefinitionVersion,
            runnableNodes.Select(node => node.NodeId).ToArray(),
            first.FailurePolicy,
            runnableNodes.Count);
    }
}

/// <summary>
/// Represents one compiled DAG node.
/// </summary>
public sealed record WorkflowDagNode(
    string NodeId,
    DefinitionId ChildDefinitionId,
    DefinitionVersion ChildDefinitionVersion,
    IReadOnlyList<string> Dependencies,
    RunChildFailurePolicy FailurePolicy);

/// <summary>
/// Represents one DAG dependency edge.
/// </summary>
public sealed record WorkflowDagEdge(
    string PrerequisiteNodeId,
    string DependentNodeId);

/// <summary>
/// Represents one DAG batch to be submitted through durable child orchestration.
/// </summary>
public sealed record WorkflowDagChildBatch(
    DefinitionId ChildDefinitionId,
    DefinitionVersion ChildDefinitionVersion,
    IReadOnlyList<string> ItemSnapshots,
    RunChildFailurePolicy FailurePolicy,
    int MaxConcurrency);
