# Harmonization Task 7.2 YY-1 / ZZ-1 hardening independent review verdict

**Date:** 2026-09-18
**Reviewer:** independent review
**Scope reviewed:** the seven-entry Task 7.2 post-approval hardening freeze on base
`e046e04ab4dae82490604adbbc9c7baca402c59b`, tree `d6ee749421ddec60c3206dd1f52a1cbbe6c90952`, named by
`harmonize-downstream-capability-specs-task-7-2-yy-1-zz-1-hardening-dirty-manifest-2026-09-18.txt`,
which closes the Round 63 observations YY-1 and ZZ-1.
**Authorization scope:** creation of the Task 7.2 post-approval hardening checkpoint for exactly this
seven-path target. This verdict does not authorize Task 7.3 or any other task.

## Summary

Both observations are closed, the approved Task 7.2 chain carries zero drift, and every validation
count reproduces.

- **YY-1:** both addition-history queries now pass `--full-history`.
  - A record added and deleted on a side branch, then merged, is red on the merged mainline and the
    failure names its path.
  - Removing the flag from the discovery query restores the Round 63 failure mode, green on that
    merge.
  - The per-entry flag has its own measured purpose. Re-adding a path with different content
    through a merge is red with it, and green without it.
- **ZZ-1:** Task 7.7 now allows only an immutable provenance record that names the exact
  predecessor and commit. Relocation waits for a reviewed tombstone mechanism, and the whole task text
  is guard-pinned. Restoring the rename option, dropping the predecessor and commit requirement, or
  claiming relocation is allowed are each red.

One P3 observation is recorded (AAA-1). The per-entry `--full-history` also turns an identical copy
of a checkpoint, merged back into its own line, into a permanent failure. It fails closed, and this
repository has no merges, so it does not block.

## Method

All probing ran in two fresh disposable `git worktree` copies created from `e046e04a`, one for
validation, the simulated checkpoint, and a simulated approval chain, and one for mutation probes.
Each received the seven frozen entries byte-for-byte, and its porcelain was confirmed byte-identical
to the frozen manifest.

The reviewed worktree was never modified, `HEAD` never moved, and nothing was staged or committed in
it. Every probe commit was detached, no branch or other ref was created, and both worktrees are
removed. The history-flag probes rebuilt the guard assembly inside the probe worktree with one flag
removed, then restored the source byte-exact and rebuilt it.

## 1. The approved Task 7.2 chain carries zero drift

- **Checkpoint:** `cfa5f2ab` has parent `261d578b` and tree `7b7a30a24fcc7169084829800e7ddab82c440d8e`,
  identical to the tree of my Round 63 simulated checkpoint, at exactly twenty-five paths.
- **Approval evidence:** `4cef1481` changes exactly three paths.
  - It adds only the Round 63 verdict, whose committed blob hashes to
    `095928655161fb4fb7b75fe3c0180b376a0f78896f43c1e95e97a46c9184a52a`.
  - It catalogs that verdict with those normalized bytes.
  - It clears the active freeze and adds archived freeze `7.2-ww-1-remediation`: 25 lines,
    `02fb612a…`, checkpoint `cfa5f2ab`, content record 3,880 bytes `be0f3e97…`.
  - It adds a Task 7.2 entry that registers all four 7.2 verdicts (three `REJECT`, one `APPROVE`).
- **Activation:** `e046e04a` changes exactly two values: `approvalEvidenceCommit` and `Approved`.
- **Projection:** in a fresh worktree, all fifteen archived freezes, including the new one,
  reproduce manifest, content record, and tree from their checkpoint blobs. All eleven entries
  reproduce their historical rows with order-exact maximal pins; Task 7.2 has twenty.

## 2. Freeze anchors reproduced

- **Raw commit-real porcelain:** **602 bytes**, SHA-256
  `41fa3fe2c09940862fed137e3484de79351ab6043ea69f0438a9e70142d7910a`, byte-identical to the frozen
  manifest. Seven entries (five modified, two untracked), none staged, index tree equal to the `HEAD`
  tree.
- **Scoped content record:** **6 rows, 943 bytes**, SHA-256
  `175202095d86cbdee795dfccf48b40601ab883e2b6136abe59425a893b9af558`. The review-provenance fixture is
  its only exclusion.
- **Active freeze:** it names the same values under the distinct task key
  `7.2-post-approval-hardening`, so it does not collide with the historical Task 7.2 entry.
- **Catalog:** it adds exactly the two new review files, and `activeFreezeManifestPath` names this
  manifest.
- **Task 7.2 pins:** its current-match pins drop exactly the three rows this target edits.
- No path under `src/**` or `openspec/specs/**` changes.

## 3. YY-1 closed

Both queries in `ValidateAppendOnlyHistoricalRecords` gain `--full-history`
(`OpenSpecCorpusGuards.cs:1950` and `:1984`), and the design states it (`design.md:233-235`).

On the committed simulated checkpoint, I used detached commits only:

| Probe | History | Both flags | Discovery flag removed | Per-entry flag removed |
|---|---|---|---|---|
| Y1 | side line adds and catalogs a reshape-family record, deletes it and its entry, `--no-ff` merged | **red**, names the path | green (Round 63 mode) | **red** |
| Y2 | same side line merged into a mainline that re-adds the path with different content | **red**, two first additions | **red** | green |
| Y3 | identical copy of the checkpoint (cherry-pick onto its parent) merged back | **red**, two first additions | **red** | green |

- **Y1:** it satisfies the request's required mutation, and removing the discovery flag restores
  exactly the reviewed failure mode.
- **Y2:** it shows why the per-entry flag is there. Without it, a path deleted on a merged branch
  can be re-added with new content and bound to the new bytes.
- **Y3:** it is the subject of AAA-1.

Baseline and final committed controls were green, and the guard source was restored byte-exact.

## 4. ZZ-1 closed

Task 7.7 (`tasks.md:380-383`) now reads, in full:

- repair the kickoff-prompt archive move by adding "an explicit immutable archive provenance record
  that names the exact predecessor and commit";
- "do not rename, delete, or edit an existing protected path";
- any future relocation needs a separately reviewed tombstone mechanism that the current contract
  does not provide.

The guard pins the whole task text (`OpenSpecCorpusGuards.cs:149`, asserted at `:1875`). These
mutations are each red:

| Probe | Mutation | Result |
|---|---|---|
| Z1 | the old rename option restored | **red** |
| Z2 | "names the exact predecessor and commit" removed | **red** |
| Z3 | relocation claimed to be provided | **red** |

The Task 7.2 completion record and its guard constant both gain the matching "Post-approval
hardening" sentence.

## 5. Validation reproduced from a clean checkout

| Gate | Result |
|---|---|
| Debug and Release builds, non-incremental, warnings as errors | 0 warnings, 0 errors each |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / Certification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Full guard lane, dirty target | 223 passed, 14 failed, 237 total |
| Classification of the 14 failures | all `ExecutableBehaviorExpectedRedGuards.Scenario_…`, zero others |
| Infrastructure lane, Release, CI filter `Disposition=Infrastructure` | 223/223 |
| Expected-red lane | exactly 14 |
| Committed simulated checkpoint, full guard lane | 223 passed, 14 expected red |
| Simulated evidence and activation, Infrastructure lane | 223/223 |
| OpenSpec `validate --all --strict` | 18 / 18 |
| Harmonization ledger | 26 complete, 8 open, 34 total |
| Review-manifest current matches | order-exact for all eleven entries (emulated) |
| `git diff --check` / changes under `src/**` or `openspec/specs/**` | clean / zero |
| Porcelain after validation | byte-identical to the manifest |

## 6. Observation AAA-1 (P3): identical re-additions are also rejected

With `--full-history`, the per-entry lookup counts every commit that adds a path. That includes an
identical copy of an addition brought in through a merge.

In Y3, the cherry-picked copy of the checkpoint has the same tree, and the merge tree equals the
checkpoint tree. No record is changed or deleted, yet the guard reports two first additions for each
record the checkpoint added and stays red. The only remedy would be a history rewrite or a guard
change.

The realistic way to get there is a squash merge of this branch into `master` that is later merged
back into a line holding the original commits. Before this target, default simplification kept Y3
green, and Y3p confirms it.

This does not block, for three reasons:

- it fails closed, so no tampering can pass;
- the branch has no merge commits, and `master` is its ancestor;
- the failure message is explicit.

A tighter rule would require every first-addition commit to carry the cataloged normalized bytes,
instead of requiring exactly one. That would keep Y2 red and let Y3 pass. Otherwise, avoid squash
merges back into lines that still hold the original checkpoints.

## 7. Simulated checkpoint and sequencing

Committing the seven frozen entries on `e046e04a` in a disposable worktree produced exactly seven
paths, two added and five modified. The resulting tree is `ec8f79e6f9fabc98b02360632e6e1a2c6a885fd4`.
The worktree was clean afterwards, and its full guard lane is 223 passed and 14 expected red.

Writing this verdict creates an **eighth** entry against the declared seven. Commit the approved
**seven-path** checkpoint first, then add this verdict in the evidence commit. Its filename matches
the Task 7.2 discovery glob `harmonize-downstream-capability-specs-task-7-2*verdict-*.md`. Following
the Task 7.1 hardening pattern (`a7d9c52`, `261d578b`), the evidence commit must:

- catalog this verdict in `appendOnlyRecords` with its LF-normalized bytes;
- clear the active freeze and add archived freeze `7.2-post-approval-hardening`;
- register this verdict as the fifth Task 7.2 verdict and make it the state evidence;
- point the Task 7.2 entry's reviewed target at the new checkpoint, with state
  `ApprovalAwaitingEvidenceCommit`.

Activation then sets `approvalEvidenceCommit` and `Approved`. My simulation of exactly this sequence
was 223/223 on the Infrastructure lane.

## 8. Reviewer hygiene

`HEAD` remained at `e046e04ab4dae82490604adbbc9c7baca402c59b` throughout. Nothing was staged, and I
created no commit or ref in the reviewed repository. It still showed exactly the seven frozen entries
when this verdict was written. Both disposable worktrees are removed and pruned.

## Determination

This is a small, precise hardening:

- the history traversal now matches its documented contract, and both flags are mutation-proven;
- Task 7.7 is aligned with the no-relocation rule and pinned against the three regressions that
  matter;
- the approved chain reproduces down to tree identity with my own rehearsal, and validation
  reproduces.

AAA-1 is a fail-closed edge in a merge topology this repository does not use.

**Verdict:** **APPROVE**
