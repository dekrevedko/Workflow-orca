# Harmonization Task 6.6 second post-review hardening independent review request

**Date:** 2026-09-14
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 6.6 second post-review-hardening checkpoint

The reviewed base is approval-activation commit `7e399826a25f2aeefe171475dcd51853ad145e2f`,
whose linear parent chain contains the approved Task 6.6 remediation checkpoint
`2581928de06d3c7f4b0db306a274d8a34d47a258`, immutable approval evidence
`dc79672e6a233b1602e0db4e01533465c17636e1`, and mechanical activation. Both prior Task 6.6
approval verdicts are registered byte-exactly.

The exact target is named by
`harmonize-downstream-capability-specs-task-6-6-second-post-review-hardening-dirty-manifest-2026-09-14.txt`.
Recompute the commit-real raw manifest and scoped content record from the base above. The
self-referential review-provenance fixture is the only content-record exclusion.

## Findings to verify closed

1. **EE-1:** the approved remediation artifact remains unchanged; a separately guard-hashed dated
   addendum records both citation-only insertions with the literal backticks around
   `docs/specs/13-phasing-and-open-questions.md` and distinguishes correction of the earlier
   presentation from any normative change.
2. **FF-1:** canonical provenance validation enumerates every top-level
   `artifacts/*openspec-provenance-*.md` file and requires exact equality with the fixture's current
   artifact plus the permanent guard-source superseded-artifact catalog.
3. **GG-1:** identifier-token checks reject `WaitLong` and `Yield` across the complete §13.4 region
   before the removed-concepts subsection, including the registry preamble; the removed subsection
   still requires both tokens and longer identifiers remain valid.
4. Task 6.6 ledger and design text name all three closures, and guard source pins those complete
   decisions plus the dated addendum's exact SHA-256 independently of the active freeze.

## Negative controls to reproduce

- remove the literal path backticks from the dated addendum: Task 6.6 guard red;
- add an unregistered `*openspec-provenance-*.md` artifact: canonical-provenance guard red;
- add `` `WaitLong` is planned future work. `` to the §13.4 preamble: Task 6.6 guard red;
- add `WaitLongAsync` and `YieldPolicy` to the same preamble: Task 6.6 guard remains green;
- weaken the ledger/design hardening decision or drift the addendum: Task 6.6 guard red.

## Validation to reproduce

- Debug and Release non-incremental warnings-as-errors builds: 0 warnings / 0 errors;
- Core 350; Ephemeral 79; Durable 99; Acceptance 37; Hosting 24; Provider Certification 96;
- PostgreSQL 101; SQL Server 72; Integration 11;
- Infrastructure 221/221 and exactly 14 separately classified intentional expected reds, for 235
  total guard cases;
- OpenSpec strict 18/18 and harmonization ledger 24 complete / 10 open / 34 total;
- review-manifest refresh `-Check` green, `git diff --check` clean, and zero changes under `src/**`.

Do not edit, stage, or commit the target. Return one dated immutable `APPROVE` or `REJECT` verdict.
