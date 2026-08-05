using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class RuntimeWaitRecord
{
    internal RuntimeWaitRecord(
        string eventName,
        CorrelationId correlationId,
        BranchId? branchId,
        DateTimeOffset registeredAt,
        string authoredPath,
        DateTimeOffset? deadline,
        Func<EventEnvelope, CancellationToken, Task> resumeAsync,
        long waitSequence,
        FiberId? fiberId,
        ScopeId? scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(authoredPath);
        ArgumentNullException.ThrowIfNull(resumeAsync);

        WaitId = WaitId.Parse(Guid.CreateVersion7().ToString());
        EventName = eventName;
        CorrelationId = correlationId;
        BranchId = branchId;
        RegisteredAt = registeredAt;
        AuthoredPath = authoredPath;
        Deadline = deadline;
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

    internal string AuthoredPath { get; }

    internal DateTimeOffset? Deadline { get; }

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
        return Status == "Active" &&
            string.Equals(EventName, envelope.EventName.Value, StringComparison.Ordinal) &&
            CorrelationId.Equals(envelope.CorrelationId);
    }

    internal void MarkMatched()
    {
        Status = "Matched";
    }

    internal void MarkActive()
    {
        Status = "Active";
    }

    internal EphemeralActiveWaitSnapshot ToSnapshot()
    {
        return new EphemeralActiveWaitSnapshot
        {
            WaitId = WaitId,
            EventName = EventName,
            CorrelationId = CorrelationId,
            RegisteredAt = RegisteredAt,
            AuthoredPath = AuthoredPath,
            Deadline = Deadline,
            BranchId = BranchId?.ToString(),
            Status = Status,
            Mode = Mode,
            WaitSequence = WaitSequence,
            FiberId = FiberId,
            ScopeId = ScopeId
        };
    }
}
