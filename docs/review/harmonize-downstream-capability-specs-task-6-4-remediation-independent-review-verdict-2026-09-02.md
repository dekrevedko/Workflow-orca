# Harmonization Task 6.4 rejection-remediation independent review verdict

**Date:** 2026-09-02
**Reviewer:** independent
**Target:** Task 6.4 remediation freeze, base `c23dc4e732881fb2ddb7792836e740d72ce2644f`,
tree `2c4e23ec4b826d58279a806ba38dfb79509926eb`, 14 worktree entries, 0 staged
**Request:** `docs/review/harmonize-downstream-capability-specs-task-6-4-remediation-independent-review-request-2026-09-02.md`
**Manifest:** `docs/review/harmonize-downstream-capability-specs-task-6-4-remediation-dirty-manifest-2026-09-02.txt`

**Verdict:** **APPROVE**

An `APPROVE` here authorizes exactly one action: the remediated Task 6.4 checkpoint commit
containing the fourteen frozen paths. It does not complete Tasks 6.5-8.3, does not archive the
harmonization change, and does not authorize reshape Task 8.0.

## Summary

The blocking defect R-1 from the 2026-09-01 rejection is fully closed. The deleted fan-out-rank
excluded-claim bullet is restored byte-for-byte in its original position, the appendix once again
agrees with its source artifact on every excluded claim except the intentional Task 6.4 rewrite, and
the deletion is now caught by two independent detectors rather than none. Both non-blocking
observations from that verdict, V-2 and V-3, are also closed and mutation-proven. Every anchor,
every declared count, and all eight declared negative controls reproduce.

Three non-blocking findings follow. One of them, W-1, was present in the rejected target and I did
not report it in round 47.

## Method

All execution was performed in a disposable `git worktree` created at the base commit, into which
the fourteen frozen paths were copied; its raw porcelain reproduced the frozen anchor byte-for-byte
before any test ran. The reviewed worktree was never modified, staged, or committed. Mutations were
applied only inside the disposable copy, each one reverted and re-verified against the frozen anchor.
The disposable worktree has been removed and pruned and no probe commit is reachable from any ref.

## 1. Freeze anchors

Both anchors were recomputed independently from the live worktree, not read from the request.

- Raw commit-real porcelain (`git status --porcelain=v1 --untracked-files=all`):
  **1,230 bytes**, SHA-256 `7daaaf63385a92290369d34e1f5195af8fea97b988b710d7c11da5e98bed85fa`.
  The published manifest file is **byte-identical** to that live projection.
- Scoped content record, ordinal-sorted `status`/`path`/`bytes`/`sha256` rows, tab-separated,
  LF-joined with a final LF, UTF-8 without BOM, excluding only the self-referential provenance
  fixture: **13 rows**, **2,064 bytes**, SHA-256
  `743687df2498c860a33167e1cf7845ef61a929a72df60e792487a79222f97e35`.
- 14 entries: 7 modified, 7 untracked, 0 staged. No `src/**`, no `openspec/specs/**`, no public API
  baseline, no package manifest. `git diff --check` exits 0.

## 2. The rejected target, verified first

- The immutable rejection verdict is unmodified: **13,640 bytes**, SHA-256
  `3f972188281bced25dbcd0ac0a6709b611e1660c91ff1da258c7cc46bd74a79d`, LF-only, exactly one terminal
  verdict line, `REJECT`.
- The original ten-path manifest is unchanged at 795 bytes /
  `3fb52cfea40ec2f0d083ca20355f5c5c18734c13aee0e56aaa382e8ce670f0e9`.
- The `6.4` provenance entry records `reviewState = Rejected` with no checkpoint commit, no
  checkpoint tree, no approval-evidence commit, and no reviewed-target commit or tree.
- Its 10-row historical content record recomputes to 1,502 bytes /
  `00027a858d2d92e7627a0e20b0f7e714babb6514ca83dbacb6d0792127c611e9`. Projecting out the
  self-referential fixture row leaves 9 rows that reproduce **1,348 bytes /
  `4ec24d5213af39e48f91d5a22570a89c4b7bb06fa071af0cb3dd0fc321f4d77b`** - exactly the scoped anchor I
  measured independently on 2026-09-01. The rejected freeze is recorded honestly.

## 3. R-1 is closed

The appendix diff against base is now **two added and two removed lines**, confined to the
`MaxActiveFibers` bullet. The line

```
- Fan-out rank one is a computational complexity class. Step bodies remain arbitrary code.
```

is present at its original position, between the unconditional item-admission claim and the
scope-tree acyclicity claim.

Diffing the whole `## Deliberately excluded claims` block against its source artifact
`openspec/changes/reshape-developer-facing-interfaces/artifacts/semantic-appendix.md` yields exactly
one hunk: the intentional Task 6.4 rewrite. Every other excluded claim, including the restored one,
is identical. The divergence the rejection identified is gone.

The block is now hash-pinned as a whole. I recomputed it from the file rather than trusting the
claim: **1,182 bytes**, SHA-256
`a989ad5ea0773cdd22eb13b65194651b61035eef9da469369134921b730c56b1`. The section is the document's
last, so the `"\n## "` terminator does not fire and the block runs to end of file; I confirmed
separately that appending a new `##` section leaves the pin correct rather than breaking it.

The deletion now has **two independent detectors**. The whole-block hash is the first. The second is
emergent and worth recording: because the rejected freeze's content rows are retained, restoring the
R-1 deletion makes `docs/specs/18-semantic-appendix.md` match the **rejected** target's recorded
bytes, which enlarges the maximal `currentWorktreeMatchPaths` set for the `6.4` entry and fails
`ReviewManifests_PreserveRawGitOrderOrDiscloseSetOnlyEvidence`. A fully coherent forgery - delete the
bullet, re-pin the block hash, and refresh the active content record - is still RED for that reason.

## 4. Everything else verified

- **Delta against the rejected target.** Using the recorded per-file hashes, exactly four files
  changed (`18-semantic-appendix.md`, the harmonization ledger, the provenance fixture,
  `OpenSpecCorpusGuards.cs`), four are new (my verdict, the remediation manifest, the remediation
  request, the remediation record), and six are **byte-identical** to what I already verified on
  2026-09-01. No reviewed file was rewritten and no unrelated content entered the target.
- **Independent token scan.** I enumerated every `MaxActiveFibers` occurrence across the repository
  myself. Product `src/**` has zero. The eight-entry active-document inventory in the guard matches
  my scan exactly, including the counts (1 / 4 / 1 / 1 / 13 / 1 / 1 / 1).
- **Provenance fixture.** All 7 entries: historical content records recompute exact, and
  `currentWorktreeMatchPaths` is exactly the maximal matching set for each (9 / 4 / 3 / 3 / 4 / 10 /
  6). All 4 archived freezes carry their authority and authority-evidence path and match what I
  approved in rounds 44-46.
- **Task 6.4's declaration accounting** (337 / 1,388 physical, 189 / 700 active) traces to the single
  new guard method; the crosswalk fixture is byte-identical to the version I verified in round 47.

## 5. Validation reproduced from a clean checkout

- Debug build 0 warnings / 0 errors; Release `-warnaserror` 0 / 0. Exact package feed: 12 packages.
- Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 96.
- PostgreSQL 101, SQL Server 72, Integration 11, run sequentially against real containers.
- Infrastructure guards **219/219**; expected-red exactly **14**; full assembly 219 passed / 14
  expected red / **233** total.
- OpenSpec strict **18/18**. Harmonization ledger **22 complete / 12 open / 34 total**.
- `git diff --check` exit 0; zero `src/**` and zero `openspec/specs/**` entries.
- **Simulated checkpoint:** `git add -A` staged exactly **14 real paths**, the commit left a clean
  tree, and the Infrastructure lane stayed 219/219 in the committed state - which exercises the
  clean-checkout branch of the active-freeze validator, not only the dirty branch.

## 6. Mutation and probe results

All eight declared controls reproduce.

| Control | Result |
| --- | --- |
| C1 delete the fan-out-rank bullet | RED (whole-block hash) |
| C2 add a contradictory excluded claim | RED |
| C3 token in `docs/specs/17-public-authoring-contract.cs` | RED |
| C4 active Markdown occurrence | RED |
| C5 product-source occurrence | RED |
| C6 reopen Task 6.4 | RED |
| C7 historical `docs/review/` occurrence | GREEN by design |
| C8 revert the Task 6.2 successor-boundary repair | RED in Task 6.2 |

C1 is the R-1 regression pin; the identical probe was GREEN in round 47. C3 closes V-2.

C8 deserves emphasis because the pair is conclusive. Reverting the boundary repair while Task 6.4 is
complete is RED, and the same revert **with Task 6.4 reopened is GREEN** - which demonstrates
directly that the old literal `"\n- [ ] 6.4 "` search was state-dependent and that the regex
replacement is what makes it state-independent. Separately, reopening Task 6.4 with the repair in
place leaves Task 6.2 and Task 6.3 GREEN.

My additional probes:

- **P1** correcting reshape task 7.20 to name task 6.4: **RED**. See W-1.
- **P2** relocating the fan-out bullet within the section (reorder only): RED.
- **P3** appending a new `##` section after the excluded claims: GREEN, correct scoping.
- **P9** trailing whitespace inside the block: RED.
- **P4** token added under `docs/archive/`: RED. See W-3.
- **P5** one-byte tamper of the immutable REJECT verdict: RED.
- **P6** relabelling the `6.4` entry `Rejected` to `Approved`: RED.
- **P7** forged active-freeze content-record digest: RED.
- **P10** coherent forgery (delete bullet, re-pin hash, refresh content record): **RED**.
- **P11/P12** deleting an unrelated normative clause from the `## Laws` section: RED while this
  freeze is registered, both dirty and committed. See W-2 for why that protection is temporary.

## 7. Non-blocking findings

### W-1 (P3) - reshape task 7.20 attributes Task 6.4's inventory to Task 6.3, and the guard forbids the correction

`openspec/changes/reshape-developer-facing-interfaces/tasks.md` now reads "maintained after ...
harmonization task 6.2, and harmonization task 6.3 at 337 sources / 1,388 declarations". At `HEAD`,
which is post-6.3, that same sentence reads **1,387**. The 1,388 count is produced by Task 6.4, which
the sentence does not name, so the statement is factually wrong about what Task 6.3 left behind.

`Task720CompletionNote_MatchesTheExecutableCrosswalkAccounting` pins the numeric half against the
crosswalk fixture correctly, but pins the provenance half as the hardcoded literal
`"harmonization task 6.2, and harmonization task 6.3 at"`, whose stated reason is "the maintained
inventory provenance must name the task that produced the current counts". It no longer does. Probe
P1 shows the accurate wording is **RED**, so the pin actively locks in the inaccuracy and any task
that moves the counts must edit the guard in order to tell the truth.

This is a recurrence of round 46's U-3: that fix appended the then-current task instead of deriving
the expected tail. Suggested shape: assert that the sentence names the most recent completed
harmonization task in the ledger, or derive the expected list from the ledger rather than from a
literal. It was present in the rejected target and I did not report it in round 47.

### W-2 (P3) - the durable pin covers the last section of the appendix only

`## Deliberately excluded claims` is now hash-pinned, which is the right fix for R-1. The rest of
`docs/specs/18-semantic-appendix.md` - `## Laws`, roughly 140 lines of numbered normative statements
and their normative-source citations - has no durable pin. P12 is RED today only because the active
freeze content-pins every path in the target; that protection lapses once this freeze is archived and
the file is no longer dirty. A silent deletion inside `## Laws` after archival would be exactly the
R-1 failure mode in a different section of the same file.

Two cheap options: extend the same whole-block treatment used for `CR-009a` and `CR-014a` to the law
statements, or add a consistency check against
`openspec/changes/reshape-developer-facing-interfaces/artifacts/semantic-appendix.md` asserting that
the only divergence is the pinned Task 6.4 bullet. The second would have caught R-1 directly and is
self-maintaining.

### W-3 (P3) - `docs/review/` and `docs/archive/` get opposite treatment

The active-document scan excludes `docs/review/` as immutable evidence but includes `docs/archive/`,
which `CLAUDE.md` treats as equally immutable provenance. P4 confirms a token added under
`docs/archive/` is RED. The consequence is operational: any future `git mv` of a document mentioning
the retired quantity into `docs/archive/` turns the must-green lane red and forces an edit to the
exhaustive inventory. Either exclusion or inclusion is defensible; the asymmetry is what is worth
deciding deliberately.

Two smaller notes, neither a finding. The remediation record describes the block terminator as "the
next `##` boundary" where the code uses `"\n## "` with a trailing space; the difference is
over-inclusive and therefore safe. And the active-document scan does not apply the
`IsGeneratedBuildPath` filter that the product scan applies; there is no build output under `docs/`
or `openspec/` today, so it is inert.

## 8. Checkpoint sequencing

Writing this verdict into the reviewed worktree makes it a fifteenth entry and turns the active-freeze
guard red at 15-versus-14. Commit the approved **14-path** checkpoint first, then add this verdict and
its registry entry in the following commit, as in rounds 44 through 46.

## 9. Reviewer hygiene

`HEAD` remains `c23dc4e732881fb2ddb7792836e740d72ce2644f`. The reviewed worktree still reports 14
entries and 0 staged, and its raw porcelain still hashes to
`7daaaf63385a92290369d34e1f5195af8fea97b988b710d7c11da5e98bed85fa`. The disposable worktree was
removed and pruned, no probe commit is reachable from any ref, and I created no commit.

## Determination

The rejection is remediated correctly and completely, without collateral change, and the repair is
guarded rather than merely present. Approved for the remediated Task 6.4 checkpoint commit only.
