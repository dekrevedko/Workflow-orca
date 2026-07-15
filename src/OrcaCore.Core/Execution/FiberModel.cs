using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Execution;

internal enum FiberPhase
{
    Runnable = 0,
    Blocked = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

internal enum FiberBlockedReason
{
    Wait = 0,
    Timer = 1,
    Scope = 2,
    ExternalJob = 3,
    ChildGroup = 4,
    Resource = 5,
    Retry = 6
}

internal sealed record FiberBlock(FiberBlockedReason Reason, string ObligationId);

internal sealed record FiberFailure(string Code, string Message);

internal sealed record FiberRecord(
    FiberId Id,
    ScopeId? OwningScopeId,
    InstructionId InstructionId,
    FiberPhase Phase,
    long LoopIteration,
    long NextScopeEntrySequence,
    byte[]? LocalStatePayload,
    byte[]? ResultPayload,
    FiberBlock? Blocked,
    FiberFailure? Failure,
    string? CancellationReason)
{
    internal long YieldCount { get; init; }

    internal long ForcedRotationCount { get; init; }

    internal int RetryAttempt { get; init; }

    internal DateTimeOffset? RetryNotBefore { get; init; }

    internal string? LogicalOperationKey { get; init; }

    internal DateTimeOffset? TimeoutDeadline { get; init; }

    internal string? ResumeFromWaitId { get; init; }

    internal static FiberRecord CreateRoot(
        InstanceId instanceId,
        long generation,
        InstructionId instructionId)
    {
        return new FiberRecord(
            FiberIdentity.CreateRoot(instanceId, generation),
            null,
            instructionId,
            FiberPhase.Runnable,
            LoopIteration: 0,
            NextScopeEntrySequence: 0,
            LocalStatePayload: null,
            ResultPayload: null,
            Blocked: null,
            Failure: null,
            CancellationReason: null);
    }
}
