using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OrcaCore.Dag.Hosting")]

namespace OrcaCore.Dag;

internal sealed class DagRuntimeView<TRunInput>
{
    private readonly IReadOnlyDictionary<DagNodeRef, DagNodePlan<TRunInput>> plans;

    internal DagRuntimeView(IReadOnlyList<DagNodePlan<TRunInput>> nodePlans)
    {
        plans = new ReadOnlyDictionary<DagNodeRef, DagNodePlan<TRunInput>>(
            nodePlans.ToDictionary(node => node.Reference));
        Nodes = Array.AsReadOnly(nodePlans.Select(node => new DagRuntimeNodeDescriptor(node.Reference,
            node.ChildDefinitionId, node.ChildDefinitionVersion, node.ChildFingerprint,
            node.InputType, node.OutputType, node.Dependencies)).ToArray());
    }

    internal IReadOnlyList<DagRuntimeNodeDescriptor> Nodes { get; }

    internal DagMappedInputResult EvaluateMapping(
        DagNodeRef node,
        TRunInput immutableRunInput,
        IReadOnlyDictionary<DagNodeRef, object?> successfulDirectDependencyOutputs)
    {
        if (node is null || successfulDirectDependencyOutputs is null || !plans.TryGetValue(node, out var plan))
        {
            return DagMappedInputResult.Invalid();
        }

        // Snapshot host-owned map structure. Values have already been detached by the
        // durable bridge; the authoring package never materializes committed bytes.
        var outputs = new Dictionary<DagNodeRef, object?>();
        foreach (var dependency in plan.Dependencies)
        {
            var declared = plans[dependency].OutputType;
            if (declared is null)
            {
                if (successfulDirectDependencyOutputs.ContainsKey(dependency))
                {
                    return DagMappedInputResult.Invalid();
                }
                continue;
            }

            if (!successfulDirectDependencyOutputs.TryGetValue(dependency, out var value) ||
                !DagValueTypes.Accepts(declared, value))
            {
                return DagMappedInputResult.Invalid();
            }
            outputs.Add(dependency, value);
        }

        if (outputs.Count != successfulDirectDependencyOutputs.Count)
        {
            return DagMappedInputResult.Invalid();
        }

        var context = new DagNodeInputContext<TRunInput>(immutableRunInput, node.PlanToken,
            plan.Dependencies.ToHashSet(), new ReadOnlyDictionary<DagNodeRef, object?>(outputs));
        try
        {
            var input = plan.Mapper(context);
            return DagValueTypes.Accepts(plan.InputType, input)
                ? new DagMappedInputResult(input, plan.InputType)
                : DagMappedInputResult.Invalid();
        }
        catch (Exception exception) when (exception is not
            (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            return DagMappedInputResult.Invalid();
        }
    }
}

internal sealed class DagRuntimeNodeDescriptor
{
    internal DagRuntimeNodeDescriptor(DagNodeRef reference, DefinitionId childDefinitionId,
        DefinitionVersion childDefinitionVersion, DefinitionFingerprint childFingerprint,
        Type inputType, Type? outputType, IReadOnlyList<DagNodeRef> dependencies)
    {
        Reference = reference;
        AuthoredOrdinal = reference.AuthoredOrdinal;
        ChildDefinitionId = childDefinitionId;
        ChildDefinitionVersion = childDefinitionVersion;
        ChildFingerprint = childFingerprint;
        InputType = inputType;
        OutputType = outputType;
        Dependencies = Array.AsReadOnly(dependencies.ToArray());
    }

    internal DagNodeRef Reference { get; }
    internal int AuthoredOrdinal { get; }
    internal DefinitionId ChildDefinitionId { get; }
    internal DefinitionVersion ChildDefinitionVersion { get; }
    internal DefinitionFingerprint ChildFingerprint { get; }
    internal Type InputType { get; }
    internal Type? OutputType { get; }
    internal IReadOnlyList<DagNodeRef> Dependencies { get; }
}

internal sealed class DagMappedInputResult
{
    internal DagMappedInputResult(object? input, Type inputType)
    {
        IsValid = true;
        Input = input;
        InputType = inputType;
    }

    private DagMappedInputResult() => FailureCode = "DAG_INPUT_MAPPING_INVALID";

    internal bool IsValid { get; }
    internal object? Input { get; }
    internal Type? InputType { get; }
    internal string? FailureCode { get; }
    internal static DagMappedInputResult Invalid() => new();
}

internal static class DagValueTypes
{
    internal static bool AcceptsNull(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
    internal static bool Accepts(Type type, object? value) => value is null ? AcceptsNull(type) : type.IsInstanceOfType(value);
}
