using System.Globalization;
using System.Text;

namespace OrcaCore.Dag;

/// <summary>Starts one typed, durable DAG definition.</summary>
public static class Dag
{
    public static WorkflowDagBuilder<TRunInput> Define<TRunInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        return new WorkflowDagBuilder<TRunInput>(definitionId, definitionVersion);
    }
}

/// <summary>Authors a typed DAG without starting children or executing input mappings.</summary>
public sealed class WorkflowDagBuilder<TRunInput>
{
    private readonly object planToken = new();
    private readonly List<DagNodeDraft<TRunInput>> nodes = [];

    internal WorkflowDagBuilder(DefinitionId definitionId, DefinitionVersion definitionVersion)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
    }

    internal DefinitionId DefinitionId { get; }

    internal DefinitionVersion DefinitionVersion { get; }

    public DagNodeBuilder<TRunInput, TNodeInput> Node<TNodeInput>(
        DagNodeId nodeId,
        DurableWorkflowRef<TNodeInput> workflow)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        ArgumentNullException.ThrowIfNull(workflow);
        var reference = new DagNodeRef(nodeId, planToken, nodes.Count);
        var draft = AddNode(reference,
            workflow.DefinitionId, workflow.DefinitionVersion, workflow.DefinitionFingerprint,
            typeof(TNodeInput), null);
        return new DagNodeBuilder<TRunInput, TNodeInput>(draft);
    }

    public DagNodeBuilder<TRunInput, TNodeInput, TNodeOutput> Node<TNodeInput, TNodeOutput>(
        DagNodeId nodeId,
        DurableWorkflowRef<TNodeInput, TNodeOutput> workflow)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        ArgumentNullException.ThrowIfNull(workflow);
        var reference = new DagNodeRef<TNodeOutput>(nodeId, planToken, nodes.Count);
        var draft = AddNode(reference,
            workflow.DefinitionId, workflow.DefinitionVersion, workflow.DefinitionFingerprint,
            typeof(TNodeInput), typeof(TNodeOutput));
        return new DagNodeBuilder<TRunInput, TNodeInput, TNodeOutput>(draft);
    }

    public WorkflowDagPlan<TRunInput> Build()
    {
        var validation = TryBuild();
        if (validation.TryGetValue(out var plan))
        {
            return plan!;
        }

        throw new WorkflowDefinitionException(validation.Diagnostics);
    }

    public Validation<WorkflowDagPlan<TRunInput>> TryBuild()
    {
        var diagnostics = new List<WorkflowDiagnostic>();
        var firstById = new Dictionary<DagNodeId, DagNodeDraft<TRunInput>>();

        foreach (var node in nodes)
        {
            if (firstById.TryGetValue(node.Reference.NodeId, out var first))
            {
                diagnostics.Add(Diagnostic("DAG-AUTH-NODE-001", node.Reference,
                    "Node identities collide.", first.Reference));
            }
            else
            {
                firstById.Add(node.Reference.NodeId, node);
            }

            if (node.MapInputCalls == 0)
            {
                diagnostics.Add(Diagnostic("DAG-AUTH-MAP-001", node.Reference,
                    "A DAG node must select one input mapping."));
            }
            else if (node.MapInputCalls > 1)
            {
                diagnostics.Add(Diagnostic("DAG-AUTH-MAP-002", node.Reference,
                    "A DAG node selected more than one input mapping."));
            }

            var seen = new HashSet<DagNodeRef>();
            foreach (var dependency in node.Dependencies)
            {
                if (!seen.Add(dependency))
                {
                    diagnostics.Add(Diagnostic("DAG-AUTH-DEPENDENCY-001", node.Reference,
                        "One node repeats a declared dependency.", dependency));
                }

                if (ReferenceEquals(dependency, node.Reference))
                {
                    diagnostics.Add(Diagnostic("DAG-AUTH-DEPENDENCY-002", node.Reference,
                        "A node cannot depend on itself."));
                }

                if (!ReferenceEquals(dependency.PlanToken, planToken) ||
                    dependency.AuthoredOrdinal < 0 ||
                    dependency.AuthoredOrdinal >= nodes.Count ||
                    !ReferenceEquals(nodes[dependency.AuthoredOrdinal].Reference, dependency))
                {
                    diagnostics.Add(Diagnostic("DAG-AUTH-DEPENDENCY-004", node.Reference,
                        "A declared dependency belongs to another DAG plan."));
                }
            }
        }

        DetectCycles(diagnostics);
        if (diagnostics.Count != 0)
        {
            return new Validation<WorkflowDagPlan<TRunInput>>(null, diagnostics);
        }

        var snapshots = nodes.Select(node => new DagNodePlan<TRunInput>(
            node.Reference,
            node.Mapper!,
            node.ChildDefinitionId, node.ChildDefinitionVersion, node.ChildFingerprint,
            node.InputType, node.OutputType,
            Array.AsReadOnly(node.Dependencies.ToArray()))).ToArray();
        var canonical = CanonicalStructure();
        var fingerprint = new DefinitionFingerprint(
            DefinitionFingerprint.ComputeCanonicalHash(canonical));
        var plan = new WorkflowDagPlan<TRunInput>(
            DefinitionId, DefinitionVersion, fingerprint, snapshots);
        return new Validation<WorkflowDagPlan<TRunInput>>(plan, diagnostics);
    }

    private DagNodeDraft<TRunInput> AddNode(
        DagNodeRef reference,
        DefinitionId childId,
        DefinitionVersion childVersion,
        DefinitionFingerprint childFingerprint,
        Type inputType,
        Type? outputType)
    {
        var draft = new DagNodeDraft<TRunInput>(
            reference, childId, childVersion, childFingerprint, inputType, outputType);
        nodes.Add(draft);
        return draft;
    }

    private void DetectCycles(List<WorkflowDiagnostic> diagnostics)
    {
        var state = new byte[nodes.Count];
        var reported = new HashSet<int>();
        var stack = new Stack<CycleFrame>();
        for (var ordinal = 0; ordinal < nodes.Count; ordinal++)
        {
            if (state[ordinal] != 0)
            {
                continue;
            }

            state[ordinal] = 1;
            stack.Push(new CycleFrame(ordinal));
            while (stack.TryPeek(out var frame))
            {
                var dependencies = nodes[frame.Ordinal].Dependencies;
                if (frame.NextDependencyIndex == dependencies.Count)
                {
                    state[frame.Ordinal] = 2;
                    stack.Pop();
                    continue;
                }

                var dependency = dependencies[frame.NextDependencyIndex++];
                if (!frame.Seen.Add(dependency) ||
                    ReferenceEquals(dependency, nodes[frame.Ordinal].Reference) ||
                    !ReferenceEquals(dependency.PlanToken, planToken) ||
                    dependency.AuthoredOrdinal < 0 ||
                    dependency.AuthoredOrdinal >= nodes.Count ||
                    !ReferenceEquals(nodes[dependency.AuthoredOrdinal].Reference, dependency))
                {
                    continue;
                }

                var target = dependency.AuthoredOrdinal;
                if (state[target] == 1 && reported.Add(frame.Ordinal))
                {
                    diagnostics.Add(Diagnostic("DAG-AUTH-DEPENDENCY-003", nodes[frame.Ordinal].Reference,
                        "Declared DAG dependencies contain a cycle.", dependency));
                }
                else if (state[target] == 0)
                {
                    state[target] = 1;
                    stack.Push(new CycleFrame(target));
                }
            }
        }
    }

    private sealed class CycleFrame(int ordinal)
    {
        internal int Ordinal { get; } = ordinal;
        internal int NextDependencyIndex { get; set; }
        internal HashSet<DagNodeRef> Seen { get; } = [];
    }

    private static WorkflowDiagnostic Diagnostic(
        string code,
        DagNodeRef node,
        string message,
        DagNodeRef? related = null) =>
        new(code, WorkflowDiagnosticSeverity.Error,
            Location(node), related is null ? [] : [Location(related)], message);

    private static AuthoredLocation Location(DagNodeRef node) =>
        new($"dag:$/dag-node:{node.AuthoredOrdinal.ToString("D8", CultureInfo.InvariantCulture)}");

    private string CanonicalStructure()
    {
        var text = new StringBuilder("orcacore-dag-v1|orcacore-json-v1|");
        foreach (var node in nodes)
        {
            AppendPart(text, node.Reference.AuthoredOrdinal.ToString(CultureInfo.InvariantCulture));
            AppendPart(text, node.Reference.NodeId.Value);
            AppendPart(text, node.ChildDefinitionId.Value.ToString("D"));
            AppendPart(text, node.ChildDefinitionVersion.Value.ToString(CultureInfo.InvariantCulture));
            AppendPart(text, node.ChildFingerprint.Value);
            AppendPart(text, node.Dependencies.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var dependency in node.Dependencies)
            {
                AppendPart(text, dependency.AuthoredOrdinal.ToString(CultureInfo.InvariantCulture));
            }
        }

        return text.ToString();
    }

    private static void AppendPart(StringBuilder text, string value) =>
        text.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(value).Append('|');
}

/// <summary>Authors a resultless durable child node.</summary>
public sealed class DagNodeBuilder<TRunInput, TNodeInput>
{
    private readonly DagNodeDraft<TRunInput> node;

    internal DagNodeBuilder(DagNodeDraft<TRunInput> node) => this.node = node;

    public DagNodeBuilder<TRunInput, TNodeInput> DependsOn(params DagNodeRef[] dependencies)
    {
        node.AddDependencies(dependencies);
        return this;
    }

    public DagNodeRef MapInput(Func<DagNodeInputContext<TRunInput>, TNodeInput> input)
    {
        node.SetMapper(input);
        return node.Reference;
    }
}

/// <summary>Authors a durable child node with a typed successful output.</summary>
public sealed class DagNodeBuilder<TRunInput, TNodeInput, TNodeOutput>
{
    private readonly DagNodeDraft<TRunInput> node;

    internal DagNodeBuilder(DagNodeDraft<TRunInput> node) => this.node = node;

    public DagNodeBuilder<TRunInput, TNodeInput, TNodeOutput> DependsOn(
        params DagNodeRef[] dependencies)
    {
        node.AddDependencies(dependencies);
        return this;
    }

    public DagNodeRef<TNodeOutput> MapInput(
        Func<DagNodeInputContext<TRunInput>, TNodeInput> input)
    {
        node.SetMapper(input);
        return (DagNodeRef<TNodeOutput>)node.Reference;
    }
}

/// <summary>Identifies a node in one authored plan without granting runtime authority.</summary>
public class DagNodeRef
{
    internal DagNodeRef(DagNodeId nodeId, object planToken, int authoredOrdinal)
    {
        NodeId = nodeId;
        PlanToken = planToken;
        AuthoredOrdinal = authoredOrdinal;
    }

    public DagNodeId NodeId { get; }

    internal object PlanToken { get; }

    internal int AuthoredOrdinal { get; }
}

/// <summary>Identifies one resultful node and its output type.</summary>
public sealed class DagNodeRef<TOutput> : DagNodeRef
{
    internal DagNodeRef(DagNodeId nodeId, object planToken, int authoredOrdinal)
        : base(nodeId, planToken, authoredOrdinal) { }
}

/// <summary>Supplies immutable run input and successful direct-dependency outputs to a mapper.</summary>
public sealed class DagNodeInputContext<TRunInput>
{
    private readonly object planToken;
    private readonly IReadOnlySet<DagNodeRef> directDependencies;
    private readonly IReadOnlyDictionary<DagNodeRef, object?> successfulOutputs;

    internal DagNodeInputContext(
        TRunInput runInput,
        object planToken,
        IReadOnlySet<DagNodeRef> directDependencies,
        IReadOnlyDictionary<DagNodeRef, object?> successfulOutputs)
    {
        RunInput = runInput;
        this.planToken = planToken;
        this.directDependencies = directDependencies;
        this.successfulOutputs = successfulOutputs;
    }

    public TRunInput RunInput { get; }

    public TDependencyOutput OutputOf<TDependencyOutput>(DagNodeRef<TDependencyOutput> dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        if (!ReferenceEquals(dependency.PlanToken, planToken) ||
            !directDependencies.Contains(dependency) ||
            !successfulOutputs.TryGetValue(dependency, out var output))
        {
            throw new InvalidOperationException(
                "DAG_INPUT_MAPPING_INVALID: the output is not a successful direct dependency.");
        }

        if (output is TDependencyOutput typed)
        {
            return typed;
        }

        if (output is null && DagValueTypes.AcceptsNull(typeof(TDependencyOutput)))
        {
            return default!;
        }

        throw new InvalidOperationException(
            "DAG_INPUT_MAPPING_INVALID: the output does not match its declared type.");
    }
}

/// <summary>One validated immutable DAG authoring plan.</summary>
public sealed class WorkflowDagPlan<TRunInput>
{
    internal WorkflowDagPlan(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        IReadOnlyList<DagNodePlan<TRunInput>> nodePlans)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        DefinitionFingerprint = definitionFingerprint;
        NodePlans = Array.AsReadOnly(nodePlans.ToArray());
        Nodes = Array.AsReadOnly(NodePlans.Select(node => node.Reference).ToArray());
    }

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public DefinitionFingerprint DefinitionFingerprint { get; }

    public IReadOnlyList<DagNodeRef> Nodes { get; }

    internal IReadOnlyList<DagNodePlan<TRunInput>> NodePlans { get; }

    internal DagRuntimeView<TRunInput> GetRuntimeView() => new(NodePlans);
}

internal sealed class DagNodeDraft<TRunInput>
{
    internal DagNodeDraft(
        DagNodeRef reference,
        DefinitionId childDefinitionId,
        DefinitionVersion childDefinitionVersion,
        DefinitionFingerprint childFingerprint,
        Type inputType,
        Type? outputType)
    {
        Reference = reference;
        ChildDefinitionId = childDefinitionId;
        ChildDefinitionVersion = childDefinitionVersion;
        ChildFingerprint = childFingerprint;
        InputType = inputType;
        OutputType = outputType;
    }

    internal DagNodeRef Reference { get; }
    internal DefinitionId ChildDefinitionId { get; }
    internal DefinitionVersion ChildDefinitionVersion { get; }
    internal DefinitionFingerprint ChildFingerprint { get; }
    internal List<DagNodeRef> Dependencies { get; } = [];
    internal Type InputType { get; }
    internal Type? OutputType { get; }
    internal Func<DagNodeInputContext<TRunInput>, object?>? Mapper { get; private set; }
    internal int MapInputCalls { get; private set; }

    internal void AddDependencies(DagNodeRef[] dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        if (dependencies.Any(dependency => dependency is null))
        {
            throw new ArgumentException("DAG dependencies cannot contain null.", nameof(dependencies));
        }

        Dependencies.AddRange(dependencies.ToArray());
    }

    internal void SetMapper<TInput>(Func<DagNodeInputContext<TRunInput>, TInput> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        MapInputCalls++;
        Mapper ??= context => mapper(context);
    }
}

internal sealed record DagNodePlan<TRunInput>(
    DagNodeRef Reference,
    Func<DagNodeInputContext<TRunInput>, object?> Mapper,
    DefinitionId ChildDefinitionId,
    DefinitionVersion ChildDefinitionVersion,
    DefinitionFingerprint ChildFingerprint,
    Type InputType,
    Type? OutputType,
    IReadOnlyList<DagNodeRef> Dependencies);
