# Independent review request — Task 7.7 and RRR-1

Date: 2026-09-23. Base: `cb17f432a00104758a157ab547e3fca143b6a3ac`.
The target is the uncommitted worktree named byte-for-byte by
`harmonize-downstream-capability-specs-task-7-7-and-rrr-1-dirty-manifest-2026-09-23.txt`.
Review both changes as one checkpoint. Do not infer approval of Task 8.1 or final harmonization.

## Scope and claims

1. **RRR-1:** The historical-owner synthetic control now contains declaration-shaped method
   signatures in a multiline string and multiline comment. Removing lexical masking from that
   control must turn the focused `RemovedDeferredAndWrongOwnerPublicSymbols_AreAbsentBeforeBaselineApproval`
   test red on `StringOnly` or `CommentOnly`. Current removed-symbol resolution remains unchanged.
2. **Task 7.7:** The new immutable archive provenance record identifies the exact predecessor
   `docs/implementation/developer-facing-interface-phase-00-kickoff-prompt-2026-07-15.md`
   at commit `ac46d99543daf85c0fa3234272997ba40f47f96b`, its Git blob and content hash, and
   the `R097` move in `ad9414088f1843dae09ef8a5d10caa8aca413561` to
   `docs/archive/plans/developer-facing-interface-phase-00-kickoff-prompt-2026-07-15.md`.
   The only move-time content edits corrected four relative links. The existing archived prompt
   remains byte-identical to the move commit. No protected path was renamed, deleted, or edited.
3. The archive index routes the old path to the prompt and this provenance record. The dated
   record is catalogued as append-only. The existing Infrastructure guard verifies the exact
   Git coordinates, content hashes, route and Task 7.7 ledger decision; a future relocation
   still requires a separately reviewed tombstone mechanism.
   The crosswalk's archive-path source check now permits only the two named Task 7.7 historical
   coordinates; arbitrary archive paths and recursive normative scans remain forbidden.
4. Task 7.7 is marked complete. Task 8.1 and all later work remain open. No `src/**` or
   `openspec/specs/**` file is in scope.

## Local validation and limit

Debug and Release non-incremental warning-as-error builds are 0/0. Product tests pass:
Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 96,
PostgreSQL 101, SQL Server 72, and Integration 11. Infrastructure guards are 226/226;
the separate ExpectedRed lane has exactly 14 documented failures. OpenSpec strict is 18/18,
the task ledger is 31 complete / 3 open, and the staged-diff whitespace check is clean.
The RRR-1 negative probe turns its focused guard red when synthetic lexical masking is removed,
and restoring that line restores its exact source hash. A temporary semantic edit of the new
immutable archive record was rejected by the local sandbox reviewer; no such mutation was made.
Please run Task 7.7 corruption probes in your isolated review copy.

## Review procedure

- Reproduce HEAD/tree, raw commit-real porcelain against the manifest, and the scoped content
  record in `review-manifest-provenance.json`; recheck them after probing. Treat the manifest's
  raw Git order as authoritative and exclude only the self-referential provenance fixture from
  the content record.
- Resolve the predecessor and move from Git objects, not from the new record alone. Check the
  archived prompt bytes and the four link-only differences. Verify the new record, archive
  index, task ledger, immutable-history catalog and source guard all agree.
- Mutation-test RRR-1 by bypassing `MaskNonCode` in the synthetic control. Mutation-test the
  new Task 7.7 guard by corrupting a move coordinate, record hash or archive route. Restore
  a disposable copy after each probe.
- Run Debug and Release warning-as-error builds, focused and full Infrastructure guards,
  expected-red isolation, product/provider lanes, OpenSpec strict validation, and
  `git diff --check`. Keep intentional Section 8 expected-red tests separate from failures.
- Simulate staging exactly the manifest paths in an isolated index and compare the resulting
  tree to the reviewed target. Do not commit or edit this request or manifest during review.

An independent APPROVE or REJECT verdict is required before a checkpoint commit. If approved,
commit exactly this frozen target first, then add the verdict in a separate evidence commit and
activate its registry entry. The verdict itself is not part of this freeze.
