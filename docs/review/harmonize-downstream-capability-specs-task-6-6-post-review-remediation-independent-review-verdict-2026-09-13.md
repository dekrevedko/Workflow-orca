# Harmonization Task 6.6 post-review remediation independent review verdict

**Date:** 2026-09-13
**Reviewer:** independent review
**Scope reviewed:** the seven-entry Task 6.6 post-review-remediation freeze on base
`665925db7aedcce7dc4fc6a716239da1d3bcbbf2`, tree `15e9ead41472afb96121b869280e4a40eb4d6716`, named
by `harmonize-downstream-capability-specs-task-6-6-post-review-remediation-dirty-manifest-2026-09-13.txt`,
which closes Task 6.6 findings BB-1, CC-1, and DD-1.
**Authorization scope:** creation of the Task 6.6 post-review-remediation checkpoint only. This
verdict does not authorize Task 7.1, reshape task 9.6 membership changes, change archival, or
Task 8.0.

## Summary

The remediation closes all three Task 6.6 findings with checks that survive activation. The
superseded 2026-08-18 provenance artifact is again pinned by the canonical gate, now through guard
source rather than the mutable fixture pointer. Removed-concept detection no longer depends on
Markdown spelling. The approval-order disclosure is truthful and hash-pinned. All five claims
reproduce, every declared negative control is red on a durable check, validation reproduces in full
from a clean checkout, and the simulated checkpoint is green at exactly the declared seven paths.

Three P3 observations are recorded, none blocking:

- **EE-1:** the DD-1 record calls its synchronized change "exact" but omits the code span around
  the cited path, and the record is now hash-pinned.
- **FF-1:** appending to the new catalog is still a manual step. I measured that the next refresh
  can orphan the current Task 6.6 refresh record exactly as BB-1 orphaned its predecessor.
- **GG-1:** the removed-concept scan covers the deferred subsection but not the §13.4 preamble.

## Method

All probing ran in two disposable `git worktree` copies created from `665925db`: one for validation
and the simulated checkpoint, one for mutation probes. Each received the seven frozen entries
byte-for-byte, and its porcelain was confirmed byte-identical to the frozen manifest. The reviewed
worktree was never modified, `HEAD` never moved, and nothing was staged or committed in it. Both
worktrees were removed and pruned. The one throwaway commit, `134f4c10`, is contained in no ref.

Three harness events are disclosed:

1. The first probe launch failed in the shell before executing anything.
2. The second run aborted after probe H1 on a console-encoding error while printing a guard
   message. Its `finally` block had already restored the edit. Before rerunning the complete
   sequence in UTF-8 mode, I verified no pending backups, clean tracked files, all seven target
   files byte-identical, and identical porcelain. Every later probe re-verified restoration.
3. PowerShell 7 is not installed on this host, and Windows PowerShell 5.1 lacks
   `SHA256.HashData`. `refresh-review-manifest-current-matches.ps1 -Check` therefore could not
   execute here. I reproduced its ordered comparison exactly in Python instead, and probe H2b
   emulates its rewrite. This is a host limitation; the script is unchanged by this target.

## 1. Freeze anchors reproduced

- **Raw commit-real porcelain:** **642 bytes**, SHA-256
  `3ebd1eea683365fded2b4369145ce761b4916b0f105d1d2935c1e09a6087f013`, byte-identical to the frozen
  manifest. It holds seven entries (four modified, three untracked) with none staged. `HEAD` is
  `665925db`, and the index tree `15e9ead4` equals the `HEAD` tree.
- **Scoped content record:** **6 rows, 982 bytes**, SHA-256
  `40dc4d16d11c7637a1daac4116cbfd874b47b2b871c7988339dc1fa9ecbea7ff`. The review-provenance
  fixture is its only exclusion.
- **`activeFreeze`:** names this request, manifest, base, `RawGitOrder`, 982 bytes, and
  `40dc4d16…` exactly.

## 2. The approved Task 6.6 chain carries zero drift

- **Checkpoint:** `1d4dec01` has parent `4f061089` and tree `96b68ba33219d493f8c39a235895bfba525c2a70`.
  That is identical to the tree of my own round-53 simulated checkpoint, at exactly twenty-two paths.
- **Approval evidence:** `dd5fd6bb` adds only the Task 6.6 verdict, whose committed blob hashes to
  `0d962ab35e3dd9ce27ea78326d271994de67a551c90c3704d778f82fb9acda08`, plus its fixture registration.
- **Activation:** `665925db` changes exactly two fixture values. `approvalEvidenceCommit` goes from
  `null` to `dd5fd6bb…`, and `reviewState` goes from `ApprovalAwaitingEvidenceCommit` to `Approved`.
- **Commit-blob projection:** all ten archived freezes reproduce manifest, content record, and tree
  from their checkpoint blobs. That includes the new 6.6 freeze at 1,569 B / `410e31b8…`,
  21 rows / 2,969 B / `708817fc…`, tree `96b68ba3…`.
- **Entries:** all nine reproduce their historical rows exactly, and every current-match pin set is
  maximal. The 6.6 entry registers its verdict as `APPROVE` with `0d962ab3…`.

## 3. Claim 1 — BB-1 closed

`ValidateReviewArtifact` now checks the guard-source catalog before it checks the current artifact.
The check has three parts:

- It pins the catalog count (1) and the digest of its ordinal-sorted `path\tsha\n` record. I
  recomputed that record as 180 bytes / `a9836be8…`.
- It forbids a catalogued path from equalling the fixture's current `artifactPath`.
- It requires every catalogued file to exist and to re-hash, after LF normalization, to its pinned
  value.

The single entry pins `9e8709096f8f3efcb8ea1ee040d1ec6f13e997a320eb6fbb7f724ec708ae2958`, which
equals the live normalized hash of the 2026-08-18 Task 4.2 record. The catalog check runs on every
execution of `CanonicalSynchronizationGate_…`, which calls `ValidateReviewArtifact` outside any
branch.

In round 53, appending a line to that artifact was red only on the lapsing active freeze. Now:

- **H1 (append a line):** red on the canonical gate with "must retain its reviewed bytes".
- **H1c (delete the file):** red with "must remain available".
- **X1 (one-character typo in the source pin):** red on the aggregate catalog digest.

BB-1's regression is closed. Its recurrence half is recorded as FF-1.

## 4. Claim 2 — CC-1 closed

`RemovedConceptRegistryNames` is now `["WaitLong", "Yield"]`. `ContainsIdentifierToken` matches
each name with `(?<![\p{L}\p{Nd}_])` and `(?![\p{L}\p{Nd}_])` boundaries. The deferred subsection
must contain neither token, and the removed-concepts subsection must contain both. Today the
deferred subsection contains zero occurrences of either token.

| Probe | Row inserted into the deferred subsection | Round 53 | Now |
|---|---|---|---|
| C1 | `WaitLong`, bare | green | **red** on Task66 |
| C2 | ``Authored `Yield` `` | green | **red** on Task66 |
| C3 | `` `Yield` `` | green | **red** on Task66 |
| C4 | `WaitLong-style waits` | not run | **red** on Task66 |
| FP | `` `WaitLongAsync` and `YieldPolicy` `` | not run | green on Task66, so no false positive |

R1, which replaces author `` `Yield` `` with "author yielding" in the removed subsection, is red
with "must remain explicitly searchable". A case variant (K1, `` `waitLong` ``) stays green. I do
not record that as a finding, because both concepts are case-sensitive C# identifiers.

## 5. Claim 3 — DD-1 disclosure verified

Every factual statement in the dated remediation record checks out against Git:

- Task 6.6 authority entered in `ad9414088f1843dae09ef8a5d10caa8aca413561` on 2026-08-05, before
  canonical-synchronization gate `ff11ead` on 2026-08-20.
- The citation-only sync was prepared and frozen before approval, and the record says so rather
  than presenting approval-first execution.
- The checkpoint, approval-evidence, and activation commits exist in that order.
- Both requirement headings match canonical OpenSpec and the reshape deltas verbatim.
- Canonical and delta blocks are byte-identical.

The record is 3,276 bytes, LF-only, and its normalized SHA-256 `913c6d28…` equals the guard
constant. D1 (invert "before" to "after") is red on Task66 with "must remain immutable". D2
(CRLF-only rewrite) stays green on Task66 by normalization design and is red only on the active
freeze, which is the intended behaviour. The byte-exactness of its "exact change" column is
recorded as EE-1.

## 6. Claims 4 and 5 — durable decisions and current-match refresh

- **Ledger and design constants:** they equal the ledger and design text exactly. L1 (weaken the
  ledger sentence) and L2 (weaken the design sentence) are both red on Task66.
- **Fixture pins:** the 6.6 entry drops exactly `design.md`, `tasks.md`, and
  `OpenSpecCorpusGuards.cs`. Those are the three historical 6.6 rows whose live bytes this target
  legitimately changes. The remaining eighteen pins are exact and in historical-row order.
- **Emulated `-Check`:** green, order-exact, for all nine entries.
- **Pin controls:** M1 (drop the valid `docs/specs/README.md` pin) and M2 (re-add the stale
  `OpenSpecCorpusGuards.cs` pin) are both red on the pin assertion, which survives activation.

## 7. Validation reproduced from a clean checkout

| Gate | Result |
|---|---|
| Debug build, non-incremental, warnings as errors | 0 warnings, 0 errors |
| Release build, non-incremental, warnings as errors | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / Certification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Full guard lane | 221 passed, 14 failed, 235 total |
| Classification of the 14 failures | all `ExecutableBehaviorExpectedRedGuards.Scenario_…` |
| OpenSpec `validate --all --strict` | 18 / 18 |
| Harmonization ledger | 24 complete, 10 open, 34 total |
| Review-manifest current matches | order-exact for all nine entries (emulated; see Method) |
| `git diff --check` | clean |
| Porcelain after validation | byte-identical to the manifest |

## 8. Scope and hunk census

The change set is 100 insertions and 11 deletions across four modified files, plus three new files:

- `design.md`: +8 / -0.
- `tasks.md`: +5 / -1.
- The review-provenance fixture: +12 / -5. That is the active freeze plus three pin removals.
- `OpenSpecCorpusGuards.cs`: +75 / -5.

No `[Fact]` or `[Theory]` is added, so declaration accounting correctly stays at 1,390 / 702. The
target makes no change under `src/**`, `openspec/specs/**`, `docs/specs/**`, or any change delta, and
the provenance checkpoint fixture is untouched. The canonical gate stays green on the unchanged
176-row record.

## 9. Mutation and probe results

| Probe | Mutation | Durable result |
|---|---|---|
| H1 | Append to the 2026-08-18 artifact | **red**, canonical gate |
| H1c | Delete the 2026-08-18 artifact | **red**, canonical gate |
| X1 | Typo in the catalog entry hash (source) | **red**, catalog digest |
| H2 | Simulated next refresh, then tamper the Task 6.6 refresh record | red only on the entry-6.6 current-match pin |
| H2b | H2 plus the routine current-match refresh | green on every durable check (FF-1) |
| C1–C4 | Removed tokens in the deferred subsection | **red**, Task66 |
| FP | Longer identifiers | green (intended) |
| K1 | `` `waitLong` `` | green (case-sensitive by design) |
| PRE | `` `WaitLong` is planned future work. `` in the §13.4 preamble | green on Task66; red only on the refreshable entry-6.6 pin (GG-1) |
| R1 | `Yield` removed from the removed subsection | **red**, Task66 |
| D1 | Approval order inverted | **red**, Task66 |
| D2 | CRLF-only remediation record | green (normalized by design) |
| L1 / L2 | Ledger / design decision weakened | **red**, Task66 |
| M1 / M2 | Valid pin dropped / stale pin re-added | **red**, pin assertion |

The baseline and final runs were green on all four targeted tests. For every doc or artifact
mutation, the lapsing active-freeze comparison was also red. Where the mutated file is a historical
6.6 row, that entry's current-match pin was also red until the routine refresh. The results above
record the owning checks.

## 10. Observation EE-1 (P3) — the DD-1 "exact change" is not byte-exact

The canonical and delta requirements gained exactly this clause:

`` at `docs/specs/13-phasing-and-open-questions.md` §13.4 ("Future-capability registry") ``

The remediation record's table, under the heading "Exact citation-only change", gives
`at docs/specs/13-phasing-and-open-questions.md §13.4 ("Future-capability registry")` as one code
span. The backticks around the path are lost, so a reader who reconstructs the synchronized bytes
from this record alone produces different bytes. The saga row only says "the same exact path and
section identity". The record also gives the activation commit as short `665925d` beside two full
SHAs.

This is non-blocking because the content is otherwise unambiguous: the position is after "the
future-capability registry", preceded by one space. The byte-exact diff is also already durable in
the repository, in section 5 of the Task 6.6 verdict, which the fixture registers by SHA-256 and
which this record names as "the authority for the exact reviewed bytes". Because Task66 pins the
record's hash, a correction after checkpoint needs a new dated record. If this target is refrozen
for any other reason, fix the table with a double-backtick code span first.

## 11. Observation FF-1 (P3) — the catalog append remains procedural

The design text says guard source "permanently catalogs every superseded provenance artifact path
and normalized hash before the mutable fixture points at its successor". The guard enforces that
for entries already in the catalog, but nothing requires the next predecessor to be added.

I simulated the next refresh without touching guard source, in two steps:

- **H2:** a byte-identical copy of the Task 6.6 refresh record at a new dated path, the fixture's
  `artifactPath` repointed to it, then one appended line in the old Task 6.6 record. The canonical
  gate stayed green; only the entry-6.6 current-match pin went red.
- **H2b:** H2 plus the routine current-match refresh. The only remaining red was the active-freeze
  comparison, which lapses at activation.

This is the same orphaning BB-1 described, deferred by one refresh. It is P3 because nothing is
unpinned today, and my own round-53 recommendation ("append one entry per refresh") was likewise
procedural.

A cheap mechanical close: enumerate every `artifacts/*openspec-provenance-*.md` file and require
each to be either the fixture's current `artifactPath` or a catalog entry. Today that matches
exactly the Task 4.2 record and the Task 6.6 refresh record, and excludes the unrelated Task 6.2
workflow-failure-provenance record.

## 12. Observation GG-1 (P3) — the §13.4 preamble is not scanned

The removed-token check runs on `registrySection[deferredStart..removedStart]`. Text between the
§13.4 heading and `### Deferred capabilities` is not scanned. Probe PRE inserted
`` `WaitLong` is planned future work. `` there. Task66 stayed green; the only other reds were the lapsing active freeze and the entry-6.6
current-match pin on that file, which the routine refresh rebuilds. Today
the preamble contains neither token. Scanning `registrySection[..removedStart]` would close the gap
without affecting the removed subsection.

## 13. Simulated checkpoint and sequencing

Committing the seven frozen entries on `665925db` in a disposable worktree produced exactly seven
paths: three added and four modified. The resulting tree is
`c4535539b60363059e4c2be87c3ea2bb523156a8`, and the worktree was clean after the commit. The full
guard lane in that committed state is 221 passed, 14 failed, 235 total, and all 14 failures are the
documented expected-red scenarios.

Writing this verdict creates an **eighth** entry against the declared seven, which reddens the
active-freeze guard. Commit the approved **seven-path** checkpoint first. Then add this verdict in
the following evidence commit. Its filename matches the 6.6 entry's discovery glob
`harmonize-downstream-capability-specs-task-6-6*verdict-*.md`, so it must be registered in that
entry's `verdictEvidence` beside the Task 6.6 verdict. That is the precedent the 6.4 and 6.5
follow-up verdicts set.

## 14. Reviewer hygiene

`HEAD` remained at `665925db7aedcce7dc4fc6a716239da1d3bcbbf2` throughout, nothing was staged, and I
created no commit in the reviewed repository. It still showed exactly the seven frozen entries when
this verdict was written. Both disposable worktrees were removed and pruned.

## Determination

BB-1, CC-1, and DD-1 are closed by source-owned checks. Each closure is proven by a mutation that
was green or unprotected in round 53 and is now red on a check that survives activation. The chain
carries zero drift down to tree identity with my own rehearsal, validation reproduces in full, and
the simulated checkpoint is clean at exactly seven paths.

EE-1, FF-1, and GG-1 are narrow P3 follow-ups, each with a concrete close. None leaves a frozen byte
incorrect or a present artifact unprotected.

**Verdict:** **APPROVE**
