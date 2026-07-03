using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Runtime-owned wait record (EV-021) created when an instance suspends on
/// <see cref="Abstractions.Steps.StepResult.WaitForEvent"/> or a <see cref="WaitNode"/>.
/// Internal only — callers observe waits through the projected <see cref="ActiveWaitSnapshot"/>.
/// </summary>
/// <param name="WaitId">Runtime-owned wait identity.</param>
/// <param name="EventName">Logical event name this wait matches on (EV-020).</param>
/// <param name="CorrelationId">Request-reply identity this wait matches on (EV-020).</param>
/// <param name="RegisteredAt">UTC timestamp when the wait was registered.</param>
/// <param name="BranchId">
/// Placeholder for the enclosing <see cref="ParallelNode"/> branch identity. Always
/// <see langword="null"/> until parallel branch wait isolation lands (T1-12); reserved so the
/// field does not need to be added later.
/// </param>
/// <param name="Mode">Wait residency mode; always <see cref="WaitMode.Resident"/> in ephemeral mode.</param>
/// <param name="Status">Current wait status.</param>
internal sealed record WaitRecord(
    WaitId WaitId,
    string EventName,
    CorrelationId CorrelationId,
    DateTimeOffset RegisteredAt,
    BranchId? BranchId,
    WaitMode Mode,
    WaitStatus Status)
{
    /// <summary>Projects the metadata-only snapshot exposed on <see cref="WorkflowInstanceSnapshot"/>.</summary>
    internal ActiveWaitSnapshot ToSnapshot() => new(WaitId, EventName, CorrelationId, RegisteredAt, Status, Mode);
}
