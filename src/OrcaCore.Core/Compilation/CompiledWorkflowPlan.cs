using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Compilation;

/// <summary>
/// Identifies the execution mode selected before workflow authoring.
/// </summary>
public enum WorkflowExecutionMode
{
    /// <summary>
    /// In-memory execution with no restart guarantee.
    /// </summary>
    Ephemeral = 0,

    /// <summary>
    /// Persisted execution with restart recovery.
    /// </summary>
    Durable = 1
}

/// <summary>
/// Immutable compiler output bound to a workflow definition.
/// </summary>
public sealed record CompiledWorkflowPlan
{
    private readonly IReadOnlyDictionary<InstructionId, CompiledInstruction> instructionsById;
    private readonly IReadOnlyDictionary<InstructionId, InstructionId?> sequentialSuccessorsById;
    private readonly IReadOnlyDictionary<ScopePlanId, CompiledScopePlan> scopesById;

    internal CompiledWorkflowPlan(
        WorkflowExecutionMode mode,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        string canonicalStructure,
        IReadOnlyList<CompiledInstruction>? instructions = null,
        IReadOnlyList<CompiledScopePlan>? scopes = null,
        IReadOnlySet<CompiledInstructionKind>? allowedInstructions = null,
        DefinitionCompilerOptions? compilerOptions = null,
        IWorkflowTypeSerializerRegistry? serializerRegistry = null)
    {
        Mode = mode;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        Instructions = Array.AsReadOnly((instructions ?? []).Select(CloneInstruction).ToArray());
        Scopes = Array.AsReadOnly((scopes ?? []).Select(CloneScope).ToArray());
        instructionsById = Instructions.ToFrozenDictionary(instruction => instruction.Id);
        sequentialSuccessorsById = Instructions
            .Select((instruction, index) => new KeyValuePair<InstructionId, InstructionId?>(
                instruction.Id,
                index + 1 < Instructions.Count ? Instructions[index + 1].Id : null))
            .ToFrozenDictionary();
        scopesById = Scopes.ToFrozenDictionary(scope => scope.Id);
        AllowedInstructions = (allowedInstructions ?? new HashSet<CompiledInstructionKind>()).ToFrozenSet();
        CompilerOptions = compilerOptions ?? new DefinitionCompilerOptions();
        SerializerRegistry = serializerRegistry ?? DefaultWorkflowTypeSerializerRegistry.Instance;
        Fingerprint = ComputeFingerprint(
            $"{FormatVersion}|{mode}|{definitionId}|{definitionVersion}|{canonicalStructure}");
    }

    /// <summary>
    /// Gets the compiled-plan serialization format version.
    /// </summary>
    public int FormatVersion => 1;

    /// <summary>
    /// Gets the selected execution mode.
    /// </summary>
    public WorkflowExecutionMode Mode { get; }

    /// <summary>
    /// Gets the definition identity compiled into the plan.
    /// </summary>
    public DefinitionId DefinitionId { get; }

    /// <summary>
    /// Gets the definition version compiled into the plan.
    /// </summary>
    public DefinitionVersion DefinitionVersion { get; }

    /// <summary>
    /// Gets the deterministic fingerprint of the compiler format, mode, identity, and graph.
    /// </summary>
    public string Fingerprint { get; }

    /// <summary>
    /// Gets the deterministic flattened instruction table.
    /// </summary>
    public IReadOnlyList<CompiledInstruction> Instructions { get; }

    /// <summary>
    /// Gets the deterministic structured-scope table.
    /// </summary>
    public IReadOnlyList<CompiledScopePlan> Scopes { get; }

    /// <summary>
    /// Gets the positive instruction allowlist for the selected mode.
    /// </summary>
    public IReadOnlySet<CompiledInstructionKind> AllowedInstructions { get; }

    /// <summary>
    /// Gets the validated execution limits bound into this plan's fingerprint.
    /// </summary>
    public DefinitionCompilerOptions CompilerOptions { get; }

    internal IWorkflowTypeSerializerRegistry SerializerRegistry { get; }

    internal CompiledInstruction GetInstruction(InstructionId instructionId)
    {
        return instructionsById.TryGetValue(instructionId, out var instruction)
            ? instruction
            : throw new InvalidOperationException(
                $"Instruction '{instructionId}' is not present in compiled plan '{Fingerprint}'.");
    }

    internal CompiledScopePlan GetScope(ScopePlanId scopePlanId)
    {
        return scopesById.TryGetValue(scopePlanId, out var scope)
            ? scope
            : throw new InvalidOperationException(
                $"Scope plan '{scopePlanId}' is not present in compiled plan '{Fingerprint}'.");
    }

    internal InstructionId? GetSequentialSuccessor(InstructionId instructionId)
    {
        return sequentialSuccessorsById.TryGetValue(instructionId, out var successor)
            ? successor
            : throw new InvalidOperationException(
                $"Instruction '{instructionId}' is not present in compiled plan '{Fingerprint}'.");
    }

    internal static CompiledWorkflowPlan FromLegacy<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        SequenceNode<TState> rootSequence,
        bool requiresDurableEngine)
    {
        var structure = string.Join(
            ',',
            Enumerate(rootSequence).Select(node => $"{node.NodeId}:{node.GetType().Name}"));
        var mode = requiresDurableEngine ? WorkflowExecutionMode.Durable : WorkflowExecutionMode.Ephemeral;
        return new CompiledWorkflowPlan(mode, definitionId, definitionVersion, $"legacy|{structure}");
    }

    private static IEnumerable<WorkflowNode<TState>> Enumerate<TState>(SequenceNode<TState> sequence)
    {
        foreach (var node in sequence.Children)
        {
            yield return node;
            foreach (var child in node switch
            {
                IfNode<TState> conditional => Enumerate(conditional.Then).Concat(Enumerate(conditional.Else)),
                WhileNode<TState> loop => Enumerate(loop.Body),
                _ => []
            })
            {
                yield return child;
            }
        }
    }

    private static string ComputeFingerprint(string canonicalStructure)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalStructure));
        return Convert.ToHexString(bytes);
    }

    private static CompiledInstruction CloneInstruction(CompiledInstruction instruction)
    {
        return instruction with
        {
            Policy = instruction.Policy with
            {
                DurableResourceKeys = Array.AsReadOnly(instruction.Policy.DurableResourceKeys.ToArray())
            }
        };
    }

    private static CompiledScopePlan CloneScope(CompiledScopePlan scope)
    {
        return scope with
        {
            Branches = Array.AsReadOnly(scope.Branches
                .Select(branch => branch with
                {
                    Instructions = Array.AsReadOnly(branch.Instructions.ToArray())
                })
                .ToArray())
        };
    }
}
