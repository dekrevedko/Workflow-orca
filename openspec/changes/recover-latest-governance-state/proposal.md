## Why

Every governance record contains the full state of its partition. Startup, management and lease commands currently load and decode every record before reading or changing the current state. The cost rises with retained history even when the current pools and obligations stay fixed.

## What Changes

- Add a required provider read for the committed partition version and its latest full-state record. Recover and make decisions from that record.
- Preserve the full-history read for explicit inspection, consecutive expected-version append and the existing v1 format.
- Certify fail-closed handling of a damaged head, retained ownership and retry identities, and distinguish indexed PostgreSQL recovery from the experimental SQL Server document costs.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `durable-runtime`: Recover a partition from its latest complete governance state and fence appends at that version.
- `management-and-querying`: Restore pool capacity and debt from the committed state and preserve operation replay.
- `quality-and-verification`: Certify current-state recovery and explicit history inspection independently and measure the physical read costs.

## Impact

The provider port and the Runtime.Protocol assembly gain one required operation and one result type. In-memory, PostgreSQL, experimental SQL Server and custom providers implement the new operation. The database schema, persisted v1 envelope, administrative APIs and four lease handoff barriers keep their existing formats.
