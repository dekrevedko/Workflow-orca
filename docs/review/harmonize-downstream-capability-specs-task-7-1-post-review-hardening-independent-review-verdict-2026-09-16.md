# Harmonization Task 7.1 post-review hardening independent review verdict

**Date:** 2026-09-16
**Reviewer:** independent review
**Scope reviewed:** the five-entry Task 7.1 post-review hardening freeze on base
`e7f8e26c54d6e00d3bb0ac1e2632a277ec0991e1`, tree `ccd4ad73eb2da5c039a41670ad3e1207d3e2fc4e`, named by
`harmonize-downstream-capability-specs-task-7-1-post-review-hardening-dirty-manifest-2026-09-16.txt`,
which closes Task 7.1 observations MM-1 and NN-1.
**Authorization scope:** creation of the Task 7.1 MM-1/NN-1 hardening checkpoint only. This verdict
does not authorize Task 7.2, reshape task 9.6 membership changes, change archival, or Task 8.0.

## Summary

Both observations are closed:

- **MM-1:** the Task 7.1 design decision now states that the companion record is a
  pre-finalization snapshot whose `design.md` and `tasks.md` rows predate their final text and do
  not represent the checkpoint-tree digest. I verified that claim against the committed checkpoint:
  exactly those two rows differ, and the checkpoint tree recomputes to `a929a756…`, not `56b6d27e…`.
  The guard pins the whole clarification, and deleting, inverting, or narrowing it is red.
- **NN-1:** the joined statements are split and the doubled blank line removed. The change is
  whitespace-only, and no other instance of either defect remains in the file.

The historical-match refresh is mechanical and exact: Task 7.1 falls from 18 to 16 for precisely the
two files this target changes, and no other entry moves. Validation reproduces, and the simulated
checkpoint matches the freeze's named tree.

One observation is recorded, not blocking:

- **OO-1 (P3):** the clarification names the rows only by filename. The record holds four
  `design.md` and four `tasks.md` rows, one of each per active change, and only this change's two
  differ.

## Method

All probing ran in two disposable `git worktree` copies created from `e7f8e26c`, one for validation
and the simulated checkpoint and one for mutation probes. Each received the five frozen entries
byte-for-byte with porcelain confirmed byte-identical to the frozen manifest. The reviewed worktree
was never modified, `HEAD` never moved, and nothing was staged or committed in it. Both worktrees
were removed and pruned, and the one throwaway commit is contained in no ref.

Each probe re-pinned the active freeze's content-record anchor to the mutated bytes, so only the
durable check under test could report. PowerShell 7 is absent on this host, so the review-manifest
`-Check` was reproduced by the Python emulation used since round 54.

The request discloses a corrupted generated Release reference assembly (`CS0009`) during the
author's first focused run. My builds were independent, non-incremental, and clean; see section 6.

## 1. Freeze anchors reproduced

- **Raw commit-real porcelain:** **456 bytes**, SHA-256
  `bbf28be6608c5da5b544bc453c31ad60b2c720408c34b9946b9bbb209dec6fab`, byte-identical to the frozen
  manifest. Five entries (three modified, two untracked), none staged, index tree `ccd4ad73` equal
  to the `HEAD` tree.
- **Scoped content record:** **4 rows, 655 bytes**, SHA-256
  `d26484c4caba60067b560bc01424fc424b85a18b5c1f03a0ac306f5248742e31`, with the review-provenance
  fixture as its only exclusion. `activeFreeze` names the same values.

## 2. The approved Task 7.1 chain carries zero drift

- **Checkpoint:** `de73ca52` has parent `99657834` and tree `bd519b0dd64e9dc6705b867faa0c63405351d04a`,
  identical to the tree of my own round-58 simulated checkpoint, at exactly nineteen paths.
- **Approval evidence:** `3142ab22` adds only the round-58 verdict, whose committed blob hashes to
  `fee564f4fdb337d24e59427e49bc2292b2ba9fea37b1ae9f839e089d55f24ebc`, plus its registration.
- **Activation:** `e7f8e26c` changes exactly two values.
- **Registration:** the new Task 7.1 entry registers all three Task 7.1 verdicts — both `REJECT`
  verdicts (`b15e501f…`, `19a64192…`) and the `APPROVE` verdict (`fee564f4…`) — with state `Approved`.
- **Projection:** all thirteen archived freezes, including the new `7.1-round-57-remediation` freeze
  at 18 rows / 2,936 bytes, reproduce manifest, content record, and tree from their checkpoint blobs.
  All ten entries reproduce their historical rows with maximal pins, and the emulated `-Check` is
  order-exact. Both rejected freezes remain registered.

## 3. MM-1 closed — the snapshot timing is disclosed, true, and pinned

The design decision gains this clarification:

> The companion is a pre-finalization scan snapshot: its `design.md` and `tasks.md` rows
> intentionally predate their final self-describing remediation text and therefore do not represent
> the checkpoint-tree digest.

I tested it against both relevant trees, reconstructing every companion row from the files:

| Tree | Rows that differ from the companion | Recomputed digest |
|---|---|---|
| Committed checkpoint `de73ca52` | exactly this change's `design.md` and `tasks.md` | `a929a756…` |
| This hardening target | exactly the same two rows | `18993dbb…` |

Both differ from the companion's `56b6d27e…`, so the statement that the companion does not represent
the checkpoint-tree digest is true, and it stays true after this target commits. The companion itself
is untouched, at 10,655 bytes and `56b6d27e…`.

The guard constant `Task71ReviewRemediationDesignDecision` now ends with the full clarification, and
the Task 7.1 guard requires the design to contain it. Deleting the clarification (M1), inverting it
(M2), or narrowing it to one file (M3) is each red on that guard.

## 4. NN-1 closed — whitespace-only

`git diff -w` on the guard reduces to the clarification constant plus the split of the joined line;
neither `design.Should().Contain(…)` assertion changes in content. No other line in the file ends one
statement and begins another, and the file contains no remaining pair of consecutive blank lines.

## 5. Historical-match refresh is exact

Task 7.1's `currentWorktreeMatchPaths` drops `design.md` and `OpenSpecCorpusGuards.cs`, the two
historical Task 7.1 rows whose live bytes this target changes, going from 18 to 16. The recomputed
maximal set equals the recorded set, in order. Re-adding the stale `design.md` pin (P1) and dropping
the still-valid `tasks.md` pin (P2) are both red. Moving an unrelated Task 6.6 pin (P3) is red, and
every other entry is unchanged.

## 6. Validation reproduced from a clean checkout

| Gate | Result |
|---|---|
| Debug build, non-incremental, warnings as errors | 0 warnings, 0 errors |
| Release build, non-incremental, warnings as errors | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / Certification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Full guard lane | 222 passed, 14 failed, 236 total |
| Classification of the 14 failures | all `ExecutableBehaviorExpectedRedGuards.Scenario_…`, zero others |
| OpenSpec `validate --all --strict` | 18 / 18 |
| Harmonization ledger | 25 complete, 9 open, 34 total |
| Review-manifest current matches | order-exact for all ten entries (emulated; PowerShell 7 absent) |
| `git diff --check` / changes under `src/**` or `openspec/specs/**` | clean / zero |
| Porcelain after validation | byte-identical to the manifest |

The request's `CS0009` note does not recur: both configurations built non-incrementally from a fresh
worktree with no stale generated metadata.

## 7. Mutation and probe results

| Probe | Mutation | Result on the owning check |
|---|---|---|
| M1 | Whole MM-1 clarification deleted from the design | **red**, Task 7.1 guard |
| M2 | Clarification inverted to "represent the checkpoint-tree digest" | **red**, Task 7.1 guard |
| M3 | Clarification narrowed to the `design.md` row only | **red**, Task 7.1 guard |
| P1 | Stale `design.md` re-added to Task 7.1 current matches | **red**, current-match pin |
| P2 | Valid `tasks.md` pin dropped from Task 7.1 | **red**, current-match pin |
| P3 | Unrelated Task 6.6 pin moved | **red**, current-match pin |
| R1 | Companion re-sorted case-insensitively (regression) | **red**, ordinal-order assertion |
| R2 | One byte appended to the initial `REJECT` verdict | **red**, Task 7.1 current-match pin |
| R2b | R2 followed by the routine current-match refresh | **red**, "immutable review evidence must remain byte-exact" |

R2 first reports on the Task 7.1 current-match pin because that rejected verdict is now also a
historical Task 7.1 row, and that pin check runs before the evidence-hash check. The routine refresh
would clear the pin, so R2b repeats the tamper after refreshing and confirms the durable
verdict-evidence hash still rejects it. Baseline and final runs were green, and restoration was
verified byte-exact against the frozen target after every probe.

## 8. Observation OO-1 (P3) — the clarification names rows by filename only

The companion contains four `design.md` rows and four `tasks.md` rows, one of each for
`add-runtime-concurrency-limits`, `bootstrap-orcacore-spec-baseline`,
`harmonize-downstream-capability-specs`, and `reshape-developer-facing-interfaces`. Only this change's
pair predates its final text; the other six rows reproduce exactly.

The clarification's "its `design.md` and `tasks.md` rows" takes "its" as the companion, so it can
fairly be read as all eight. The sentence sits inside this change's own design, which points a
careful reader to the right pair, and a row-level check resolves it in seconds. That keeps this at
P3. Naming the rows by full path, or saying "this change's own", would remove the ambiguity at the
next touch of the design. The guard constant pins the current wording, so the change needs a paired
constant update.

## 9. Simulated checkpoint and sequencing

Committing the five frozen entries on `e7f8e26c` in a disposable worktree produced exactly five
paths, two added and three modified. The resulting tree is
`c612ed39d67556ee65670baf54095d9821d62e7e`, which equals the tree named in the freeze. The worktree was
clean afterwards, and the full guard lane in that committed state is 222 passed, 14 failed, 236
total, with all 14 failures the documented expected-red scenarios.

Writing this verdict creates a **sixth** entry against the declared five, which reddens the
active-freeze guard. Commit the approved **five-path** checkpoint first, then add this verdict in the
following evidence commit. Its filename matches the Task 7.1 discovery glob
`harmonize-downstream-capability-specs-task-7-1*verdict-*.md`, so the Task 7.1 entry must register it
as its fourth verdict.

## 10. Reviewer hygiene

`HEAD` remained at `e7f8e26c54d6e00d3bb0ac1e2632a277ec0991e1` throughout, nothing was staged, and I
created no commit in the reviewed repository. It still showed exactly the five frozen entries when
this verdict was written. Both disposable worktrees were removed and pruned.

## Determination

This is a small, precise hardening. The snapshot-timing disclosure is true at both the committed
checkpoint and this target, is pinned by the guard against deletion, inversion, and narrowing, and
leaves the immutable companion untouched. The formatting fix is whitespace-only and complete. The
pin refresh moves exactly the two rows it should. The chain carries zero drift down to tree identity
with my own rehearsal, and validation reproduces.

OO-1 is a wording ambiguity that a row-level check resolves immediately.

**Verdict:** **APPROVE**
