using System.Collections.Concurrent;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Durable.Hosting.Dag;

namespace OrcaCore.Dag.Hosting;

internal sealed class DagDefinitionRegistry(DurableDagRunBridge bridge)
{
    private readonly ConcurrentDictionary<(DefinitionId, DefinitionVersion), InstalledDagPlan> plans = new();

    internal InstalledDagPlan Install<TRunInput>(WorkflowDagPlan<TRunInput> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var adapter = new DagInputMappingAdapter<TRunInput>(plan);
        var binding = new DagRunBindingV1(plan.DefinitionId, plan.DefinitionVersion, plan.DefinitionFingerprint.Value);
        var ordinals = adapter.Nodes.ToDictionary(node => node.Reference, node => node.AuthoredOrdinal);
        var descriptors = adapter.Nodes.Select(node => new InstalledDagNode(node.AuthoredOrdinal, node.Reference,
            new(node.ChildDefinitionId, node.ChildDefinitionVersion, node.ChildFingerprint, node.InputType, node.OutputType),
            node.Dependencies.Select(dependency => ordinals[dependency]).Order().ToArray())).ToArray();
        var key = bridge.InstallRunInput(binding, typeof(TRunInput));
        var installed = new InstalledDagPlan(binding, key, typeof(TRunInput), descriptors, (node, values) =>
        {
            var outputs = values.Outputs.ToDictionary(output => descriptors[output.Identity.AuthoredOrdinal].Reference,
                output => output.Value);
            return adapter.Evaluate(node, (TRunInput)values.RunInput!, outputs);
        });
        return plans.GetOrAdd((plan.DefinitionId, plan.DefinitionVersion), installed);
    }

    internal InstalledDagPlan? Find(DagRunBindingV1 binding) =>
        plans.TryGetValue((binding.DefinitionId, binding.DefinitionVersion), out var plan) &&
        plan.Graph.Definition == binding ? plan : null;
}

internal sealed record InstalledDagNode(int Ordinal, DagNodeRef Reference, DurableDagWorkflowBindingV1 Workflow, int[] Dependencies)
{
    internal DagRunNodeV1 ToStoredNode()
    {
        var coordinate = FormattableString.Invariant($"workflow:{Workflow.DefinitionId}:{Workflow.DefinitionVersion.Value}");
        return new(Ordinal, Reference.NodeId.Value,
            new(Workflow.DefinitionId, Workflow.DefinitionVersion, Workflow.DefinitionFingerprint.Value),
            coordinate + ":input", Workflow.OutputType is null ? null : coordinate + ":output", Dependencies);
    }
}

internal sealed class InstalledDagPlan(DagRunBindingV1 binding, string inputKey, Type inputType,
    InstalledDagNode[] nodes, Func<DagNodeRef, DurableDagMappingInputsV1.Available, DagMappingEvaluation> evaluate)
{
    internal DagRunGraphV1 Graph { get; } = new(binding, inputKey, nodes.Select(node => node.ToStoredNode()).ToArray());
    internal Type InputType { get; } = inputType;
    internal InstalledDagNode[] Nodes => nodes;
    internal DurableDagNodeContractV1 NodeContract(Guid runId, int ordinal) =>
        new(new(runId, ordinal), nodes[ordinal].Workflow, Array.AsReadOnly(nodes[ordinal].Dependencies.Select(dependency =>
            new DurableDagDependencyV1(new(runId, dependency), nodes[dependency].Workflow)).ToArray()));
    internal DagMappingEvaluation Evaluate(int ordinal, DurableDagMappingInputsV1.Available values) =>
        evaluate(nodes[ordinal].Reference, values);
}
