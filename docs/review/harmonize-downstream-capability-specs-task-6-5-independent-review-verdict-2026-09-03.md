# Harmonization Task 6.5 independent review verdict

**Date:** 2026-09-03
**Target base:** `5a4a2871f4394645e40250295adc2e46aef9d461`
**Target tree:** `ccfb092a96f18f73b5b602034ca0eb708c052e05`
**Scope reviewed:** Task 6.5 public-authoring companion decision, plus closure of round-49 finding X-1

## Summary

The eleven-path target records the Task 6.5 decision — the public authoring companion stays
byte-unchanged because the authoring-session lifecycle types are implementation-only — and closes
round-49 finding X-1. Both freeze anchors reproduce byte-exactly. The Task 6.4 hardening chain
landed with zero drift, and all three Task 6.4 verdicts remain registered byte-exactly.

Every hunk of the eight-file diff was read line by line. The one substantive deletion — an Open
Question removed from `design.md` — is the question Task 6.5 exists to answer, and the same commit
adds the paragraph that resolves it. There is no undisclosed content change.

One non-blocking finding is recorded: the companion's "byte-unchanged" pin is a consistency check
against a mutable fixture value, structurally the same shape as X-1. It does not affect the frozen
bytes, does not weaken the real public API surface, and does not block the checkpoint.

## Method

Work was performed in a disposable `git worktree` at `X:/ocv77` detached at the base commit with
the eleven target paths copied in. The reviewed worktree was never modified, staged, or committed.
`HEAD` did not move. Every mutation was applied in the disposable copy and reverted with
`git reset --hard` plus `git clean -fd`. The worktree was removed and pruned; its one probe commit
`0cb082a5` is unreachable from every ref.

## 1. Freeze anchors

| Anchor | Declared | Recomputed |
|---|---|---|
| Raw manifest | 897 B / `fb8cf0ee…b734aa29` | identical |
| Scoped content record | 10 rows / 1,521 B / `d73fc37f…56163dbb` | identical |

Eleven entries — 8 modified, 3 untracked, 0 staged — matching the declared shape. The published
dirty-manifest file is again byte-identical to live porcelain, and the disposable worktree
reproduced the identical 897-byte porcelain after replication.

## 2. The Task 6.4 hardening chain landed with zero drift

| Commit | Paths | Verified |
|---|---|---|
| `737f2fd4` checkpoint | 9 | exact |
| `7960bd0d` approval evidence | 2 | exact |
| `5a4a2871` mechanical activation | 1 | exact |

The manifest recovered from `737f2fd4` is 768 B / `43cb861b…`, byte-identical to the anchor I
approved on 2026-09-02, and projecting the content record from that commit's blobs yields
8 rows / 1,249 B / `2f47137a…` — exactly my approved record.

The strongest single confirmation: **the checkpoint's tree is `f29f5610…`, which is precisely the
tree my round-49 simulated checkpoint produced.** The commit the author created and the commit I
rehearsed are the same object.

My approval verdict landed byte-exact at 12,827 B / `aa3b00fe…` with its single `APPROVE` line, and
is registered under that hash. The `6.4` entry now carries all three verdicts — `REJECT`
`3f972188…`, remediation `APPROVE` `4d6456b7…`, hardening `APPROVE` `aa3b00fe…`. Nothing was
rewritten. The archived `6.4-post-review-hardening` freeze pins `checkpointCommit`,
`checkpointTree`, and `pathSortedSha256` `fcf5bcd3…`, which I reproduced. The activation changed
exactly two fields.

## 3. Fixture integrity

Schema 10; 7 entries; 6 archived freezes. All seven historical content records recompute to their
declared byte counts and digests, and **all seven pin sets are exactly maximal**. The `6.4` pin set
correctly shrinks 4 → 3 because the crosswalk fixture is modified again by this slice.

## 4. X-1 is closed

`SemanticAppendixSourceSha256` now pins the source artifact at
`131d22bea736b6c7c4ac8a310ef1db72c992dcc867776b664c01fe2988d57be6` — the exact value I reported —
as a constant in guard source, verified before the projection is built.

The decisive evidence is the probe itself: **the coherent two-file `## Laws` edit that was GREEN in
round 49 is now RED** (control C1). A one-sided edit of the source artifact alone is also RED
(probe P2). The projection is now an immutability chain rather than a consistency check.

## 5. The Task 6.5 decision

The companion is byte-unchanged at 53,745 bytes / `41f6472c…`, matching the pre-existing
`v1-public-contract.json` pin, which is itself unmodified. All six named lifecycle types are real
and all six are declared `internal` in `src/OrcaCore.Core/Building/` — so the guard's
`NotContainAny` assertions are not vacuous:

| Type | Declaration |
|---|---|
| `AuthoringSessionState` | `internal enum`, AuthoringLifecycle.cs:5 |
| `AuthoringLifecycleSession` | `internal sealed class`, :12 |
| `AuthoringLifecycleHandle` | `internal sealed class`, :237 |
| `AuthoringLexicalToken` | `internal sealed class`, :260 |
| `AuthoringJoinToken` | `internal sealed class`, :273 |
| `WorkflowAuthoringSession` | `internal abstract class`, SelectedWorkflowBuilder.cs:13 |

The approved `OrcaCore.Core` baseline is genuinely empty — 48 bytes, header plus assembly line, no
exported declarations — and the checked-in twelve-assembly baseline directory is unmodified.

Claim 2's independence holds. Exposing a lifecycle type reddens the **pre-existing exhaustive
baseline guard on its own** (probe P3, 2 of its 14 cases fail), not merely the new Task 6.5 guard.
And the protection is name-independent: a newly added public type whose name is on no forbidden
list is equally rejected (probe P4). Making `AuthoringLifecycleHandle` public is not even
reachable — it is a CS0051 compile error, because its constructor consumes internal types.

The guard uses no reflection bridge and adds no product code, as claimed.

## 6. Validation reproduced

From a clean checkout of the frozen target:

| Check | Result |
|---|---|
| Debug build, `-warnaserror` | 0 warnings / 0 errors |
| Release build, `-warnaserror` | 0 warnings / 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / Certification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Infrastructure disposition | 220 / 220 |
| Intentional expected-red | exactly 14 |
| Guards total | 234 |
| OpenSpec `validate --all --strict` | 18 passed / 0 failed |
| Task ledger | 23 complete / 11 open / 34 total |
| `git diff --check` | clean |

The fourteen reds are the same documented `ExecutableBehaviorExpectedRedGuards` set as every prior
round; the totals move 233 → 234 solely because of the one new guard.

**Simulated checkpoint:** staging the target produced exactly **11 real paths**, left a clean tree,
and the guard lane remained **220/220 in the committed state**.

## 7. Accounting coherence

The corpus guard gains exactly one `[Fact]` declaration (8 → 9), which independently explains all
three crosswalk movements: `physicalDeclarations` 1,388 → 1,389, `activeDeclarations` 700 → 701,
and the file's own declaration count 8 → 9. Reshape task 7.20 names harmonization task 6.5 and
quotes 1,389; reverting it to the stale task-6.4 attribution is RED (probe P6), and perturbing the
active count is RED (probe P7). The 866-row retirement ledger is unchanged.

## 8. Scope

No change to `src/**`, canonical `openspec/specs/**`, `docs/specs/**` (companion included), the
twelve approved API baselines, package manifests, or the behavior-scenario/contract projects.
Task 6.6 remains open and unstarted.

## 9. Mutation and probe results

| Probe | Mutation | Result |
|---|---|---|
| C1 | coherent two-file appendix edit (round-49 P1) | **RED — X-1 closed** |
| C2 | named lifecycle type into companion + re-pinned fixture | RED |
| C2b | *differently named* public type + re-pinned fixture | GREEN — see Y-1 |
| C3 | expose `AuthoringSessionState` publicly | RED |
| C3b | expose `AuthoringLifecycleHandle` publicly | compile error (CS0051) |
| C4 | remove the Task 6.5 completion marker | RED |
| C4b | alter the recorded decision wording | RED |
| P2 | one-sided source-artifact edit | RED |
| P3 | leak checked by the pre-existing baseline guard alone | RED |
| P4 | new public type with a non-forbidden name | RED |
| P5 | same leak via the Task 6.5 guard's own diff | RED |
| P6 | stale reshape 7.20 attribution | RED |
| P7 | perturbed active declaration count | RED |

## 10. Non-blocking finding

### Y-1 (P3) — the companion pin is a consistency check, not an immutability pin

`Task65_…` compares the companion's bytes to `contract.CompanionSha256`, read from
`tests/…/Fixtures/v1-public-contract.json`. That value is fixture data: the digest
`41f6472c…` appears in no guard source constant, unlike `DeliberatelyExcludedClaimsSha256` and the
newly added `SemanticAppendixSourceSha256`.

Consequently the guard's forbidden-content check is what actually carries the claim, and it is a
fixed six-name list. Probe C2b appends `public sealed class AuthoringSessionScopeHandle { }` to the
companion and re-pins the fixture digest coherently: **GREEN** on the focused guard, and on the full
lane it is caught only by `ReviewManifests_PreserveRawGitOrderOrDiscloseSetOnlyEvidence` — the
active-freeze manifest guard, which lapses at archival. This is structurally the same shape as
X-1, in the guard added to close X-1.

The severity is low for a specific reason I verified rather than assumed: the companion is a
compile-shaped documentation file, not compiled product, and the real public surface is protected
independently and name-independently by the exhaustive twelve-assembly baseline (probes P3 and P4).
So the exposure is misdocumentation, not an actual visibility leak — Task 6.5's substantive
decision holds regardless.

Suggested closure: promote `41f6472c…` to a guard-source constant, exactly as this slice did for
the semantic-appendix source.

## 11. Checkpoint sequencing

Writing this verdict creates a twelfth worktree entry against a declared eleven, which will redden
the active-freeze guard. **Commit the approved eleven-path checkpoint first**, then record this
verdict and its registry entry in the following commit.

## 12. Reviewer hygiene

`HEAD` remained at `5a4a2871f4394645e40250295adc2e46aef9d461` throughout. Nothing was staged. The
reviewed worktree was not modified. The disposable worktree was removed and pruned and its probe
commit is unreachable from every ref. I created no commit in the reviewed repository.

## Determination

The Task 6.5 decision is recorded, guarded, and mutation-proven; round-49 finding X-1 is closed with
the probe that previously passed now failing; the Task 6.4 chain landed byte-identical to the tree I
rehearsed; both anchors and all seven entry records reproduce; the full validation packet reproduces
from a clean checkout; and the simulated checkpoint is exactly eleven paths and green committed.
Finding Y-1 is non-blocking and recorded for a future round.

**Verdict:** **APPROVE**
