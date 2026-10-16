# Harmonization Task 6.4 independent review verdict

**Date:** 2026-09-01
**Reviewer:** independent review session (cold verification, no authoring involvement)
**Target:** the dirty worktree named by
`docs/review/harmonize-downstream-capability-specs-task-6-4-dirty-manifest-2026-09-01.txt`
**Base:** `c23dc4e732881fb2ddb7792836e740d72ce2644f`
**Requested authorization:** create only the Task 6.4 checkpoint commit

## Summary

One blocking defect. The target silently deletes an unrelated normative bullet from
`docs/specs/18-semantic-appendix.md` that no claim, artifact, or ledger entry mentions and no guard
detects. Everything else in this target verified cleanly - all seven claims, both freeze anchors,
the full checkpoint chain, every declared validation number including the three container lanes, and
twelve of thirteen mutations. The remedy is one restored line plus a refreeze.

## Method

Every claim was recomputed rather than accepted. The reviewed worktree was never modified, staged,
or committed. All builds, suites, container lanes, mutations, and the simulated checkpoint ran in a
disposable `git worktree` created at the declared base with the ten target files copied in; that
worktree was removed and pruned before this verdict was written. No probe commit is reachable from
any ref.

## 1. Freeze anchors

Reproduced three times - live target before validation, independent clean checkout, live target
after validation. All three agree.

- raw porcelain (`--untracked-files=all`): **795 bytes**,
  `3fb52cfea40ec2f0d083ca20355f5c5c18734c13aee0e56aaa382e8ce670f0e9` - matches the declared value.
- `...task-6-4-dirty-manifest-2026-09-01.txt` is **byte-identical** to live porcelain (`cmp` against
  `git status` output: no difference).
- scoped content record over the nine non-fixture paths: **1,348 bytes**,
  `4ec24d5213af39e48f91d5a22570a89c4b7bb06fa071af0cb3dd0fc321f4d77b` - matches the declared value.
- entries: **10** - 7 modified, 3 untracked, 0 staged. No `src/**`, no `openspec/specs/**`, no
  public API baseline, no package manifest.

The unscoped ten-row record is 1,502 bytes /
`00027a858d2d92e7627a0e20b0f7e714babb6514ca83dbacb6d0792127c611e9`; it is claimed nowhere and is
recorded only so the scoping is unambiguous.

## 2. Prior checkpoint chain

- `609e9c45` (parent `2dbf2b94`) contains **exactly the sixteen paths** of the Task 6.3 target I
  approved on 2026-09-01, verified by set difference.
- `db0ae75c` carries my Task 6.3 verdict **byte-exact** at
  `046078afab63a82c5f0c741de9cebefe7c86d0993d3acf67d5336120ac0eb579`.
- `c23dc4e7` is the one-path mechanical activation and its tree is the declared base tree
  `2c4e23ec`.

**All four of my Task 6.3 findings are closed, and I verified each at the fix:** U-1 is a whole-block
SHA-256 pin on `CR-009a` (`085fb975...`, which I independently recomputed from the live block);
U-2 now resolves each companion link's target document *and* its heading anchor, and requires a
single occurrence; U-3 renames Task 6.3 into the 7.20 provenance sentence; U-4 captures the class
attribute block and checks it with order-independent `ContainAll`.

## 3. The blocking defect

### R-1 (P1) - an unrelated normative excluded-claim bullet is deleted without disclosure

`docs/specs/18-semantic-appendix.md` at the base contains, at line 175:

```
- Fan-out rank one is a computational complexity class. Step bodies remain arbitrary code.
```

That line is **absent from the target**. The file diff is 3 added / 3 removed and reads as a rewrite
of the `MaxActiveFibers` bullet, but the removed three lines are the old two-line `MaxActiveFibers`
bullet **plus** this unrelated bullet.

It is a genuine entry of the `## Deliberately excluded claims` list, not a stray:

- it survives verbatim in the source artifact
  `openspec/changes/reshape-developer-facing-interfaces/artifacts/semantic-appendix.md:179`;
- it appears in the dated amendment
  `AMENDMENT-2026-07-28-root-only-fanout-and-authoring-lifecycle.md:559` as an excluded claim; and
- it is quoted in the 2026-07-28 approval verdict for that amendment.

So the target now leaves `docs/specs/18-semantic-appendix.md` diverged from its own source artifact,
and the normative appendix no longer records that the semantic model does **not** claim fan-out rank
one is a complexity class.

Nothing discloses it. The review request's claim 1 describes only the `MaxActiveFibers` change; the
disposition record's `Result` section describes only that; neither ledger mentions it. The scope
statement is "correct the stale `MaxActiveFibers` implementation statement", and this is not that.

Nothing detects it either. The whole Infrastructure lane is 219/219 green with the bullet gone -
`Task64_...` pins the appendix only through the `MaxActiveFibers` sentence and the
`## Deliberately excluded claims` heading, and no other guard reads this file. Probe Q6 restores the
bullet and `Task64_...` stays green, which confirms both that the deletion is entirely unguarded and
that the correction is guard-neutral: restore the line, recompute the two anchors, and refreeze.

I am rejecting rather than approving-with-a-finding because the fix necessarily changes the target
bytes, and therefore the freeze. An `APPROVE` here would authorize a checkpoint commit that
permanently lands an undisclosed deletion of normative content, after which it becomes the
historical baseline that later reviews measure against.

## 4. Everything else verified

All seven claims hold apart from the omission above.

**1. Appendix disposition.** `MaxActiveFibers` appears in `docs/specs/18-semantic-appendix.md`
exactly once, inside `## Deliberately excluded claims`, immediately followed by "Task 5.13 removed
that quantity; the v1 execution model has only host execution-path capacity and node-local `ForEach`
admission." Correct and correctly placed.

**2. Zero product-source occurrences.** Confirmed by an independent repository-wide scan, not by the
guard: no `src/**` path contains the token.

**3. Inventory exactness.** I enumerated every occurrence in the repository myself, tracked and
untracked, rather than trusting the table. The nine non-review owners are exactly as declared -
appendix 1, disposition artifact 4, harmonization ledger 1, dated amendment 13, reshape design 1,
proposal 1, tasks 1 - plus the guard's own source, which holds the constant and the expected
inventory. Probe Q8 (13 to 12) is RED, so the counts are exact rather than approximate. See V-2 for
the file types the inventory does not reach.

**4. Non-vacuous guard.** Confirmed. All five of the author's declared negative controls reproduce
exactly: Q1 product source RED, Q2 active document RED, Q3 lost negative disposition RED, Q4
reopened Task 6.4 RED, Q5 historical `docs/review/` occurrence GREEN.

**5. Review-record exclusion.** Confirmed as designed and correctly scoped to the `docs/review/`
prefix.

**6. Crosswalk delta.** 1,387 to 1,388 declarations and 699 to 700 active declarations, with
`physicalFiles` unchanged at 337 - exactly the one new guard fact. Q12 confirms a count edit is RED.
The 866-row retired ledger is untouched.

**7. Scope boundaries.** Confirmed by inspection of all ten paths.

**The inherited Task 6.2 boundary fix is correct and load-bearing.** The Task 6.2 guard previously
terminated the Task 6.3 ledger block at the literal `"\n- [ ] 6.4 "`, so completing Task 6.4 broke
it. The replacement matches `^- \[[ xX]\] 6\.4 `, which is state-independent, while Task 6.4's own
guard still requires `[x]`. Probe Q7 reverts the fix and `Task62_...` goes RED, so the correction is
necessary and not cosmetic.

## 5. Validation reproduced from an independent clean checkout

Disposable worktree at `c23dc4e7` with the ten target files copied in; both anchors reproduced there
before any build.

| gate | result |
|------|--------|
| Debug `dotnet build OrcaCore.slnx --no-incremental -m:1 -warnaserror` | 0 warnings, 0 errors |
| Release, same flags | 0 warnings, 0 errors |
| exact package feed | 12 packages |
| `OrcaCore.Core.Tests` | 350/350 |
| `OrcaCore.Engine.Ephemeral.Tests` | 79/79 |
| `OrcaCore.Engine.Durable.Tests` | 99/99 |
| `OrcaCore.Acceptance.Tests` | 37/37 |
| `OrcaCore.Hosting.Tests` | 24/24 |
| `OrcaCore.ProviderCertification` | 96/96 |
| `OrcaCore.Providers.PostgreSql.Tests` | 101/101 |
| `OrcaCore.Providers.SqlServer.Tests` | 72/72 |
| `OrcaCore.Integration.Tests` | 11/11 |
| `Disposition=Infrastructure` | 219/219 |
| `Disposition=ExpectedRed` | exactly 14 failures, 0 passed |
| unfiltered guards | 233 total = 219 green + 14 expected red |
| `openspec validate --all --strict` | 18 passed, 0 failed |
| `git diff --check` | exit 0 |

Every declared number reproduces exactly. A simulated checkpoint produced **exactly 10 real paths**
and a clean worktree, so the target is mechanically committable - the objection is to its content,
not its shape.

## 6. Mutation and probe results

Control green. RED:

| id | mutation | result |
|----|----------|--------|
| Q1 | add a product-source occurrence | RED (author's control 1) |
| Q2 | add an active-document occurrence | RED (author's control 2) |
| Q3 | invert the appendix's negative disposition | RED (author's control 3) |
| Q4 | reopen Task 6.4 | RED (author's control 4) |
| Q7 | revert the Task 6.2 ledger-boundary fix | RED (Task 6.2 guard) |
| Q8 | change the amendment's inventory count 13 to 12 | RED |
| Q11 | forge the scoped content anchor by one hex digit | RED |
| Q12 | tamper the crosswalk active-declaration count | RED |

GREEN by design, and correctly so:

| id | probe | result |
|----|-------|--------|
| Q5 | add a historical `docs/review/` occurrence | GREEN (author's control 5) |

GREEN, and these support R-1 and V-2:

| id | probe | result |
|----|-------|--------|
| Q6 | restore the deleted `Fan-out rank one` bullet | **Task64 GREEN** - the deletion is unguarded in both directions |
| Q9 | add an occurrence to `docs/specs/17-public-authoring-contract.cs` | **Task64 GREEN** |
| Q10 | add an occurrence to test source | **Task64 GREEN** |

## 7. Non-blocking findings

### V-2 (P3) - the inventory reaches only `.md` under `docs/` and `openspec/`

`Task64_...` scans `src/**/*.cs` for product source and `*.md` under `docs/` and `openspec/` for
documentation. Probe Q9 adds `MaxActiveFibers` to `docs/specs/17-public-authoring-contract.cs` - a
`.cs` documentation artifact that lives inside `docs/specs/` and is itself the subject of the very
next task, 6.5 - and the guard stays green. Claim 3 says "every non-review documentation occurrence
is exactly inventoried"; in practice that means every Markdown one.

Q10 shows the same for test source, which is correct by design - the guard's own file legitimately
holds ten occurrences - but the exclusion is implicit rather than stated.

Widening the documentation scan to `*.cs` under `docs/`, or stating the `.md`-only scope in claim 3,
closes the gap.

### V-3 (P3, observation) - nothing pins the appendix beyond one sentence

R-1 was possible because `18-semantic-appendix.md` has no structural protection: `Task64_...`
asserts one sentence inside one section and nothing else. Both `CR-009a` and `CR-014a` now carry
whole-block SHA-256 pins for exactly this failure mode. The `## Deliberately excluded claims` list is
a closed enumeration of negative claims and would benefit from the same treatment, or at minimum
from an assertion that it stays in sync with its source artifact
`openspec/changes/reshape-developer-facing-interfaces/artifacts/semantic-appendix.md`, which still
holds the correct list.

## 8. What a re-freeze needs

1. Restore the deleted bullet to `docs/specs/18-semantic-appendix.md`, in its original position
   between "Every `ForEach` item is unconditionally eventually admitted." and "Scope-tree acyclicity
   rules out resource wait cycles." - or, if its removal was intended, say so explicitly in the
   disposition record and justify it, because it is not within Task 6.4's stated scope.
2. Recompute the raw porcelain and scoped content-record anchors and refresh the active-freeze
   registry entry.
3. Re-run the packet. No guard change is required: probe Q6 confirms the restoration is
   guard-neutral.
4. Optionally close V-2 and V-3 in the same refreeze.

Nothing else in this target needs to change.

## 9. Reviewer hygiene

- The reviewed worktree was never modified, staged, or committed by this reviewer. `HEAD` is
  unchanged at `c23dc4e732881fb2ddb7792836e740d72ce2644f`, nothing is staged, and both anchors
  reproduce after validation exactly as they did before it.
- All builds, suites, container lanes, mutations, and the simulated checkpoint ran in a disposable
  `git worktree`, now removed and pruned. `git log --all` contains no probe commit.
- This reviewer created no commit in the reviewed repository.

## Determination

The declared work is done well: the appendix disposition is correct, the inventory is exact and
independently confirmed, the guard is non-vacuous with all five author controls reproduced, the
inherited Task 6.2 boundary defect was found by the author's own broad run and fixed correctly, and
the entire validation packet - including all three container lanes - reproduces from a clean
checkout. But the frozen target also removes a normative excluded-claim bullet that no claim
describes, no artifact records, and no guard protects, leaving `docs/specs/18-semantic-appendix.md`
inconsistent with its own source artifact. That is content the checkpoint would make permanent, so it
must be corrected before the freeze is approved rather than after.

**Verdict:** **REJECT**
