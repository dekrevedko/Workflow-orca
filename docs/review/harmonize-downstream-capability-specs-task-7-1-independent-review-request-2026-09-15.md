# Harmonization Task 7.1 independent review request

**Date:** 2026-09-15
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 7.1 documentation/guard checkpoint

The implementation and first validation pass began on 2026-09-14; the freeze date rolled to
2026-09-15. The reviewed base is Task 6.6 second-hardening approval activation commit
`99657834684deefa2d22cb6526d714c4566b7ae0`. Its linear parent chain contains the exact reviewed
checkpoint `7ce561911bf5a9f1aefc42b2d86fcede16bb4148`, immutable approval-evidence commit
`058ddeb616c0a6a10fbd098a984e466d3c9a8935`, and the two-line mechanical activation. The Task 6.6
verdict dated 2026-09-14 is registered byte-exactly.

The exact target is named by
`harmonize-downstream-capability-specs-task-7-1-dirty-manifest-2026-09-15.txt`. Recompute the
commit-real raw manifest and scoped content record from the base above. The self-referential
review-provenance fixture is the only content-record exclusion.

## Task 7.1 obligations to verify

1. Re-run Task 3.1's positive removed/deferred API call scan over root guidance, active
   documentation, canonical OpenSpec, and every active change proposal/design/task ledger/delta.
   The dated record must reproduce 86 sources, 10,655 record bytes, SHA-256
   `74fc76977157666e7f018d1449c9729e5d0112c5ec395f98b6b75336c83774f6`, and zero findings.
2. Verify no active source teaches callable `WaitLong`, authored `Yield`, `WhenFirst`, Saga,
   `RunExternalJob`, `RunChild`, `RunChildren`, or public pause/resume/archive/purge/cancel forms.
3. Verify the ephemeral, Kubernetes scheduler, and Orleans guides retain concise deferred/removed
   notes and exact §13.4 future-capability-registry re-entry links. The Kubernetes guide is the only
   guide content changed by this task.
4. Confirm Task 7.1 is complete at 25/34 tasks and the dated artifact is independently pinned by
   guard source at normalized SHA-256
   `23e75f842bddace81e93d472899b1c822f1d9bb4929e55bf6d6336b18bc32528`.

## Carried review observations to verify closed

- **HH-1:** OpenSpec provenance artifact discovery is recursive across all
  `openspec/changes/**`, including nested superseded/archive locations, and still requires exact
  equality with the current record plus the permanent superseded catalog.
- **II-1:** the removed-concepts region ends at the next `###` subsection; removed identifiers are
  rejected everywhere else in §13.4, including a subsection appended after `Removed concepts`,
  while longer identifiers such as `WaitLongAsync` and `YieldPolicy` remain valid.

## Negative controls to reproduce

- add `WhenFirst()` to an active guide: Task 7.1 guard red;
- break any one of the three guide registry anchors: Task 7.1 guard red;
- add an unregistered nested `*openspec-provenance-*.md`: canonical provenance guard red;
- append a later §13.4 subsection promising `WaitLong`: Task 6.6 guard red;
- replace that token with `WaitLongAsync` and `YieldPolicy`: Task 6.6 guard remains green;
- drift the dated Task 7.1 artifact, completion decision, or design decision: Task 7.1 guard red.

## Validation to reproduce

- Debug and Release non-incremental warnings-as-errors builds: 0 warnings / 0 errors;
- Core 350; Ephemeral 79; Durable 99; Acceptance 37; Hosting 24; Provider Certification 96;
- PostgreSQL 101; SQL Server 72; Integration 11;
- Infrastructure 222/222 and exactly 14 separately classified intentional expected reds, for 236
  total guard cases;
- focused Task 6.6/7.1/crosswalk/accounting controls 6/6;
- OpenSpec strict 18/18 and harmonization ledger 25 complete / 9 open / 34 total;
- `git diff --check` clean, zero positive-call findings, zero `src/**` changes, and zero canonical
  `openspec/specs/**` changes.

The implementation self-review mutation-tested HH-1, II-1, identifier-boundary non-false-positive
behavior, a positive `WhenFirst()` call, and a broken guide anchor; every negative case failed and
the final control passed after byte-identical restoration.

Do not edit, stage, or commit the target. Return one dated immutable `APPROVE` or `REJECT` verdict.