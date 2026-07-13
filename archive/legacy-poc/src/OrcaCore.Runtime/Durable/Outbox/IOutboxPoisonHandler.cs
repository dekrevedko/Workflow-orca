using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Outbox;

public interface IOutboxPoisonHandler
{
    Task HandleAsync(OutboxRecord record, Exception exception, CancellationToken cancellationToken);
}
