## Why

A durable workflow can finish on a different runtime while a caller waits for its output. The observing runtime receives no local commit notification, so a caller who can already query the committed output can remain asleep indefinitely.

## What Changes

- Subscribe to local commit hints before the first authoritative read. After each pending durable workflow output read, schedule another read within one second even if no hint arrives.
- Retain the local fast path, serialize each caller's reads, and release the observer on completion, cancellation, host shutdown, owner disposal or query failure.
- Keep ephemeral output, physical settlement and DAG terminal observation on their existing notification protocols. DAG observer fan-out and backlog remain open for separate measurement.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `developer-facing-surface`: Define how the workflow handle and start helper observe remote completion.
- `workflow-contracts`: Specify mode-specific typed output waiting while preserving result and cancellation semantics.
- `management-and-querying`: Bound durable output refresh and retain the separate DAG terminal fault and notification rules.
- `quality-and-verification`: Require cross-host, clock, lifecycle, read-rate and registration-race evidence.

## Impact

The durable workflow facade and its hosting lifecycle own the change. The public handle signatures, provider ports, storage schema, and DAG observer writes stay compatible. Each pending durable waiter adds at most one timer-driven query per elapsed second of pending time, plus registration and local-hint reads.
