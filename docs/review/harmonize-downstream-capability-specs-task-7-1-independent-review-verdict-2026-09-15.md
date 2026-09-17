# Harmonization Task 7.1 independent review verdict

**Date:** 2026-09-15
**Reviewer:** independent review
**Scope reviewed:** the twelve-entry Task 7.1 freeze on base
`99657834684deefa2d22cb6526d714c4566b7ae0`, tree `dc221c3a95ba3543b18b464112e405485c416869`, named
by `harmonize-downstream-capability-specs-task-7-1-dirty-manifest-2026-09-15.txt`, including the
carried Task 6.6 observations HH-1 and II-1.
**Authorization scope:** none. This verdict rejects the target as frozen.

## Summary

Every substantive obligation of Task 7.1 reproduces. The active corpus contains zero positive
removed or deferred call forms, the three guides carry resolvable §13.4 re-entry links, the new
executable guard rescans the live corpus rather than trusting the dated record, HH-1 and II-1 are
closed, and every declared negative control is red on a check that survives activation. Validation
reproduces in full.

One obligation does not reproduce, and it blocks:

**JJ-1 (P2, blocking).** Obligation 1 requires the dated record to reproduce 86 sources, 10,655
record bytes, and SHA-256 `74fc7697…`. The population and the byte count reproduce exactly. The
digest does not, at any repository state or under any reading of the published recipe. At the frozen
target the record's true digest is
`56b6d27ece05d0ff536d9ca5114ba3e1856f0f3288f4bf5226bab341e36963f2`.

This is a false byte-exact claim inside a dated artifact that this same target makes immutable by
pinning its content hash in guard source. Committing it freezes an unverifiable number permanently,
and Tasks 7.3 through 7.5 inherit it. The remedy is mechanical and small.

## Method

All probing ran in two disposable `git worktree` copies created from `99657834`: one for validation
and the simulated checkpoint, one for mutation probes. Each received the twelve frozen entries
byte-for-byte, with porcelain confirmed byte-identical to the frozen manifest. The reviewed worktree
was never modified, `HEAD` never moved, and nothing was staged or committed in it. Both worktrees
were removed and pruned, and the one throwaway commit is contained in no ref.

The record reproduction was written from the artifact's published recipe and cross-checked against
the guard's own enumeration code, not copied from either.

PowerShell 7 is not installed on this host, so the review-manifest refresh `-Check` was reproduced
by the exact Python emulation of its ordered comparison used since round 54.

## 1. Freeze anchors reproduced

- **Raw commit-real porcelain:** **926 bytes**, SHA-256
  `76b82093f24cb47e5acfe9e55b1ef4a63b811268228355c17430ca532d4cf6e6`, byte-identical to the frozen
  manifest. It holds twelve entries (nine modified, three untracked) with none staged. `HEAD` is
  `99657834`, and the index tree `dc221c3a` equals the `HEAD` tree.
- **Scoped content record:** **11 rows, 1,621 bytes**, SHA-256
  `0ae3eb4a3d2bad47c93095cabe2773ac693469358baf862e998a721397a541a5`, with the review-provenance
  fixture as its only exclusion. `activeFreeze` names the same values.
- **Capability inventory:** recomputed from the filesystem as **16 directories, 1,321 bytes**,
  SHA-256 `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5`, equal to the unchanged
  provenance checkpoint fixture.

## 2. The approved Task 6.6 second-hardening chain carries zero drift

- **Checkpoint:** `7ce56191` has parent `7e399826` and tree `930828cc6c63d3e0a4601871753a21d88d4fd0eb`,
  identical to the tree of my own round-55 simulated checkpoint, at exactly seven paths.
- **Approval evidence:** `058ddeb6` adds only the round-55 verdict, whose committed blob hashes to
  `2ed9ef2ce2ff11ada2e5f8d44fe877980e971be8c4d385ac20835cc9e59e3a59`, plus its registration.
- **Activation:** `99657834` changes exactly two values.
- **Projection:** all twelve archived freezes reproduce manifest, content record, and tree from
  their checkpoint blobs. All nine entries reproduce their historical rows with maximal pins; entry
  6.6 correctly drops to fourteen for the four rows this target changes. The emulated `-Check` is
  order-exact for every entry.

## 3. JJ-1 (P2, blocking) — the dated record's SHA-256 does not reproduce

The artifact publishes a complete recipe: normalize CRLF and lone CR to LF, encode UTF-8 without
BOM, render `path\tlength\tsha256`, sort rows by ordinal path, join with LF, keep one final LF, hash
the result. I implemented exactly that.

**What reproduces.** The population is exactly as described — root guidance 2, active documentation
42, canonical OpenSpec 14, active change planning and deltas 28, totalling **86** sources — and the
record is **10,655 bytes**. Both figures match the artifact.

**What does not.** The digest at the frozen target is `56b6d27e…`, not `74fc7697…`. I then searched
for any state or reading that yields the published value:

| Search | Result |
|---|---|
| All 16 combinations of base and target content for the four corpus files this target edits | no match |
| The record computed from each of the last 25 commit trees | no match |
| Raw versus normalized length and hash, character counts, uppercase hex | no match |
| No final LF, CRLF-joined rows, a BOM-prefixed record | no match |
| Concatenated file bytes, concatenated per-file digests, a paths-only record | no match |
| Populations excluding `docs/**` C# contracts, or including `artifacts/**` | no match, and both miss the stated 86 / 10,655 |

Only the declared population reproduces the stated row and byte counts, which confirms the row
shape and path set are right. The single unexplained element is the digest itself. The most likely
history is that the scan ran against an intermediate working state on 2026-09-14 that was edited
again before the freeze; that state is not recoverable from the repository.

**Why it blocks.** The request makes this an obligation to verify, and it cannot be verified. Worse,
the same target pins the artifact's own hash (`23e75f84…`) in guard source, so the unverifiable
number becomes immutable on commit, and the later documentation tasks inherit it as evidence.

**Remedy.** Recompute the record at the frozen state, write `56b6d27e…` (or the value for whatever
state is final after remediation) into the artifact, update
`Task71PositiveCallScanArtifactSha256`, refreeze, and re-request review. Everything else in this
target can stand unchanged. If the intent is to preserve a 2026-09-14 measurement, the artifact
must also name the exact commit or state it was computed against, so a reviewer can reproduce it.

## 4. Obligation 2 — zero positive call forms, verified independently

My own scan of the 86 declared sources found **zero** matches for `WaitLong(`, `Yield(`,
`WhenFirst(`, `Saga(`, `RunExternalJob(`, `RunChild(`, `RunChildren(`, and `.Pause(` / `.Resume(` /
`.Archive(` / `.Purge(` / `.Cancel(`. A wider sweep over every active Markdown file outside the
declared population also found zero.

The new guard is the durable part: it rescans the live corpus on every run rather than trusting the
dated record. Probes confirm its breadth — a positive call is red whether it is added to a guide
(P1), a canonical spec (P2), or an active change proposal (P3) — and `WaitLongAsync()` with
`YieldPolicy()` stays green (P4).

Two declared scope boundaries behave as documented and are not defects: a management call without a
receiver dot (P18) and a positive call inside an excluded dated artifact (P19) both stay green. The
artifact states the `.Pause(`-style forms and the artifact exclusion explicitly.

## 5. Obligation 3 — guide notes and re-entry links

All three guides carry the exact anchor `#134-future-capability-registry`, and each link resolves to
the real heading at `docs/specs/13-phasing-and-open-questions.md:143`, including the Orleans guide's
`../specs/` relative form. The Kubernetes guide is the only guide whose content this target changes,
and that change replaces nothing: it adds the registry sentence and link. Breaking any one of the
three anchors is red (P5, P6, P7) on both Task 7.1 and the Task 6.6 guard.

## 6. Obligation 4 — completion, accounting, and immutability

- The harmonization ledger is **25 complete / 9 open / 34 total**, with 7.1 marked `[x]`.
- The dated artifact's live hash equals the pinned `23e75f842bddace81e93d472899b1c822f1d9bb4929e55bf6d6336b18bc32528`.
  Drifting it is red (P8), as is weakening the ledger (P9) or design (P10) decision.
- Declaration accounting moves by exactly one for the one new `[Fact]`: 1,390 to 1,391 physical and
  702 to 703 active, with `OpenSpecCorpusGuards.cs` 10 to 11, matched in the crosswalk fixture, the
  recovery crosswalk guard, and the reshape 7.20 provenance sentence. Reverting the fixture (P16) or
  that sentence (P17) is red. The lane totals move consistently to 222 must-be-green and 236 cases.

## 7. HH-1 and II-1 are closed

- **HH-1:** discovery now walks `openspec/changes/**` recursively for any `*.md` whose filename
  contains `openspec-provenance-`. A nested unregistered artifact is red (P11), and the exact
  round-55 evasion — move the uncatalogued predecessor into `artifacts/superseded/`, repoint the
  fixture, tamper, then refresh the pins — is now red on the canonical gate (P12).
- **II-1:** the removed region ends at the next `### ` heading, and everything else in §13.4 is
  scanned. The round-55 evasion, a later subsection promising `WaitLong`, is red (P13), longer
  identifiers stay green (P14), and the removed subsection must still contain both tokens (P15).

The section-concatenation used for the non-removed region cannot synthesize a false token across the
seam, because the second part always begins with a newline.

## 8. Validation reproduced from a clean checkout

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
| Review-manifest current matches | order-exact for all nine entries (emulated; see Method) |
| `git diff --check` / changes under `src/**` | clean / zero |
| Independent positive-call scan | 0 findings across 86 sources |
| Porcelain after validation | byte-identical to the manifest |

## 9. Scope and hunk census

214 insertions and 26 deletions across nine modified files, plus three new files. The largest are
`OpenSpecCorpusGuards.cs` at +179 / -11 and the review-provenance fixture at +12 / -6, which is the
active freeze plus four pin removals. The target makes no change under `src/**`, canonical
`openspec/specs/**`, or any change delta, and the provenance checkpoint fixture is untouched.

## 10. Simulated checkpoint

Committing the twelve frozen entries on `99657834` in a disposable worktree produced exactly twelve
paths: three added and nine modified. The resulting tree is
`87fb19bc277ad98cce82250008c1a734e0edb197`, the worktree was clean afterwards, and the full guard
lane in that committed state is 222 passed, 14 failed, 236 total, with all 14 failures the
documented expected-red scenarios.

This is recorded for completeness. It does not authorize a checkpoint.

## 11. Sequencing for the remediated target

Writing this verdict adds a thirteenth entry to the worktree, which reddens the active-freeze
guard while the current freeze stands. Carry it into the remediated freeze so its manifest names
this file, or record it under your existing evidence-commit protocol before refreezing. Its name
matches the Task 7.1 discovery glob
`harmonize-downstream-capability-specs-task-7-1*verdict-*.md`, so whichever entry carries Task 7.1
must register it.

## 12. Reviewer hygiene

`HEAD` remained at `99657834684deefa2d22cb6526d714c4566b7ae0` throughout, nothing was staged, and I
created no commit in the reviewed repository. It still showed exactly the twelve frozen entries when
this verdict was written. Both disposable worktrees were removed and pruned.

## Determination

The engineering in this target is sound and reproduces in full: zero positive call forms across an
independently enumerated corpus, durable guard coverage that rescans the live tree, resolvable
registry links, closed HH-1 and II-1 with their exact round-55 evasions now red, consistent
declaration accounting, and green validation.

It is rejected on one point only. A dated artifact that this target makes permanently immutable
publishes a byte-exact SHA-256 that no state of this repository produces, and the review request
elevates that value to an obligation. Freezing an unreproducible provenance number is the specific
failure this review series exists to prevent, and the fix is a recomputation plus a constant update.

Recompute the record at the frozen state, update the artifact and the guard pin, refreeze, and
re-request. No other change is required by this verdict.

**Verdict:** **REJECT**
