# Harmonization Task 7.1 post-review hardening independent review request

**Date:** 2026-09-16
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 7.1 MM-1/NN-1 hardening checkpoint

Review this target on immutable base `e7f8e26c54d6e00d3bb0ac1e2632a277ec0991e1`. Do not edit,
stage, or commit it. Return one dated immutable verdict. The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-1-post-review-hardening-dirty-manifest-2026-09-16.txt`.

## Prior approved chain

- Task 7.1 reviewed checkpoint: `de73ca521343d303cd8f774990954889463a782c`, tree
  `bd519b0dd64e9dc6705b867faa0c63405351d04a`.
- Independent approval evidence: `3142ab22027a0745635772195b01362d884202db`.
- Activated approval state: `e7f8e26c54d6e00d3bb0ac1e2632a277ec0991e1`.
- The Task 7.1 entry retains both prior `REJECT` verdicts and the final `APPROVE` verdict.

## Findings that must be closed

### MM-1 — source-record snapshot timing

The Task 7.1 design decision must now state that the 86-row TSV is a pre-finalization scan
snapshot. It must name `design.md` and `tasks.md` as the two rows that intentionally predate their
final self-describing remediation text and must not present the TSV digest as a checkpoint-tree
digest. The executable Task 7.1 guard must pin that whole clarification, so deleting or weakening it
fails after this active freeze is archived. The TSV itself remains immutable and honest.

### NN-1 — guard formatting

`OpenSpecCorpusGuards.cs` must place the two `design.Should().Contain(...)` statements on separate
lines and retain only one separating blank line before the artifact validation. There must be no
semantic change to either assertion.

The maximal historical-byte pins must be refreshed mechanically: Task 7.1 falls from 18 to 16
current matches because the design and guard bytes changed; no other historical match may move.

## Scope and validation

- Exact scope: the design, `OpenSpecCorpusGuards.cs`, the provenance fixture, this request, and this
  raw-order manifest.
- No `src/**`, canonical `openspec/specs/**`, or Task 7.2 implementation is in scope.
- Release non-incremental warnings-as-errors build: 0 warnings / 0 errors.
- Infrastructure guards: 222/222.
- Strict OpenSpec validation: 18/18.
- Harmonization ledger: 25 complete / 9 open / 34 total.
- `git diff --check`: clean.
- The review-manifest current-match refresh check is green with Task 7.1 at 16 matches.

The first focused test attempt encountered a corrupted generated Release reference assembly
(`CS0009`). A serialized non-incremental Release rebuild replaced that generated metadata and
completed with 0 warnings / 0 errors; the focused and full infrastructure lanes then passed.

The sole content-record exclusion is the self-referential
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json`.
