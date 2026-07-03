# OrcaCore.Providers.Redis

Redis is a projection/cache provider profile for OrcaCore durable metadata.

Implemented ports:

- `IWorkflowProjectionStore`

Non-goals:

- Redis does not advertise or implement `IWorkflowEventStore`.
- Redis does not provide durable inbox, outbox, timer, resource-pool, or retention semantics.
- Redis projection queries are metadata-only and do not require business-payload deserialization.
