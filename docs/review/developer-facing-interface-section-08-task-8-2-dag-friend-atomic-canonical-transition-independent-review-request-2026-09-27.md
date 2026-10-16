# Independent review request — atomic DAG friend canonical/registry transition

Date: 2026-09-27
Base: `fcbfcab1621d5b46219e58d9936776beac345dcc`
Change: `admit-dag-authoring-friend-boundary`, tasks 1.3 and 1.4 together.
Manifest: `developer-facing-interface-section-08-task-8-2-dag-friend-atomic-canonical-transition-dirty-manifest-2026-09-27.txt`.
Raw manifest: 22 paths, 1,606 bytes, SHA-256
`3dfdd796aa5a32a360fe1df41d81815b71e677c8f6c502eaed2f6b8d5a240581`;
byte-identical to `git status --porcelain=v1 --untracked-files=all` with LF records.
Prior contract approval: `developer-facing-interface-section-08-task-8-2-dag-friend-contract-yyy-1-remediation-independent-review-verdict-2026-09-27.md`, 10,269 bytes, SHA-256 `398031734b5b98d3244e19e85f519c464aae80f2faf843e0c34d3b0c180cdf2f`.

Review one atomic pre-source transition. The two approved `MODIFIED` delta blocks are copied
verbatim into canonical `developer-facing-surface` and `repository-foundation` with their
preambles, every other heading, and order preserved. The post-gate registry retains its original
`Proposed` records and adds two source-pinned `ApprovedPending` records. The reshape predecessor
blocks remain in their active deltas, tied to their pre-sync canonical hashes; the new canonical
blocks tie to the approved successor hashes. The real contract checkpoint/evidence commits and
immutable verdict are bound by the guard. The prior process provenance artifact is cataloged as
superseded rather than edited. No `src/**` file, compiled friend attribute, public API, package
edge, or Task 8.2 product implementation is in scope.

The active numbered, binding, guide, and repository-instruction documents now call the friend
contract independently approved while stating that it remains absent from compiled metadata.
The dated Decision 22 proposal entry is retained as history and followed by an approval entry.
The Task 7.3 normalized hashes and the Task 8.0 map pin move with these status corrections;
the earlier reshape Decision 22 remains dated change-local rationale, not a current graph list.

The new `approved-pending-openspec-provenance-2026-09-27.md` is source-pinned whole and records
178 requirement rows, 46,784 UTF-8 bytes, two `SupersededByApprovedSuccessor` predecessors,
173 synchronized rows, three bootstrap-only rows, zero pending canonical operations, and
`semanticApprovalEligible: true`. The completed Section 7B permanent amendment entry is
unchanged. Task 2.1 and the remaining implementation tasks stay open.

## Reviewer notes from the contract verdict

- CP-020 and PR-005 now use the exact exclusion “No OrcaCore package other than
  `OrcaCore.Dag.Hosting` SHALL depend on `OrcaCore.Dag`.” Task 7.3 rows 1, 6, 7, 12, 14,
  and 18 and its source-pinned artifact digest move with the active status corrections;
  document 08 is outside that 22-row artifact.
- The amendment design explicitly excludes reshape's older change-local Decision 22 rationale
  from its “active document” sweep without rewriting historical design text.
- This packet states a reproducible content-record convention rather than an unexplained digest.
  For the 19 semantic paths dirty before this request/manifest and the immutable-history fixture
  were added, sort repository-relative paths by ordinal comparison, render each line as
  `path<TAB>raw-byte-count<TAB>lowercase-SHA-256`, join with LF and one final LF, and hash
  UTF-8 without BOM: **19 rows, 2,539 bytes, SHA-256
  `9912dbc9e3ee6885c64c5df005537e340bc05a1751c0f3362743a3594801b091`**.
  The manifest and staged tree bind this self-inclusive review packet; the reviewer should
  compute an all-file record independently after the freeze.

## Independent checks requested

1. Reproduce the exact manifest, parent, staged tree, and both content-record scopes; do not
   treat raw dirty-worktree hashes as committed-object hashes.
2. Compare each canonical requirement block byte-for-byte with its approved delta and its
   predecessor at the base. Confirm preambles, all other requirement blocks, and heading order
   are unchanged. The only canonical diff is these two `MODIFIED` operations.
3. Inspect the machine registry and guard: exact two approved-pending identities, predecessor
   and successor hashes, separate retained `Proposed` history, real approval verdict and its
   direct-child evidence commit, and open Task 2.1. A forged approval, changed predecessor,
   third owner, altered canonical block, or prematurely complete implementation task must red.
4. Recompute provenance rows/state counts and both artifact digests. Confirm the old process
   artifact is byte-unchanged and source-pinned in the superseded catalog. Review all six Task 7.3
   rows, the Task 8.0 map pin, the approved-but-not-compiled status in active documents, and the
   maximal current-match refresh.
5. Run Debug/Release warning-as-error builds, focused and full `Disposition=Infrastructure`
   guards, the 14 expected-red scenarios separately, strict OpenSpec, and staged
   `git diff --check`. Verify no `src/**` or compiled friend metadata changed.

If approved, checkpoint this exact target, add the verdict byte-exact in a separate evidence
commit, and activate the review state only. Task 8.2 source remains barred until that checkpoint.
