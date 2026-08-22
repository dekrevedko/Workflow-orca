# Task 5.2 independent-review remediation

**Date:** 2026-08-21

**Source verdict:**
`docs/review/harmonize-downstream-capability-specs-task-5-2-independent-review-verdict-2026-08-21.md`

**Provenance-correction verdict:**
`docs/review/harmonize-downstream-capability-specs-task-5-2-provenance-remediation-independent-review-verdict-2026-08-21.md`

**Verdict preserved:** `REJECT`

## Disposition

The Task 5.2 reviewer reproduced the complete technical target with zero drift in the long-lived
working copy: both builds were clean, the infrastructure lane passed 211/211, the expected-red lane
contained exactly its 14 intentional failures, provider certification passed 96/96, strict OpenSpec
passed 18/18, all eight canonical blocks matched their authoritative deltas, and the provenance,
fixture, and mutation claims reproduced. Two later Task 5.1 checkpoint-provenance reviews showed
that a clean checkout instead failed the deletion-ledger guard because an absent retired-project
directory was enumerated without an existence check. The contaminated working copy's untracked
`bin`/`obj` debris had hidden that deterministic defect.

Approval remains blocked for one provenance reason. Task 5.1 checkpoint
`ff11ead781f8fef343fafc6e6bc8307d746e4a05` now has two independent `REJECT` verdicts and no
`APPROVE` verdict or explicit dated owner-approval disclosure. Task 5.2 was frozen directly on that
checkpoint. Green technical validation cannot substitute for approval of the exact base target.

## Executable remediation

- `review-manifest-provenance.json` registers every harmonization review manifest with its evidence
  disposition, byte count, hashes, base, checkpoint, exact historical content rows, and current
  review state. Schema 3 recomputes every aggregate content anchor from those rows.
- The Task 5.1 manifest is preserved unchanged and disclosed as `SetOnlyPathSorted`. Its checked-in
  SHA-256 is `271309bd1da188c023610ffdde4065729fbc17f16f3ae786fc17561d1842f898`, while the
  frozen raw-porcelain SHA-256 remains
  `33a4d11c3ba86a9cc7dc9a538ea84bcdf5a1ea9cbca2eb680963daf846ae81c8`.
- The guard validates Task 5.1's exact 17-path committed delta from parent
  `179421029f62bc0cd4d5968d465cf045f420ba34`, commit tree
  `3baddd97a6919bf5f674daed33bc236496a234c2`. The 17 historical rows reproduce the actual published
  2,428-byte dirty-worktree content SHA-256
  `741cfd6bbdd46cb4390c2f40c0d21d81d35b3e3749438b38efda44f26da1ff72`, byte-identical to the raw
  commit-blob projection. The superseded checkout-filtered projection
  `b18924f74e0f0e8d47d638db9440ebed4709eb597bb7103a36c5be943d1d32ba` is retained only in the
  immutable rejected review: checkout filters depend on the active attributes and are not checkpoint
  provenance. The superseded 2,427-byte `e73b7f4b...` value and its mixed-line-ending explanation
  were incorrect and are not accepted as provenance.
- The Task 5.2 manifest is registered as `RawGitOrder`: its raw and manifest SHA-256 is
  `9a1cda496e71eb2f40ec02cb834478c3906df9b89d18b7461816a70845781b0f`; its
  counterfactual path-sorted SHA-256 is
  `8b6d0890b058dc9bb03262d37479f522a60944b83a3166bcee7233f4cd818b6b`.
- Its 12 historical rows independently reconstruct the published 1,793-byte content record
  `0068973b0dbd4c1f79086a0262cefe62b728911362b5cd43fe472e0c7daebc8a`. The later
  `86ffab08...` recomputation in the rejected review is retained as immutable review history but is
  not treated as the frozen anchor.
- A mutation regression path-sorts the Task 5.2 bytes and requires the raw-order assertion to fail
  with a specific diagnostic. This closes the review's non-blocking P3 observation.
- The checkpoint-provenance guard records Task 5.1 as `Rejected`, registers and hashes both Task 5.1
  `REJECT` verdicts plus both Task 5.2 `REJECT` verdicts, requires remediation task 5.2a to remain open,
  and prohibits a Task 5.2 checkpoint. `MissingApproval` is reserved for a target with no verdict
  evidence. Its future `Approved` path permits preserved rejection history but requires exactly one
  `APPROVE` verdict in a distinct approval-evidence commit and requires every later Task 5.2
  checkpoint to descend from that commit.
- The deletion-ledger discovery regression executes the retired-package calculation against a
  genuinely absent directory. It fails on the superseded implementation and proves that clean
  checkouts return the retired package instead of throwing `DirectoryNotFoundException`.
- Historical crosswalk discovery now drains the complete `git archive` stream before parsing its
  TAR payload. This removes the reproducible exit-141 broken-pipe failure from the other clean-tree
  infrastructure finding without weakening the exact historical declaration comparison.
- `.gitattributes` pins `docs/specs/17-public-authoring-contract.cs`, canonical
  `openspec/specs/**/*.md`, and the Section 7 declaration crosswalk to LF. The exact-baseline guard
  requires every rule and verifies the current files contain no CR bytes, so a fresh Windows checkout
  hashes those reviewed artifacts independently of ambient `core.autocrlf` settings.
- Canonical preambles are pinned separately from requirement provenance: 14 LF-normalized
  per-capability hashes plus one aggregate record detect an unreviewed `## Purpose` rewrite. This
  hardening was added after the frozen Task 5.2 request and is recorded here rather than rewriting
  that immutable 8,647-byte request.

## Approved-remediation follow-up

The independent provenance-remediation review approved the corrected historical records and found
one additional freeze defect before authorizing this remediation checkpoint. Raw `git status`
intermittently listed four canonical specs whose bytes and Git blobs were identical to `HEAD`; a
real commit would therefore contain 25 paths while an unfiltered status manifest claimed 29. New
freezes now retain Git-emitted records only for the union of tracked content diffs and untracked
files. The infrastructure guard exercises the live projection and a synthetic status-only phantom,
so attribute/index metadata cannot overstate a future commit target.

The same review noted that four Task 5.2 historical rows remain byte-verifiable in the current
worktree even though Task 5.2 has no checkpoint commit: the Task 4.3 artifact, post-gate fixture,
dirty manifest, and review request. Schema 4 names those paths explicitly and checks their current
byte counts and SHA-256 values against the frozen rows. This opportunistic evidence does not approve
Task 5.2 or replace the missing Task 5.1 approval.

## Remaining gate

Task 5.2 is not approved. The two Task 5.1 provenance reviews are immutable `REJECT` evidence, not
approval. A reviewer must approve a new remediation target that contains the clean-checkout fix and
the corrected rejected-state machinery. The resulting approval evidence must then be committed as
its own provenance checkpoint before Task 5.2 is refrozen or re-submitted. The implementation owner
cannot manufacture that approval. Task 5.3, the harmonization exit gate, archival, and reshape Task
8.0 remain blocked.

## Consolidated verification

- The absent-retired-directory regression failed on the superseded helper with
  `DirectoryNotFoundException` and passes after the existence-aware discovery change.
- The historical crosswalk test reproduced `git archive` exit 141 before the stream-draining fix
  and passes after the complete TAR payload is buffered.
- Mutating Task 5.1 from `Rejected` to `MissingApproval` fails because the two recorded `REJECT`
  verdicts cannot be hidden; the restored rejected state passes.
- Mutating either historical row set, byte count, or aggregate digest fails; Task 5.1 additionally
  fails unless the row record equals the raw checkpoint-blob record.
- Mutating the companion path rule from `eol=lf` to `eol=crlf` fails the exact baseline guard.
- Release and Debug builds complete with zero warnings and zero errors; Infrastructure is 214/214;
  ExpectedRed remains exactly 14/14 intentional failures; provider certification is 96/96; strict
  OpenSpec is 18/18; and `git diff --check` is clean.
- The frozen Task 5.2 request is restored to 8,647 bytes. The three canonical specs and the Section
  7 declaration crosswalk use LF, eliminating line-ending-only diff churn while retaining the
  reviewed eight synchronization operations and exact declaration-accounting changes.
