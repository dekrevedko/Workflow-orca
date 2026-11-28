## Why

A stalled DAG observer accumulates a durable outbox record on every append. One deliverable hint already prompts an authoritative run read. Replacing that hint on every append can also invalidate a claim faster than a busy recipient can acknowledge it.

## What Changes

Persist the first generated addressed wake that still passes the accepted subscription cursor. Replace a live wake only after the observer has caught up. Fence acknowledgements, releases and poison from a superseded claim.

## Capabilities

### Modified Capabilities

- `durable-persistence-and-outbox`: Bound generated live addressed DAG wakes per registration and define both orders of cursor renewal and append.

## Impact

In-memory, PostgreSQL and SQL Server providers implement the same outbox rule. PostgreSQL uses a first-create schema and SQL Server changes its state document format. Provision fresh prerelease databases for this change. The public provider ports and DAG wait API retain their signatures.
