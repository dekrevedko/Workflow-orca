using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Compilation;

/// <summary>
/// Stable identity of one compiled instruction.
/// </summary>
internal readonly record struct InstructionId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Stable identity of one authored structured-scope plan.
/// </summary>
internal readonly record struct ScopePlanId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Stable identity of one authored branch plan.
/// </summary>
internal readonly record struct BranchPlanId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Instruction kinds emitted by the structured definition compiler.
/// </summary>
internal enum CompiledInstructionKind
{
    Init,
    Step,
    End,
    If,
    IfJoin,
    LoopCheck,
    LoopBack,
    LoopExit,
    StartScope,
    BranchReturn,
    ScopeJoin,
    ScopeExit,
    Wait,
    Delay,
    ContinueAsNew,
    AcquireResources,
    ReleaseResources
}

/// <summary>
/// Structured scope kinds emitted by the compiler.
/// </summary>
internal enum CompiledScopeKind
{
    WhenAll,
    WhenAllOutcomes,
    WhenFirst,
    ForEach
}

/// <summary>
/// Merge contract selected for one structured scope.
/// </summary>
internal enum CompiledMergeKind
{
    WhenAll,
    WhenAllOutcomes,
    WhenFirst,
    ForEach
}

/// <summary>
/// Fully resolved retry settings attached to an instruction.
/// </summary>
internal sealed record CompiledRetryPolicy(int MaxAttempts, TimeSpan Backoff);

/// <summary>
/// Fully resolved execution policies attached to one compiled instruction.
/// </summary>
internal sealed record CompiledPolicyPlan
{
    public static CompiledPolicyPlan Empty { get; } = new();

    public CompiledRetryPolicy? Retry { get; init; }

    public TimeSpan? Timeout { get; init; }

    public bool CancellationEnabled { get; init; }

    public string? TransientPoolKey { get; init; }

    public IReadOnlyList<string> DurableResourceKeys { get; init; } = [];
}

/// <summary>
/// One immutable instruction and its authored location.
/// </summary>
internal sealed record CompiledInstruction(
    InstructionId Id,
    CompiledInstructionKind Kind,
    string Path,
    CompiledPolicyPlan Policy)
{
    public InstructionId? NextInstructionId { get; init; }

    public InstructionId? AlternateInstructionId { get; init; }

    public string? EventName { get; init; }

    public WaitMode? WaitMode { get; init; }

    public TimeSpan? WaitTimeout { get; init; }

    public TimeSpan? DelayDuration { get; init; }

    public global::OrcaCore.ResourceLeaseRequest? StaticLeaseRequest { get; init; }

    public Delegate? LeaseRequestSelector { get; init; }

    public int? MaxConcurrency { get; init; }

    public Delegate? Operation { get; init; }

    public Type? StepType { get; init; }

    public Type? OutputType { get; init; }

    public string? OutputSchemaIdentity { get; init; }

    public Delegate? OutputSelector { get; init; }

    public string? FixedOutcomeName { get; init; }
}

/// <summary>
/// Typed branch input projection and serialization contract.
/// </summary>
internal sealed record CompiledBranchInputPlan
{
    public CompiledBranchInputPlan(
        Type parentStateType,
        Type branchStateType,
        string parentStateSchemaIdentity,
        string branchStateSchemaIdentity,
        Delegate projector)
    {
        ParentStateType = parentStateType;
        BranchStateType = branchStateType;
        ParentStateSchemaIdentity = parentStateSchemaIdentity;
        BranchStateSchemaIdentity = branchStateSchemaIdentity;
        Projector = projector;
    }

    public Type ParentStateType { get; }

    public Type BranchStateType { get; }

    public string ParentStateSchemaIdentity { get; }

    public string BranchStateSchemaIdentity { get; }

    public Delegate Projector { get; }
}

/// <summary>
/// Typed branch-return and serialization contract.
/// </summary>
internal sealed record CompiledBranchResultPlan
{
    public CompiledBranchResultPlan(
        Type branchStateType,
        Type resultType,
        string branchStateSchemaIdentity,
        string resultSchemaIdentity,
        Delegate projector)
    {
        BranchStateType = branchStateType;
        ResultType = resultType;
        BranchStateSchemaIdentity = branchStateSchemaIdentity;
        ResultSchemaIdentity = resultSchemaIdentity;
        Projector = projector;
    }

    public Type BranchStateType { get; }

    public Type ResultType { get; }

    public string BranchStateSchemaIdentity { get; }

    public string ResultSchemaIdentity { get; }

    public Delegate Projector { get; }
}

/// <summary>
/// Typed replacement-state merge and serialization contract.
/// </summary>
internal sealed record CompiledMergePlan
{
    public CompiledMergePlan(
        CompiledMergeKind kind,
        Type parentStateType,
        Type resultType,
        string parentStateSchemaIdentity,
        string resultSchemaIdentity,
        Delegate? merge)
    {
        Kind = kind;
        ParentStateType = parentStateType;
        ResultType = resultType;
        ParentStateSchemaIdentity = parentStateSchemaIdentity;
        ResultSchemaIdentity = resultSchemaIdentity;
        Merge = merge;
    }

    public CompiledMergeKind Kind { get; }

    public Type ParentStateType { get; }

    public Type ResultType { get; }

    public string ParentStateSchemaIdentity { get; }

    public string ResultSchemaIdentity { get; }

    public Delegate? Merge { get; }
}

/// <summary>
/// One immutable authored branch inside a scope plan.
/// </summary>
internal sealed record CompiledBranchPlan(
    BranchPlanId Id,
    string BranchId,
    int Ordinal,
    CompiledBranchInputPlan Input,
    CompiledBranchResultPlan Result,
    IReadOnlyList<InstructionId> Instructions);

/// <summary>
/// One immutable structured-scope plan.
/// </summary>
internal sealed record CompiledScopePlan(
    ScopePlanId Id,
    CompiledScopeKind Kind,
    Type ResultType,
    IReadOnlyList<CompiledBranchPlan> Branches,
    CompiledMergePlan Merge,
    InstructionId JoinInstructionId,
    InstructionId ExitInstructionId)
{
    public CompiledForEachPlan? ForEach { get; init; }
}

/// <summary>
/// Dynamic item materialization and admission contract for an ephemeral ForEach scope.
/// </summary>
internal sealed record CompiledForEachPlan
{
    public CompiledForEachPlan(
        Type itemType,
        string itemSchemaIdentity,
        Delegate itemSelector,
        object partitioner,
        Delegate itemStateProjector,
        ForEachJoinPolicy joinPolicy,
        ForEachFailurePolicy failurePolicy,
        int? maxItems,
        int? maxConcurrency)
    {
        ItemType = itemType;
        ItemSchemaIdentity = itemSchemaIdentity;
        ItemSelector = itemSelector;
        Partitioner = partitioner;
        ItemStateProjector = itemStateProjector;
        JoinPolicy = joinPolicy;
        FailurePolicy = failurePolicy;
        MaxItems = maxItems;
        MaxConcurrency = maxConcurrency;
    }

    public Type ItemType { get; }

    public string ItemSchemaIdentity { get; }

    public ForEachJoinPolicy JoinPolicy { get; }

    public ForEachFailurePolicy FailurePolicy { get; }

    public int? MaxItems { get; }

    public int? MaxConcurrency { get; }

    public Delegate ItemSelector { get; }

    public object Partitioner { get; }

    public Delegate ItemStateProjector { get; }
}
