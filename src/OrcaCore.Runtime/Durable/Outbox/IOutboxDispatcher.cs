using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Outbox;

public interface IOutboxDispatcher
{
    Task DispatchAsync(OutboxRecord record, CancellationToken cancellationToken);
}
