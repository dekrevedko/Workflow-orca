namespace OrcaCore.Dag.Hosting;

// The future durable bridge consumes this host-owned snapshot, not DAG drafts,
// delegates or non-allowlisted authoring internals. No codec or child work occurs here.
internal sealed class DagInputMappingAdapter<TRunInput>
{
    private readonly DagRuntimeView<TRunInput> view;

    internal DagInputMappingAdapter(WorkflowDagPlan<TRunInput> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        view = plan.GetRuntimeView();
        Nodes = Array.AsReadOnly(view.Nodes.Select(node => new DagMappingNode(
            node.Reference, node.AuthoredOrdinal, node.ChildDefinitionId, node.ChildDefinitionVersion,
            node.ChildFingerprint, node.InputType, node.OutputType,
            Array.AsReadOnly(node.Dependencies.ToArray()))).ToArray());
    }

    internal IReadOnlyList<DagMappingNode> Nodes { get; }

    internal DagMappingEvaluation Evaluate(DagNodeRef node, TRunInput immutableRunInput,
        IReadOnlyDictionary<DagNodeRef, object?> successfulDirectDependencyOutputs)
    {
        var result = view.EvaluateMapping(node, immutableRunInput, successfulDirectDependencyOutputs);
        return new DagMappingEvaluation(result.IsValid, result.Input, result.InputType, result.FailureCode);
    }
}

internal sealed record DagMappingNode(DagNodeRef Reference, int AuthoredOrdinal,
    DefinitionId ChildDefinitionId, DefinitionVersion ChildDefinitionVersion,
    DefinitionFingerprint ChildFingerprint, Type InputType, Type? OutputType,
    IReadOnlyList<DagNodeRef> Dependencies);

internal sealed record DagMappingEvaluation(bool IsValid, object? Input, Type? InputType, string? FailureCode);
