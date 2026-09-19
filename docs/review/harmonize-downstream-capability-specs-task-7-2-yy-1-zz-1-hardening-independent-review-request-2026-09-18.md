# Harmonization Task 7.2 YY-1 / ZZ-1 hardening independent review request

**Date:** 2026-09-18
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 7.2 post-approval hardening checkpoint

Review this target on immutable base
`e046e04ab4dae82490604adbbc9c7baca402c59b`. Do not edit, stage, or commit it.
The raw-order manifest is `harmonize-downstream-capability-specs-task-7-2-yy-1-zz-1-hardening-dirty-manifest-2026-09-18.txt`.
The approved Task 7.2 checkpoint is `cfa5f2ab5b97272856e683c2a42bce2482010645`,
the approval-evidence commit is `4cef1481419b1abf5e50bc14c5c662b4aad4f8ce`, and the
activation commit is the base above. Task 7.3 remains blocked.

## YY-1 closure

Both Git addition-history queries in `ValidateAppendOnlyHistoricalRecords` now pass
`--full-history`: the repository-wide path discovery and each catalog entry's first-addition lookup.
This keeps a record visible when it was added and deleted on a side branch that was later merged.
The design and completed Task 7.2 record state this traversal explicitly and are pinned by guard-owned
exact text.

Required mutation: create a side branch that adds a historical record, commits its deletion, and is
then merged. With the catalog omitting that path, the merged mainline must fail and name the path.
Removing `--full-history` from either owning query must restore the reviewed failure mode rather than
producing an unrelated error.

## ZZ-1 closure

Open Task 7.7 no longer offers a history-preserving rename under the current no-relocation contract.
It requires an immutable archive provenance record naming the exact predecessor and commit, forbids
renaming/deleting/editing an existing protected path, and says a future relocation first requires a
separately reviewed tombstone mechanism. The Task 7.2 guard pins the whole Task 7.7 decision.

Required mutations: restore the old rename option, remove the predecessor/commit requirement, or claim
that relocation is currently allowed. Each must fail the focused guard.

## Regression and validation

Reproduce the Task 7.2 and review-manifest guards in dirty and simulated-checkpoint states, the full
Infrastructure and ExpectedRed lanes separately, strict OpenSpec validation, task accounting, and
`git diff --check`. Confirm no `src/**` or canonical `openspec/specs/**` path changes.

## Exact scope

The exact target is the companion raw-order manifest. It contains only the two post-approval findings,
their durable wording/guard pins, the two evidence registries, and this request/manifest. This approval
does not authorize Task 7.3.