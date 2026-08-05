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
    Resource = 3,
    Retry = 4
}

internal sealed record FiberBlock(FiberBlockedReason Reason, string ObligationId);

internal sealed record FiberFailure
{
    public FiberFailure(
        string code,
        string message,
        IReadOnlyList<FiberFailure>? causes = null,
        AuthoredLocation? authoredLocation = null,
        FailureOccurrence? occurrence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
        Causes = Array.AsReadOnly(causes?.ToArray() ?? []);
        AuthoredLocation = authoredLocation ?? FailureProvenance.Location("workflow:$");
        Occurrence = occurrence ?? FailureProvenance.RootOccurrence();
    }

    public string Code { get; }

    public string Message { get; }

    public IReadOnlyList<FiberFailure> Causes { get; }

    public AuthoredLocation AuthoredLocation { get; }

    public FailureOccurrence Occurrence { get; }
}

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
    public long QuantumRotationCount { get; init; }

    public long ForcedRotationCount { get; init; }

    public int RetryAttempt { get; init; }

    public DateTimeOffset? RetryNotBefore { get; init; }

    public string? LogicalOperationKey { get; init; }

    public bool AttemptInFlight { get; init; }

    public DateTimeOffset? TimeoutDeadline { get; init; }

    public string? ResumeFromWaitId { get; init; }

    public static FiberRecord CreateRoot(
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
