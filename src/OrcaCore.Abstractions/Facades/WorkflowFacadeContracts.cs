using System.Security.Cryptography;
using OrcaCore.Internal;

namespace OrcaCore;

/// <summary>Describes the externally observable lifecycle of one workflow instance.</summary>
public enum WorkflowInstanceStatus
{
    Pending,
    Running,
    Waiting,
    CancellationRequested,
    Completed,
    Failed,
    TimedOut,
    Cancelled,
    Terminated
}

/// <summary>Describes why a definition cannot be registered by the selected host role.</summary>
public abstract record DefinitionHostCompatibilityFailure
{
    private protected DefinitionHostCompatibilityFailure()
    {
    }

    public sealed record EngineModeMismatch : DefinitionHostCompatibilityFailure
    {
        public EngineModeMismatch(WorkflowMode hostMode, WorkflowMode definitionMode)
        {
            HostMode = hostMode;
            DefinitionMode = definitionMode;
        }

        public WorkflowMode HostMode { get; }
        public WorkflowMode DefinitionMode { get; }
    }

    public sealed record MissingTransientPools : DefinitionHostCompatibilityFailure
    {
        public MissingTransientPools(IReadOnlyList<TransientPoolName> poolNames) =>
            PoolNames = CopyPoolNames(poolNames);

        public IReadOnlyList<TransientPoolName> PoolNames { get; }
    }

    public sealed record MissingDurableResourcePools : DefinitionHostCompatibilityFailure
    {
        public MissingDurableResourcePools(IReadOnlyList<ResourcePoolName> poolNames) =>
            PoolNames = CopyPoolNames(poolNames);

        public IReadOnlyList<ResourcePoolName> PoolNames { get; }
    }

    private static IReadOnlyList<TName> CopyPoolNames<TName>(IReadOnlyList<TName> poolNames)
    {
        ArgumentNullException.ThrowIfNull(poolNames);
        var copied = poolNames
            .Distinct()
            .OrderBy(value => value?.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (copied.Length == 0)
        {
            throw new ArgumentException("At least one missing pool name is required.", nameof(poolNames));
        }

        return Array.AsReadOnly(copied);
    }
}

/// <summary>Describes a structural conflict with an existing definition registration.</summary>
public sealed class DefinitionRegistrationConflict
{
    internal DefinitionRegistrationConflict(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint existingFingerprint,
        DefinitionFingerprint attemptedFingerprint)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        ArgumentNullException.ThrowIfNull(existingFingerprint);
        ArgumentNullException.ThrowIfNull(attemptedFingerprint);
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        ExistingFingerprint = existingFingerprint;
        AttemptedFingerprint = attemptedFingerprint;
    }

    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint ExistingFingerprint { get; }
    public DefinitionFingerprint AttemptedFingerprint { get; }
}

/// <summary>Describes incompatible reuse of a start idempotency key.</summary>
public sealed class StartIdempotencyConflict
{
    internal StartIdempotencyConflict(
        StartIdempotencyKey key,
        DefinitionId existingDefinitionId,
        DefinitionVersion existingDefinitionVersion,
        DefinitionFingerprint existingDefinitionFingerprint,
        PayloadFingerprint existingInputFingerprint,
        DefinitionId attemptedDefinitionId,
        DefinitionVersion attemptedDefinitionVersion,
        DefinitionFingerprint attemptedDefinitionFingerprint,
        PayloadFingerprint attemptedInputFingerprint)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(existingDefinitionId);
        ArgumentNullException.ThrowIfNull(existingDefinitionVersion);
        ArgumentNullException.ThrowIfNull(existingDefinitionFingerprint);
        ArgumentNullException.ThrowIfNull(existingInputFingerprint);
        ArgumentNullException.ThrowIfNull(attemptedDefinitionId);
        ArgumentNullException.ThrowIfNull(attemptedDefinitionVersion);
        ArgumentNullException.ThrowIfNull(attemptedDefinitionFingerprint);
        ArgumentNullException.ThrowIfNull(attemptedInputFingerprint);
        Key = key;
        ExistingDefinitionId = existingDefinitionId;
        ExistingDefinitionVersion = existingDefinitionVersion;
        ExistingDefinitionFingerprint = existingDefinitionFingerprint;
        ExistingInputFingerprint = existingInputFingerprint;
        AttemptedDefinitionId = attemptedDefinitionId;
        AttemptedDefinitionVersion = attemptedDefinitionVersion;
        AttemptedDefinitionFingerprint = attemptedDefinitionFingerprint;
        AttemptedInputFingerprint = attemptedInputFingerprint;
    }

    public StartIdempotencyKey Key { get; }
    public DefinitionId ExistingDefinitionId { get; }
    public DefinitionVersion ExistingDefinitionVersion { get; }
    public DefinitionFingerprint ExistingDefinitionFingerprint { get; }
    public PayloadFingerprint ExistingInputFingerprint { get; }
    public DefinitionId AttemptedDefinitionId { get; }
    public DefinitionVersion AttemptedDefinitionVersion { get; }
    public DefinitionFingerprint AttemptedDefinitionFingerprint { get; }
    public PayloadFingerprint AttemptedInputFingerprint { get; }
}

/// <summary>A closed definition-registration result.</summary>
public abstract record WorkflowRegistrationResult<TDefinitionHandle>
{
    private protected WorkflowRegistrationResult()
    {
    }

    public TDefinitionHandle GetHandleOrThrow() => this switch
    {
        Registered registered => registered.Handle,
        HostIncompatible incompatible => throw new WorkflowDefinitionHostCompatibilityException(incompatible.Error),
        Conflict conflict => throw new WorkflowDefinitionRegistrationConflictException(conflict.Error),
        _ => throw new InvalidOperationException("Unknown workflow registration result.")
    };

    public sealed record Registered(TDefinitionHandle Handle) : WorkflowRegistrationResult<TDefinitionHandle>;
    public sealed record Conflict(DefinitionRegistrationConflict Error) : WorkflowRegistrationResult<TDefinitionHandle>;
    public sealed record HostIncompatible(DefinitionHostCompatibilityFailure Error)
        : WorkflowRegistrationResult<TDefinitionHandle>;
}

/// <summary>A closed start-or-get result.</summary>
public abstract record WorkflowStartResult<TInstanceHandle>
{
    private protected WorkflowStartResult()
    {
    }

    public TInstanceHandle GetHandleOrThrow() => this switch
    {
        Accepted accepted => accepted.Handle,
        Conflict conflict => throw new WorkflowStartIdempotencyConflictException(conflict.Error),
        _ => throw new InvalidOperationException("Unknown workflow start result.")
    };

    public sealed record Accepted(TInstanceHandle Handle, bool WasExisting)
        : WorkflowStartResult<TInstanceHandle>;
    public sealed record Conflict(StartIdempotencyConflict Error)
        : WorkflowStartResult<TInstanceHandle>;
}

/// <summary>One active authored event wait visible through the application facade.</summary>
public sealed record ActiveWaitSnapshot(
    WaitId WaitId,
    AuthoredLocation AuthoredLocation,
    WorkflowEventContract EventContract,
    CorrelationId CorrelationId,
    DateTimeOffset RegisteredAt,
    DateTimeOffset? Deadline);

/// <summary>A detached application-only snapshot of one workflow instance.</summary>
public sealed record WorkflowInstanceSnapshot(
    InstanceId InstanceId,
    WorkflowMode Mode,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    DefinitionFingerprint DefinitionFingerprint,
    WorkflowInstanceStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    WorkflowOutcomeName? Outcome,
    WorkflowFailure? Failure,
    IReadOnlyList<ActiveWaitSnapshot> ActiveWaits);

/// <summary>A nonblocking typed terminal-output query.</summary>
public abstract record WorkflowOutputResult<TOutput>
{
    private protected WorkflowOutputResult()
    {
    }

    public sealed record Pending : WorkflowOutputResult<TOutput>;
    public sealed record Available(TOutput Output) : WorkflowOutputResult<TOutput>;
    public sealed record Unavailable(WorkflowInstanceStatus Status, WorkflowFailure? Failure)
        : WorkflowOutputResult<TOutput>;
}

public enum WorkflowCancellationRequestStatus
{
    Requested,
    AlreadyRequested,
    AlreadyTerminal
}

public enum WorkflowTerminationStatus
{
    Terminated,
    AlreadyTerminal
}

/// <summary>A runtime-created authority for one resultless workflow instance.</summary>
public class WorkflowInstanceHandle
{
    private readonly Func<CancellationToken, ValueTask<WorkflowInstanceSnapshot>> getSnapshot;
    private readonly Func<Type, CancellationToken, ValueTask<object?>> getState;
    private readonly Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation;
    private readonly Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate;

    internal WorkflowInstanceHandle(
        InstanceId instanceId,
        Func<CancellationToken, ValueTask<WorkflowInstanceSnapshot>> getSnapshot,
        Func<Type, CancellationToken, ValueTask<object?>> getState,
        Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation,
        Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        ArgumentNullException.ThrowIfNull(getSnapshot);
        ArgumentNullException.ThrowIfNull(getState);
        ArgumentNullException.ThrowIfNull(requestCancellation);
        ArgumentNullException.ThrowIfNull(terminate);
        InstanceId = instanceId;
        this.getSnapshot = getSnapshot;
        this.getState = getState;
        this.requestCancellation = requestCancellation;
        this.terminate = terminate;
    }

    public InstanceId InstanceId { get; }

    public ValueTask<WorkflowInstanceSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default) =>
        getSnapshot(cancellationToken);

    public async ValueTask<TState> GetStateAsync<TState>(
        CancellationToken cancellationToken = default)
    {
        var state = await getState(typeof(TState), cancellationToken).ConfigureAwait(false);
        return state is TState typed
            ? typed
            : throw new WorkflowStateTypeMismatchException(
                InstanceId,
                state?.GetType() ?? typeof(object),
                typeof(TState));
    }

    public ValueTask<WorkflowCancellationRequestStatus> RequestCancellationAsync(
        CancellationToken cancellationToken = default) =>
        requestCancellation(cancellationToken);

    public ValueTask<WorkflowTerminationStatus> TerminateAsync(
        CancellationToken cancellationToken = default) =>
        terminate(cancellationToken);
}

/// <summary>A runtime-created authority for one resultful workflow instance.</summary>
public sealed class WorkflowInstanceHandle<TOutput> : WorkflowInstanceHandle
{
    private readonly Func<CancellationToken, ValueTask<WorkflowOutputResult<TOutput>>> getOutput;
    private readonly Func<CancellationToken, ValueTask<TOutput>> waitForOutput;

    internal WorkflowInstanceHandle(
        InstanceId instanceId,
        Func<CancellationToken, ValueTask<WorkflowInstanceSnapshot>> getSnapshot,
        Func<Type, CancellationToken, ValueTask<object?>> getState,
        Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation,
        Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate,
        Func<CancellationToken, ValueTask<WorkflowOutputResult<TOutput>>> getOutput,
        Func<CancellationToken, ValueTask<TOutput>> waitForOutput)
        : base(instanceId, getSnapshot, getState, requestCancellation, terminate)
    {
        ArgumentNullException.ThrowIfNull(getOutput);
        ArgumentNullException.ThrowIfNull(waitForOutput);
        this.getOutput = getOutput;
        this.waitForOutput = waitForOutput;
    }

    public ValueTask<WorkflowOutputResult<TOutput>> GetOutputAsync(
        CancellationToken cancellationToken = default) =>
        getOutput(cancellationToken);

    public ValueTask<TOutput> WaitForOutputAsync(
        CancellationToken cancellationToken = default) =>
        waitForOutput(cancellationToken);
}

public static class WorkflowStartResultExtensions
{
    public static ValueTask<TOutput> WaitForOutputAsync<TOutput>(
        this WorkflowStartResult<WorkflowInstanceHandle<TOutput>> start,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        return start.GetHandleOrThrow().WaitForOutputAsync(cancellationToken);
    }
}

/// <summary>Thrown when a definition is incompatible with the selected host role.</summary>
public sealed class WorkflowDefinitionHostCompatibilityException : OrcaCoreException
{
    internal WorkflowDefinitionHostCompatibilityException(DefinitionHostCompatibilityFailure failure)
        : base("WF-DEFINITION-HOST-INCOMPATIBLE", "The workflow definition is incompatible with this host.")
    {
        Failure = failure ?? throw new ArgumentNullException(nameof(failure));
    }

    public DefinitionHostCompatibilityFailure Failure { get; }
}

/// <summary>Thrown when an exact workflow reference is not installed in the selected host catalog.</summary>
public sealed class WorkflowDefinitionNotRegisteredException : OrcaCoreException
{
    internal WorkflowDefinitionNotRegisteredException(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint)
        : base(
            "WF-DEFINITION-NOT-REGISTERED",
            $"Workflow definition '{definitionId}' version '{definitionVersion}' with fingerprint " +
            $"'{definitionFingerprint}' is not registered.")
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        DefinitionFingerprint = definitionFingerprint;
    }

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public DefinitionFingerprint DefinitionFingerprint { get; }
}

public sealed class WorkflowDefinitionRegistrationConflictException : OrcaCoreException
{
    internal WorkflowDefinitionRegistrationConflictException(DefinitionRegistrationConflict conflict)
        : base("WF-DEFINITION-REGISTRATION-CONFLICT", "A different structural definition is already registered.")
    {
        Conflict = conflict ?? throw new ArgumentNullException(nameof(conflict));
    }

    public DefinitionRegistrationConflict Conflict { get; }
}

public sealed class WorkflowStartIdempotencyConflictException : OrcaCoreException
{
    internal WorkflowStartIdempotencyConflictException(StartIdempotencyConflict conflict)
        : base("WF-START-IDEMPOTENCY-CONFLICT", "The start idempotency key is bound to different start facts.")
    {
        Conflict = conflict ?? throw new ArgumentNullException(nameof(conflict));
    }

    public StartIdempotencyConflict Conflict { get; }
}

public sealed class WorkflowInstanceNotFoundException : OrcaCoreException
{
    internal WorkflowInstanceNotFoundException(InstanceId instanceId)
        : base("WF-INSTANCE-NOT-FOUND", $"Workflow instance '{instanceId}' was not found.")
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
    }

    public InstanceId InstanceId { get; }
}

public sealed class WorkflowInstanceDefinitionMismatchException : OrcaCoreException
{
    internal WorkflowInstanceDefinitionMismatchException(
        InstanceId instanceId,
        DefinitionId expectedDefinitionId,
        DefinitionId actualDefinitionId)
        : base(
            "WF-INSTANCE-DEFINITION-MISMATCH",
            $"Workflow instance '{instanceId}' belongs to definition '{actualDefinitionId}', not '{expectedDefinitionId}'.")
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        ExpectedDefinitionId = expectedDefinitionId ?? throw new ArgumentNullException(nameof(expectedDefinitionId));
        ActualDefinitionId = actualDefinitionId ?? throw new ArgumentNullException(nameof(actualDefinitionId));
    }

    public InstanceId InstanceId { get; }
    public DefinitionId ExpectedDefinitionId { get; }
    public DefinitionId ActualDefinitionId { get; }
}

public sealed class WorkflowStateTypeMismatchException : OrcaCoreException
{
    internal WorkflowStateTypeMismatchException(InstanceId instanceId, Type actualType, Type requestedType)
        : base(
            "WF-STATE-TYPE-MISMATCH",
            $"Workflow instance '{instanceId}' state type is '{actualType}', not requested type '{requestedType}'.")
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        ActualType = actualType ?? throw new ArgumentNullException(nameof(actualType));
        RequestedType = requestedType ?? throw new ArgumentNullException(nameof(requestedType));
    }

    public InstanceId InstanceId { get; }
    public Type ActualType { get; }
    public Type RequestedType { get; }
}

public sealed class AmbiguousWaitRegistrationException : OrcaCoreException
{
    internal AmbiguousWaitRegistrationException(
        DefinitionId definitionId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId)
        : base(
            "WF-WAIT-AMBIGUOUS",
            $"More than one active wait matches definition '{definitionId}', event " +
            $"'{eventContract?.EventName}' version '{eventContract?.Version}', and correlation '{correlationId}'.")
    {
        DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
        EventContract = eventContract ?? throw new ArgumentNullException(nameof(eventContract));
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
    }

    public DefinitionId DefinitionId { get; }
    public WorkflowEventContract EventContract { get; }
    public CorrelationId CorrelationId { get; }
}

public sealed class WorkflowOutputUnavailableException : OrcaCoreException
{
    internal WorkflowOutputUnavailableException(
        WorkflowInstanceStatus status,
        WorkflowFailure? failure)
        : base("WF-OUTPUT-UNAVAILABLE", $"Workflow terminal status '{status}' has no successful output.")
    {
        Status = status;
        Failure = failure;
    }

    public WorkflowInstanceStatus Status { get; }
    public WorkflowFailure? Failure { get; }
}

internal static class WorkflowPayloadFingerprint
{
    internal static PayloadFingerprint Create<T>(T value)
    {
        var bytes = FixedWorkflowValueCodec.Serialize(value, typeof(T));
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        return (PayloadFingerprint)typeof(PayloadFingerprint)
            .GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single()
            .Invoke([digest]);
    }
}
