# OrcaCore.Providers.RabbitMq

RabbitMQ dispatcher plugin for OrcaCore durable outbox records.

## Implemented ports

- `IMessageDispatcher`

## Delivery envelope

The dispatcher publishes committed outbox records after the workflow commit boundary. It uses
RabbitMQ publisher confirms and reports normalized outcomes:

- confirmed publish -> `DispatchResult.Success`
- transient broker or connection failure -> `DispatchResult.RetryableFailure`
- mandatory unroutable publish -> `DispatchResult.PermanentFailure`

This is an at-least-once dispatch adapter. Consumers must be idempotent. The plugin makes no exactly-once delivery claim.

## Scope

This package does not implement event ingestion, workflow definitions, consumer callbacks, or
Kubernetes adapters. Broker exchange, queue, retry, and dead-letter topology are host or
deployment configuration concerns.
