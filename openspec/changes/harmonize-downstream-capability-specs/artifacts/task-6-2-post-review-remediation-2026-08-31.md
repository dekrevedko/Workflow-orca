# Task 6.2 post-review remediation — 2026-08-31

## Approved checkpoint and evidence

- Reviewed checkpoint: `7abf95e3d691214201027f47ee8b7e0365f715b7`.
- Reviewed tree: `48ff41ffa0ba8da3ac0a0906c2235ad1084d7f31`.
- Parent/base: `532940a3865b8eec85c315dca9da09b874cb4173`.
- Independent verdict: `docs/review/harmonize-downstream-capability-specs-task-6-2-independent-review-verdict-2026-08-31.md`, 17,553 bytes, SHA-256 `486b73cebf7a5048f7a655227f73d60405b6191356e458d13044b41b1b5b10fd`.

The approved 15-path checkpoint was committed before this verdict entered the worktree. This
remediation therefore preserves the reviewed object and records the verdict in the green
`ApprovalAwaitingEvidenceCommit` transition before a following mechanical activation.

## Findings closed

- **T-1:** review-provenance schema 10 distinguishes `IndependentReview` from
  `OwnerAuthorization`. Independent states and archived freezes reject an
  `-owner-approval-verdict-` state-evidence path; owner states require it. Every archived freeze
  names one registered immutable approval verdict as its authority evidence.
- **T-2:** the `CR-014a` numbered block terminates at the next heading of level two or deeper by
  searching for `\n##`, so inserting a new `##` section cannot leave moved clauses inside the
  guarded block accidentally.
- **T-3:** the complete normalized `CR-014a` block is pinned by SHA-256
  `8fd23512e8469b4f8fc16e42bcbf85632bc51094936e733735f8040cc094a4ad`; an appended
  contradiction fails even when every required fragment remains present.
- **T-4:** Task 6.3 now names the synchronized quality-and-verification requirement,
  structured-fiber ordering contract, and public-contract companion, plus the owning-failure,
  ordering-key, non-negative-index, creation, one-failure, per-cause, and malformed-codec
  obligations. The Task 6.2 infrastructure guard pins that complete follow-on scope.

## Self-review and mutation evidence

Each mutation ran against the restored target and made its focused guard red:

1. Task 6.1 `Approved` state pointed at its owner-approval verdict.
2. A real level-two heading was inserted before the CR-014a codec clauses.
3. A directly contradictory codec-coercion sentence was appended to CR-014a.
4. The structured-fiber normative source was removed from Task 6.3.

Control after restoration: the provenance and Task 6.2 focused guards pass 2/2.

## Validation

- Full Release build with warnings as errors: 0 warnings, 0 errors.
- Infrastructure guards: 217 discovered and the complete lane exits 0.
- Expected-red guards: exactly 14 failed and 0 passed, unchanged.
- OpenSpec strict validation: 18 passed, 0 failed.
- Historical current-byte pins: Task 5.1 = 9, 5.2 = 4, 5.3 = 3, 6.1 = 4, 6.2 = 12.
- Harmonization ledger remains 20 complete / 14 open / 34 total.
- `git diff --check`: exit 0.
