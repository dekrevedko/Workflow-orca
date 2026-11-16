## Why

PR #23 scoped ephemeral start reservations to the exact idempotency key.
The shutdown contract still describes the former start semaphore.

## What Changes

- Specify reservation ownership and same-key admission rechecks at Closed.
- Align MG-066 and AC-546 with the merged shutdown behavior.

## Capabilities

### Modified Capabilities

- `management-and-querying`: govern ephemeral shutdown and start admission.

## Impact

Shutdown documentation, existing acceptance evidence, and OpenSpec provenance.
