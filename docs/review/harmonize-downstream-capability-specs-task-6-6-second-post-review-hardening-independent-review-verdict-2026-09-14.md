# Harmonization Task 6.6 second post-review hardening independent review verdict

**Date:** 2026-09-14
**Reviewer:** independent review
**Scope reviewed:** the seven-entry Task 6.6 second post-review-hardening freeze on base
`7e399826a25f2aeefe171475dcd51853ad145e2f`, tree `53ea4b5155944ea118b82f1f1b8b68ce449e54cf`, named
by `harmonize-downstream-capability-specs-task-6-6-second-post-review-hardening-dirty-manifest-2026-09-14.txt`,
which closes Task 6.6 remediation observations EE-1, FF-1, and GG-1.
**Authorization scope:** creation of the Task 6.6 second post-review-hardening checkpoint only.
This verdict does not authorize Task 7.1, reshape task 9.6 membership changes, change archival, or
Task 8.0.

## Summary

The hardening closes all three observations from the Task 6.6 remediation review:

- **EE-1:** a separately hash-pinned dated addendum records both synchronization fragments
  byte-exactly, and the approved remediation record is left unchanged.
- **FF-1:** the canonical gate now enumerates provenance artifacts. The next-refresh omission I
  measured as green in round 54 is now red on that gate.
- **GG-1:** the removed-token check now covers the §13.4 preamble.

All four claims reproduce. Every declared negative control is red, the longer-identifier control is
green, validation reproduces in full from a clean checkout, and the simulated checkpoint is green at
exactly the declared seven paths.

Two P3 boundary observations are recorded. Each concerns a mechanism the target describes
accurately, and neither blocks:

- **HH-1:** the enumeration is top-level only, so moving or deleting an uncatalogued predecessor
  evades it.
- **II-1:** a subsection placed after "Removed concepts" is scanned as part of the removed region.

## Method

All probing ran in two disposable `git worktree` copies created from `7e399826`: one for validation
and the simulated checkpoint, one for mutation probes. Each received the seven frozen entries
byte-for-byte, and its porcelain was confirmed byte-identical to the frozen manifest. The reviewed
worktree was never modified, `HEAD` never moved, and nothing was staged or committed in it. Both
worktrees were removed and pruned, and the one throwaway commit is contained in no ref.

Harness events are disclosed. The probe script stopped twice on defects in my own harness: once on
a Windows path-length limit in a backup filename during F4, and once on a wrong heading assumption
during G5. In both cases the `finally` restore ran. Before continuing I verified no pending backups,
identical porcelain, clean tracked files outside the target, and all seven target files
byte-identical. Probing resumed at the failed probe in a new batch with its own green baseline, and
every probe is accounted for below.

PowerShell 7 is not installed on this host, so the review-manifest refresh `-Check` was reproduced
by the exact Python emulation of its ordered comparison used in round 54.

## 1. Freeze anchors reproduced

- **Raw commit-real porcelain:** **657 bytes**, SHA-256
  `2278ae72ae96d334bc71a1e9e6b51fb3b7d9531b19cab4e48a54de505cc11c98`, byte-identical to the frozen
  manifest. It holds seven entries (four modified, three untracked) with none staged. `HEAD` is
  `7e399826`, and the index tree `53ea4b51` equals the `HEAD` tree.
- **Scoped content record:** **6 rows, 997 bytes**, SHA-256
  `eb885946de1976b4dc04c88b76a0754865feec1191b0da16b69482538522ac6b`. The review-provenance
  fixture is its only exclusion, and `activeFreeze` names the same values.
- **Capability inventory:** recomputed from the filesystem in the guard's declared record format as
  **16 directories, 1,321 bytes**, SHA-256
  `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5`. That equals the unchanged
  provenance checkpoint fixture.

## 2. The approved Task 6.6 remediation chain carries zero drift

- **Checkpoint:** `2581928d` has parent `665925db` and tree `c4535539b60363059e4c2be87c3ea2bb523156a8`.
  That is identical to the tree of my own round-54 simulated checkpoint, at exactly seven paths.
- **Approval evidence:** `dc79672e` adds only the remediation verdict, whose committed blob hashes to
  `3393e1c72e17ffe7e831845305dd488e16d83e71119559f3c49544db026f25e6`, plus its registration.
  Entry 6.6 now registers both approving Task 6.6 verdicts.
- **Activation:** `7e399826` changes exactly two values. `approvalEvidenceCommit` goes from `null`
  to `dc79672e…`, and `reviewState` goes to `Approved`.
- **Commit-blob projection:** all eleven archived freezes reproduce manifest, content record, and
  tree from their checkpoint blobs. That includes `6.6-post-review-remediation` at 6 rows / 982 B,
  tree `c4535539…`.
- **Entries:** all nine reproduce their historical rows exactly with maximal pins, and the emulated
  `-Check` is order-exact for every entry.

## 3. Claim 1 — EE-1 closed

The addendum is 2,513 bytes and LF-only, and its SHA-256 `caa11515…` equals the new guard constant.
Its `text` block names both requirement owners and holds two identical fragments:

`` at `docs/specs/13-phasing-and-open-questions.md` §13.4 ("Future-capability registry")``

That is one leading space, then the literal backticks around the path. I checked it
programmatically against all four synchronized files (two canonical, two reshape deltas):

- the fragment occurs exactly once at `1d4dec01`;
- removing it reproduces the `4f061089` bytes exactly; and
- the live bytes equal `1d4dec01`.

The approved remediation record is not in the target and still hashes to its pinned `913c6d28…`.
E1, which strips the path backticks from the addendum, is red on Task66 with "must remain
immutable". E2, a CRLF-only rewrite, stays green by normalization design.

## 4. Claim 2 — FF-1 closed

`ValidateReviewArtifact` now enumerates `*openspec-provenance-*.md` in the directory of the current
`artifactPath`. It requires the ordinal-sorted discovered set to equal the catalog plus the current
artifact.

- **F1 (add an unregistered matching artifact):** red on the canonical gate with "every OpenSpec
  provenance artifact must be the current record or a permanently catalogued predecessor".
- **F2 (next-refresh simulation):** a byte-identical successor at a new dated path, the fixture
  repointed, one line appended to the uncatalogued Task 6.6 refresh record, then the routine
  current-match refresh. It is **red on the canonical gate**. The identical sequence was green on
  every durable check in round 54 (H2b).
- **H1 (tamper with the catalogued 2026-08-18 artifact):** still red on the gate.

The residual boundary is recorded as HH-1.

## 5. Claim 3 — GG-1 closed

The removed-token check now runs on `registrySection[..removedStart]`, which covers the heading,
the preamble, and the deferred table.

| Probe | §13.4 preamble insertion | Round 54 | Now |
|---|---|---|---|
| G1 | `` `WaitLong` is planned future work. `` | green (PRE) | **red** on Task66 |
| G3 | ``Author `Yield` is planned future work.`` | not run | **red** on Task66 |
| G2 | `` `WaitLongAsync` and `YieldPolicy` are planned future work. `` | not run | green (intended) |

G4, which removes `Yield` from the removed subsection, is still red with "must remain explicitly
searchable". The covered region contains neither token today. The residual boundary is recorded as
II-1.

## 6. Claim 4 — durable decisions

- **Ledger and design constants:** the new constants equal the ledger and design text exactly. L1
  (drop "exhaustive" from the ledger sentence) and L2 (invert "including its preamble" in the design
  sentence) are both red on Task66.
- **Addendum:** its hash pin is asserted in the same test.
- **Fixture:** the target's only change is the active freeze. None of the three modified files is
  currently pinned by any entry, because Task 6.6's pins already dropped them in round 54. No
  current-match refresh is therefore required, and pins remain maximal.

## 7. Validation reproduced from a clean checkout

| Gate | Result |
|---|---|
| Debug build, non-incremental, warnings as errors | 0 warnings, 0 errors |
| Release build, non-incremental, warnings as errors | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / Certification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Full guard lane | 221 passed, 14 failed, 235 total |
| Classification of the 14 failures | all `ExecutableBehaviorExpectedRedGuards.Scenario_…`, zero others |
| OpenSpec `validate --all --strict` | 18 / 18 |
| Harmonization ledger | 24 complete, 10 open, 34 total |
| Review-manifest current matches | order-exact for all nine entries (emulated; see Method) |
| `git diff --check` / changes under `src/**` | clean / zero |
| Porcelain after validation | byte-identical to the manifest |

## 8. Scope and hunk census

The change set is 75 insertions and 2 deletions across four modified files, plus three new files:

- `design.md`: +7.
- `tasks.md`: +4.
- The review-provenance fixture: +12 / -1, the active freeze only.
- `OpenSpecCorpusGuards.cs`: +52 / -1.

No `[Fact]` or `[Theory]` is added, so declaration accounting is unchanged. The target makes no
change under `src/**`, `openspec/specs/**`, `docs/specs/**`, or any change delta, and the provenance
checkpoint fixture is untouched.

## 9. Mutation and probe results

| Probe | Mutation | Result on the owning check |
|---|---|---|
| E1 | Path backticks stripped from the addendum | **red**, Task66 |
| E2 | CRLF-only addendum | green (normalized by design) |
| F1 | Unregistered `*openspec-provenance-*.md` artifact | **red**, canonical gate |
| F2 | Next refresh without cataloguing, predecessor tampered, pins refreshed | **red**, canonical gate |
| F3 | Next refresh, predecessor deleted, pins refreshed | green on every durable check (HH-1) |
| F4 | Next refresh, predecessor moved into `artifacts/superseded/` and tampered, pins refreshed | green on every durable check (HH-1) |
| G1 / G3 | Removed token in the §13.4 preamble | **red**, Task66 |
| G2 | Longer identifiers in the preamble | green (intended) |
| G4 | `Yield` removed from the removed subsection | **red**, Task66 |
| G5 | New subsection after "Removed concepts" promising `WaitLong` | green on Task66 (II-1) |
| L1 / L2 | Ledger / design hardening decision weakened | **red**, Task66 |
| H1 | 2026-08-18 artifact tampered (regression) | **red**, canonical gate |

Each batch began and ended green on all four targeted tests. For every mutation, the lapsing
active-freeze comparison was also red. Where a mutated file is a historical 6.6 row, that entry's
refreshable current-match pin was red as well.

## 10. Observation HH-1 (P3) — top-level enumeration can be sidestepped by moving the predecessor

The enumeration is limited to the top level of the directory holding the current artifact, and the
target's own wording says "top-level". FF-1 targeted a *silent omission*, and F2 proves that
omission is now caught. Two non-silent routes still pass:

- **F4:** the next refresh moves its uncatalogued predecessor into `artifacts/superseded/`. It is
  then free to change. After the routine refresh, only the lapsing active freeze is red.
- **F3:** the predecessor is deleted instead, with the same result.

Both leave a visible `D` row in the frozen manifest, which is why this is P3. F4 still deserves
attention, because moving superseded material is this repository's documented convention.

Enumerating `*openspec-provenance-*.md` recursively under `openspec/changes/**`, including the
archive, would close F4. Today that search matches exactly the two governed files, and Git history
shows no provenance artifact ever deleted or renamed. Deletion (F3) cannot be detected by guard
source without a record of every artifact ever seen, so manifest review remains the control for it.

## 11. Observation II-1 (P3) — a subsection after "Removed concepts" is treated as removed

`removedSection` is `registrySection[removedStart..]`, which runs to the end of §13.4, and §13.4
ends the file. In G5, a new `### Planned follow-ups` subsection after "Removed concepts" containing
`` `WaitLong` is planned future work. `` stayed green on Task66. The only reds were the refreshable
entry-6.6 pin and the lapsing active freeze. The target's wording, "before the removed-concepts
subsection", is accurate.

Either of two cheap fixes closes it: end the removed region at the next `### ` heading and scan the
rest of §13.4, or require "Removed concepts" to be the final subsection. Neither observation needs a
review round of its own; both can ride with the next target that touches this guard.

## 12. Simulated checkpoint and sequencing

Committing the seven frozen entries on `7e399826` in a disposable worktree produced exactly seven
paths: three added and four modified. The resulting tree is
`930828cc6c63d3e0a4601871753a21d88d4fd0eb`, and the worktree was clean after the commit. The full
guard lane in that committed state is 221 passed, 14 failed, 235 total, and all 14 failures are the
documented expected-red scenarios.

Writing this verdict creates an **eighth** entry against the declared seven, which reddens the
active-freeze guard. Commit the approved **seven-path** checkpoint first. Then add this verdict in
the following evidence commit. Its filename matches entry 6.6's discovery glob
`harmonize-downstream-capability-specs-task-6-6*verdict-*.md`, so it must be registered in that
entry's `verdictEvidence` as the third Task 6.6 verdict.

## 13. Reviewer hygiene

`HEAD` remained at `7e399826a25f2aeefe171475dcd51853ad145e2f` throughout, nothing was staged, and I
created no commit in the reviewed repository. It still showed exactly the seven frozen entries when
this verdict was written. Both disposable worktrees were removed and pruned.

## Determination

EE-1, FF-1, and GG-1 are closed by guard-source checks that survive activation. Each is proven by a
mutation that was green or unrecorded in round 54 and is now red. The chain carries zero drift down
to tree identity with my own rehearsal, validation reproduces in full, and the simulated checkpoint
is clean at exactly seven paths.

HH-1 and II-1 are narrow P3 boundaries of accurately described controls. Neither leaves a frozen
byte incorrect or a present artifact unprotected.

**Verdict:** **APPROVE**
