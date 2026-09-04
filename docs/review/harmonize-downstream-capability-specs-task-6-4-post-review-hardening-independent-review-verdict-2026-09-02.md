# Harmonization Task 6.4 post-review-hardening independent review verdict

**Date:** 2026-09-02
**Target base:** `adc6a8f542a6e5362096df8a7213e06f55f11ad2`
**Target tree:** `1b8a3d12580ea56abe529e33665cfed66bdfc0f7`
**Scope reviewed:** W-1, W-2, and W-3 from the 2026-09-02 Task 6.4 remediation approval verdict

## Summary

The nine-path hardening target closes all three non-blocking observations from the Task 6.4
remediation verdict. Both freeze anchors reproduce byte-exactly from an independent recomputation.
The approved remediation chain landed with zero drift from the approved target, and both prior
Task 6.4 verdicts remain registered byte-exactly with no prior record edited.

Every hunk of the six-file diff was read line by line, including the hunks whose surrounding
context was unchanged. There is no undisclosed content change of the kind that caused the
2026-09-01 rejection.

One non-blocking finding is recorded: the W-2 mechanism is a consistency check between two mutable
files, not an immutability pin, so its recorded reach is stated more broadly than it is enforced.
This does not affect the frozen bytes and does not block the checkpoint.

## Method

Work was performed in a disposable `git worktree` at `X:/ocv76` detached at the base commit, with
the nine target paths copied in. The reviewed worktree was never modified, staged, or committed.
`HEAD` did not move. Every mutation was applied in the disposable copy and reverted with
`git reset --hard` plus `git clean -fd`. The worktree was removed and pruned; its one probe commit
`b80638cd` is unreachable from every ref.

## 1. Freeze anchors

Both anchors reproduce exactly from live state:

| Anchor | Declared | Recomputed |
|---|---|---|
| Raw manifest | 768 B / `43cb861b…f873e37c` | identical |
| Scoped content record | 8 rows / 1,249 B / `2f47137a…92c86146` | identical |

The worktree is 9 entries — 6 modified, 3 untracked, 0 staged — matching the declared shape. The
published dirty-manifest file is byte-identical to live porcelain: its own recorded content hash
in the scoped record is `43cb861b…`, the same value as the manifest digest, so the manifest is
self-consistent rather than a separately maintained copy.

The disposable worktree reproduced the identical 768-byte porcelain after replication, confirming
the target is fully described by its manifest.

## 2. The approved chain landed with zero drift

| Commit | Paths | Verified |
|---|---|---|
| `5e38de27` remediation checkpoint | 14 | exact |
| `df3d866c` approval evidence | 2 | exact |
| `adc6a8f5` mechanical activation | 1 | exact |

The manifest recovered from `5e38de27` is 1,230 B / `7daaaf63…`, byte-identical to the anchor I
approved on 2026-09-02, and its fourteen paths equal the commit's fourteen. Projecting the scoped
content record from that commit's blobs yields 13 rows / 2,064 B / `743687df…` — exactly my
approved record. **The checkpoint carries no drift from the target I approved.**

My approval verdict landed byte-exact at 14,017 B / `4d6456b7…` with its single `APPROVE` line
intact, and is registered under that hash. The rejection verdict remains registered at
13,640 B / `3f972188…`. Both are retained; neither was rewritten.

`df3d866c` archives the `6.4-remediation` freeze with `authority: IndependentReview` and pins
`manifestSha256`, `frozenRawPorcelainSha256`, `pathSortedSha256` (`dd47edb8…`, which I reproduced),
`checkpointCommit`, and `checkpointTree`. `adc6a8f5` changes exactly two fields —
`approvalEvidenceCommit` and `reviewState` → `Approved`. Nothing else moved.

The `6.4` entry carries `checkpointCommit: null` with `reviewedTargetCommit: 5e38de27`. This is not
an anomaly: entry `5.2` uses the same shape, and the checkpoint commit is recorded on the archived
freeze. No state is unrecorded.

## 3. Fixture integrity

Schema 10; 7 entries; 5 archived freezes. All seven entries' historical content records recompute
to their declared byte counts and digests, and **all seven pin sets are exactly maximal** — the
declared `currentWorktreeMatchPaths` equals the set of recorded rows whose current worktree bytes
still match, with no extras and no omissions.

The `6.4` entry's pin set correctly shrinks 6 → 4: `reshape .../tasks.md` and
`RecoveryCrosswalkGuards.cs` are modified again by this hardening, so they no longer match the
rejected target's recorded bytes. That shrink is mechanical maintenance of the maximal-pin
invariant, not an edit to a prior record. The four pre-existing archived freezes are untouched.

## 4. W-1 — closed

Reshape task 7.20 now reads `harmonization task 6.3, and harmonization task 6.4 at 337 sources /
1,388 declarations`, and `Task720CompletionNote_MatchesTheExecutableCrosswalkAccounting` requires
that suffix. The count is unchanged at 1,388 and is now attributed to the task that produced it.

Probe C1 restores the stale task-6.3 suffix: **RED**. Probe P10 perturbs the pinned count to 1,389:
**RED**. The identical probe that showed the *correct* wording failing in the previous round now
passes, and the stale wording fails — the pin is inverted correctly.

## 5. W-2 — closed for one-sided drift; see finding X-1

`Task64_MaxActiveFibersMentionsAreHistoricalOrExplicitlyNegative` now derives the complete expected
canonical appendix from the reshape source artifact through five declared publication transforms
and compares the whole document byte-for-byte.

I reproduced the projection independently. The two files genuinely differ (8,415 B source vs
8,379 B published) and every transform fires — 16 canonical-link rewrites, 1 deep-link rewrite,
2 citation-label rewrites, 1 lease-note removal, 1 admission-wording replacement. My independent
projection equals the published appendix exactly. **The mechanism is real and non-vacuous.**

Probe C2 perturbs an unrelated `## Laws` sentence: **RED**. Probe P7 performs the exact 2026-09-01
rejection shape — a one-sided deletion of the fan-out-rank bullet: **RED**. R-1 now has a third
independent detector beyond the block hash and the maximal-pin invariant.

## 6. W-3 — closed

The active-document scan excludes `docs/archive/` and `docs/review/` through one named prefix
allowlist. Because the corpus guard must now name `docs/archive/`, a carve-out was added to the
pre-existing historical-input guard that strips exactly one exact declaration before scanning.

The carve-out is tightly bounded, and I confirmed it cannot broaden:

- Probe P3 adds a second `docs/archive/` reference to the corpus guard: **RED**.
- Probe P5 respells the declaration so the strip matches zero occurrences: **RED**.
- Probe P4 duplicates the declaration: **compile error** (CS0102), a stronger red than the throw.

The exclusion is presently *inert* — `docs/archive/` contains zero occurrences of the retired
token today, so the eight-entry owner list is unchanged. It is correctly anticipatory: it prevents
a future `git mv` into the archive from reddening the must-green lane. It masks nothing now.

I recomputed the eight active-document owners independently; my list matches the guard's declared
list exactly, path and count.

## 7. Validation reproduced

From a clean checkout of the frozen target:

| Check | Result |
|---|---|
| Debug build, `-warnaserror` | 0 warnings / 0 errors |
| Release build, `-warnaserror` | 0 warnings / 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / Certification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Infrastructure disposition | 219 / 219 |
| Intentional expected-red | exactly 14 |
| Guards total | 233 |
| OpenSpec `validate --all --strict` | 18 passed / 0 failed |
| Task ledger | 22 complete / 12 open / 34 total |
| `git diff --check` | clean |

All fourteen reds are `ExecutableBehaviorExpectedRedGuards.Scenario_HasOneRuntimeRecordedExact…`
cases across tasks 3.8–3.11b — the documented expected-red set, unchanged.

The SQL Server lane again reports a low wall-clock figure because xunit excludes collection-fixture
startup; the container lane genuinely runs.

**Simulated checkpoint:** staging the target produced exactly **9 real paths** with no phantom
entries, left a clean tree, and the guard lane remained **219/219 in the committed state** —
exercising the clean-checkout branch of the freeze validator, not only the dirty branch.

## 8. Scope claims

No change to `src/**`, `openspec/specs/**`, the declaration crosswalk fixture, package manifests,
or public API baselines. No prior review artifact under `docs/review/` is modified. Task 6.5
remains open and unstarted.

Probe C5 alters the rejection verdict and probe C5b alters my approval verdict: both **RED**
through registered provenance evidence.

## 9. Mutation and probe results

| Probe | Mutation | Result |
|---|---|---|
| C1 | restore stale task-6.3 inventory suffix | RED |
| C2 | alter an unrelated canonical appendix law | RED |
| C3 | retired token beneath `docs/archive/` | GREEN by design |
| C3b | retired token beneath `docs/review/` | GREEN by design |
| C4 | retired token in active documentation | RED |
| C4c | retired token in product source `.cs` | RED |
| C4d | retired token in `docs/specs/*.cs` | RED |
| C5 | alter the immutable rejection verdict | RED |
| C5b | alter the immutable approval verdict | RED |
| P1 | coherent two-file `## Laws` edit, targeted guard | GREEN — see X-1 |
| P1b | same mutation, full lane | RED, freeze guard only |
| P2 | coherent two-file fan-out-bullet deletion | RED (block hash) |
| P3 | second archive reference in corpus guard | RED |
| P4 | duplicate exclusion declaration | compile error |
| P5 | respelled exclusion declaration | RED |
| P7 | one-sided fan-out-bullet deletion | RED |
| P9 | remove token from a declared owner | RED |
| P10 | perturb the pinned 7.20 count | RED |

## 10. Non-blocking finding

### X-1 (P3) — the appendix projection compares two mutable files

`BuildPublishedSemanticAppendix` reads
`openspec/changes/reshape-developer-facing-interfaces/artifacts/semantic-appendix.md` and compares
its projection to the canonical appendix. That source artifact is **not pinned anywhere**: its
SHA-256 `131d22bea736b6c7c4ac8a310ef1db72c992dcc867776b664c01fe2988d57be6` appears in no guard, no
fixture, and no content record; the fixture records only the canonical `docs/specs/` path. The
artifact is called immutable by convention, not by enforcement.

Consequently the hardening record's claim — "Drift in `## Laws`, any other section, or the
excluded-claims list therefore fails after the dirty freeze is archived" — and review-request
claim 2 hold for **one-sided** drift only. Probe P1 applies the same edit to both files and is
**GREEN** on the guard; on the full lane it is caught solely by
`ReviewManifests_PreserveRawGitOrderOrDiscloseSetOnlyEvidence`, the active-freeze manifest guard,
which lapses when this freeze is archived — precisely the archival case the claim addresses.

The excluded-claims section is unaffected: probe P2 shows its block SHA-256 catches a coherent
two-file deletion, so the R-1 bullet keeps durable cover. The gap is confined to the roughly
140-line `## Laws` section and any future section.

This is a strengthening opportunity, not a defect in the frozen bytes. W-2's actual purpose —
converting the one-sided drift that caused the rejection into a durable red — is achieved and
mutation-proven (P7). Suggested closure: pin the source artifact's SHA-256 next to
`DeliberatelyExcludedClaimsSha256`, which makes the projection an immutability chain rather than a
consistency check, and narrow the recorded claim to match whichever is implemented.

## 11. Checkpoint sequencing

Writing this verdict creates a tenth worktree entry, which will report the active freeze at 10
entries against a declared 9 and turn the freeze guard red. **Commit the approved nine-path
checkpoint first**, then record this verdict and its registry entry in the following commit, as in
the preceding rounds.

## 12. Reviewer hygiene

`HEAD` remained at `adc6a8f542a6e5362096df8a7213e06f55f11ad2` throughout. Nothing was staged. The
reviewed worktree was not modified. The disposable worktree was removed and pruned and its probe
commit is unreachable from every ref. I created no commit in the reviewed repository.

## Determination

All three observations from the Task 6.4 remediation verdict are closed and mutation-proven. The
approved chain landed with zero drift, both anchors reproduce, every entry record and pin set is
exact, the full validation packet reproduces from a clean checkout, and the simulated checkpoint is
exactly nine paths and green in the committed state. Finding X-1 is non-blocking and recorded for a
future round.

**Verdict:** **APPROVE**
