# Harmonization Task 6.1 review-remediation independent review verdict

**Date:** 2026-08-23
**Reviewer:** independent review session (cold verification, no authoring involvement)
**Target:** the dirty worktree named by
`docs/review/harmonize-downstream-capability-specs-task-6-1-review-remediation-dirty-manifest-2026-08-23.txt`
**Base:** `603dffc02b13f0a3515ffb007af247f378f7b790`
**Requested authorization:** create only the Task 6.1 remediation checkpoint

## Scope

This verdict covers the six confirmations in
`harmonize-downstream-capability-specs-task-6-1-review-remediation-independent-review-request-2026-08-23.md`,
the freeze anchors, the checkpoint chain, the provenance fixture at schema 8, and the declared
validation packet. It does not re-open Task 6.1's approved content, which was reviewed and approved
on 2026-08-22 and committed as `603dffc0`.

## Method

Every claim was recomputed rather than accepted. The reviewed worktree was never modified, staged,
or committed. All builds, lanes, mutations, and simulated checkpoints ran in a disposable
`git worktree` created at the declared base with the sixteen target files copied in; that worktree
was removed and pruned before this verdict was written. No probe commit is reachable from any ref.

## 1. Freeze anchors

Both anchors were reproduced three times: from the live target before validation, from the
independent clean checkout, and from the live target after validation. All three agree.

- raw porcelain (`--untracked-files=all`): **1,361 bytes**,
  `5948bbc3f14a2c5a30b4e800df1d8a4993c5f6e700364e82b42cbbc39274cd6d` — matches the declared value.
- `...review-remediation-dirty-manifest-2026-08-23.txt` is **byte-identical** to live porcelain
  (`cmp` against `git status` output: no difference). There are no phantom rows.
- scoped content record over the fifteen non-fixture paths: **2,336 bytes**,
  `f2baefb0c901f215831b1a765ffecf3a11464c01ae8a691a6683a6e274a62f1f` — matches the declared value.
- entries: **16** — 11 modified, 5 untracked, 0 staged. No path under `src/**`.

The unscoped sixteen-row record is 2,490 bytes /
`739171fd70b7cb3b7016eaa5cd14aeeba5921eaa605eac5223795d0f9fbb79d3`; it is not claimed anywhere and
is recorded here only so the scoping is unambiguous.

## 2. Checkpoint chain

- `603dffc0` has parent `87f0b0dd` and tree `f6c7821a` exactly as declared.
- Its path set is **exactly the eleven paths** of the approved Task 6.1 manifest, verified by set
  difference rather than by count.
- Task 6.1's registered verdict evidence
  `...task-6-1-independent-review-verdict-2026-08-22.md` hashes to
  `fc660b737c60e1f75eeea9d792818571d57b09db1de7ff17eaa8a0df33e4b739`, which is byte-for-byte the
  verdict this reviewer issued on 2026-08-22. It was registered unmodified.

All four provenance entries were recomputed from first principles:

| task | manifest | path-sorted | historical rows | committed-blob record | pins |
|------|----------|-------------|-----------------|-----------------------|------|
| 5.1 | 17 / 1,228 B exact | exact | 17 / 2,428 B exact | `741cfd6b` exact | 9, exact + ordered |
| 5.2 | 12 / 947 B exact | exact | 12 / 1,793 B exact | n/a | 4, exact + ordered |
| 5.3 | 10 / 821 B exact | exact | 10 / 1,529 B exact | `dac6b842` exact | 3, exact + ordered |
| 6.1 | 11 / 878 B exact | exact | 11 / 1,657 B exact | `e6863f9c` exact | 8, exact + ordered |

Task 6.1's dirty record and its committed-blob projection are the same bytes, which is the direct
evidence that `603dffc0` landed the frozen target with zero drift. Pin maximality was recomputed
independently for all four entries: every recorded pin verifies, and there is no unpinned row that
still matches the live worktree.

## 3. The six requested confirmations

**1. `MissingApproval` cannot carry a checkpoint; owner authority is named, not disguised.**
Confirmed. `RequireMissingApprovalCheckpointAbsent` rejects a non-null checkpoint commit or tree,
and `ValidateMissingApprovalStateSemantics` proves both rejections and the clean case in-process.
Task 5.3 moved to the new `OwnerAuthorizationAwaitingEvidenceCommit` state, whose validator requires
a checkpoint commit **and** tree, one APPROVE, no approval-evidence commit, and a state-evidence
path containing `-owner-approval-verdict-`. The disclosure record quotes the owner instruction
verbatim and states in its own body that it is explicit owner authorization and not an independent
technical review. `design.md` and `tasks.md` carry the same distinction. Mutations M3 (state
downgraded to `MissingApproval` with the checkpoint retained) and M4 (verdict renamed to an
independent-review filename) are both RED.

**2. Task 6.1's evidence is immutable historical provenance.** Confirmed — see section 2. Tampering
with the registered verdict hash (M12) or with the verdict body (M13) is RED.

**3. Callback-local scope handles in canonical and active reshape requirements.** Confirmed. Both
`openspec/specs/workflow-authoring/spec.md` and
`openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md` now read
"A nested, branch, item, leased, or callback-local scope handle SHALL expire...", with the matching
scenario line updated. The requirement heading is unchanged, and the two requirement blocks are
**byte-identical** (2,426 bytes, `b6acd0dd`), so the canonical file is a faithful projection of its
owning delta rather than a hand-edit. Reverting either side alone (M1, M2) is RED on both the
canonical synchronization gate and the freeze guard. See finding S-2 for what is *not* pinned.

**4. Lifecycle corpus guard.** Confirmed, and each clause is independently mutation-proven:
M7 (re-add `[Trait("AC", "AC-021")]` alongside the new trait) RED — this is exactly the Q-3 hole,
now closed; M8 (`Skip` a representative evidence member) RED — the Q-4 hole, now closed; M9 (remove
AC-021 from `StagedWorkflowBuilderTests`) RED, so the real owner cannot be orphaned; M10 (break the
CI filter) RED. The `Requirement=CR-009a` lane selects and passes **20 tests**, and the CI step sits
in the `unit` job after the Release solution build with `--no-build --no-restore -c Release`, so it
is genuinely executable rather than decorative. See S-4 for the weakness in *how* the CI text is
checked.

**5. Active-freeze schema 8 scoped content record, before and after commit.** Confirmed, and both
branches are proven non-vacuous:

- `contentRecordExcludedPaths` must equal exactly the singleton provenance-fixture path; widening it
  to a second file (M6) is RED, and the manifest must itself contain that fixture.
- Drift in any non-excluded target file (M5) is RED on the pre-commit worktree projection.
- A forged but well-formed anchor (M14, one hex digit changed) is RED.
- **Post-commit:** a simulated checkpoint committing all sixteen paths leaves a clean tree and keeps
  the Infrastructure lane at 216/216. Omitting one manifest path (P-2c) is RED with "the checkpoint
  must contain exactly the active frozen manifest paths". Committing all sixteen paths with one
  file's bytes drifted (P-3) is RED with "the committed checkpoint must preserve the active freeze's
  scoped content anchor" — the committed-blob branch is live, not merely present.

**6. Leased retry harness awaits its notification.** Confirmed as to the race: both fixtures now
`await gate.SecondStarted.Task` before awaiting start completion, the old post-completion snapshot
is forbidden by source guard, and reverting either fixture (M11) is RED with the count named. Four
concurrent `causal-release-gap-recovery` Release runs are green. **However, the replacement shape
introduced a distinct defect — see S-1, which is measured, not inferred.**

Scope claims also hold: no `src/**` path is in the target, `6.2` is still an open checkbox, and the
harmonization ledger is **19 complete / 15 open / 34 total**.

## 4. Validation reproduced from an independent clean checkout

Disposable worktree at `603dffc0` with the sixteen target files copied in; both anchors reproduced
there before any build.

| gate | result |
|------|--------|
| Debug `dotnet build OrcaCore.slnx --no-incremental -m:1 -warnaserror` | 0 warnings, 0 errors |
| Release, same flags | 0 warnings, 0 errors |
| exact package feed | 12 packages |
| `OrcaCore.Core.Tests` | 350/350 |
| `Requirement=CR-009a` | 20/20 |
| `OpenSpecCorpusGuards` | 5/5 |
| `Disposition=Infrastructure` | 216/216 |
| `Disposition=ExpectedRed` | exactly 14 failures, 0 passed |
| unfiltered guards | 230 total = 216 green + 14 expected red |
| `causal-release-gap-recovery`, 4 concurrent Release runs | 4/4 green |
| `openspec validate --all --strict` | 18 passed, 0 failed |
| `git diff --check` | exit 0 |

Committability: the simulated checkpoint produced **exactly 16 real paths**, a clean worktree, and
216/216 afterwards. The round-40 phantom-row defect cannot recur on this manifest.

## 5. Mutation and probe results

Control green. All of the following are RED:

| id | mutation | result |
|----|----------|--------|
| M1 | revert scope wording in canonical only | RED (sync gate + freeze) |
| M2 | revert scope wording in the reshape delta only | RED (sync gate + freeze) |
| M3 | Task 5.3 state to `MissingApproval`, checkpoint retained | RED |
| M4 | rename the owner verdict to an independent-review filename | RED |
| M5 | drift one non-excluded target file | RED |
| M6 | add a second path to the exclusion set | RED |
| M7 | restore `[Trait("AC", "AC-021")]` on the lifecycle suite | RED (Q-3 closed) |
| M8 | `Skip` a representative lifecycle member | RED (Q-4 closed) |
| M9 | remove AC-021 from its real lambda-mode owner | RED |
| M10 | break the CI `Requirement=CR-009a` filter | RED |
| M11 | restore the pre-remediation scheduler sample | RED |
| M12 | tamper the registered Task 6.1 verdict hash | RED |
| M13 | edit the immutable Task 6.1 verdict body | RED |
| M14 | forge the scoped content anchor by one hex digit | RED |
| P-2c | checkpoint omitting one manifest path, clean tree | RED |
| P-3 | checkpoint with all paths but one drifted blob | RED |
| R-3 | restore the removed `rejections > 0` invariant | RED (see S-3) |

One probe is **GREEN**, and that is finding S-2:

| id | probe | result |
|----|-------|--------|
| P-1 | coherently revert the `scope` wording from **both** trees, refresh the provenance record, artifact hash, and scoped anchor | **GREEN — 216/216** |

## 6. Findings

### S-1 (P2) — the lease fix trades a flaky red for an unbounded hang, and the new guard blocks the correct fix

The remediation replaced a post-completion sample of `gate.SecondStarted.Task.IsCompleted`, guarded
by a named `InvalidOperationException`, with a bare `await gate.SecondStarted.Task;`. Removing the
scheduler-sensitive sample is right. But the replacement is an **unbounded** await, and there is no
per-test timeout anywhere in this solution: no `xunit.runner.json`, no `Timeout` on the scenario
theories.

Measured, not reasoned. With the second-attempt signal suppressed so the workflow completes without
starting its retry — precisely the condition the deleted diagnostic named:

- **remediated shape:** the `causal-release-gap-recovery` lane produced no result after **150
  seconds** and was killed. In CI it would consume the job's entire budget.
- **pre-remediation shape, identical fault:** failed in **486 ms** with
  `System.InvalidOperationException: The timed-out retry fixture completed without starting its
  second attempt.`

The repository already contains the correct pattern, used two lines earlier in both fixtures for
`gate.FirstStarted`: `AwaitSignalBeforeWorkflowCompletionAsync(signal, workflow, message)` awaits
the notification **and** fails fast with a named message if the workflow completes first. That is
notification-driven and diagnosable at the same time.

Compounding it, the new guard pins the count of `await gate.SecondStarted.Task;` occurrences to
exactly two. Adopting the helper drops that count to zero, so the guard as written **forbids the
better fix**. The remedy is one line per fixture plus widening the guard to accept the helper form.

This does not touch product source, does not affect the green lane, and is strictly better than the
pre-remediation shape on the passing path. It should not block the checkpoint, but it should be
fixed rather than inherited.

### S-2 (P3) — the Q-2 fix is content-only; nothing pins it

P-1 is green: reverting "or callback-local scope handle" from canonical *and* the reshape delta
together, then refreshing the provenance record, the artifact hash, and the scoped anchor, leaves
all 216 Infrastructure tests passing. The Task 6.1 corpus guard pins the three lifecycle states, all
five `SFE-AUTH-LIFECYCLE-*` codes, and five exact CR-009a phrases, but it never pins the
callback-local handle enumeration in `CR-009a`, in canonical `workflow-authoring`, or in the delta.

Q-1, Q-3, Q-4, and Q-5 all received regression pins. Q-2 did not, so the exact defect it named can
silently return. One containment assertion on the enumeration in each of the three files closes it.

### S-3 (P3) — an undisclosed invariant was removed

`ApprovalAwaitingEvidenceCommit` previously required `rejections > 0`. That line is deleted in this
target and the deletion appears in none of the six confirmations, the remediation record, or
`design.md`. R-3 confirms it was **necessary**: restoring it turns the freeze guard RED with
"Expected rejections to be greater than 0, but found 0", because Task 6.1 was approved on the first
pass. The change is correct; only its silence is the finding. A state machine that assumed every
approval follows a rejection was an accident of the 5.1/5.2 history, and the packet should say so.

### S-4 (P3) — the CI check for CR-009a is a bare substring match

The guard asserts only that `ci.yml` contains the `Requirement=CR-009a` filter text. It does not
verify that the text sits inside a `dotnet test` step, in a job that builds Release, or even outside
a YAML comment. The same file already contains `ValidateInfrastructureGuardCiLane`, which parses the
workflow into steps and validates their structure. The step is genuinely correct today — I verified
its job and flags by hand — but the pin is weaker than the one next to it and would survive
commenting the step out.

### S-5 (P3) — the schema 8 self-test exercises a different helper than the code path it defends

`ValidateActiveFreezeContentRecordSemantics` proves that drift inside the record changes it and that
the excluded fixture does not, using `BuildHistoricalContentRecord`. The active freeze actually uses
`BuildWorktreeContentRecord` and `BuildCommittedContentRecord`. The real anchors do cover those
paths — M5 and P-3 prove it — so nothing is unprotected; the self-test is simply narrower than it
reads.

### S-6 (P3) — a stranded line in a normative file

Both `workflow-authoring` specs now contain a three-word line, `operator through a`, left by the
rewrap. Harmless to every guard, since the requirement blocks are byte-identical, but it is a
formatting wart in a file classified NORMATIVE. Worth rewrapping the next time the block is touched.

### S-7 (P3) — half the new state pair is unexercised

No entry uses `OwnerAuthorized`, so only the `OwnerAuthorizationAwaitingEvidenceCommit` branch is
covered by data today. This mirrors how `Approved` was introduced and is not a defect, only an
untested branch to remember at the next mechanical transition.

### Note on the owner-authorization quotation

The disclosure quotes the owner instruction verbatim. That sentence cannot be verified from
repository artifacts; it is chat provenance. It is consistent with the recorded history, and the
record's value is that it names the authority source honestly rather than laundering it as
independent review. Recorded here only so the limit of my verification is explicit.

## 7. Operational note on landing this verdict

Writing this file into the reviewed worktree makes the live projection **17** rows against a
**16**-row manifest, and `ValidateActiveReviewFreeze` asserts raw-order equality between the two, so
the Infrastructure lane goes RED until the two are reconciled. The same trap was measured in the
previous round. Commit the approved sixteen-path checkpoint first, then add this verdict; or
refreeze to seventeen entries before validating. This is the guard working as designed.

## 8. Reviewer hygiene

- The reviewed worktree was never modified, staged, or committed by this reviewer. `HEAD` is
  unchanged at `603dffc02b13f0a3515ffb007af247f378f7b790`, nothing is staged, and both anchors
  reproduce after validation exactly as they did before it.
- All builds, lanes, mutations, and simulated checkpoints ran in a disposable `git worktree`, which
  has been removed and pruned. `git log --all` contains no probe commit.
- This reviewer created no commit in the reviewed repository.

## Determination

The six confirmations are delivered and every one of them is mutation-proven, the freeze anchors and
the whole provenance chain reproduce independently, the declared validation packet reproduces from a
clean checkout, and the checkpoint is provably committable at exactly sixteen paths. S-1 is a real
regression introduced by this remediation, but it is confined to test-harness failure diagnostics,
leaves the green lane and all product source untouched, and has a one-line remedy; it is recorded to
be fixed, not to block. S-2 through S-7 are disclosure and durability gaps, not correctness defects.

**Verdict:** **APPROVE**
