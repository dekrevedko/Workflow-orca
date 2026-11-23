// Standalone proof, RV-2 remediation, for admit-dag-public-handle-seam (2026-11-15).
// Proof.Dag stands in for OrcaCore.Dag; Proof.Dag.Hosting for OrcaCore.Dag.Hosting.
// The only non-public members the host assembly touches are the two handle constructors.
// RV-1 added the declared run-input contract to registration, start replay and reopen, and
// signals the integrity-fault state through nullable delegate results. RV-2 makes the start
// outcome a closed, inspectable DagStartResult.RunInputConflict that keeps the owner's run id.
using System.Runtime.CompilerServices;
using OrcaCore;
using OrcaCore.Dag;

[assembly: InternalsVisibleTo("Proof.Dag.Hosting")]

namespace Proof.Dag;

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

// Stand-ins for the three typed DAG exceptions whose real constructors are internal to OrcaCore.Dag.
public sealed class ProofRunNotFoundException(DagRunId runId) : Exception($"DAG-RUN-NOT-FOUND {runId}");

public sealed class ProofRunDefinitionMismatchException(
    DagRunId runId,
    DefinitionId expectedId, DefinitionVersion expectedVersion, DefinitionFingerprint expectedFingerprint,
    DefinitionId actualId, DefinitionVersion actualVersion, DefinitionFingerprint actualFingerprint)
    : Exception($"DAG-RUN-DEFINITION-MISMATCH {runId}: {expectedId}/{expectedVersion}/{expectedFingerprint} " +
        $"vs {actualId}/{actualVersion}/{actualFingerprint}");

public sealed class ProofRunFaultedException(DagRunId runId) : Exception($"DAG-RUN-FAULTED {runId}");

// Proposed new public exception (real constructor internal to OrcaCore.Dag).
public sealed class DagRunInputContractMismatchException : Exception
{
    internal DagRunInputContractMismatchException(
        DefinitionId definitionId, DefinitionVersion definitionVersion, DefinitionFingerprint definitionFingerprint,
        Type attemptedRunInputType, Type? existingRunInputType, DagRunId? runId)
        : base($"DAG-RUN-INPUT-CONTRACT-MISMATCH {definitionId}/{definitionVersion}")
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
    /// <summary>Null when the other contract was recorded by another host and is known only by its key.</summary>
    public Type? ExistingRunInputType { get; }
    /// <summary>Null for a registration-time conflict.</summary>
    public DagRunId? RunId { get; }
}

public abstract record DagRegistrationResult<TRunInput>
{
    private protected DagRegistrationResult() { }

    public DagDefinitionHandle<TRunInput> GetHandleOrThrow() => this switch
    {
        Registered registered => registered.Handle,
        // The real type throws DagDefinitionRegistrationConflictException(conflict.Error).
        Conflict => throw new InvalidOperationException("registration conflict"),
        RunInputConflict conflict => throw new DagRunInputContractMismatchException(
            conflict.DefinitionId, conflict.DefinitionVersion, conflict.DefinitionFingerprint,
            conflict.AttemptedRunInputType, conflict.ExistingRunInputType, runId: null),
        _ => throw new InvalidOperationException()
    };

    public sealed record Registered(DagDefinitionHandle<TRunInput> Handle) : DagRegistrationResult<TRunInput>;
    public sealed record Conflict(DefinitionRegistrationConflict Error) : DagRegistrationResult<TRunInput>;

    // Proposed third variant: identical identity, version and structural fingerprint,
    // but another declared run-input contract is already registered on this host.
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
        // The real type throws DagStartIdempotencyConflictException(conflict.Error).
        Conflict => throw new InvalidOperationException("start conflict"),
        RunInputConflict conflict => throw new DagRunInputContractMismatchException(
            conflict.DefinitionId, conflict.DefinitionVersion, conflict.DefinitionFingerprint,
            conflict.AttemptedRunInputType, existingRunInputType: null, conflict.ExistingRunId),
        _ => throw new InvalidOperationException()
    };

    public sealed record Accepted(DagRunHandle Handle, bool WasExisting) : DagStartResult;
    public sealed record Conflict(StartIdempotencyConflict Error) : DagStartResult;

    // Proposed third variant: the start key is owned by a run of this exact definition
    // triple under another run-input contract. The owner's run id stays inspectable.
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

    // PROPOSED FRIEND MEMBER 16 of 17.
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
        // Every start outcome, including another run-input contract, is a closed result variant.
        return startOrGet(input, idempotencyKey, cancellationToken);
    }

    public async ValueTask<DagRunHandle> GetRunAsync(DagRunId runId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runId);
        var found = await getRun(runId, cancellationToken).ConfigureAwait(false);
        if (found.Run is not null) return found.Run;
        // OrcaCore.Dag owns every typed exception; the host never constructs them.
        if (found.RunInputContractDiffers) throw InputContract(runId);
        if (found.ActualDefinitionId is null) throw new ProofRunNotFoundException(runId);
        throw new ProofRunDefinitionMismatchException(
            runId, DefinitionId, DefinitionVersion, DefinitionFingerprint,
            found.ActualDefinitionId, found.ActualDefinitionVersion!, found.ActualDefinitionFingerprint!);
    }

    private DagRunInputContractMismatchException InputContract(DagRunId runId) =>
        new(DefinitionId, DefinitionVersion, DefinitionFingerprint, typeof(TRunInput), existingRunInputType: null, runId);
}

public sealed class DagRunHandle
{
    private readonly IReadOnlyList<DagNodeRef> planNodes;
    private readonly Func<CancellationToken, ValueTask<DagRunSnapshot>> getSnapshot;
    private readonly Func<DagNodeRef, CancellationToken,
        ValueTask<(DagNodeStatus Status, bool HasOutput, object? Output, WorkflowFailure? Failure)>> getOutput;
    private readonly Func<CancellationToken, ValueTask<DagRunSnapshot?>> waitForTerminal;
    private readonly Func<CancellationToken, ValueTask<DagCancellationRequestStatus?>> requestCancellation;

    // PROPOSED FRIEND MEMBER 17 of 17.
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
        // Foreign-plan rejection happens here, before any query, by reference identity.
        if (!planNodes.Any(candidate => ReferenceEquals(candidate, node)))
            throw new ArgumentException("The node reference belongs to another DAG plan.", nameof(node));
        var read = await getOutput(node, cancellationToken).ConfigureAwait(false);
        if (!read.HasOutput) return new DagNodeOutputResult<TOutput>.Unavailable(read.Status, read.Failure);
        // The durable bridge decoded with the declared output type; a mismatch is a runtime defect.
        if (read.Output is null ? default(TOutput) is not null : read.Output is not TOutput)
            throw new InvalidOperationException("The runtime supplied an output that is not the declared node output type.");
        return new DagNodeOutputResult<TOutput>.Available((TOutput)read.Output!);
    }

    // Null from either delegate: the run is in the internal integrity-fault state and
    // (for cancellation) no cancellation barrier had been committed before the fault.
    public async ValueTask<DagRunSnapshot> WaitForTerminalAsync(CancellationToken cancellationToken = default) =>
        await waitForTerminal(cancellationToken).ConfigureAwait(false) ?? throw new ProofRunFaultedException(RunId);

    public async ValueTask<DagCancellationRequestStatus> RequestCancellationAsync(CancellationToken cancellationToken = default) =>
        await requestCancellation(cancellationToken).ConfigureAwait(false) ?? throw new ProofRunFaultedException(RunId);
}
