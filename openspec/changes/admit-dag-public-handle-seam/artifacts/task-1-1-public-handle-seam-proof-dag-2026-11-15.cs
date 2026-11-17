// Standalone proof for admit-dag-public-handle-seam (2026-11-15).
// Proof.Dag stands in for OrcaCore.Dag; Proof.Dag.Hosting for OrcaCore.Dag.Hosting.
// The only non-public members the host assembly touches are the two handle constructors.
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

public abstract record DagRegistrationResult<TRunInput>
{
    private protected DagRegistrationResult() { }

    public DagDefinitionHandle<TRunInput> GetHandleOrThrow() => this switch
    {
        Registered registered => registered.Handle,
        // The real type throws DagDefinitionRegistrationConflictException(conflict.Error).
        Conflict => throw new InvalidOperationException("registration conflict"),
        _ => throw new InvalidOperationException()
    };

    public sealed record Registered(DagDefinitionHandle<TRunInput> Handle) : DagRegistrationResult<TRunInput>;
    public sealed record Conflict(DefinitionRegistrationConflict Error) : DagRegistrationResult<TRunInput>;
}

public abstract record DagStartResult
{
    private protected DagStartResult() { }

    public DagRunHandle GetHandleOrThrow() => this switch
    {
        Accepted accepted => accepted.Handle,
        // The real type throws DagStartIdempotencyConflictException(conflict.Error).
        Conflict => throw new InvalidOperationException("start conflict"),
        _ => throw new InvalidOperationException()
    };

    public sealed record Accepted(DagRunHandle Handle, bool WasExisting) : DagStartResult;
    public sealed record Conflict(StartIdempotencyConflict Error) : DagStartResult;
}

public interface IDagDefinitionRegistry
{
    DagRegistrationResult<TRunInput> Register<TRunInput>(WorkflowDagPlan<TRunInput> plan);
}

public sealed class DagDefinitionHandle<TRunInput>
{
    private readonly Func<TRunInput, StartIdempotencyKey, CancellationToken, ValueTask<DagStartResult>> startOrGet;
    private readonly Func<DagRunId, CancellationToken, ValueTask<(DagRunHandle? Run, DefinitionId? ActualDefinitionId,
        DefinitionVersion? ActualDefinitionVersion, DefinitionFingerprint? ActualDefinitionFingerprint)>> getRun;

    // PROPOSED FRIEND MEMBER 16 of 17.
    internal DagDefinitionHandle(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TRunInput, StartIdempotencyKey, CancellationToken, ValueTask<DagStartResult>> startOrGet,
        Func<DagRunId, CancellationToken, ValueTask<(DagRunHandle? Run, DefinitionId? ActualDefinitionId,
            DefinitionVersion? ActualDefinitionVersion, DefinitionFingerprint? ActualDefinitionFingerprint)>> getRun)
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
        if (found.Run is not null) return found.Run;
        // OrcaCore.Dag owns both typed exceptions; the host never constructs them.
        if (found.ActualDefinitionId is null)
            throw new KeyNotFoundException($"stand-in for DagRunNotFoundException({runId})");
        throw new InvalidOperationException(
            $"stand-in for DagRunDefinitionMismatchException({runId}, {DefinitionId}/{DefinitionVersion}, " +
            $"{found.ActualDefinitionId}/{found.ActualDefinitionVersion})");
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

    // A null delegate result means the run is in the internal integrity-fault state.
    public async ValueTask<DagRunSnapshot> WaitForTerminalAsync(CancellationToken cancellationToken = default) =>
        await waitForTerminal(cancellationToken).ConfigureAwait(false) ?? throw Faulted();

    public async ValueTask<DagCancellationRequestStatus> RequestCancellationAsync(CancellationToken cancellationToken = default) =>
        await requestCancellation(cancellationToken).ConfigureAwait(false) ?? throw Faulted();

    // The real type throws the proposed public DagRunFaultedException(RunId), owned by OrcaCore.Dag.
    private InvalidOperationException Faulted() => new($"stand-in for DagRunFaultedException({RunId})");
}
