# Harmonization Task 8.3 closeout and TTT-1 independent review request

**Date:** 2026-09-26

**Change:** `harmonize-downstream-capability-specs`

**Base:** `6faf798f5662dc47baba1cb4343e15861b7e757c`

Please review this as a separate, uncommitted post-checkpoint closeout target. It does not
rewrite or enlarge the nine-path Task 8.1 target approved on 2026-09-24. The approved
checkpoint is `843c79b6e0c6aee11a61e33588310e12b3dd4d67`, its direct approval-evidence
child is `ed8fb81513fba67c019e6cdd9db64d9e3bd90231`, and the two-field activation is
`6faf798f5662dc47baba1cb4343e15861b7e757c`. The Task 8.1 verdict remains unchanged
at SHA-256 `0a0870b3258c4de5894cad08b0bc73b3484994c31ce4703b3f335b614d99408b`.

## Reviewed scope

The companion `docs/review/harmonize-downstream-capability-specs-task-8-3-closeout-ttt-1-dirty-manifest-2026-09-26.txt`
is the authoritative commit-real, Git-emitted order. This target changes only:

- the harmonization task ledger, marking 8.2 and 8.3 complete with the actual verdict and
  three-commit checkpoint chain;
- `OpenSpecCorpusGuards.cs`, adding guard-source LF-normalized SHA-256 pins for the unchanged
  Task 8.1 audit and the complete final ledger section, closing exit-review finding TTT-1;
- the review-manifest fixture's mechanical maximal-current-match refresh and new active freeze;
- the immutable-document-history catalog for this request and manifest.

No `src/**`, canonical `openspec/specs/**`, prior verdict, or Task 8.1 audit bytes change.
The historical audit truthfully describes its own 2026-09-23 state, when 8.2 and 8.3 were
still open. The new completion record does not authorize reshape Task 8.0 or Section 8
implementation; those remain subject to reshape's independent gates.

## Evidence and negative controls

- The Task 8.1 audit's LF-normalized SHA-256 is
  `0618ab997def24d79d58648f2adf29d65d619063cabfd4f279086d76a54a3a52`.
  The guard owns this value in source, not in a mutable fixture.
- The guard also pins the complete final ledger section and requires exactly one checked
  8.1, 8.2, and 8.3 task, with the reviewed checkpoint and approval-evidence IDs present.
- Editing one audit sentence and running the normal current-match refresh made the focused
  guard red on the audit-source constant. Editing 226/226 to 225/226 in the ledger and
  refreshing likewise made it red on the final-section constant. Both edits were restored
  byte-for-byte before the freeze.
- The existing review-provenance guard still verifies the verdict's bytes, the exact
  checkpoint tree and parent, the evidence commit's direct-child relation, the verdict
  addition, and the activated state.

The non-incremental Release guard build has zero warnings and errors. The focused provenance
guard passes, Infrastructure passes 226/226, and `openspec validate --all --strict` passes
18/18. The 14 intentionally expected-red executable-behavior guards remain a separate
disposition and are not being called green. Run the full validation you consider necessary
against the frozen target; verify `git diff --check` and the scoped content record as well.

## Verdict and checkpoint boundary

Please return a dated, immutable `APPROVE` or `REJECT` verdict for exactly the manifest
paths and content-record bytes. A verdict file is not part of this frozen target. Do not
commit the target before approval. A subsequent evidence commit must register any verdict
under the Task 8.3 review entry and the append-only history catalog. Archival of this
change, reshape Task 8.0, and Section 8 work are outside this request.
