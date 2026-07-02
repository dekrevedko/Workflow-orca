using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class RuntimeWaitRecord
{
    internal RuntimeWaitRecord(
        string eventName,
        CorrelationId correlationId,
        DateTimeOffset registeredAt,
        Func<EventEnvelope, CancellationToken, Task> resumeAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(resumeAsync);

        WaitId = WaitId.New();
        EventName = eventName;
        CorrelationId = correlationId;
        RegisteredAt = registeredAt;
        ResumeAsync = resumeAsync;
    }

    internal WaitId WaitId { get; }

    internal string EventName { get; }

    internal CorrelationId CorrelationId { get; }

    internal DateTimeOffset RegisteredAt { get; }

    internal string Status { get; private set; } = "Active";

    internal string Mode { get; } = "Resident";

    internal Func<EventEnvelope, CancellationToken, Task> ResumeAsync { get; }

    internal bool Matches(EventEnvelope envelope)
    {
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
            Status = Status,
            Mode = Mode
        };
    }
}
