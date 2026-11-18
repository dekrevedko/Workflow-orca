## Why

The current runtime can publish from a stale execution position, accept a step result after its deadline, and confuse distinct definition or registration identities. Reviewed source repairs exist; their protocol declaration, provider evidence and exact baselines must now agree with the product contract before Phase A closes.

## What Changes

- Persist `WorkflowExecutionPositionChangedEvent` with every yield/fiber-failure checkpoint so a changed position advances optimistic concurrency even without another fact. Load the envelope and aggregate from one captured checkpoint; quarantine stable unreconciled tails.
- Declare the additive workflow codec entry, preserving all existing workflow discriminators and DAG record framing.
- **BREAKING:** complete compiler format 2 structural encoding with length-prefixed hexadecimal UTF-16 fields, invariant scalar formatting and ordinal schema ordering. Format 2 is unreleased; no legacy alias or development-data migration is promised.
- Preserve clock-based attempt election and physical ownership, and compare frozen host options structurally. Add concrete regression and real-provider obligations for these existing guarantees.
- Refresh exact public API, source provenance and executable-test inventory; integrate the separately reviewed PostgreSQL checkpoint-lineage correction from the merged DAG work.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `durable-runtime`: add precise checkpoint-position concurrency and coherent-load invariants under the existing crash-safe execution guarantees.
- `durable-persistence-and-outbox`: declare the additive workflow position fact and unchanged prior codec entries.
- `workflow-contracts`: specify injective, culture-independent compiler format 2 field encoding.
- `quality-and-verification`: add executable concurrency, deadline, identity and provider regression obligations.

## Impact

Owning numbered declarations: docs/specs 04, 06, 10, 12, 16 and the advanced protocol section of 17. Document 03 retains the existing separate binding identities. The public authoring companion has no protocol-event declaration and remains unchanged. The advanced `OrcaCore.Runtime.Protocol` API gains one record; no provider read port or public application operation is added.

The existing broad canonical requirements remain valid. New, specialized ADDED requirement headings avoid changing unrelated DAG or facade owner chains. The original 21 workflow codec entries remain unchanged; the new entry is workflow-only. Fresh/reset stores and matched protocol/provider binaries are the qualification target. SQL Server correctness certification does not promote its storage scale/support status. DAG facade activation, DAG join repair, shared codec fidelity, distributed output observation and SQL support-contract publication remain with their existing owners.

This is the owning product change for Phase A of the accepted architecture remediation plan, not a permanent program capability. Source checkpoints `c27b0b4e`, `1aab3e1b` and `2fe5b9c6` have scoped independent source reviews; this change does not retroactively label those reviews as normative or real-provider acceptance.
