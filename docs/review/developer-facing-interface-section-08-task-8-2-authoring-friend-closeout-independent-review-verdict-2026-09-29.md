# Authoring-friend task 3.2 closeout independent review verdict

**Date:** 2026-09-29
**Reviewer:** independent review
**Scope reviewed:** the nineteen-entry freeze on base `738c3b591b6f6e72de13a7750601cb36430c514f`,
named by
`developer-facing-interface-section-08-task-8-2-authoring-friend-closeout-dirty-manifest-2026-09-29.txt`.
This is `admit-dag-authoring-friend-boundary` task 3.2, the permanent-record closeout of the
approved Task 8.2 source.
**Authorization requested:** checkpoint of this closeout target only. This verdict does not
authorize:
- Task 8.3 source;
- a `Dag -> Dag.Hosting` friend;
- any codec access for `OrcaCore.Dag`.

## Summary

The closeout records the approved Task 8.2 implementation truthfully, and its new checks resist
coherent forgery:

- **Registry.**
  - Schema 5 keeps `Proposed` and `ApprovedPending` as history.
  - It adds a source-pinned `Complete` record bound to:
    - the real checkpoint `a9f835f` and its tree `cf11b3f6`;
    - the direct-child evidence `055e7b8` and the verdict bytes;
    - the byte-exact refreeze manifest and request;
    - the five implementation tasks;
    - three executable evidence files, each present in the checkpoint.
  - Tasks 2.1–2.5 and 3.1 are checked, and task 3.2 stays open.
- **Approval validation.** Both contract and source approvals now require:
  - exactly one `**Verdict:** **APPROVE**` line, as the final nonblank line;
  - a single-parent evidence commit whose parent is the reviewed checkpoint;
  - a real `A` addition of the verdict in that commit.
- **Metadata probes.** Temporary probe projects copy and verify the repository `global.json`
  before building, so the SDK pin now applies. This resolves round 84's P3-3.
- **Documentation.**
  - `CLAUDE.md`, documents 03, 08, 10, and 17, Decision 22, the solution architecture, and the
    overview now name the approved checkpoint. This resolves round 84's P3-2.
  - The dated Decision 22 entries keep their bytes, and a 2026-09-29 entry is appended.
- **Scope:**
  - no product source, canonical spec, friend, codec reference, test declaration, API, package, or
    crosswalk change;
  - provenance stays at 178 rows, `0dd47120…`, with zero pending.

No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree. Probes ran in a second one. Both held the same
nineteen entries. Every script asserted its disposable location and that the main `HEAD` was still
`738c3b59`. Main was never modified, and the review created no ref in the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, commit-real paths | 19 lines, 1,354 B, `53eb5278…6d80` | identical; the union of diff and untracked paths is exactly 19 |
| Semantic record: 15 rows, ordinal path sort, `path<TAB>bytes<TAB>sha256` | 1,958 B, `6fbd91e9…04c1` | identical with the stated recipe |
| Active record: all except `review-manifest-provenance.json`, rendered-record ordinal sort | pinned in the fixture | 18 rows, 2,545 B, `58462995…`, equal to the fixture |
| All-file content record, 19 rows (reviewer) | — | 2,700 B, `d65e7de63dfb78afe342bf525178cde5b7e14787bff28d5abc8b8791618e645d` |
| Simulated checkpoint | tree `2ae62652c4ab763c539b3c93c2be3bb26398ab26` | identical from a copied index and from a committed disposable copy; parent `738c3b59`; 3 A / 16 M; all blobs equal the raw bytes |

- **Prior chain:**
  - checkpoint `a9f835f` has the approved tree `cf11b3f6`;
  - evidence `055e7b8` is its only child and adds the round-84 verdict byte-exact (8,742 B,
    `513871b8…`);
  - activation `738c3b5` changes review state only.
- **Immutable records:**
  - the round-84 refreeze manifest (`c9462505…`), request (`c2e58b56…`), and verdict are
    byte-exact;
  - the new manifest and request are cataloged, and the history catalog names this manifest.
- **Pins:**
  - all 22 Task 7.3 rows (six refreshed) and the artifact digest reproduce;
  - the closeout artifact pin (`00c1fd7e…`) reproduces;
  - the maximal current-match refresh reproduces the fixture byte-for-byte.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 240/240 |
| `ExecutableBehaviorExpectedRedGuards` | exactly 14 failures |
| Package fixtures, `Green` / `ExpectedRed` | 8 green / exactly `dag-hosting` red |
| Strict OpenSpec | 19/19 |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 240 passed plus the same 14 expected-red |
| `src/**` and canonical `openspec/specs/**` | unchanged |

## 3. Independent negative controls

These are my own controls, independent of the nine the author reported. Each was restored
byte-exactly, with rebuilds where source changed.

| Control | Mutation | Result |
|---|---|---|
| K0 / K0m | unmodified: canonical gate / metadata guard | green / green |
| K1 | fixture `Complete` → `ApprovedPending` | **red**: source-pinned complete record |
| K2 | fixture and source evidence commit → activation `738c3b5`, which contains the verdict | **red**: not a single-parent direct child of the checkpoint |
| K3 | fixture and source checkpoint → `db5f3fb` | **red**: the evidence commit's parent mismatches |
| K4 | task 2.3 reopened | **red**: task-state evidence |
| K5 | unauthorized seam sentence appended to the closeout artifact | **red**: artifact pin |
| K6 | text appended after `APPROVE` in the source verdict, with its SHA repinned in fixture and source | **red**: the verdict must be the final nonblank line |
| K7 | fixture and source evidence path → a file absent from the checkpoint | **red**: unreadable from the checkpoint |
| K8 | `global.json` copy removed from the probe helper | **red**: missing pinned SDK file |
| R0 / R0m | restored | green / green; porcelain equals the manifest |

## 4. Non-blocking observation (P3)

- **The Task 8.0 map still describes the old status.** Its 8.4 row says the friend "is compiled in
  the Task 8.2 source candidate, whose separate implementation review remains pending". The map is
  outside this target and is a pinned change artifact, not a normative, binding, or guide document.
  The upcoming runtime-view amendment must edit that same 8.4 row to add the `Dag -> Dag.Hosting`
  seam; refresh its status there.

## 5. Reviewer hygiene and checkpoint instructions

`HEAD` is `738c3b591b6f6e72de13a7750601cb36430c514f`, with the nineteen frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 19 manifest paths from the live index;
- confirm the tree is `2ae62652c4ab763c539b3c93c2be3bb26398ab26` with parent `738c3b59`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it;
- clear the active freeze.

The activation should then check task 3.2 only. After that chain, the separate `Dag -> Dag.Hosting`
runtime-view amendment may be prepared. It must keep fixed-codec normalization, fingerprinting, and
commit on the durable bridge, not in `OrcaCore.Dag`.

## Determination

The authoring-friend amendment now has a permanent, forgery-resistant completion record bound to
the real independently approved implementation. Its approval checks are strict, and its
documentation status is current. It admits no new seam.

**Verdict:** **APPROVE**
