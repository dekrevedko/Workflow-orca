using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Engine;

public sealed class DurableWorkflowEngineOptions
{
    private IPayloadSchemaResolver _payloadSchemaResolver = DurablePayloadTypeRegistry.Default;
    private IPayloadEnvelopeSerializer? _payloadEnvelopeSerializer;
    private IMessageDispatcher? _messageDispatcher;

    public bool AutoDispatchOutbox { get; init; } = true;

    public TimeSpan OutboxDispatchPollingInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan OutboxLeaseDuration { get; init; } = TimeSpan.FromMinutes(1);

    public IPayloadSchemaResolver PayloadSchemaResolver
    {
        get => _payloadSchemaResolver;
        init => _payloadSchemaResolver = value ?? throw new ArgumentNullException(nameof(value));
    }

    public IPayloadEnvelopeSerializer PayloadEnvelopeSerializer
    {
        get => _payloadEnvelopeSerializer ??= new JsonPayloadEnvelopeSerializer(_payloadSchemaResolver);
        init => _payloadEnvelopeSerializer = value ?? throw new ArgumentNullException(nameof(value));
    }

    public IMessageDispatcher? MessageDispatcher
    {
        get => _messageDispatcher;
        init => _messageDispatcher = value;
    }

    public IMessageDispatcher? OutboxDispatcher
    {
        init => _messageDispatcher = value;
    }

    public IOutboxPumpDelayStrategy? OutboxPumpDelayStrategy { get; init; }

    public IOutboxPumpObserver? OutboxPumpObserver { get; init; }

    public int MaxOutboxDispatchAttempts { get; init; } = 5;

    public IOutboxPoisonHandler? OutboxPoisonHandler { get; init; }
}
