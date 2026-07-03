using System.Text.Json;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// A mailbox-buffered event (EV-030) plus the loop-iteration identity of <see cref="WorkflowInstance{TState}.Pointer"/>
/// at the moment it was buffered (EV-043). Used to reject bidirectional matches against a later
/// iteration's freshly registered wait when the buffered entry belongs to an earlier iteration.
/// </summary>
internal sealed record BufferedEvent(EventEnvelope Envelope, IReadOnlyList<int> LoopIterationPath);

/// <summary>
/// Non-generic metadata view over a live <see cref="WorkflowInstance{TState}"/> (T1-13,
/// EV-013/AC-115): lets the registry and management layer enumerate/inspect instances in bulk
/// without knowing each instance's <c>TState</c> at the call site. Still internal (CR-021) —
/// only engine-internal code reads through this; every public result stays a snapshot/copy.
/// </summary>
internal interface IWorkflowInstance
{
    InstanceId InstanceId { get; }

    DefinitionId DefinitionId { get; }

    DefinitionVersion DefinitionVersion { get; }

    WorkflowStatus Status { get; }

    DateTimeOffset CreatedAt { get; }

    DateTimeOffset UpdatedAt { get; }

    string? ErrorSummary { get; }

    string? EndOutcomeName { get; }

    /// <summary>
    /// This instance's currently active waits (T1-13): the top-level wait when present, plus any
    /// branch waits from an in-flight <see cref="ActiveParallelJoin"/> (CP-001). Metadata-only —
    /// never the live <see cref="ActiveWait"/> reference itself.
    /// </summary>
    IReadOnlyList<ActiveWaitSnapshotSource> ActiveWaitSources();

    /// <summary>Returns a copy of the business state, boxed as <see cref="object"/> — never the live reference (CR-021, T1-13).</summary>
    object GetStateCopy();
}

/// <summary>
/// Metadata carried out of <see cref="IWorkflowInstance.ActiveWaitSources"/> before the
/// management layer turns it into a public <c>ActiveWaitSnapshot</c> (T1-13) — kept
/// engine-internal so <see cref="ActiveWait"/>/<see cref="BranchRuntime"/> are never exposed.
/// </summary>
internal readonly record struct ActiveWaitSnapshotSource(
    WaitId WaitId,
    string EventName,
    CorrelationId CorrelationId,
    DateTimeOffset RegisteredAt,
    BranchId? BranchId);

/// <summary>
/// The live, mutable runtime state of an executing instance (CR-020/CR-021): engine-owned
/// runtime metadata plus the workflow-owned <typeparamref name="TState"/>. Never exposed
/// publicly — callers only ever see a <see cref="WorkflowInstanceSnapshot"/> (CR-021).
/// Mutations are engine-internal; the execution lane (T1-06) will serialize access.
/// </summary>
internal sealed class WorkflowInstance<TState> : IWorkflowInstance
{
    public WorkflowInstance(
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        TState state,
        DateTimeOffset createdAt)
    {
        InstanceId = instanceId;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        State = state;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Status = WorkflowStatus.Running;
        Pointer = ExecutionPointer.Empty;
    }

    public InstanceId InstanceId { get; }

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public TState State { get; set; }

    public WorkflowStatus Status { get; set; }

    public ExecutionPointer Pointer { get; set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? ErrorSummary { get; set; }

    public string? EndOutcomeName { get; set; }

    /// <summary>The instance's single resident wait (EV-021), or null when not waiting. Instance-targeted only.</summary>
    public ActiveWait? ActiveWait { get; set; }

    /// <summary>
    /// The in-flight <see cref="ParallelNode"/> join (CP-002), or null when no <c>Parallel</c> is
    /// currently active at this position. While set, <see cref="Pointer"/> stays parked at the
    /// <see cref="ParallelNode"/>'s own position — see <see cref="ActiveParallelJoin"/>.
    /// </summary>
    internal ActiveParallelJoin? ActiveJoin { get; set; }

    /// <summary>
    /// The envelope that resumed the current run, surfaced to the first step's
    /// <see cref="OrcaCore.Abstractions.Steps.StepContext{TState}.ResumedEvent"/> only (EV-022).
    /// Cleared by the interpreter immediately after that one step executes.
    /// </summary>
    public EventEnvelope? PendingResumedEvent { get; set; }

    /// <summary>
    /// Per-instance pending-event mailbox (EV-030): events that arrived but did not match the
    /// active wait (or arrived with no active wait at all) are buffered here, keyed by
    /// <see cref="EventId"/> to prevent duplicate buffering (EV-031). Cleared entry-by-entry
    /// only after the resuming transition it drives commits (EV-032) — never before. Each entry
    /// also carries the loop-iteration identity it was buffered under (EV-043) so a later
    /// iteration's wait registration cannot bidirectionally match stale earlier-iteration residue.
    /// </summary>
    public Dictionary<EventId, BufferedEvent> Mailbox { get; } = [];

    /// <summary>
    /// Per-instance loop-iteration counters (EV-043): keyed by the immutable, definition-shared
    /// <see cref="WhileNode"/> reference, so the same node reliably identifies "this loop
    /// position" for the instance's whole lifetime. Incremented every time the node is re-entered
    /// with its condition true, giving each iteration a distinct <see cref="Frame.LoopIteration"/>.
    /// </summary>
    internal Dictionary<WhileNode, int> LoopIterationCounters { get; } = [];

    /// <summary>
    /// Runtime dedup set of <see cref="EventId"/>s whose matching transition has already
    /// committed (EV-031/EV-032). Checked before buffering or resuming so a duplicate delivery
    /// never causes a second continuation.
    /// </summary>
    public HashSet<EventId> ConsumedEventIds { get; } = [];

    /// <summary>Test/inspection surface: number of events currently buffered and unconsumed.</summary>
    public int PendingMailboxCount => Mailbox.Count;

    /// <summary>Test/inspection surface: whether <paramref name="eventId"/> has committed a resume.</summary>
    public bool IsEventConsumed(EventId eventId) => ConsumedEventIds.Contains(eventId);

    /// <inheritdoc />
    public IReadOnlyList<ActiveWaitSnapshotSource> ActiveWaitSources()
    {
        if (ActiveJoin is { } join)
        {
            List<ActiveWaitSnapshotSource>? branchWaits = null;
            foreach (var branch in join.Branches)
            {
                if (branch.ActiveWait is { Status: WaitStatus.Active } branchWait)
                {
                    (branchWaits ??= []).Add(new ActiveWaitSnapshotSource(
                        branchWait.WaitId, branchWait.EventName, branchWait.CorrelationId, branchWait.RegisteredAt, branchWait.BranchId));
                }
            }

            return branchWaits ?? [];
        }

        if (ActiveWait is { Status: WaitStatus.Active } wait)
        {
            return [new ActiveWaitSnapshotSource(wait.WaitId, wait.EventName, wait.CorrelationId, wait.RegisteredAt, wait.BranchId)];
        }

        return [];
    }

    /// <summary>
    /// T1-13/CR-021: returns a copy of <see cref="State"/>, never the live reference. Uses a
    /// JSON round-trip (the only serializer whitelisted in core, per 00-stack-decisions.md) as
    /// the simplest reflection-free way to copy an arbitrary business-state type - see the T1-13
    /// PROGRESS.md deviation note for why this was chosen over a hand-rolled clone framework.
    /// </summary>
    public object GetStateCopy()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(State);
        return JsonSerializer.Deserialize<TState>(json)!;
    }
}
