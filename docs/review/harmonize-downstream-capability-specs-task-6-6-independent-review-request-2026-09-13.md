# Harmonization Task 6.6 independent review request

**Date:** 2026-09-13
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 6.6 checkpoint, including the Task 6.5 AA-1
carry-forward remediation already named by Task 6.6

The reviewed base is commit `4f061089bae7602a7d1f255436a0e6d8507c39df`, immediately after the
approved Task 6.5 second-hardening checkpoint, its immutable verdict evidence, and mechanical
activation. The Task 6.5 second-hardening verdict is registered byte-exactly with SHA-256
`e1f472e61d851e18f0538914c625f35bc65a8d919cd90aefc699db5dd7de07d5`.

The exact target is named by
`harmonize-downstream-capability-specs-task-6-6-dirty-manifest-2026-09-13.txt`. Recompute the
commit-real raw manifest and scoped content record from the base above. The self-referential review
provenance fixture is the only content-record exclusion.

## Claims to verify

1. `docs/specs/13-phasing-and-open-questions.md` §13.4 is named exactly "Future-capability
   registry" and separates a deferred-capability table from a removed-concepts subsection.
2. `WaitLong` and authored `Yield` are absent from the deferred table, present in the removed
   subsection, and remain removed rather than promised future capabilities.
3. Canonical `developer-facing-surface` and `saga-orchestration` requirements cite the exact docs
   path/name, and each complete requirement block is byte-identical to its active reshape delta.
4. Active guide anchors and the normative source map use the same identity; the former mismatch
   claim, old heading, and old anchor are absent outside immutable review/archive provenance.
5. The new Task 6.6 infrastructure guard binds the structure, cross-references, active-guide links,
   completed task text, and complete design decision using named semantic constants.
6. Reshape task 9.6 remains the owner of final future-registry membership; Task 6.6 does not hide or
   assume that cleanup.
7. Task 6.5 review finding AA-1 is closed independently of the active freeze by pinning the whole
   lifecycle-internality design decision, not only its final sentence.
8. The dated 2026-08-18 provenance artifact is unchanged; the live fixture instead points to a new
   dated Task 6.6 refresh whose 176-row/46,211-byte record has zero pending operations and SHA-256
   `ea8719e768182f4eea097cb280d3487426a9a7450ca7becb72616e567e30b756`.
9. Declaration accounting is exactly 337 physical sources / 1,390 declarations / 702 active
   declarations, and the maintained reshape 7.20 note names Task 6.6.

## Negative controls to reproduce

- invert or delete the newly covered first portion of the Task 6.5 design decision: Task 6.5 guard
  red;
- restore the old §13.4 name or old active-guide anchor: Task 6.6 guard red;
- introduce an arbitrary typo into either active guide anchor: Task 6.6 guard red;
- move `WaitLong` into the deferred table: Task 6.6 guard red;
- remove or alter either canonical registry cross-reference without making the same approved delta
  change: Task 6.6 and/or canonical-provenance guard red;
- drift the new provenance record, crosswalk totals, task completion, or design disposition: its
  owning infrastructure guard red.

## Validation to reproduce

- Debug and Release non-incremental warnings-as-errors builds: 0 warnings / 0 errors;
- Core 350; Ephemeral 79; Durable 99; Acceptance 37; Hosting 24; Provider Certification 96;
- PostgreSQL 101; SQL Server 72; Integration 11;
- Infrastructure 221/221 and exactly 14 separately classified intentional expected reds, for 235
  total guard cases;
- OpenSpec strict 18/18 and harmonization ledger 24 complete / 10 open / 34 total;
- `git diff --check` clean and zero changes under `src/**`.

Do not edit, stage, or commit the target. Return one dated immutable `APPROVE` or `REJECT` verdict.
