# OrcaCore.Providers.Redis

Redis is a projection/cache provider profile for OrcaCore durable metadata.

Implemented ports:

- `IWorkflowProjectionStore`

Non-goals:

- Redis does not advertise or implement `IWorkflowEventStore`.
- Redis does not provide durable inbox, outbox, timer, resource-pool, or retention semantics.
- Redis projection queries are metadata-only and do not require business-payload deserialization.

Operational notes:

- Register the projection cache explicitly with `AddOrcaCoreRedisProjectionCache(...)`.
- Redis is treated as a trusted projection boundary and must be protected with ACLs,
  network isolation, and tenant-separated keyspaces.
- Adapter-backed snapshots are stored with a versioned checksum envelope; malformed or
  tampered blobs are ignored by list/get paths instead of being surfaced as projections.
- Metadata filters use secondary Redis sets for definition, status, root, and parent
  candidates before reading snapshot payloads.
