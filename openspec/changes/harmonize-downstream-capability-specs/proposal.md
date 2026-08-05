Authoritative product contract: [`reshape-developer-facing-interfaces`](../reshape-developer-facing-interfaces/proposal.md).
This change owns only downstream canonical/documentation harmonization and the two non-overlapping
capability deltas named below. It introduces repository workflow and provenance requirements but no
new product runtime semantics.

## Why

Earlier canonical synchronization was delta-scoped and ran before later approved amendments. That
left some canonical and active documentation claims stale, allowed two changes to own the same
requirement headings, and provided no reverse check that every canonical spec still agreed with the
approved contract.

`reshape-developer-facing-interfaces` now contains complete deltas for event routing/waits, durable
persistence/outbox, repository friend ownership, and durable resource governance. Retaining copies
here would create silent last-writer-wins synchronization even where the text is currently equal.
This change therefore relinquishes those four capabilities and retains only unique downstream work.

## What Changes

- Record `event-driven-prototype` as planning history outside the first release. Future prototype
  work requires a separate reviewed capability amendment.
- Remove the residual `WaitLong` distinction from `state-driven-runtime`; ephemeral `Wait`
  residency remains runtime/hosting policy and has no restart-safe guarantee.
- Remove duplicate delta ownership for `event-routing-and-waits`,
  `durable-persistence-and-outbox`, `repository-foundation`, and `durable-runtime`; all four are
  owned exclusively by `reshape-developer-facing-interfaces`.
- Add a canonical-consistency process that enumerates every canonical capability and every active
  delta heading, rejects uncoordinated duplicate ownership, and verifies provenance rather than
  treating structural strict validation as semantic approval.
- Reconcile numbered requirements, acceptance criteria, current guides, the future-capability
  registry, archive classification/history, and active vocabulary with the approved reshape target.
- Preserve every immutable prior review and require a new dated superseding approval before
  canonical synchronization or a checkpoint.

## Capabilities

### Modified Capabilities

- `event-driven-prototype`: Records the prototype as outside v1 and requires a future reviewed
  amendment before any project, package, or public surface returns.
- `state-driven-runtime`: Restates ephemeral limitations without the removed `WaitLong` concept and
  keeps ordinary `Wait` residency as runtime/hosting policy.

## Impact

- No product runtime `src/`, behavior tests, samples, packages, provider schema, or runtime behavior
  is changed by this planning change. Planning may require documentation/OpenSpec corrections and
  narrowly scoped infrastructure guard tests that enforce the approved synchronization contract.
- Canonical OpenSpec synchronization is limited to the two unique deltas after independent
  approval. Messaging, persistence/outbox, friend-graph, and governance requirements remain
  exclusively owned and synchronized by `reshape-developer-facing-interfaces`.
- The change adds cross-tree process and documentation work: canonical provenance checks,
  post-gate amendment routing, numbered requirement/acceptance coverage, active-document vocabulary
  and link checks, exact archive/history handling, and a final immutable approval/checkpoint gate.
- The committed Section 7 checkpoint remains a recovery anchor only. Neither this change nor strict
  validation authorizes Section 8; both changes must complete their approval and checkpoint gates.
