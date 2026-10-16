using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Outbox;

public interface IOutboxPumpObserver
{
    void OnDispatchSucceeded(OutboxRecord record);

    void OnDispatchFailed(OutboxRecord record, Exception exception);

    void OnCycleCompleted(OutboxPumpCycleResult result);
}
