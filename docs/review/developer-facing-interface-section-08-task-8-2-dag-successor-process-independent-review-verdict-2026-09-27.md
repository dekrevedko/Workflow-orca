# DAG successor process gate independent review verdict

**Date:** 2026-09-27
**Reviewer:** independent review
**Scope reviewed:** the sixteen-entry process-only freeze on base
`590df004ac346f85fe085d94ccd646165bc07e61`, named by
`developer-facing-interface-section-08-task-8-2-dag-successor-process-dirty-manifest-2026-09-27.txt`.
**Authorization requested:** checkpoint of the process-only proposed-successor gate
(`admit-dag-authoring-friend-boundary` task 0.1). This verdict authorizes that checkpoint for the
exact frozen target only.

## What this verdict does not approve

- **The friend:** it does not approve the `OrcaCore -> OrcaCore.Dag` friend contract. The two
  `MODIFIED` deltas, the proposal, and the design are present so the guards can inspect them. Their
  content remains unapproved until tasks 1.1–1.2.
- **Later work:** it approves no canonical synchronization, friend attribute, metadata allowlist,
  shared hash operation, or Task 8.2 product source.

## Summary

The gate does exactly what it claims, and it is narrow.

- **The exception:**
  - A source-pinned, two-row registry in `post-gate-amendment-path.json` (schema 3) lets exactly two
    headings have two active owners.
  - Each row names a predecessor/successor change pair, capability, verbatim heading, both
    operations, the `Proposed` stage, and open task 1.3.
  - Every other duplicate owner stays red.
- **Per-pair checks:** the guard requires exactly two owners. The reshape predecessor must still
  match canonical, the successor must remain `PendingModification`, and task 1.3 must be open.
- **Provenance:** it is refrozen honestly. There are two pending operations, and
  `semanticApprovalEligible` is false. The prior 176-row record is kept byte-exact in the superseded
  catalog.
- **Scope:** nothing under `src/**` or canonical `openspec/specs/**` changed.
- **Probes:** every one was red at its intended check.

No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree. Probes ran in a second one. Both held the same
sixteen entries. Every script asserted its disposable location and that the main `HEAD` was still
`590df004`. Main was never modified, and the review created no ref in the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 16 lines, 1,344 B, `bf1490bc…0c7c` | identical |
| Content record, all 16 rows | 2,470 B, `4ebe85d9…9c44` | identical |
| Entries | 7 modified, 9 untracked, 0 staged | identical |
| Simulated checkpoint | tree `d4b0ac65` | `d4b0ac65f486c374476fe312ebb225aad6b6be4b` from a copy of the live index and from a committed disposable copy; path set equals the manifest (9 A / 7 M) |

- **History catalog:** the new manifest and request (3,644 B, `5e9e7921…`, LF) are in
  `appendOnlyRecords`, and `activeFreezeManifestPath` names this manifest.
- **Pins:**
  - `CLAUDE.md` is a Task 7.3-pinned source, edited only in its post-gate paragraph;
  - its row 1 (`e43882a8…`) and the artifact digest (`dcad67cd…`) are refreshed in this same frozen
    target, as the pin rule requires;
  - the Task 6.6 current-match pin loses only the one intentionally changed fixture.
- **Deltas:**
  - both reshape predecessor blocks are byte-identical to canonical;
  - both successor blocks are full copies of canonical with additive edits only (plus "three" →
    "four" grants);
  - the headings are verbatim.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 226/226 |
| `ExecutableBehaviorExpectedRedGuards` | exactly 14 failures |
| Strict OpenSpec | 19/19 |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 226 passed plus the same 14 expected-red |

## 3. Guard probes

Each probe refreshed the mutable current-match pins after its edits and ran every
`OpenSpecCorpusGuards` test.

| Probe | Mutation | Result |
|---|---|---|
| P0 / P0b / P0c | pins refreshed only (positive controls) | green, 15/15 |
| N1c | a complete copy of the change as a third owner | **red**: "exactly its named active predecessor" (3 owners), in both the sync gate and Task 5.3 |
| N2 | the new change also modifies an unregistered heading | **red**: "unregistered duplicate owners" |
| N3a–N3d | registry predecessor, successor operation, stage (`Approved`), or task (1.4) changed | **red**: the source-pinned registry mismatch, in both tests |
| N4c | the `repository-foundation` successor removed, with the proposal kept consistent | **red**: 1 owner where 2 are required |
| N5 | successor block made equal to canonical (premature sync) | **red**: it must remain `PendingModification` |
| N6a / N6b | provenance claims zero pending and semantic approval / semantic approval only | **red** each time |
| N7 | task 1.3 marked complete | **red**: an open task is required |
| N8 | one word of successor text changed | **red**: recomputed provenance record SHA |
| N9 | canonical hand-edited to the successor text | **red**: the predecessor must still be `Synchronized` |

Earlier N1 and N4 variants were stopped by structural proposal checks, so they were rerun as N1c
and N4c. One probe's restore step also removed a restored delta directory. I recopied that file
byte-exact from main. The probe copy's porcelain and full content record then matched the frozen
target again, and the final restore run was green.

## 4. Non-blocking observations (P3)

- **P3-1: task 1.3 cannot complete under this guard as written.**
  - Synchronizing canonical necessarily breaks all three invariants the gate enforces:
    - the reshape predecessor stops matching canonical;
    - the successor becomes `Synchronized`;
    - task 1.3 becomes complete.
  - After that sync, the reshape predecessor blocks will also be stale forever unless something
    reconciles them.
  - Task 1.4 (the registry stage transition) is ordered after 1.3. The contract-approval target
    should either:
    - move the 1.4 transition before or into 1.3; or
    - define the post-sync predecessor disposition explicitly.
- **P3-2: the change is dated the day after the review.** `.openspec.yaml` records
  `created: 2026-09-28`, while the request and artifact say 2026-09-27. This is probably a UTC
  timestamp and is cosmetic.

## 5. Reviewer hygiene and checkpoint instructions

`HEAD` is `590df004ac346f85fe085d94ccd646165bc07e61`, with the sixteen frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 16 manifest paths from the live index;
- confirm the tree is `d4b0ac65f486c374476fe312ebb225aad6b6be4b` with parent `590df004`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it in `appendOnlyRecords`.

The activation should then change only the review-state transition. Tasks 1.1–1.2 (contract
approval) are the next gate. No friend attribute or Task 8.2 source may land before them.

## Determination

The process gate admits exactly the two named predecessor/successor pairs. It keeps the successors
visibly unapproved and pending, and it rejects every other form of duplicate ownership. It approves
no part of the friend contract.

**Verdict:** **APPROVE**
