# Durable Development Store Reset

Structured-fiber execution writes checkpoint content type
`application/vnd.orcacore.durable-envelope.v2+json`. The provisional format-1 cursor
envelope is intentionally unsupported and has no compatibility executor or automatic
migration. This is an active-development decision: there are no supported production
instances to preserve.

## When a reset is required

Reset a development store or fixture when either condition is true:

- a checkpoint has content type `application/vnd.orcacore.durable-envelope.v1+json`;
- a format-2 checkpoint has a compiler format, definition version, or plan fingerprint
  that no longer matches the registered definition.

The runtime parks these instances with a visible runtime-state-version or version-binding
diagnostic. Registering a new definition does not reinterpret or silently unpark them.

## Test fixtures

Repository tests construct format-2 envelopes directly. Delete local copied snapshots or
golden payloads created before this refactor and regenerate them from the current helpers.
The Testcontainers-based PostgreSQL suite creates a disposable database; stopping and recreating
the failed test container is sufficient.

The in-memory provider has no on-disk state. Restart the test or host process to reset it.

## PostgreSQL development database

Use a dedicated development database. Connect to a different database such as `postgres`,
drop and recreate the OrcaCore development database, then let the provider migration runner
create the schema again.

```sql
DROP DATABASE orcacore_dev WITH (FORCE);
CREATE DATABASE orcacore_dev;
```

Do not run this against a shared or production database. OrcaCore does not provide an
in-place format-1-to-format-2 migration.

## Verification

After reset, start a new durable instance and inspect its checkpoint. The content type must
be format 2. A stale payload must park with an explicit diagnostic and must never resume by
selecting cursor execution code.
