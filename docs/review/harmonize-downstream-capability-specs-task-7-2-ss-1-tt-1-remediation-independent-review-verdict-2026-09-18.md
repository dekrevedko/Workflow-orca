# Harmonization Task 7.2 SS-1 / TT-1 remediation independent review verdict

**Date:** 2026-09-18
**Reviewer:** independent review
**Scope reviewed:** the twenty-two-entry superseding Task 7.2 freeze on base
`261d578b11a4b2d09e033aa44c8123cbc4ec653e`, tree `cc4943fa88d38e23726d435e84b6bc6602222cbc`, named by
`harmonize-downstream-capability-specs-task-7-2-ss-1-tt-1-remediation-dirty-manifest-2026-09-17.txt`,
which remediates Round 61 findings SS-1, TT-1, UU-1, and VV-1.
**Authorization scope:** the request asks to create the superseding Task 7.2 immutable-history
checkpoint. This verdict authorizes nothing.

## Summary

All four Round 61 findings are closed, and every validation count, negative control, and anchor in
the request reproduces:

- **SS-1:** a new reshape-family review record is admitted through `activeFreezeManifestPath`,
  commits green, and needs no guard-source or registry change.
- **TT-1:** after simulated approval, the approved request is bound to its first Git addition.
  Editing it stays red after the routine current-match refresh.
- **UU-1:** a real `core.autocrlf=true` checkout of the committed target fails exactly the same six
  Infrastructure guards as the base, so the target adds none.
- **VV-1:** an infinite or 1500-second timeout is red, and so is `baselineCount` changed alone.

One new blocking defect remains in the append-only rule:

- **WW-1 (P2):** the guard enforces the content of a post-baseline record, not its presence. A
  committed reshape-family record can be deleted together with its catalog entry, and the
  Infrastructure lane stays green before and after the deletion is committed. The committed Task
  7.2 text says the guard "rejects missing … records" and calls the set append-only.

My Round 61 remediation note proposed the content rule without a presence requirement, so WW-1 is a
new finding, not a failure to follow that note. One P3 wording observation (XX-1) is also recorded.

## Method

All probing ran in fresh disposable environments created from `261d578b`:

- one worktree for validation, the simulated checkpoint, and a simulated approval chain;
- one worktree for mutation probes;
- one separate `core.autocrlf=true` clone for a real CRLF checkout.

Each worktree received the twenty-two frozen entries byte-for-byte, and its porcelain was confirmed
byte-identical to the frozen manifest. The reviewed worktree was never modified, `HEAD` never moved,
and nothing was staged or committed in it. All environments are removed, and every throwaway commit
is contained in no ref.

Each probe ran only its owning guards. Probes that had to satisfy the active freeze rebuilt its
manifest from porcelain and re-anchored the content record. PowerShell 7 is absent on this host, so
the current-match `-Check` and the routine refresh were reproduced by the Python emulation used
since round 54.

## 1. Freeze anchors reproduced

- **Raw commit-real porcelain:** **1,914 bytes**, SHA-256
  `4872d525d61200683053731fa4deb20f2977755b75005cddec077ebfd786a4c3`, byte-identical to the frozen
  manifest. Twenty-two entries (thirteen modified, nine untracked), none staged, index tree equal to
  the `HEAD` tree.
- **Scoped content record:** **21 rows, 3,315 bytes**, SHA-256
  `2b2ed68299e9a7b3459365ed4a98867a0abdef4efca5e4a1f6ac45a5e0782637`. The review-provenance fixture
  is its only exclusion, and `activeFreeze` names the same values.
- No path under `src/**` or `openspec/specs/**` changes.

## 2. Both prior rejections are preserved exactly

Compared with the Round 61 target, this target changes only:

- the archive index, design, and task ledger wording;
- the two guard files;
- both fixtures;
- the addition of the Round 61 verdict and this request and manifest.

Nothing else moves. In detail:

- The Round 60 packet (request `3a070ab1…`, manifest `8c7cb3f4…`, verdict `05c9f280…`) and the
  Round 61 packet (request 4,400 bytes `a194a68b…`, manifest 1,551 bytes `d1c4d30a…`, verdict
  20,625 bytes `7ddb3fb7…`) are byte-identical to what those rounds reviewed and issued.
- They are registered as `7.2-initial-rejected` and `7.2-round-60-remediation-rejected`. The other
  three rejected freezes, all fourteen archived freezes, and all ten entries are unchanged.
- In a fresh worktree, the archived freezes reproduce manifest, content record, and tree from their
  checkpoint blobs, and the maximal current-match pins are order-exact.
- The 427 baseline records are identical to the Round 61 target and still reproduce
  `66,459` bytes and `5be2e456…` from the committed blobs.

## 3. SS-1 closed: any review family is admitted without a guard-source change

The registry exception is gone. Every file under either root outside the baseline and the two
mutable surfaces must now be an `appendOnlyRecords` entry:

- while uncommitted, it must be listed in `activeFreezeManifestPath`;
- once committed, its normalized bytes must equal its first Git addition.

Measured:

| Probe | State | Result |
|---|---|---|
| N1 | new `developer-facing-interface-phase-08-…` record, cataloged and added to the active freeze | green on both guards |
| X1 | the same record, uncataloged | **red** |
| N4 | cataloged but outside the active manifest | **red** |
| R1 | after simulated approval: a reshape-family freeze with its own manifest, uncommitted | green |
| R2 | that freeze committed | green |
| R3 | committed record edited, then routine refresh | **red**, first-addition binding |
| R4 | committed record, CRLF-only transform | green |

## 4. TT-1 closed: an approved request cannot be rewritten

On the simulated checkpoint (tree `30bfa0f9…`), I replayed the Task 7.1 evidence and activation
pattern:

- the approving verdict, cataloged as an append-only record;
- a new archived freeze;
- a Task 7.2 entry registering all three 7.2 verdicts;
- activation.

The Infrastructure lane is **223/223** in that state. Then:

| Probe | Action | Result |
|---|---|---|
| T1 | approved request edited, manifest name kept | **red** on both guards |
| T1b | T1 followed by the routine refresh (7.2 pins 20 to 19) | **red**, `Task72_…` first-addition binding |
| T1c | approved request, CRLF-only transform | green |

## 5. UU-1 and VV-1 closed

- **Lease guard:** it normalizes the lease host before its shape checks.
- **Manifest read:** the active manifest is read lazily, only when an uncommitted append-only
  record needs it, through a normalizing reader that keeps BOM rejection and the terminal newline.
- **CRLF conversion (N5):** converting both the lease host and the active manifest to CRLF is green.
- **CRLF clone, base (`261d578b`):** 216 passed, 6 failed.
- **CRLF clone, committed target:** 217 passed, 6 failed, with an identical failure set.
- **Remaining failures:** two need a local package feed the clone lacks. The other four are the same
  pre-existing corpus guards.
- **`TerminalObservationTimeout`:** it is pinned by its full declaration
  (`LeaseDiscoveryAndGovernanceContractGuards.cs:84`). `Timeout.InfiniteTimeSpan` (N6) and 1500
  seconds (N6b) are both red.
- **`baselineCount`:** it must equal the guard constant and the row count
  (`OpenSpecCorpusGuards.cs:1761`). Changing it alone is red (N7).

## 6. WW-1 (P2, blocking): a committed post-baseline record can be deleted silently

`ValidateAppendOnlyHistoricalRecords` walks only the entries that are still in the catalog
(`OpenSpecCorpusGuards.cs:1925`). The classification check (`:1840`) compares the catalog with the
files that currently exist. Nothing requires a post-baseline path that was once committed to still
exist, so removing the file and its entry satisfies both checks.

The fixed baseline cannot be weakened this way, because its record list, count, and digest are
owned by guard source. History shows the gap is new: every one of the 429 paths ever added under
`docs/archive/` or `docs/review/` up to the base still exists.

In the approved state from section 4, after committing the R2 reshape-family record:

| Probe | Action | Result |
|---|---|---|
| D1 | `git rm` the committed reshape record and remove its catalog entry | **green** on both guards |
| D1b | D1 committed | **green** on both guards |
| D2 | same for the registered Round 61 `REJECT` verdict | **red**, harmonization registry |
| D3 | same for the approved Task 7.2 request | **red**, harmonization registry |

Only harmonization evidence is protected, and the protection comes from its registry. Reshape-family
review records and archive successors, the classes SS-1 brought under this guard, can be removed.
A `git mv` of such a record to a new path would pass for the same reason. That follows from the same
code path and was not probed separately.

The committed text claims more than the guard enforces:

- The Task 7.2 completion (`tasks.md:335-337`) says every later record "must be append-only" and
  that the guard "rejects missing, modified, unclassified, duplicate, or broadened records".
- The design (`design.md:229`) calls it "one family-independent append-only set".

Deletion is the most complete rewrite of a dated record, and it is exactly the kind of cleanup an
executable immutability guard exists to stop.

The fix is small and uses the history the guard already requires:

- List every path added under either root since the baseline:
  `git log --diff-filter=A --name-only --format= <baselineCommit>..HEAD -- docs/archive docs/review`.
- Require each of those paths to still exist and to remain in `appendOnlyRecords`.
- That turns D1, D1b, and a moved record red.
- An uncommitted deletion of a committed record is caught the same way.

If a post-baseline record ever needs to be withdrawn, that should be an explicit, reviewed
tombstone rather than a silent catalog edit.

## 7. Validation reproduced from a clean checkout

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

The lease host is byte-identical to the Round 61 target, where its six scenarios passed 20/20 at 4×
concurrency. They passed in every full lane run this round.

## 8. The request's eight negative controls

| # | Control | Result |
|---|---|---|
| 1 | reshape-family record admitted by the active freeze, no guard-source change | green (N1) |
| 2 | after checkpoint, that record edited, then routine refresh | **red** (R3) |
| 3 | approved request edited after simulated approval, then refresh | **red** (T1b) |
| 4 | uncommitted post-baseline file outside the active manifest | **red** (N4) |
| 5 | lease host and active manifest converted to CRLF | green (N5) |
| 6 | 15-second timeout replaced by `Timeout.InfiniteTimeSpan` | **red** (N6) |
| 7 | `baselineCount` changed alone | **red** (N7) |
| 8 | either prior rejection dropped, or either rejected verdict changed | **red** each (N8a–N8d) |

Additional probes:

| Probe | Mutation | Result |
|---|---|---|
| X2 | `activeFreezeManifestPath` outside `docs/review/` | **red** |
| X3 | a rejected verdict's catalog entry dropped | **red** |
| X4 | a rejected verdict, CRLF-only transform, on `Task72_…` | green |

Baseline and final controls were green, and restoration was verified byte-exact after every probe.

## 9. Observation XX-1 (P3): the new rule text is imprecise

- **Reversed relation in the design:** it says a later record "before checkpoint … must name the
  catalog's active freeze manifest while uncommitted" (`design.md:229-230`). It is the manifest that
  names the record, and "before checkpoint" repeats "while uncommitted".
- **Stray short lines:** the Task 7.2 completion breaks after "reusable review template. A", and
  archive README rule 5 breaks after "Update this index to route readers to the".

Guard constants pin all three texts, so any correction needs paired constant updates.

## 10. Simulated checkpoint and sequencing

Committing the twenty-two frozen entries on `261d578b` in a disposable worktree produced exactly
twenty-two paths, nine added and thirteen modified. The resulting tree is
`30bfa0f93355ef91b23d3c256233c1a5e27bc60b`, equal to the simulated checkpoint tree submitted with
this freeze. The worktree was clean afterwards, and its full guard lane is 223 passed and 14 expected
red.

Because this verdict rejects the target, **do not commit it**. This verdict is a new untracked
post-baseline `docs/review/` file matching the Task 7.2 discovery glob
`harmonize-downstream-capability-specs-task-7-2*verdict-*.md`.
The next freeze must carry it byte-exact, in two places:

- register it as a third Task 7.2 rejection, for example as rejected freeze
  `7.2-ss-1-tt-1-remediation-rejected`;
- catalog it in `appendOnlyRecords` with its LF-normalized bytes.

## 11. Required remediation

1. **Close WW-1.** Every path added under either historical root since `baselineCommit` must still
   exist and remain in `appendOnlyRecords`, derived from Git history as in section 6. Any
   withdrawal should need an explicit reviewed tombstone.
2. **Prove it with these probes:**
   - a committed reshape-family record deleted with its entry, both uncommitted and committed;
   - a committed record moved with `git mv`.

   All must be red, while the section 8 controls stay as measured here.
3. **Correct the committed text so it matches the enforced rule.** Optionally address XX-1 at the
   same time.

## 12. Reviewer hygiene

`HEAD` remained at `261d578b11a4b2d09e033aa44c8123cbc4ec653e` throughout. Nothing was staged, and I
created no commit in the reviewed repository. It still showed exactly the twenty-two frozen entries
when this verdict was written. All disposable worktrees and the CRLF clone are removed and pruned.

## Determination

The remediation does what Round 61 asked, and every claim it makes reproduces:

- one family-independent rule now admits reshape-family records without a registry or guard-source
  change;
- approved requests and all committed post-baseline records are bound to their first Git addition,
  so routine refresh can no longer launder an edit;
- CRLF checkouts gain no new failures, and the deadline and baseline count are pinned exactly.

The rule enforces content but not presence. A committed dated review record outside the harmonization
registry can be deleted, or moved, with the Infrastructure lane green. The committed Task 7.2 text
promises append-only behavior and rejection of missing records, so the target cannot be checkpointed
as complete until presence is enforced.

**Verdict:** **REJECT**
