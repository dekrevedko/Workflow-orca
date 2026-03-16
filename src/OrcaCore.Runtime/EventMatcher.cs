using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

internal static class EventMatcher
{
    public static WaitRecord? FindMatch(IEnumerable<WaitRecord> activeWaits, EventEnvelope envelope)
    {
        return activeWaits.FirstOrDefault(w =>
            w.Status == WaitStatus.Active
            && w.EventName == envelope.EventName
            && w.CorrelationId == envelope.CorrelationId);
    }
}
