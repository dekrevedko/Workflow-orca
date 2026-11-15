using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Engine.Durable.Dag;
using OrcaCore.Engine.Durable.Definitions;

namespace OrcaCore.Durable.Hosting.Dag;

internal readonly record struct DurableDagNodeIdentityV1(Guid RunId, int AuthoredOrdinal);

internal sealed record DurableDagWorkflowBindingV1(
    DefinitionId DefinitionId, DefinitionVersion DefinitionVersion,
    DefinitionFingerprint DefinitionFingerprint, Type InputType, Type? OutputType);

internal sealed record DurableDagDependencyV1(
    DurableDagNodeIdentityV1 Identity, DurableDagWorkflowBindingV1 Workflow);

internal sealed record DurableDagNodeContractV1(
    DurableDagNodeIdentityV1 Identity, DurableDagWorkflowBindingV1 Workflow,
    IReadOnlyList<DurableDagDependencyV1> DirectDependencies);

internal sealed record DurableDagDecodedOutputV1(
    DurableDagNodeIdentityV1 Identity, Type DeclaredType, object? Value);

internal abstract record DurableDagMappingInputsV1
{
    private protected DurableDagMappingInputsV1() { }
    internal sealed record Available(
        object? RunInput, Type RunInputType,
        IReadOnlyList<DurableDagDecodedOutputV1> Outputs) : DurableDagMappingInputsV1;
    internal sealed record MappingInvalid : DurableDagMappingInputsV1;
}

// Each read uses one authoritative checkpoint. Protocol checks precede
// materialization so a malformed binding cannot be recorded as user failure.
internal sealed class DurableDagMappingReader(DurableDagRunStore store,
    DurableDagRunInputCatalog catalog, DurableDefinitionRegistry workflows)
{
    internal async ValueTask<DurableDagMappingInputsV1> ReadMappingInputsAsync(
        DurableDagNodeContractV1 node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        var run = await store.ReadAsync(node.Identity.RunId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("DAG run does not exist.");
        var inputs = Inspect(run, node);
        try
        {
            var runInput = DurableDagValues.DecodeVerified(inputs.RunBytes, inputs.RunType);
            var decoded = inputs.Outputs.Select(output => new DurableDagDecodedOutputV1(
                output.Identity, output.Type, DurableDagValues.DecodeVerified(output.Bytes, output.Type))).ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            return new DurableDagMappingInputsV1.Available(runInput, inputs.RunType, Array.AsReadOnly(decoded));
        }
        catch (Exception error) when (error is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new DurableDagMappingInputsV1.MappingInvalid();
        }
    }

    internal MappingBytes Inspect(DagRunSnapshotV1 run, DurableDagNodeContractV1 node)
    {
        var ordinal = node.Identity.AuthoredOrdinal;
        if (run.RunId != node.Identity.RunId || (uint)ordinal >= (uint)run.Nodes.Count)
            throw new InvalidDataException("DAG node ordinal is outside the persisted plan.");
        if (run.State != DagRunStateV1.Running || run.Nodes[ordinal].State != DagNodeStateV1.Ready)
            throw new InvalidOperationException("DAG node is no longer ready.");
        var target = run.Graph.Nodes[ordinal];
        ValidateWorkflow(target, node.Workflow);
        if (node.DirectDependencies.Count != target.DirectDependencies.Count)
            throw new InvalidDataException("DAG mapping dependency set differs from the persisted plan.");
        var runType = catalog.Resolve(run.Graph) ?? throw new DurableDagDefinitionUnavailableException();
        var runBytes = DurableDagValues.VerifiedBytes(run.RunInput, run.Graph.RunInputContractKey);
        var values = new List<OutputBytes>();
        var dependencies = new Dictionary<int, DurableDagDependencyV1>();
        foreach (var item in node.DirectDependencies)
            if (!dependencies.TryAdd(item.Identity.AuthoredOrdinal, item))
                throw new InvalidDataException("DAG mapping repeats a direct dependency.");
        foreach (var dependencyOrdinal in target.DirectDependencies)
        {
            if (!dependencies.TryGetValue(dependencyOrdinal, out var dependency) || dependency.Identity.RunId != run.RunId)
                throw new InvalidDataException("DAG mapping dependency coordinates differ from the persisted plan.");
            var declared = run.Graph.Nodes[dependencyOrdinal];
            ValidateWorkflow(declared, dependency.Workflow);
            var saved = run.Nodes[dependencyOrdinal];
            if (saved.State != DagNodeStateV1.Succeeded)
                throw new InvalidDataException("A ready DAG node depends on an unsuccessful node.");
            if (dependency.Workflow.OutputType is { } outputType)
            {
                if (saved.Output is null)
                    throw new InvalidDataException("A successful resultful DAG dependency has no output.");
                values.Add(new(dependency.Identity, outputType,
                    DurableDagValues.VerifiedBytes(saved.Output, DurableDagWire.ChildOutputKey(declared.Workflow))));
            }
            else if (saved.Output is not null)
                throw new InvalidDataException("A resultless DAG dependency has an output.");
        }

        return new(runType, runBytes, values.AsReadOnly());
    }

    internal sealed record OutputBytes(DurableDagNodeIdentityV1 Identity, Type Type, byte[] Bytes);
    internal sealed record MappingBytes(Type RunType, byte[] RunBytes, IReadOnlyList<OutputBytes> Outputs);

    private void ValidateWorkflow(DagRunNodeV1 persisted, DurableDagWorkflowBindingV1 supplied)
    {
        var binding = persisted.Workflow;
        if (binding.DefinitionId != supplied.DefinitionId || binding.DefinitionVersion != supplied.DefinitionVersion ||
            binding.Fingerprint != supplied.DefinitionFingerprint.Value)
            throw new InvalidDataException("DAG mapping workflow binding differs from the persisted plan.");
        var installed = workflows.ResolveDagBinding(binding.DefinitionId, binding.DefinitionVersion)
            ?? throw new DurableDagDefinitionUnavailableException();
        if (installed.Fingerprint != binding.Fingerprint || installed.InputType != supplied.InputType ||
            installed.OutputType != supplied.OutputType || persisted.InputContractKey != DurableDagWire.ChildInputKey(binding) ||
            persisted.OutputContractKey != (installed.OutputType is null ? null : DurableDagWire.ChildOutputKey(binding)))
            throw new InvalidDataException("DAG mapping workflow type differs from the installed binding.");
    }
}
