# Harmonization Task 7.2 WW-1 remediation independent review verdict

**Date:** 2026-09-18
**Reviewer:** independent review
**Scope reviewed:** the twenty-five-entry superseding Task 7.2 freeze on base
`261d578b11a4b2d09e033aa44c8123cbc4ec653e`, tree `cc4943fa88d38e23726d435e84b6bc6602222cbc`, named by
`harmonize-downstream-capability-specs-task-7-2-ww-1-remediation-dirty-manifest-2026-09-18.txt`,
which remediates Round 62 findings WW-1 and XX-1.
**Authorization scope:** creation of the Task 7.2 immutable-history checkpoint for exactly this
twenty-five-path target. This verdict does not authorize Task 7.3 or any other task.

## Summary

WW-1 and XX-1 are closed, every earlier Task 7.2 finding stays closed, and every anchor, validation
count, and mutation control in the request reproduces.

- **WW-1:** the guard now derives every post-baseline addition under either historical root from
  Git history and requires each path to stay present and cataloged. The following are all red, and
  each failure names the historical path taken from Git:
  - deleting a committed record with its catalog entry, before and after committing the deletion;
  - a `git mv` with the catalog path updated, before and after committing the move;
  - moving a record out of the roots;
  - deleting a committed archive record.
- **XX-1:** the design, Task 7.2 completion, and archive rule now say the active manifest names each
  uncommitted record. The stray short lines are gone, and the guard pins the new text.
- **Earlier findings:** PP-1, QQ-1, RR-1, SS-1, TT-1, UU-1, and VV-1 all stay closed under their
  original controls. All three rejected Task 7.2 packets are byte-exact, registered, and cataloged.

Two P3 observations are recorded (YY-1, ZZ-1). Neither blocks. The request's disclosed rehearsal
commit in the main worktree is verified harmless (section 2).

## Method

All probing ran in fresh disposable `git worktree` copies created from `261d578b`:

- one for validation, the simulated checkpoint, and a simulated approval chain;
- one for mutation probes.

Each received the twenty-five frozen entries byte-for-byte, and its porcelain was confirmed
byte-identical to the frozen manifest. The reviewed worktree was never modified, `HEAD` never moved,
and nothing was staged or committed in it. No branch or other ref was created: every probe commit was
detached and is contained in no ref, and both worktrees are removed.

The line-ending logic is unchanged from Round 62, where a real `core.autocrlf=true` clone added no
failures, so no CRLF clone was rebuilt this round. The CRLF-only controls below still ran.
PowerShell 7 is absent on this host, so the current-match `-Check` and the routine refresh were
reproduced by the Python emulation used since round 54.

## 1. Freeze anchors reproduced

- **Raw commit-real porcelain:** **2,268 bytes**, SHA-256
  `02fb612ac79010a4f104de9d0ba7a90d6ac32d39ea5904557baebddf57d4bb2c`, byte-identical to the frozen
  manifest. Twenty-five entries (thirteen modified, twelve untracked), none staged, index tree equal to
  the `HEAD` tree.
- **Scoped content record:** **24 rows, 3,880 bytes**, SHA-256
  `be0f3e9778803ad0fed06f0b28bb0837040ada5434f922586837c2ee0b3d26fb`. The review-provenance fixture
  is its only exclusion, and `activeFreeze` names the same values.
- No path under `src/**` or `openspec/specs/**` changes.

## 2. Chain, prior rejections, and the rehearsal commit

Compared with the Round 62 target, only these change:

- the archive index, design, and task ledger wording;
- `OpenSpecCorpusGuards.cs`;
- both fixtures;
- the addition of the Round 62 verdict and this request and manifest.

The two lease files, the telemetry changes, the crosswalk, and every earlier review packet are
byte-identical.

The rejected packets:

- **Round 62 packet:** request 4,046 bytes `2fbc3fad…`, manifest 1,914 bytes `4872d525…`, verdict
  15,459 bytes `061ab847…`. It is byte-identical to what that round reviewed and issued, and is
  registered as `7.2-ss-1-tt-1-remediation-rejected`.
- **Earlier packets:** `7.2-initial-rejected` and `7.2-round-60-remediation-rejected` are unchanged.
- **Catalog:** all three packets are cataloged in `appendOnlyRecords`, and every catalog entry
  matches its file's normalized bytes in ordinal order.
- **Unchanged:** the other two rejected freezes, all fourteen archived freezes, and all ten entries.
  In a fresh worktree they reproduce from their checkpoint blobs, and the maximal current-match pins
  are order-exact.

**The rehearsal commit** is exactly as disclosed:

- The reflog shows `220aa15` ("Probe Task 7.2 WW-1 remediation checkpoint") followed by a reset back
  to `261d578b`.
- Its parent is `261d578b`, and its tree is `7b7a30a24fcc7169084829800e7ddab82c440d8e`, the rehearsed
  checkpoint tree.
- It holds twenty-five paths (twelve added, thirteen modified), and every blob equals the current
  frozen bytes.
- No ref contains it. `HEAD` and `refs/heads/feature/v3-rebuild` are both `261d578b`, the index tree
  equals the `HEAD` tree, and the porcelain equals the manifest.

The frozen provenance is therefore intact.

## 3. WW-1 closed: committed records cannot be deleted or moved

`ValidateAppendOnlyHistoricalRecords` now does two things (`OpenSpecCorpusGuards.cs:1937-1953`):

- runs
  `git log --diff-filter=A --name-only --format= <baselineCommit>..HEAD -- docs/archive docs/review`;
- requires every path it lists to be in `appendOnlyRecords` and to exist.

The per-entry first-addition binding is unchanged.

I checked how Git reports moves:

- **Move within the roots:** a `git mv` is reported as a rename, so the new path is not in the
  addition list. The old path still is, and it no longer exists, so the move is red on the old path.
- **Move into the roots:** a `git mv` from an active directory into `docs/archive/` is reported as an
  addition. The prescribed archival flow therefore still admits new archive records.

In the simulated approved state (section 5), a reshape-family review record was frozen through
`activeFreezeManifestPath` and committed, and then:

| # | Probe | Result and failure text |
|---|---|---|
| 1 | record and its catalog entry deleted, uncommitted (M1) | **red**, "must remain cataloged", names the record's path |
| — | record deleted, catalog entry kept (M1a) | **red**, "cannot be deleted or moved", names the path |
| 2 | deletion committed (M2) | **red**, names the path |
| 3 | `git mv` to a new path, catalog path updated, uncommitted (M3) | **red**, names the original path |
| 4 | move committed (M4) | **red**, names the original path |
| — | `git mv` out of both roots, entry removed (M3x) | **red**, names the path |
| — | committed archive record and entry deleted, uncommitted and committed (A1, A2) | **red** each, names the path |
| — | registered Round 62 `REJECT` verdict and entry deleted (D2) | **red** on both guards |

Every row fails on the new history assertion in `Task72_…`, which names the path. D2 also fails the
harmonization registry. None is a compile error or an active-freeze mismatch, as the request requires.

## 4. XX-1 closed

- **Design:** it now reads "While uncommitted, the catalog's active freeze manifest must name the
  record" (`design.md:229-230`).
- **Task 7.2 completion and archive rule 5:** both state the same relation, and neither has a stray
  short line.
- **Presence rule:** all three texts now describe it, including "rejects missing, moved, modified,
  unclassified, duplicate, or broadened records" (`tasks.md:337-338`).
- **Tombstones:** the texts state that the current contract provides no tombstone mechanism.
- **Guard constants:** the three constants match their documents, and the Task 7.2 guard is green.

## 5. Earlier controls retained

On the uncommitted target:

- the reshape-family admission is green;
- uncataloged and out-of-freeze records are red;
- a CRLF-only change to the lease host and active manifest is green;
- infinite and 1500-second deadlines are red;
- `baselineCount` changed alone is red;
- `activeFreezeManifestPath` outside `docs/review/` is red;
- dropping a rejected verdict's catalog entry is red.

For all three rejected packets, dropping the registration or editing the verdict is red (N8a–N8g),
and so is editing the Round 62 request.

On the simulated checkpoint (tree `7b7a30a2…`), I replayed the Task 7.1 evidence and activation
pattern:

- the approving verdict, cataloged as an append-only record;
- a new archived freeze;
- a Task 7.2 entry registering all four 7.2 verdicts;
- activation.

The Infrastructure lane is **223/223** in that state. Then:

- an edit to the approved request is **red** after the routine refresh (7.2 pins 23 to 22), on the
  first-addition binding;
- a committed record's content edit is **red** after refresh;
- a CRLF-only change is green;
- final controls are green, and restoration was verified byte-exact after every uncommitted probe.

## 6. Validation reproduced from a clean checkout

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
| Review-manifest current matches | order-exact for all ten entries (emulated) |
| `git diff --check` / changes under `src/**` or `openspec/specs/**` | clean / zero |
| Porcelain after validation | byte-identical to the manifest |

## 7. Observations (non-blocking)

### YY-1 (P3): records born and deleted on a merged side branch are not seen

Both `git log` calls use Git's default history simplification. At a merge that is TREESAME to one
parent for the pathspec, Git follows only that parent. I measured this against the guard, using
detached commits only:

1. On a side commit, add and catalog a reshape-family record, then delete it and its entry. At that
   side tip the guard is **red**.
2. Merge that tip into the mainline with `--no-ff`. The mainline is **green**.

Default `git log` omits the addition, and `--full-history` lists it.

This does not block:

- only a record that never existed on the mainline is affected;
- deleting any record that reached the mainline is still caught;
- CI runs this lane on every `feature/**` push, so the side branch shows red before a merge;
- this repository checkpoints on one linear branch.

Adding `--full-history` to both history queries would make the text "derives every post-baseline
addition from Git history" exact.

### ZZ-1 (P3): Task 7.7 now has one viable branch

The design now forbids relocating a historical path until a reviewed tombstone mechanism exists.
Harmonization Task 7.7 offers two ways to repair the Phase-0 kickoff prompt archive move:

- a history-preserving rename;
- an "explicit immutable archive provenance record".

The prompt, `docs/archive/plans/developer-facing-interface-phase-00-kickoff-prompt-2026-07-15.md`,
is a baseline record, so the rename branch would need a tombstone mechanism first. The provenance
record branch is an ordinary append-only archive record and remains available. Task 7.7 should say
which branch it takes when it is planned.

## 8. Simulated checkpoint and sequencing

Committing the twenty-five frozen entries on `261d578b` in a disposable worktree produced exactly
twenty-five paths, twelve added and thirteen modified. The resulting tree is
`7b7a30a24fcc7169084829800e7ddab82c440d8e`, equal to the rehearsed checkpoint tree in the request.
The worktree was clean afterwards, and its full guard lane is 223 passed and 14 expected red.

Writing this verdict creates a **twenty-sixth** entry against the declared twenty-five. Commit the
approved **twenty-five-path** checkpoint first, then add this verdict in the evidence commit. Its
filename matches the Task 7.2 discovery glob
`harmonize-downstream-capability-specs-task-7-2*verdict-*.md`. Following the Task 7.1 pattern, the
evidence commit must:

- catalog this verdict in `appendOnlyRecords` with its LF-normalized bytes;
- add the Task 7.2 archived freeze and entry, with the entry registering all four Task 7.2 verdicts
  (three `REJECT`, this `APPROVE`).

Until that evidence commit exists, the uncommitted verdict is red under both the active-freeze and
immutable-history guards, as in earlier rounds. My simulation of exactly this evidence and
activation sequence was green.

## 9. Reviewer hygiene

`HEAD` remained at `261d578b11a4b2d09e033aa44c8123cbc4ec653e` throughout. Nothing was staged, and I
created no commit or ref in the reviewed repository. It still showed exactly the twenty-five frozen
entries when this verdict was written. Both disposable worktrees are removed and pruned.

## Determination

The presence invariant is the right shape and closes WW-1:

- it is derived from Git history, not from the editable catalog;
- it is checked in both directions against the catalog and the working tree;
- deletion, relocation, and catalog repointing all fail before and after commit, naming the exact
  historical path.

Together with the committed-blob baseline, first-addition content binding, family-independent
admission, and three byte-exact rejection packets, Task 7.2 now makes every dated record under both
historical roots immutable and present, as its completion text states. The wording is accurate and
pinned. The chain carries zero drift, and validation reproduces. YY-1 and ZZ-1 are narrow and
non-blocking.

**Verdict:** **APPROVE**
