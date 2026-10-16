# Harmonization Task 6.4 post-review-hardening independent review request

**Date:** 2026-09-02
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 6.4 post-review-hardening checkpoint

The approved Task 6.4 chain is:

- remediation checkpoint `5e38de27a99a1a5ba5bd55a3ac2beb49f1b043e0`;
- approval-evidence commit `df3d866c925f4b7ca525d21ad28634de957a7480`; and
- mechanical activation `adc6a8f542a6e5362096df8a7213e06f55f11ad2`.

The immutable approval verdict is 14,017 bytes with SHA-256
`4d6456b7606ba7915c206d2c18f727ea8b64941394a199d860a9d6e6161a5397`.
This request reviews only W-1, W-2, and W-3 from that verdict. It does not authorize Task 6.5,
change archival, or reshape Task 8.0.

The exact target is named by
`harmonize-downstream-capability-specs-task-6-4-post-review-hardening-dirty-manifest-2026-09-02.txt`.
Recompute the commit-real raw manifest and scoped content record from base `adc6a8f542a6e5362096df8a7213e06f55f11ad2`.

## Claims to verify

1. Reshape task 7.20 attributes 337 sources / 1,388 declarations to harmonization task 6.4, and the
   accounting guard rejects the former task-6.3 suffix.
2. `docs/specs/18-semantic-appendix.md` remains unchanged from checkpoint `5e38de27`; the guard now
   constructs its complete expected content from the immutable reshape source artifact and the exact
   approved publication transforms, so drift in `## Laws` is rejected after archival.
3. The retired `MaxActiveFibers` token remains absent from product source and bounded to the same
   eight active-document owners. Both `docs/archive/` and `docs/review/` are immutable exclusions;
   active `.md` and `.cs` documentation remain scanned.
   The historical-input guard strips only the exact named exclusion declaration before checking for
   forbidden archive reads, so the exception cannot broaden into historical content consumption.
4. The Task 6.4 rejection, remediation approval, checkpoint, evidence commit, and mechanical
   activation remain registered byte-exactly. No prior record is edited.
5. No `src/**`, `openspec/specs/**`, public API baseline, package-manifest, or declaration-crosswalk
   path is changed by this hardening.

## Negative controls

- restore the stale task-6.3 inventory suffix: red;
- alter an unrelated canonical appendix law: red;
- add the retired token beneath `docs/archive/`: green by design;
- add it beneath active documentation: red; and
- alter any immutable prior Task 6.4 review artifact: red through provenance evidence.

## Validation

Reproduce Debug and Release warnings-as-errors builds, the focused Task 6.4/accounting/provenance
control, the complete Infrastructure disposition, the unchanged 14 intentional expected reds,
OpenSpec strict validation, task accounting, and `git diff --check`. Confirm Task 6.5 remains open.

Do not edit, stage, or commit the target. Return one dated immutable `APPROVE` or `REJECT` verdict.