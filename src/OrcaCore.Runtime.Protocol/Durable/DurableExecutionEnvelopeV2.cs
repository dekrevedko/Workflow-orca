using System.Text.Json;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;

namespace OrcaCore.Abstractions.Durable;

/// <summary>
/// Serialized checkpoint supplied to aggregate commands independently of envelope format.
/// </summary>
public sealed record DurableCheckpointPayload
{
    public required string ContentType { get; init; }

    public required byte[] Payload { get; init; }

    public static implicit operator DurableCheckpointPayload(DurableExecutionEnvelopeV2 envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return new DurableCheckpointPayload
        {
            ContentType = DurableExecutionEnvelopeV2.ContentType,
            Payload = envelope.Serialize()
        };
    }
}

/// <summary>
/// Format-2 durable checkpoint containing the complete structured-fiber execution state.
/// </summary>
public sealed record DurableExecutionEnvelopeV2
{
    public const string ContentType = "application/vnd.orcacore.durable-envelope.v2+json";

    public const int CurrentVersion = 2;

    public required int EnvelopeVersion { get; init; }

    public required InstanceId InstanceId { get; init; }

    public required long ContinueAsNewGeneration { get; init; }

    public required string RootFiberId { get; init; }

    public required DurablePlanBinding PlanBinding { get; init; }

    public required string StateContentType { get; init; }

    public required byte[] StatePayload { get; init; }

    public required IReadOnlyList<DurableFiberState> Fibers { get; init; }

    public required IReadOnlyList<DurableExecutionScopeState> Scopes { get; init; }

    public required DurableFiberSchedulerState Scheduler { get; init; }

    /// <summary>
    /// Gets the workflow-wide absolute deadline computed once from the original start instant.
    /// The value is retained across host replacement and ContinueAsNew generations.
    /// </summary>
    public DateTimeOffset? WorkflowDeadline { get; init; }

    /// <summary>
    /// Gets the durable timer that wakes the instance at <see cref="WorkflowDeadline"/>.
    /// </summary>
    public TimerId? WorkflowDeadlineTimerId { get; init; }

    public IReadOnlyList<DurableOwnedObligationState> OwnedObligations { get; init; } = [];

    public long NextRegistrationSequence { get; init; } = 1;

    public DurableExecutionDiagnostics Diagnostics { get; init; } = new();

    public DurableWorkflowOutputState? Output { get; init; }

    public byte[] Serialize()
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            this,
            OrcaCoreJsonSerializerContext.Default.DurableExecutionEnvelopeV2);
    }

    public static DurableExecutionEnvelopeV2 Deserialize(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return JsonSerializer.Deserialize(
                payload,
                OrcaCoreJsonSerializerContext.Default.DurableExecutionEnvelopeV2)
            ?? throw new JsonException("Durable format-2 execution envelope could not be deserialized.");
    }
}

/// <summary>
/// Serialized typed workflow output committed with the terminal durable checkpoint.
/// </summary>
public sealed record DurableWorkflowOutputState
{
    public required string TypeName { get; init; }

    public required string SchemaIdentity { get; init; }

    public required byte[] Payload { get; init; }
}

public sealed record DurablePlanBinding
{
    public required DefinitionId DefinitionId { get; init; }

    public required DefinitionVersion DefinitionVersion { get; init; }

    public required int CompilerFormatVersion { get; init; }

    public required string CompilerProfileId { get; init; }

    public required string PlanFingerprint { get; init; }
}

public sealed record DurableFiberState
{
    public required string FiberId { get; init; }

    public string? OwningScopeId { get; init; }

    public required string InstructionId { get; init; }

    public required DurableFiberPhase Phase { get; init; }

    public required long LoopIteration { get; init; }

    public required long NextScopeEntrySequence { get; init; }

    public byte[]? LocalStatePayload { get; init; }

    public byte[]? ResultPayload { get; init; }

    public DurableFiberBlock? Blocked { get; init; }

    public DurableFiberFailure? Failure { get; init; }

    public string? CancellationReason { get; init; }

    public long QuantumRotationCount { get; init; }

    public long ForcedRotationCount { get; init; }

    public int RetryAttempt { get; init; }

    public DateTimeOffset? RetryNotBefore { get; init; }

    public string? LogicalOperationKey { get; init; }

    public bool AttemptInFlight { get; init; }

    public DateTimeOffset? TimeoutDeadline { get; init; }

    public string? ResumeFromWaitId { get; init; }
}

public sealed record DurableFiberBlock
{
    public required DurableFiberBlockedReason Reason { get; init; }

    public required string ObligationId { get; init; }
}

public sealed record DurableFiberFailure
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public required string AuthoredLocation { get; init; }

    public required string OccurrenceKind { get; init; }

    public string? BranchId { get; init; }

    public int? ItemIndex { get; init; }

    public IReadOnlyList<DurableFiberFailure> Causes { get; init; } = [];
}

public enum DurableFiberPhase
{
    Runnable,
    Blocked,
    Completed,
    Failed,
    Cancelled
}

public enum DurableFiberBlockedReason
{
    Wait,
    Timer,
    Scope,
    Resource,
    Retry
}

public sealed record DurableExecutionScopeState
{
    public required string ScopeId { get; init; }

    public required string ScopePlanId { get; init; }

    public required long ScopeEntrySequence { get; init; }

    public string? ParentScopeId { get; init; }

    public required string ParentFiberId { get; init; }

    public required DurableExecutionScopeKind Kind { get; init; }

    public required DurableExecutionScopePhase Phase { get; init; }

    public required IReadOnlyList<string> ChildFiberIds { get; init; }

    public string? WinnerFiberId { get; init; }

    public IReadOnlyList<DurableCommittedResult> CommittedResults { get; init; } = [];

    public DurableForEachScopeState? ForEach { get; init; }
}

public enum DurableExecutionScopeKind
{
    WhenAll,
    WhenFirst,
    ForEach
}

public enum DurableExecutionScopePhase
{
    Created,
    Running,
    Joinable,
    Merging,
    Completed,
    Failed,
    Cancelled
}

public sealed record DurableCommittedResult
{
    public required string FiberId { get; init; }

    public byte[]? Payload { get; init; }
}

public sealed record DurableForEachScopeState
{
    public required IReadOnlyList<DurableForEachItemDescriptor> Descriptors { get; init; }

    public required int NextAdmissionOffset { get; init; }

    public required int MaxConcurrency { get; init; }

    public required string JoinPolicy { get; init; }

    public required string FailurePolicy { get; init; }

    public IReadOnlyList<DurableForEachFiberBinding> ItemFibers { get; init; } = [];

    public IReadOnlyList<DurableForEachItemOutcomeState> Outcomes { get; init; } = [];
}

public sealed record DurableForEachItemDescriptor
{
    public required int Index { get; init; }

    public required byte[] LocalStatePayload { get; init; }
}

public sealed record DurableForEachFiberBinding
{
    public required string FiberId { get; init; }

    public required int ItemIndex { get; init; }
}

public sealed record DurableForEachItemOutcomeState
{
    public required int Index { get; init; }

    public required string Status { get; init; }

    public byte[]? ResultPayload { get; init; }

    public DurableFiberFailure? Failure { get; init; }
}

public sealed record DurableFiberSchedulerState
{
    public required IReadOnlyList<string> RunnableFiberIds { get; init; }

    public string? NextFiberId { get; init; }
}

public sealed record DurableOwnedObligationState
{
    public required DurableOwnedObligationKind Kind { get; init; }

    public required string ObligationId { get; init; }

    public required string FiberId { get; init; }

    public string? ScopeId { get; init; }

    /// <summary>
    /// Gets the compiled instruction that authored this obligation when recovery needs its
    /// structural contract after host replacement.
    /// </summary>
    public string? InstructionId { get; init; }

    /// <summary>Gets the compiler path used to reconstruct detached authored diagnostics.</summary>
    public string? AuthoredPath { get; init; }

    /// <summary>
    /// Gets the persisted scoped-lease lifecycle phase when this is a resource obligation.
    /// </summary>
    public string? LeasePhase { get; init; }

    /// <summary>
    /// Gets the stable provider holder identity when this is a resource obligation.
    /// </summary>
    public string? HolderKey { get; init; }

    /// <summary>
    /// Gets the opaque protection identity committed before provider admission.
    /// </summary>
    public string? ProtectionToken { get; init; }

    /// <summary>
    /// Gets the copied normalized request committed before provider admission.
    /// </summary>
    public IReadOnlyList<ResourcePoolRequirement> LeaseRequirements { get; init; } = [];

    /// <summary>Gets the exact tickets committed for this obligation.</summary>
    public IReadOnlyList<DurableLeaseTicketState> LeaseTickets { get; init; } = [];

    public string? AcceptedConfirmationId { get; init; }

    public required long RegistrationSequence { get; init; }
}

public sealed record DurableLeaseTicketState
{
    public required string TicketId { get; init; }

    public required string PoolName { get; init; }

    public required int Units { get; init; }

    public required long ProviderGeneration { get; init; }

    public DateTimeOffset? ReviewDeadline { get; init; }

    public bool ReviewMarked { get; init; }
}

public enum DurableOwnedObligationKind
{
    Wait,
    Timer,
    PendingResume,
    Resource,
    Retry
}

public sealed record DurableExecutionDiagnostics
{
    public long TotalQuantumRotations { get; init; }

    public long ForcedRotations { get; init; }
}
