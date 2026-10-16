# Harmonization Task 7.2 WW-1 remediation independent review request

**Date:** 2026-09-18
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the superseding Task 7.2 immutable-history checkpoint

Review this superseding target on immutable base
`261d578b11a4b2d09e033aa44c8123cbc4ec653e`. Do not edit, stage, or commit it.
Return one dated immutable verdict. The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-2-ww-1-remediation-dirty-manifest-2026-09-18.txt`.
Task 7.3 remains blocked.

## Prior rejected freezes

Three Task 7.2 targets were rejected. Their requests, manifests, and `REJECT` verdicts remain
byte-immutable and are registered separately as `7.2-initial-rejected`,
`7.2-round-60-remediation-rejected`, and `7.2-ss-1-tt-1-remediation-rejected`. This target carries
all three without rewriting them.

## WW-1 closure

The append-only validator now reconciles Git history in both directions. It enumerates every path
first added after `baselineCommit` under `docs/archive/` or `docs/review/` and requires each path to
remain present and cataloged in `appendOnlyRecords`. Existing per-entry checks still bind normalized
bytes to the first Git addition. A committed record therefore cannot be deleted or moved by removing
or repointing its catalog entry.

The current contract has no withdrawal mechanism. Any future withdrawal must first introduce an
explicit reviewed tombstone mechanism; silently deleting or relocating a historical path is invalid.

## XX-1 closure

The design, Task 7.2 completion, archive rule, and their guard-source pins now state the relation
precisely: while a record is uncommitted, the catalog's active freeze manifest names the record.
The stray short lines in the completion and archive text are removed.

## Required mutation controls

Against a green control in disposable worktrees, verify:

1. commit a reshape-family review record, delete it and its catalog entry, and observe red before
   the deletion commit;
2. commit that deletion and observe red afterward;
3. restore the committed record, `git mv` it, update its catalog path, and observe red before commit;
4. commit the move and observe red afterward;
5. retain the Round 61 controls: family-independent admission remains green, an uncataloged or
   out-of-freeze record is red, a committed content edit is red, and CRLF-only normalization is green;
6. drop or edit any of the three rejection packets and observe red; and
7. prove the normal dirty and simulated-checkpoint states remain green.

The failure must name the historical path derived from Git. A compile error, active-freeze mismatch,
or unrelated registry failure is not acceptable mutation evidence.

## Validation target

Reproduce Debug and Release warnings-as-errors builds, all product and provider suites, the
Infrastructure and expected-red lanes separately, strict OpenSpec validation, task accounting,
`git diff --check`, and a simulated checkpoint. No `src/**` or canonical `openspec/specs/**` path
may change.

## Exact scope and sequencing

The exact target is the companion raw-order manifest. It carries the previously reviewed Task 7.2
implementation, all three immutable rejected packets, the reverse Git-history presence invariant,
the corrected policy wording, this request, and its manifest. Do not checkpoint or start Task 7.3
until this exact target receives independent approval.