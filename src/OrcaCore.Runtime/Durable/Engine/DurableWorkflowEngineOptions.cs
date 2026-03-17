using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Engine;

public sealed class DurableWorkflowEngineOptions
{
    public IDurablePayloadTypeResolver PayloadTypeResolver { get; init; } = DurablePayloadTypeRegistry.Default;

    public bool AutoDispatchOutbox { get; init; } = true;

    public TimeSpan OutboxDispatchPollingInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    public IOutboxDispatcher? OutboxDispatcher { get; init; }

    public IOutboxPumpDelayStrategy? OutboxPumpDelayStrategy { get; init; }

    public IOutboxPumpObserver? OutboxPumpObserver { get; init; }

    public int MaxOutboxDispatchAttempts { get; init; } = 5;

    public IOutboxPoisonHandler? OutboxPoisonHandler { get; init; }
}
