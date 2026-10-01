# Independent review request — authoring-friend task 3.2 closeout

Date: 2026-09-29. Change: `admit-dag-authoring-friend-boundary`, task 3.2.
Base: `738c3b591b6f6e72de13a7750601cb36430c514f`.
This is the permanent-record closeout of independently approved reshape Task 8.2, not
Task 8.3 source, not a new Dag-to-Dag.Hosting friend, and not a codec-allowlist amendment.

Manifest: `developer-facing-interface-section-08-task-8-2-authoring-friend-closeout-dirty-manifest-2026-09-29.txt`.
It includes this request, itself and both review-history fixtures. The live index must remain
unstaged throughout review. Do not edit this packet or add a verdict to its checkpoint.

## Freeze convention

- Commit-real paths are the union of `git diff --name-only --no-renames HEAD` and
  `git ls-files --others --exclude-standard`. Filter
  `git status --porcelain=v1 --untracked-files=all --no-renames` to that set, preserve Git's
  raw order, join with LF and one final LF, UTF-8 without BOM. This removes status-only
  attribute/index phantoms; it is not a path-sorted manifest.
- Manifest: **19** paths, **1,354** bytes, SHA-256
  `53eb5278f150b73883db9d114c82e55b7b696cd2c8af2c6bccf7e86f9c7b6d80`.
- Semantic record: exclude all `docs/review/` paths plus
  `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json` and
  `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json`.
  Sort remaining **paths** with ordinal comparison. Render each as
  `path<TAB>raw-file-byte-count<TAB>lowercase-SHA-256`, LF-join with one final LF, UTF-8
  without BOM. **15** rows, **1,958** bytes, SHA-256
  `6fbd91e9908af52d32d4bfd06acd0954e3a0362d7db1383c5f407646ad2704c1`.
- The separate all-target active content record excludes only `review-manifest-provenance.json`.
  Its exact bytes and hash are in `activeFreeze` in that fixture. For each manifest line render
  `XY<TAB>path<TAB>raw-file-byte-count<TAB>lowercase-SHA-256`, ordinal-sort the **rendered
  records**, LF-join with one final LF. This is the guard's recipe, not the semantic recipe.
- Derive the rehearsal tree with a copied temporary `GIT_INDEX_FILE`, staging only manifest
  paths into that copy. Never alter the live index or commit a probe on the live branch.
  Every staged blob must equal its raw worktree bytes. Tree and all-target hash are published
  separately so this self-inclusive request has no digest/self-reference cycle.

The earlier Task 8.2 request's sort description is not retroactively edited. Its `ff924cd5…`
semantic hash reproduced in raw Git order; the reviewer disclosed ordinal `85522a3a…`.
The source approval binds the reviewed tree, raw manifest and immutable verdict. This new
packet uses the explicit recipes above and carries no claim that a dirty hash is a blob hash.

## Reviewed source authority

| Object | Exact identity |
| --- | --- |
| Source checkpoint | `a9f835f939d683500ca231c7ba491ab8eae2aaae` |
| Source tree | `cf11b3f6732f11250ba3a0255114bcdc4ae00060` |
| Direct-child evidence | `055e7b8e71e8dfe79e76f267f8782b7f6f79f7b8` |
| Source activation / this base | `738c3b591b6f6e72de13a7750601cb36430c514f` |
| Source verdict | `developer-facing-interface-section-08-task-8-2-dag-authoring-source-zzz-remediation-independent-review-verdict-2026-09-28.md` |
| Source verdict raw bytes / SHA-256 | 8,742 / `513871b82c87f5949d4801108f4f1c901b8f8cfe7084121217359cf96a9ccd4a` |

## Changes and observations fixed

1. Schema 5 keeps Proposed/ApprovedPending records as historical transitions and adds
   `completeEvidence`, source-pinned to the real source checkpoint, tree, direct-child verdict
   addition, refreeze request/manifest, exact five implementation tasks and compiled test paths.
   Tasks 2.1–2.5 are checked because their implementation is already approved and checkpointed.
   Task 3.2 remains open: this closeout still requires its own independent approval/checkpoint.
2. Both contract and source approval checks require one final nonblank APPROVE line, one
   parent equal to the reviewed checkpoint and `A` for the verdict in that evidence commit.
   A later activation merely containing the verdict is insufficient.
3. Current CLAUDE/numbered/guide status names the approved source checkpoint. Decision 22's
   dated entries remain unchanged; the 2026-09-29 source-approval entry is appended. The six
   affected Task 7.3 rows and the guard-source artifact digest are refreshed in this target.
4. External metadata probe projects copy the repository `global.json`, verify its bytes and
   build from that directory, so machine-default SDK selection cannot bypass the repo pin.
5. The new permanent closeout artifact is itself LF-normalized SHA-256-pinned by the canonical
   gate. Existing source-validation and provenance artifacts, all earlier review records,
   canonical specs and product source remain unchanged.

The provenance checkpoint still has 178 rows / 46,784 bytes /
`0dd47120d11d2442fccc217902386dd36d4bbf57b7fb476bd7c670dbbbaa5257`:
173 synchronized, 2 superseded, 3 outside canonical, zero pending, semantic approval eligible.
There are no new Fact/Theory declarations; no crosswalk, API baseline or package-source hash
is refreshed. The compiled eight-friend graph and six-member authoring allowlist are unchanged.

## Validation to reproduce

- Release and Debug solution builds: `-warnaserror --no-incremental --no-restore`, 0 warnings
  and 0 errors. Focused Debug canonical/metadata/DAG behavior: 15/15.
- Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 96;
  PostgreSQL 101, SQL Server 72, Integration 11. No skipped tests.
- Infrastructure 240/240. Separately, exactly 14 failed / zero passed in `Disposition=ExpectedRed`,
  all in `ExecutableBehaviorExpectedRedGuards`; do not pool them into the green denominator.
  For a clean checkout, build the full solution and seed its twelve-package feed with
  `pack-exact-package-feed.ps1 -NoBuild -OutputDirectory artifacts/phase0-packages` before
  the Infrastructure lane. Use a short disposable path on Windows: deeply nested generated
  compile fixtures can exceed legacy PowerShell cleanup path limits. Those prerequisite/path
  failures are not product reds and are not included in the final passing packet.
- Package fixture runner with shipped Section 7 cutoff: Green 8/8, ExpectedRed exactly
  `dag-hosting`, exit 1 by design. A Section 8 cutoff prematurely selects that whole unfinished
  runtime fixture; it is not the correct cutoff for this authoring-only slice.
- Strict OpenSpec: 19/19. Amendment progress 12/13; task 3.2 is open. Both worktree and copied
  staged-tree `git diff --check` must be clean. Current-match refresh `-Check` must be byte-idempotent.

Nine isolated negative controls passed against an initial green canonical control and final
2/2 canonical/metadata control. No live branch/index was changed:

| Control | Intended red |
| --- | --- |
| C1 Complete → ApprovedPending | Exact source record mismatch |
| C2 Drop 2.5 and coherently reduce count | Exact implementation set mismatch |
| C3 Reopen 2.4 | Completed-task evidence mismatch |
| C4 Coherently point fixture + source pin at later containing activation | Single-parent direct-child rule |
| C5 Add a phantom line to historical source manifest | Raw immutable manifest hash |
| C6 Append unauthorized seam to closeout artifact | Permanent artifact hash |
| C7 Add second APPROVE and coherently repin source + fixture hash | Single verdict rule |
| C8 Add text after APPROVE and coherently repin source + fixture hash | Terminal verdict rule |
| C9 Remove SDK copy from probe helper | Missing pinned `global.json` before compilation |

All mutated files restored byte-exactly. Re-run or extend controls in a positively identified
disposable worktree only. In particular, verify no new friend, codec reference, canonical edit,
unregistered owner, test declaration or Task 8.3 implementation enters this closeout.

## Independent verdict and sequencing

Review the whole manifest target, not just the registry diff. Check all immutable chain objects,
the block/hash history, document-status updates, all 22 documentation pins and current-match
maximality. Reproduce dirty and committed-state guards; do not treat green validation as approval.

If approved, authorize only this task 3.2 closeout checkpoint. Write a separate immutable verdict
under `developer-facing-interface-section-08-task-8-2-authoring-friend-closeout-independent-review-verdict-2026-09-29.md`.
Its addition intentionally dirties the reviewed target: checkpoint only the frozen manifest
paths first, then add/catalog the verdict and clear the active freeze in the direct-child evidence
commit, then activate by checking task 3.2 only. No documentary refresh is folded into activation.

Only after that chain may the separate Dag-to-Dag.Hosting runtime-view amendment be prepared.
It must keep fixed-codec normalization/fingerprint/commit on the durable bridge, not widen
OrcaCore-to-Dag authoring access. Neither this closeout nor the source approval authorizes Task 8.3.
