using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Outbox;

public sealed record OutboxDispatchItemResult(
    OutboxRecord Record,
    bool Succeeded,
    Exception? Error);
