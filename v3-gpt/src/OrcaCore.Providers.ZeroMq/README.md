# OrcaCore.Providers.ZeroMq

ZeroMQ dispatch is a brokerless `IMessageDispatcher` adapter over NetMQ.

Delivery envelope:

- OrcaCore still has at-least-once outbox intent: records remain retryable until the dispatcher reports success.
- ZeroMQ is brokerless here, so delivery guarantees are weaker than broker-backed dispatchers.
- The adapter reports peer unavailability as retryable failure.
- It does not provide durable broker acknowledgements, persistent queues, poison routing, or external-system exactly-once delivery.

Implemented ports:

- `IMessageDispatcher`
