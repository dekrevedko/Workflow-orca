# Task 4.3 post-gate amendment path

Date: 2026-08-18

This record defines the mandatory re-entry path for a decision approved after its original
implementation-section approval and canonical-synchronization gates have completed. A completed
historical gate remains immutable evidence; it does not authorize later decisions silently.

## Required stages

Every post-gate amendment must record all eight stages below in the machine-readable companion:

1. `amendment-approval`;
2. `canonical-open-spec`;
3. `numbered-requirements`;
4. `acceptance-criteria`;
5. `implementation-tasks`;
6. `executable-evidence`;
7. `refreeze`; and
8. `independent-approval`.

Task references carry an expected `Open` or `Complete` state. Completing a downstream task without
refreezing the fixture is therefore visible drift rather than an implicit success. Evidence paths
must exist, approval verdicts are line-ending normalized and must identify the exact task plus an
`APPROVE` verdict, and the post-gate record must match the change/capability/count tuple that
triggered it.

Normal archival is supported. Task lookup resolves exactly one active or dated archived change
record, and every amendment embeds the exact requirement identities that must still exist in that
resolved record. The Section 7B record therefore remains verifiable after its owning change moves
under `openspec/changes/archive/`; it does not depend on rows emitted only by active deltas.
Archival still changes the active provenance rows and requires the ordinary reviewed
provenance-fixture refreeze defined by task 4.2.

## Existing Section 7B amendment

`reshape-developer-facing-interfaces` declared `developer-facing-surface` (18) as new, task 4.15 approved
the original contract, and task 10.14 synchronized the original canonical baseline. Task 7.23 then
approved the later Section 7B event/fanout/start-or-deliver/publish amendment. The current delta has
18 requirements while seven canonical operations remain pending.

The post-gate record therefore preserves the following exact disposition:

- amendment approval: reshape task 7.23 is complete;
- canonical reconciliation: harmonize task 5.1 remains open and owns
  `developer-facing-surface` (7);
- numbered event/wait requirements and acceptance criteria: harmonize task 7.3 remains open and
  names `docs/specs/05-requirements-events-waits-timers.md` and
  `docs/specs/12-acceptance-criteria.md`;
- implementation: exactly 11 reshape tasks, 7.24 through 7.34, are complete;
- refreeze and independent approval: reshape task 7.22, its frozen manifest, and its dated verdict
  are complete and retained; and
- executable evidence: the facade/hosting boundary, durable publish, definition fanout, and
  start-or-deliver certifications remain named repository paths.

This closes the missing route owned by task 4.3. It does not pre-close tasks 5.1 or 7.3 and does not
report the seven canonical mismatches as synchronized.

The companion embeds the exact 18 requirement headings from the Section 7B
`developer-facing-surface` delta. Those identities, rather than the active-delta aggregate, are the
permanent amendment boundary; current canonical state is recomputed from the resolved active or
archived delta whenever the guard runs.

## Machine-readable companion

The executable record is
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json`. The task 4.2
provenance fixture links the 18-row declared-new/already-canonical disposition to the exact
`reshape-section-7b-developer-facing-surface` record rather than to an open 4.3 checkbox.

## Checkpoint validation determinism

Concurrent infrastructure-lane stress reproduced the pre-existing 3.11c lease-recovery flake at
`RunTimedOutRetryAsync`: the product had observed the deterministic first-attempt timeout, but a
30-second wall-clock wait expired before the scheduler started the retry while sibling lanes needed
more than one minute under load. The shared lease-exit/recovery fixture now awaits its existing
workflow-owned start and completion signals, asserts that the retry actually started, and carries no
`WaitAsync(TimeSpan...)` gate. The infrastructure contract guard scans both fixture source files so
the scheduler-sensitive wait cannot silently return.
