# Issue #13 task 2.1 review — 2026-10-24

## Verdict: APPROVE (internal transition slice only)

No blocking findings. This is a separate self-review pass over the completed
canonical/docs/binding slice, not an external human review or source/native
approval. The user authorized starting task 2. Product implementation remains
for task 2.2; this review creates no new API, support claim, push or checkpoint
authority. The coherent task-2 target remains uncommitted until its final review.

## Reviewed bytes

- Base/HEAD: `10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd`.
- Reviewed paths: 37; exact short-worktree rehearsal tree
  `f2ecd5e17f1521aaaf0e3cceb08b6a4721e6cee5`.
- Content SHA-256: `68af3d24670ee0d56fa84d1210a7e7980bd559eec29308d41ff65e4301b92bc9`.
- Hash rule: concatenate UTF-8 path, NUL, raw file bytes, NUL in ordinal path
  order, then SHA-256. The path list and raw Git/test checks are retained under
  ignored `.artifacts/publishing-validation/issue-13-task-2/`.
- This review file is excluded from those 37 reviewed paths. The author index
  remains empty; no task-2 commit was made.

## Review findings and checks

The contract chain is exact: checkpoint 6122025 has parent 3b7f98a and approved
29-path tree 2b981a6; direct child 10aec46 adds only the byte-exact R1 verdict.
The transition adds exactly seven approved blocks across four canonical specs.
Every earlier raw canonical byte is retained. F1 and all nine P3/R1 subcases are
unchanged from the approved deltas. NF-034, AC-563–574, the exact prospective
signature, startup/seal/DI rules and version-scoped pending guides are consistent.
The declaration and executable baselines stay with real source in task 2.2.

Schema 20 adds only its two named native-support keys. All prior records are
data-equal; source/native and final canonical approval remain null. Git lookups
pin the exact parent/tree, sole verdict child and raw verdict bytes and fail
closed. The existing DAG/shutdown/assessment checks now validate exact historical
layers plus the exact reviewed successor text; no general canonical/source
exemption or unrelated engine edit is permitted.

The 115 active-doc hits in 31 owners and the overlapping metadata-inclusive
145-hit pass were read individually in context, then checked against unchanged
Git prefixes. The owner decisions record the edited, retained and source-deferred
owners. Released preview.2/issue #3 Unsupported results remain unchanged. No
archive, old review, product source, runtime test, workflow, dependency/friend,
public declaration, API/compile or package baseline changes occur in this slice.

Provenance has 206 rows, 195 synchronized / 8 superseded / 3 outside-canonical,
zero pending. The 199 baseline rows are retained; seven issue #13 rows change to
synchronized. All 23 predecessor artifacts are byte-exact. Canonical provenance
eligibility means sync completeness only, not native/source approval. The test
crosswalk adds one file/four Infrastructure facts; every retired/historical row
and zero replacement-credit decision is preserved. The live owner hash rows and
Task 7.20 totals now match those intentionally edited owners and exact inventory.

## Validation

- Strict OpenSpec: 25/25, independently rerun after final owner/task edits.
- Release solution: 0 warnings / 0 errors with warnings as errors; build inputs
  did not change after the final build. Exact twelve-package feed packed.
- Full CI Infrastructure route with engine-only coverage include: 263/263,
  no failed/skipped tests. Earlier transition tail/inventory mismatches were
  fixed before this review; they are not counted as green runs.
- Final task-2.1 checkbox/owner-document additions were copied to the short
  checkout; focused binding/task-accounting guards reran 6/6. They add no build
  inputs. All 37 staged rehearsal blobs match the author bytes; staged whitespace
  and review-manifest current-match checks pass.
- Five targeted binding mutations (wrong parent, source approval, old registry
  mutation, prior canonical edit and removed prospective signature) each fail
  their intended guard. No product acceptance/coverage credit is claimed.
- Runtime/provider/integration/native qualification lanes were not rerun for
  this docs/guard-only slice. They remain obligations of tasks 2.2–2.5.

## Next major step

Task 2.1 is complete in the uncommitted coherent target. Task 2.2 owns the
behavior-first subprocess/codec tests and source/API/baseline slice, followed by
its own review before further dispatch/native work. Task 2.6 and publication
remain unchecked and gated. Clean owned build/temporary output after retaining
compact receipts; never delete earlier review deliverables or unrelated work.
