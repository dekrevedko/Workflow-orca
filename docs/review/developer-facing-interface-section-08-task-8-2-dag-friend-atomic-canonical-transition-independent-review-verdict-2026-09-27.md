# DAG friend atomic canonical/registry transition independent review verdict

**Date:** 2026-09-27
**Reviewer:** independent review
**Scope reviewed:** the twenty-two-entry freeze on base
`fcbfcab1621d5b46219e58d9936776beac345dcc`, named by
`developer-facing-interface-section-08-task-8-2-dag-friend-atomic-canonical-transition-dirty-manifest-2026-09-27.txt`.
It covers `admit-dag-authoring-friend-boundary` tasks 1.3 and 1.4 together.
**Authorization requested:** checkpoint of the atomic canonical synchronization and the
`ApprovedPending` registry transition. This verdict authorizes that checkpoint for the exact frozen
target only.

## What this verdict does not approve

It adds no friend attribute and changes no compiled metadata, public API, or package edge. It does
not implement Task 8.2 product source, the shared hash operation, or the member-reference guard.
Task 2.1 and the later implementation tasks remain open, and each needs its own reviewed target.

## Summary

The transition is exact and atomic:
- **Canonical sync:** each canonical spec changes in exactly one requirement block. That block now
  equals the approved `MODIFIED` delta byte-for-byte; the preambles, every other block, and the
  heading order (18 and 10 blocks) are unchanged.
- **Registry:**
  - it keeps the two `Proposed` records as history;
  - it adds two source-pinned `ApprovedPending` rows whose pre-sync and post-sync block hashes
    recompute independently;
  - it binds the real approval: the round-81 verdict is byte-pinned, evidence commit `bbac097`
    contains it byte-exact, and that commit's parent is the reviewed checkpoint `89a2b47`.
- **Predecessors:** the reshape predecessor deltas are retained byte-exact and reclassified as
  `SupersededByApprovedSuccessor` only for those two hash-matched identities.
- **Provenance:** 173 synchronized, 2 superseded, 0 pending; semantic approval is now eligible.
- **Documents:** every active document now calls the friend approved but not compiled.
- **Scope:** nothing under `src/**` changed, and `OrcaCore` still grants friends only to Core and
  the two engines.

All 13 guard probes were red at their intended checks. No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree. Probes ran in a second one. Both held the same
twenty-two entries. Every script asserted its disposable location and that the main `HEAD` was
still `fcbfcab1`. Main was never modified, and the review created no ref in the reviewed
repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 22 lines, 1,606 B, `3dfdd796…0581` | identical |
| Semantic content record: 19 rows, `path<TAB>bytes<TAB>sha256`, excluding the request, manifest, and history catalog | 2,539 B, `9912dbc9…b091` | identical |
| All-file content record, 22 rows (reviewer convention: status, path, bytes, SHA) | — | 3,164 B, `7b95c3ca8256c7aeefc44dedf5f6cfb77cf869b1c6b6427fc1bd7675ea258b30` |
| Simulated checkpoint | tree `0eb146f6566c2ac8568b873163b3eae30c13e2db` | identical from a copy of the live index and from a committed disposable copy; parent `fcbfcab1`; path set equals the manifest (3 A / 19 M) |

- **Byte binding:** the repository sets `core.autocrlf=false`. All 22 blobs in the tree equal the
  raw file bytes.
- **Prior chain:**
  - checkpoint `89a2b47` has tree `3cc2b8eb` (the round-81 approved target), with parent
    `5adddc3b`;
  - evidence `bbac097` is its only child and adds the round-81 verdict byte-exact (10,269 B,
    `39803173…`);
  - activation `fcbfcab` flips one checkbox (task 1.2).
- **Records:** the new request (5,290 B, `eef3b2f1…`) and manifest are cataloged, and the
  active-freeze pointer names this manifest.

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
| `src/**`, compiled friend declarations | unchanged; `OrcaCore` grants only `OrcaCore.Core` and the two engines |

## 3. Canonical, registry, and provenance checks

- **Canonical blocks:**
  - `developer-facing-surface` "Implementation package boundaries use exact internal friends" and
    `repository-foundation` "Dependency direction remains one-way" each equal their approved
    successor delta block exactly;
  - at the base, each canonical block equaled its reshape predecessor block;
  - no other block, preamble, or ordering changed. The diff is 14 insertions and 2 deletions across
    the two files.
- **Block hashes:** the registry's normalized block hashes recompute independently.

  | Capability | Historical (pre-sync) | Canonical (post-sync) |
  |---|---|---|
  | `developer-facing-surface` | `e12c77e5…` | `bed102a2…` |
  | `repository-foundation` | `d0d512ea…` | `bbae0c22…` |

- **Guard:** it pins these in source and requires, per identity:
  - exactly the registered predecessor/successor owners;
  - the predecessor block to equal the historical hash, and the successor to be `Synchronized`;
  - tasks 1.2, 1.3, and 1.4 complete, and 2.1 open;
  - the verdict bytes, and the evidence commit's blob and parent.
- **Provenance:**
  - the fixture records 178 rows, 46,784 B, `0dd47120…`, with zero pending and semantic approval
    eligible;
  - the new artifact (`f174df2b…`) is source-pinned whole;
  - the prior process artifact is byte-unchanged and cataloged as superseded (`85cf55f1…`);
  - the Section 7B completed amendment is unchanged.
- **Pins:**
  - all 22 Task 7.3 rows recompute (rows 1, 6, 7, 12, 14, and 18 refreshed), and the artifact
    digest equals its guard constant;
  - the Task 8.0 map pin (`f4d87af4…`) is correct;
  - the maximal current-match refresh reproduces `review-manifest-provenance.json` byte-for-byte.
- **Documents:**
  - `CLAUDE.md`, documents 03, 08, 10, and 17, Decision 22, the solution architecture, the
    overview, and the Task 8.0 map now say "approved, not yet compiled";
  - CP-020 and PR-005 use the exact "No OrcaCore package other than `OrcaCore.Dag.Hosting`"
    exclusion, resolving round 81's P3-2;
  - the amendment design scopes "active document" to exclude change-local design rationale,
    resolving round 81's P3-1.

## 4. Guard probes

Each probe refreshed the mutable current-match pins after its edits and ran every
`OpenSpecCorpusGuards` test.

| Probe | Mutation | Result |
|---|---|---|
| P0 | pins refreshed only (positive control) | green, 15/15 |
| A1 | fixture approval-verdict SHA forged | **red**: source-pinned evidence |
| A2 | evidence commit re-pointed to `fcbfcab` in both fixture and source (rebuilt) | **red**: not the reviewed checkpoint's direct child |
| A3 | reshape predecessor block reworded | **red**: historical block hash |
| A4 | complete copy of the change as a third owner | **red**: 3 owners, in both the sync gate and Task 5.3 |
| A5 | synchronized canonical block altered | **red**: the successor is no longer `Synchronized` |
| A6 | an unrelated canonical requirement hand-edited | **red**: the provenance record changes, so the reclassification hides nothing else |
| A7 | task 2.1 marked complete prematurely | **red** |
| A8 | task 1.3 unchecked | **red** |
| A9 | registry stage `ApprovedPending` → `Complete` | **red**: source-pinned evidence |
| A10 | fixture canonical block SHA forged | **red**: source-pinned successor rows |
| A11 | provenance fixture claims semantic approval false | **red** |
| A12 | new provenance artifact altered | **red**: source-pinned artifact digest |
| A13 | superseded process artifact altered | **red**: superseded catalog |
| R0 | restored | green; porcelain equals the manifest |

## 5. Non-blocking observations (P3)

- **P3-1: a dated entry was reworded in place.**
  - The 2026-09-27 Decision 22 proposal entry in `00-stack-decisions.md` changed its closing clause
    ("the compiled graph is unchanged until separate contract and source reviews" → "This entry
    records the proposal state before independent review").
  - The request describes it as retained as history. The meaning is preserved, but dated decision
    entries should be append-only.
- **P3-2: the approval-evidence checks are looser than they could be.**
  - `RequireApprovalEvidence` now accepts `**Verdict:** **APPROVE**`, but it matches any such line
    rather than requiring exactly one terminal verdict line.
  - The direct-child check reads only the first parent (`<commit>^`).
  - Both are harmless here, because the verdict bytes and both commit IDs are pinned in guard source.
    A later general-purpose use should require a single terminal verdict and a single parent.

## 6. Reviewer hygiene and checkpoint instructions

`HEAD` is `fcbfcab1621d5b46219e58d9936776beac345dcc`, with the twenty-two frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 22 manifest paths from the live index;
- confirm the tree is `0eb146f6566c2ac8568b873163b3eae30c13e2db` with parent `fcbfcab1`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it in `appendOnlyRecords`.

The activation should then change only the review state. Task 2.1 (the friend attribute), the
shared hash operation, the Task 8.2 authoring implementation, and the member-reference guard remain
a separate reviewed target.

## Determination

The approved friend contract is now canonical, and the registry is truthfully `ApprovedPending`. It
is bound to the real reviewed checkpoint and verdict, and its predecessor history is intact and
hash-pinned. No product source, friend metadata, or implementation claim landed.

**Verdict:** **APPROVE**
