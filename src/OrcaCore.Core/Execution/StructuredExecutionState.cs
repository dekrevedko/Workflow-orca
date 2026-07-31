using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Execution;

public sealed record StructuredExecutionState(
    InstanceId InstanceId,
    long ContinueAsNewGeneration,
    FiberId RootFiberId,
    FiberSchedulerState Scheduler,
    IReadOnlyDictionary<FiberId, FiberRecord> Fibers,
    IReadOnlyDictionary<ScopeId, ExecutionScopeRecord> Scopes)
{
    public DateTimeOffset? WorkflowDeadline { get; init; }

    public TimerId? WorkflowDeadlineTimerId { get; init; }

    public long CompletedYieldCount { get; init; }

    public long CompletedForcedRotationCount { get; init; }

    public long NextRegistrationSequence { get; init; } = 1;

    public static StructuredExecutionState Create(
        InstanceId instanceId,
        long generation,
        InstructionId rootInstructionId)
    {
        var root = FiberRecord.CreateRoot(instanceId, generation, rootInstructionId);
        return new StructuredExecutionState(
            instanceId,
            generation,
            root.Id,
            FiberScheduler.Create([root.Id]),
            new Dictionary<FiberId, FiberRecord> { [root.Id] = root },
            new Dictionary<ScopeId, ExecutionScopeRecord>());
    }
}

public enum ExecutionScopePhase
{
    Created = 0,
    Running = 1,
    Joinable = 2,
    Merging = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}

public sealed record ExecutionScopeRecord(
    ScopeId Id,
    ScopePlanId ScopePlanId,
    long ScopeEntrySequence,
    ScopeId? ParentScopeId,
    FiberId ParentFiberId,
    CompiledScopeKind Kind,
    ExecutionScopePhase Phase,
    IReadOnlyList<FiberId> ChildFiberIds,
    FiberId? WinnerFiberId,
    IReadOnlyDictionary<FiberId, byte[]?> CommittedResults)
{
    public ForEachRuntimeState? ForEach { get; init; }
}

public sealed record ForEachItemDescriptor(int Index, byte[] LocalStatePayload);

public sealed record ForEachTerminalOutcome(
    int Index,
    ForEachItemTerminalStatus Status,
    byte[]? ResultPayload,
    FiberFailure? Failure);

public sealed record ForEachRuntimeState(
    IReadOnlyList<ForEachItemDescriptor> Descriptors,
    int NextAdmissionOffset,
    int MaxConcurrency,
    ForEachJoinPolicy JoinPolicy,
    ForEachFailurePolicy FailurePolicy,
    IReadOnlyDictionary<FiberId, int> ItemIndexByFiber,
    IReadOnlyDictionary<int, ForEachTerminalOutcome> Outcomes);

public sealed record ScopeStartTransition(
    StructuredExecutionState State,
    ScopeId ScopeId,
    IReadOnlyList<FiberId> ChildFiberIds);

public sealed record ScopeChildTransition(
    StructuredExecutionState State,
    ScopeId ScopeId,
    bool ScopeBecameJoinable);

public sealed record ForEachScopeTransition(
    StructuredExecutionState State,
    ScopeId ScopeId,
    bool ScopeBecameJoinable,
    IReadOnlyList<FiberId> AdmittedFiberIds);

public sealed record ChildTerminalOutcome(
    FiberId FiberId,
    byte[]? ResultPayload,
    FiberFailure? Failure)
{
    public static ChildTerminalOutcome Succeeded(FiberId fiberId, byte[]? resultPayload)
    {
        return new ChildTerminalOutcome(fiberId, resultPayload?.ToArray(), null);
    }

    public static ChildTerminalOutcome Failed(FiberId fiberId, FiberFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new ChildTerminalOutcome(fiberId, null, failure);
    }
}
