# Harmonization Task 7.7 and RRR-1 independent review verdict

**Date:** 2026-09-23
**Reviewer:** independent review
**Scope reviewed:** the eleven-entry freeze on base
`cb17f432a00104758a157ab547e3fca143b6a3ac`, named by
`harmonize-downstream-capability-specs-task-7-7-and-rrr-1-dirty-manifest-2026-09-23.txt`.
**Authorization requested:** one checkpoint for the Task 7.7 archive provenance record and the
RRR-1 masking control. This verdict authorizes that checkpoint for the exact frozen target only.

## Summary

Both changes are correct and mutation-proven:
- **RRR-1:** the synthetic control now places declaration-shaped method lines inside a multi-line
  verbatim string and a block comment. Removing lexical masking from the control alone, or from
  both call sites, turns the owner guard red.
- **Task 7.7:** every coordinate in the new record was resolved independently from Git objects:
  - the predecessor path and commit, and the move commit as its only child;
  - both blobs, byte counts, and SHA-256 values;
  - the `R097` rename;
  - the four link-only line changes.
  The archived prompt is byte-identical to the move blob, and no protected path changed.
  Corrupting the record, the catalog, a guard coordinate, the ledger, the design text, or the
  provenance route is red, including with coherent digest refreshes.
- **Validation:** every reported count and both freeze anchors reproduce.

One P3 bundle is recorded (SSS-1). It does not block the checkpoint.

## Method

There was no dedicated review worktree for this round. All builds, tests, and mutations ran in
two disposable detached worktrees holding the same eleven entries, and in a disposable CRLF clone
built from a bundle of `HEAD`.

**Reviewer incident, disclosed.** My first CRLF-clone attempt could not clone the local path
because of Git's `safe.directory` ownership check. The unguarded script then ran its later steps
in the main repository:
- it created commit `c9bb8a0` "probe crlf checkpoint" on `feature/v3-rebuild`, whose tree was
  exactly the frozen target `ee5fe64a`;
- it then ran `git rm -r --cached .` and `git reset --hard`.

Within three minutes I restored the branch and index with
`git reset --mixed cb17f432a00104758a157ab547e3fca143b6a3ac`. Afterwards:
- `feature/v3-rebuild` points at `cb17f432` again, and nothing is staged;
- the raw porcelain is byte-identical to the frozen manifest, and the content record reproduces;
- all eleven target files equal the frozen copies byte-for-byte;
- nothing was pushed.

What remains:
- The unreferenced commit object `c9bb8a0` and three reflog entries.
- New modification times on all 1,621 tracked files.
- Any line-ending-only working copies over LF blobs, such as the 123 observed on 2026-09-21, now
  hold their LF blob bytes. Git reported those files clean before and after.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all` | 11 entries, 845 B, `6f7376e7…8621` | identical, before probing and after restoration |
| Scoped content record | 10 rows, 1,469 B, `7458f1f3…cad1` | identical |
| Entries | 8 modified, 3 untracked, 0 staged | identical |
| Simulated checkpoint | tree `ee5fe64a…` | identical from a copy of the live index; path set equals the manifest |

- **Prior chain:** checkpoint `a99ae33f` has tree `e0130dbd`, exactly the approved target.
  Evidence commit `b6405865` is its only child and adds the 2026-09-23 verdict byte-for-byte
  (8,682 B, `c8e11973…`). Activation `cb17f432` changes only the two transition values.
- **Registry:** the 19 archived freezes and 13 entries reproduce from committed objects, and every
  declared current-match pin is exact.
- **New records:** the provenance record (1,855 B, `20be3dcd…`), request (4,421 B), and manifest
  are in `appendOnlyRecords`, and `activeFreezeManifestPath` names the manifest.
- **Scope:** no `src/**` or canonical `openspec/specs/**` change.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Guards, full project | 226 passed, 14 failed, all 14 `ExecutableBehaviorExpectedRedGuards.Scenario_*` |
| `Disposition=Infrastructure`, Release | 226/226 |
| Strict OpenSpec | 18/18 |
| Harmonization ledger | 31 complete / 3 open / 34, Task 8.1 still open |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 226 passed plus the same 14 expected-red |

## 3. Task 7.7 coordinates, resolved from Git

- `ad9414088f1843dae09ef8a5d10caa8aca413561` has exactly one parent,
  `ac46d99543daf85c0fa3234272997ba40f47f96b`.
- **Predecessor:** blob `b60942be…` at that parent, 14,526 bytes, SHA-256 `14c07da0…`.
- **Archived prompt:** blob `d9d509cf…` at the move commit, 14,538 bytes, SHA-256 `80ea80cf…`.
  It is the same blob at `HEAD`, the working copy is identical, and no later commit touches it.
  The old path is absent.
- **Rename:** default rename detection reports
  `R097 docs/implementation/…-phase-00-kickoff-prompt-2026-07-15.md → docs/archive/plans/…`.
- **Content:** the move changed exactly four lines. Each only deepens a relative link by one
  `../`, for spec 17, its public-authoring companion, the reshape task ledger, and the review-E
  status record.
- **History:** `git log --follow` crosses the rename into the pre-move history.

## 4. Controls

| Probe | Mutation | Result |
|---|---|---|
| R1 / R2 | `MaskNonCode` bypassed in the synthetic control / also in the archive reader | each **red** |
| T1a–T1c | the record's `R097`, move commit, or predecessor size changed | each **red** |
| T1ac–T1cc | the same, with the append-only catalog refreshed | each **red**, guard-source digest |
| T5a–T5c | the move commit changed, "The four changed lines" removed, or `R097` changed to `R100`, with catalog and guard digest refreshed | each **red** |
| T6a / T6b | the guard's move-commit constant or predecessor-blob expectation changed | each **red** |
| T1d / T2 | the record deleted / one byte appended to the archived prompt | each **red** |
| T3a | the provenance link removed from the archive index | **red** |
| T4a–T4c | the Task 7.7 completion sentence weakened, the task unchecked, or the design decision weakened | each **red** |
| X1 | a third `docs/archive/` path constant added to the corpus guard | **red**, crosswalk archive-path guard |
| X2 | an allowed Task 7.7 coordinate retargeted to another archive file | **red** |

## 5. SSS-1 (P3): observations

- **The Task 7.7 check is not checkout-independent.** It reads raw bytes in three places:
  - it hashes the record's raw bytes;
  - it compares that raw length with the LF-normalized catalog length;
  - it compares the archived prompt's working bytes with the LF Git blob.

  In a real `core.autocrlf=true` clone, the Task 7.2 immutable-history guard now fails on the
  record hash (`78b6a37f…` against `20be3dcd…`). At base that guard passes, so CRLF failures rise
  from 6 to 7. The other six are pre-existing and CI checks out LF, the same situation rated P3
  as UU-1 on 2026-09-17. The existing `NormalizeHistoricalDocumentBytes` helper would fix this.
- **The old-path routing key is not pinned.** Changing the archive index row's predecessor key
  from `…-2026-07-15.md` to `…-2026-07-16.md` stays green (T3b). Only the provenance link text is
  asserted.

## 6. Reviewer hygiene and checkpoint instructions

`HEAD` is `cb17f432a00104758a157ab547e3fca143b6a3ac`, with the eleven frozen entries and nothing
staged. The one commit I created, `c9bb8a0`, is disclosed and unreferenced. When this verdict was
written, the repository showed exactly the frozen entries plus this new, untracked verdict. The
disposable worktrees and the clone are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 11 manifest paths from the live index;
- confirm the tree is `ee5fe64ab9790e31a6ed3364e3907cabf9c9902d` with parent `cb17f432`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict and catalog it in `appendOnlyRecords`;
- register it as an `APPROVE` row in a registry entry whose `task-{task}*verdict-*.md` glob
  discovers it, for example a new Task 7.7 entry;
- archive the active freeze.

The activation must then pin that evidence commit by its full id.

## Determination

The Task 7.7 record states exactly what Git proves about the kickoff prompt's move, and nothing
protected was edited. The guard catches coordinate, record, route, and ledger corruption. RRR-1's
masking control is now load-bearing. The remaining observations are a CRLF-checkout portability
gap and one unpinned routing key.

**Verdict:** **APPROVE**
