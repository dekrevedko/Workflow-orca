using System.Collections.Frozen;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Internal;

namespace OrcaCore.Core.Compilation;

/// <summary>
/// Identifies the execution mode selected before workflow authoring.
/// </summary>
internal enum WorkflowExecutionMode
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
internal sealed record CompiledWorkflowPlan
{
    internal const string CodecFormat = FixedWorkflowValueCodec.Format;

    private readonly IReadOnlyDictionary<InstructionId, CompiledInstruction> instructionsById;
    private readonly IReadOnlyDictionary<InstructionId, InstructionId?> sequentialSuccessorsById;
    private readonly IReadOnlyDictionary<ScopePlanId, CompiledScopePlan> scopesById;

    public CompiledWorkflowPlan(
        WorkflowExecutionMode mode,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        string canonicalStructure,
        IReadOnlyList<CompiledInstruction>? instructions = null,
        IReadOnlyList<CompiledScopePlan>? scopes = null,
        IReadOnlySet<CompiledInstructionKind>? allowedInstructions = null,
        DefinitionCompilerOptions? compilerOptions = null,
        bool detachedAttemptState = false,
        TimeSpan? workflowTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);

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
        DetachedAttemptState = detachedAttemptState;
        WorkflowTimeout = workflowTimeout;
        Fingerprint = DefinitionFingerprint.ComputeCanonicalHash($"{CodecFormat}|{canonicalStructure}");
    }

    /// <summary>
    /// Gets the compiled-plan serialization format version.
    /// </summary>
    public int FormatVersion => 1;

    /// <summary>
    /// Gets the runtime-affecting compiler profile owned by the persisted compiler format.
    /// </summary>
    public string CompilerProfileId =>
        $"orcacore-compiler-v{FormatVersion};quantum={CompilerOptions.MaxInternalInstructionsPerQuantum}";

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
    /// Gets the deterministic fingerprint of the fixed codec and inspectable authored structure.
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
    /// Gets validated runtime/compiler options that do not contribute to structural identity.
    /// </summary>
    public DefinitionCompilerOptions CompilerOptions { get; }

    public bool DetachedAttemptState { get; }

    public TimeSpan? WorkflowTimeout { get; }

    public CompiledInstruction GetInstruction(InstructionId instructionId)
    {
        return instructionsById.TryGetValue(instructionId, out var instruction)
            ? instruction
            : throw new InvalidOperationException(
                $"Instruction '{instructionId}' is not present in compiled plan '{Fingerprint}'.");
    }

    public CompiledScopePlan GetScope(ScopePlanId scopePlanId)
    {
        return scopesById.TryGetValue(scopePlanId, out var scope)
            ? scope
            : throw new InvalidOperationException(
                $"Scope plan '{scopePlanId}' is not present in compiled plan '{Fingerprint}'.");
    }

    public InstructionId? GetSequentialSuccessor(InstructionId instructionId)
    {
        return sequentialSuccessorsById.TryGetValue(instructionId, out var successor)
            ? successor
            : throw new InvalidOperationException(
                $"Instruction '{instructionId}' is not present in compiled plan '{Fingerprint}'.");
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
