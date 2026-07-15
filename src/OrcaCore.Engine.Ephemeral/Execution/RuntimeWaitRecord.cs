using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class RuntimeWaitRecord
{
    internal RuntimeWaitRecord(
        string eventName,
        CorrelationId correlationId,
        BranchId? branchId,
        DateTimeOffset registeredAt,
        Func<EventEnvelope, CancellationToken, Task> resumeAsync,
        long waitSequence,
        FiberId? fiberId,
        ScopeId? scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(resumeAsync);

        WaitId = WaitId.New();
        EventName = eventName;
        CorrelationId = correlationId;
        BranchId = branchId;
        RegisteredAt = registeredAt;
        ResumeAsync = resumeAsync;
        WaitSequence = waitSequence;
        FiberId = fiberId;
        ScopeId = scopeId;
    }

    internal WaitId WaitId { get; }

    internal string EventName { get; }

    internal CorrelationId CorrelationId { get; }

    internal BranchId? BranchId { get; }

    internal DateTimeOffset RegisteredAt { get; }

    internal long WaitSequence { get; }

    internal FiberId? FiberId { get; }

    internal ScopeId? ScopeId { get; }

    internal string Status { get; private set; } = "Active";

    internal string Mode { get; } = "Resident";

    internal Func<EventEnvelope, CancellationToken, Task> ResumeAsync { get; }

    private Action? cancelLoser;

    internal void SetCancelLoser(Action cancel)
    {
        ArgumentNullException.ThrowIfNull(cancel);

        cancelLoser = cancel;
    }

    internal void CancelLoser()
    {
        cancelLoser?.Invoke();
        cancelLoser = null;
    }

    internal bool Matches(EventEnvelope envelope)
    {
        if (!string.IsNullOrWhiteSpace(envelope.BranchId) &&
            !string.Equals(BranchId?.ToString(), envelope.BranchId, StringComparison.Ordinal))
        {
            return false;
        }

        return Status == "Active" &&
            string.Equals(EventName, envelope.EventName, StringComparison.Ordinal) &&
            CorrelationId == envelope.CorrelationId;
    }

    internal void MarkMatched()
    {
        Status = "Matched";
    }

    internal void MarkActive()
    {
        Status = "Active";
    }

    internal ActiveWaitSnapshot ToSnapshot()
    {
        return new ActiveWaitSnapshot
        {
            WaitId = WaitId,
            EventName = EventName,
            CorrelationId = CorrelationId,
            RegisteredAt = RegisteredAt,
            BranchId = BranchId?.ToString(),
            Status = Status,
            Mode = Mode,
            WaitSequence = WaitSequence,
            FiberId = FiberId,
            ScopeId = ScopeId
        };
    }
}
