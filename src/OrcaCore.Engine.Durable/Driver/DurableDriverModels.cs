using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

/// <summary>
/// Executes one durable advancement segment for a registered definition (DR-001 interpreter
/// side). Implementations own no threads, timers, queues, or distribution; the driver host
/// invokes them and they emit kernel commands through the processor seam only.
/// </summary>
internal interface IDurableDriverExecutor
{
    Task<DurableSegmentResult> RunSegmentAsync(
        DurableDriverContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Everything one advancement segment needs: rehydrated facts, persisted position, and the
/// kernel seam. Deterministic inputs per DR-015.
/// </summary>
internal sealed record DurableDriverContext(
    InstanceId InstanceId,
    DurableWorkflowAggregate Aggregate,
    DurableExecutionEnvelopeV2? FiberEnvelope,
    DurableCommandProcessor Processor,
    IWorkflowPayloadSerializer Serializer,
    TimeProvider TimeProvider,
    DurableDriverBudget Budget);

/// <summary>
/// Hard segment budgets (DR-051). Reaching either budget while runnable ends the segment with
/// committed progress and a fresh continuation record; the worker turn is released and the
/// driver later resumes from the persisted position. Budgets are hard limits — unbounded
/// segments are not a supported production configuration.
/// </summary>
public sealed record DurableDriverBudget(int MaxCommandsPerSegment, TimeSpan MaxSegmentDuration)
{
    /// <summary>
    /// DR-OQ-3 defaults, flagged for review: 256 commands / 30 seconds per segment keeps a
    /// segment well below claim lease durations (5 minutes) and grain-turn expectations.
    /// </summary>
    public static DurableDriverBudget Default { get; } = new(256, TimeSpan.FromSeconds(30));
}

/// <summary>
/// Why the driver was asked to advance an instance. Opportunistic drives skip instances whose
/// definition is not driver-registered; required drives (claimed continuations) park them
/// (DR-016).
/// </summary>
internal enum DurableDriveMode
{
    Opportunistic,
    Required
}

/// <summary>
/// How one advancement segment ended.
/// </summary>
internal enum DurableSegmentOutcome
{
    /// <summary>
    /// The instance reached a terminal lifecycle state.
    /// </summary>
    Terminal,

    /// <summary>
    /// Every cursor is suspended on runtime-owned work; nothing is runnable.
    /// </summary>
    Suspended,

    /// <summary>
    /// A yield committed; the instance is immediately runnable again (DR-031).
    /// </summary>
    Yielded,

    /// <summary>
    /// A policy admission checkpoint committed and its successor continuation owns execution
    /// of the decorated step (for example, an absolute timeout deadline).
    /// </summary>
    PolicyBoundary,

    /// <summary>
    /// A segment budget was reached while the instance is still runnable (DR-051).
    /// </summary>
    BudgetExhausted,

    /// <summary>
    /// A fresh generation and runnable position committed; its successor continuation owns
    /// further execution.
    /// </summary>
    ContinuedAsNew,

    /// <summary>
    /// A kernel command was rejected because the stream moved; reload and retry (DR-030).
    /// </summary>
    Conflict,

    /// <summary>
    /// The instance was parked with a diagnostic (DR-017).
    /// </summary>
    Parked,

    /// <summary>
    /// The root sequence is exhausted without an End node; the instance stays as committed
    /// (ephemeral parity), and no further advancement is possible.
    /// </summary>
    Idle
}

internal sealed record DurableSegmentResult(
    DurableSegmentOutcome Outcome,
    string? Message = null,
    bool CommittedProgress = false)
{
    internal static DurableSegmentResult Terminal { get; } = new(DurableSegmentOutcome.Terminal);

    internal static DurableSegmentResult Suspended { get; } = new(DurableSegmentOutcome.Suspended);

    internal static DurableSegmentResult Yielded { get; } = new(DurableSegmentOutcome.Yielded);

    internal static DurableSegmentResult PolicyBoundary { get; } =
        new(DurableSegmentOutcome.PolicyBoundary, CommittedProgress: true);

    internal static DurableSegmentResult ContinuedAsNew { get; } =
        new(DurableSegmentOutcome.ContinuedAsNew, CommittedProgress: true);

    internal static DurableSegmentResult Idle { get; } = new(DurableSegmentOutcome.Idle);
}
