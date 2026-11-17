namespace OrcaCore.Dag;

public enum DagRunStatus { Running, CancellationRequested, Succeeded, Failed, Cancelled }

public enum DagNodeStatus
{
    Pending, Ready, Running, CancellationRequested, Succeeded, Failed, Cancelled, DependencyBlocked
}

public enum DagCancellationRequestStatus { Requested, AlreadyRequested, AlreadyTerminal }

public sealed record DagNodeSnapshot(
    DagNodeId NodeId,
    int AuthoredOrdinal,
    DagNodeStatus Status,
    IReadOnlyList<DagNodeId> Dependencies,
    InstanceId? ChildInstanceId,
    PayloadFingerprint? MappedInputFingerprint,
    bool OutputAvailable,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    WorkflowFailure? Failure);

public sealed record DagRunSnapshot(
    DagRunId RunId,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    DefinitionFingerprint DefinitionFingerprint,
    DagRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<DagNodeSnapshot> Nodes);

public abstract record DagNodeOutputResult<TOutput>
{
    private protected DagNodeOutputResult() { }
    public sealed record Available(TOutput Output) : DagNodeOutputResult<TOutput>;
    public sealed record Unavailable(DagNodeStatus Status, WorkflowFailure? Failure) : DagNodeOutputResult<TOutput>;
}

public sealed class DagRunFaultedException : OrcaCoreException
{
    internal DagRunFaultedException(DagRunId runId)
        : base("DAG-RUN-FAULTED", $"DAG run '{runId}' is in an integrity-fault state and needs reconciliation.") =>
        RunId = runId;

    public DagRunId RunId { get; }
}

public sealed class DagRunInputContractMismatchException : OrcaCoreException
{
    internal DagRunInputContractMismatchException(
        DefinitionId definitionId, DefinitionVersion definitionVersion, DefinitionFingerprint definitionFingerprint,
        Type attemptedRunInputType, Type? existingRunInputType, DagRunId? runId)
        : base("DAG-RUN-INPUT-CONTRACT-MISMATCH",
            $"DAG definition '{definitionId}' version '{definitionVersion}' is bound to another run-input contract.")
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        DefinitionFingerprint = definitionFingerprint;
        AttemptedRunInputType = attemptedRunInputType;
        ExistingRunInputType = existingRunInputType;
        RunId = runId;
    }

    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }
    public Type AttemptedRunInputType { get; }
    public Type? ExistingRunInputType { get; }
    public DagRunId? RunId { get; }
}

public abstract record DagRegistrationResult<TRunInput>
{
    private protected DagRegistrationResult() { }

    public DagDefinitionHandle<TRunInput> GetHandleOrThrow() => this switch
    {
        Registered registered => registered.Handle,
        Conflict conflict => throw new DagDefinitionRegistrationConflictException(conflict.Error),
        RunInputConflict conflict => throw new DagRunInputContractMismatchException(
            conflict.DefinitionId, conflict.DefinitionVersion, conflict.DefinitionFingerprint,
            conflict.AttemptedRunInputType, conflict.ExistingRunInputType, runId: null),
        _ => throw new InvalidOperationException("Unknown DAG registration result.")
    };

    public sealed record Registered(DagDefinitionHandle<TRunInput> Handle) : DagRegistrationResult<TRunInput>;
    public sealed record Conflict(DefinitionRegistrationConflict Error) : DagRegistrationResult<TRunInput>;
    public sealed record RunInputConflict(
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        DefinitionFingerprint DefinitionFingerprint,
        Type ExistingRunInputType,
        Type AttemptedRunInputType) : DagRegistrationResult<TRunInput>;
}

public abstract record DagStartResult
{
    private protected DagStartResult() { }

    public DagRunHandle GetHandleOrThrow() => this switch
    {
        Accepted accepted => accepted.Handle,
        Conflict conflict => throw new DagStartIdempotencyConflictException(conflict.Error),
        RunInputConflict conflict => throw new DagRunInputContractMismatchException(
            conflict.DefinitionId, conflict.DefinitionVersion, conflict.DefinitionFingerprint,
            conflict.AttemptedRunInputType, existingRunInputType: null, conflict.ExistingRunId),
        _ => throw new InvalidOperationException("Unknown DAG start result.")
    };

    public sealed record Accepted(DagRunHandle Handle, bool WasExisting) : DagStartResult;
    public sealed record Conflict(StartIdempotencyConflict Error) : DagStartResult;
    public sealed record RunInputConflict(
        DagRunId ExistingRunId,
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        DefinitionFingerprint DefinitionFingerprint,
        Type AttemptedRunInputType) : DagStartResult;
}

public interface IDagDefinitionRegistry
{
    DagRegistrationResult<TRunInput> Register<TRunInput>(WorkflowDagPlan<TRunInput> plan);
}

public sealed class DagDefinitionHandle<TRunInput>
{
    private readonly Func<TRunInput, StartIdempotencyKey, CancellationToken, ValueTask<DagStartResult>> startOrGet;
    private readonly Func<DagRunId, CancellationToken, ValueTask<(
        DagRunHandle? Run,
        DefinitionId? ActualDefinitionId,
        DefinitionVersion? ActualDefinitionVersion,
        DefinitionFingerprint? ActualDefinitionFingerprint,
        bool RunInputContractDiffers)>> getRun;

    // Exact reviewed facade-handle constructor; implementation decisions stay in the durable host.
    internal DagDefinitionHandle(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TRunInput, StartIdempotencyKey, CancellationToken, ValueTask<DagStartResult>> startOrGet,
        Func<DagRunId, CancellationToken, ValueTask<(
            DagRunHandle? Run,
            DefinitionId? ActualDefinitionId,
            DefinitionVersion? ActualDefinitionVersion,
            DefinitionFingerprint? ActualDefinitionFingerprint,
            bool RunInputContractDiffers)>> getRun)
    {
        DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
        DefinitionVersion = definitionVersion ?? throw new ArgumentNullException(nameof(definitionVersion));
        DefinitionFingerprint = definitionFingerprint ?? throw new ArgumentNullException(nameof(definitionFingerprint));
        this.startOrGet = startOrGet ?? throw new ArgumentNullException(nameof(startOrGet));
        this.getRun = getRun ?? throw new ArgumentNullException(nameof(getRun));
    }

    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }

    public ValueTask<DagStartResult> StartOrGetAsync(
        TRunInput input, StartIdempotencyKey idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        return startOrGet(input, idempotencyKey, cancellationToken);
    }

    public async ValueTask<DagRunHandle> GetRunAsync(DagRunId runId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runId);
        var found = await getRun(runId, cancellationToken).ConfigureAwait(false);
        if (found.ActualDefinitionId is { } actual &&
            (actual != DefinitionId || found.ActualDefinitionVersion != DefinitionVersion ||
             !Equals(found.ActualDefinitionFingerprint, DefinitionFingerprint)))
            throw new DagRunDefinitionMismatchException(runId, DefinitionId, actual,
                DefinitionVersion, found.ActualDefinitionVersion!, DefinitionFingerprint, found.ActualDefinitionFingerprint!);
        if (found.RunInputContractDiffers)
            throw new DagRunInputContractMismatchException(DefinitionId, DefinitionVersion, DefinitionFingerprint,
                typeof(TRunInput), existingRunInputType: null, runId);
        return found.Run ?? throw new DagRunNotFoundException(runId);
    }
}

public sealed class DagRunHandle
{
    private readonly IReadOnlyList<DagNodeRef> planNodes;
    private readonly Func<CancellationToken, ValueTask<DagRunSnapshot>> getSnapshot;
    private readonly Func<DagNodeRef, CancellationToken,
        ValueTask<(DagNodeStatus Status, bool HasOutput, object? Output, WorkflowFailure? Failure)>> getOutput;
    private readonly Func<CancellationToken, ValueTask<DagRunSnapshot?>> waitForTerminal;
    private readonly Func<CancellationToken, ValueTask<DagCancellationRequestStatus?>> requestCancellation;

    // Exact reviewed facade-handle constructor; typed validation stays in this assembly.
    internal DagRunHandle(
        DagRunId runId,
        IReadOnlyList<DagNodeRef> planNodes,
        Func<CancellationToken, ValueTask<DagRunSnapshot>> getSnapshot,
        Func<DagNodeRef, CancellationToken,
            ValueTask<(DagNodeStatus Status, bool HasOutput, object? Output, WorkflowFailure? Failure)>> getOutput,
        Func<CancellationToken, ValueTask<DagRunSnapshot?>> waitForTerminal,
        Func<CancellationToken, ValueTask<DagCancellationRequestStatus?>> requestCancellation)
    {
        RunId = runId ?? throw new ArgumentNullException(nameof(runId));
        this.planNodes = planNodes ?? throw new ArgumentNullException(nameof(planNodes));
        this.getSnapshot = getSnapshot ?? throw new ArgumentNullException(nameof(getSnapshot));
        this.getOutput = getOutput ?? throw new ArgumentNullException(nameof(getOutput));
        this.waitForTerminal = waitForTerminal ?? throw new ArgumentNullException(nameof(waitForTerminal));
        this.requestCancellation = requestCancellation ?? throw new ArgumentNullException(nameof(requestCancellation));
    }

    public DagRunId RunId { get; }

    public ValueTask<DagRunSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        getSnapshot(cancellationToken);

    public async ValueTask<DagNodeOutputResult<TOutput>> GetOutputAsync<TOutput>(
        DagNodeRef<TOutput> node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!planNodes.Any(candidate => ReferenceEquals(candidate, node)))
            throw new ArgumentException("The node reference belongs to another DAG plan.", nameof(node));
        var read = await getOutput(node, cancellationToken).ConfigureAwait(false);
        if (!read.HasOutput) return new DagNodeOutputResult<TOutput>.Unavailable(read.Status, read.Failure);
        if (!DagValueTypes.Accepts(typeof(TOutput), read.Output))
            throw new InvalidOperationException("The runtime supplied an output that is not the declared node output type.");
        return new DagNodeOutputResult<TOutput>.Available((TOutput)read.Output!);
    }

    public async ValueTask<DagRunSnapshot> WaitForTerminalAsync(CancellationToken cancellationToken = default) =>
        await waitForTerminal(cancellationToken).ConfigureAwait(false) ?? throw new DagRunFaultedException(RunId);

    public async ValueTask<DagCancellationRequestStatus> RequestCancellationAsync(CancellationToken cancellationToken = default) =>
        await requestCancellation(cancellationToken).ConfigureAwait(false) ?? throw new DagRunFaultedException(RunId);
}
