# Issue #1 task 3.3 combined ownership slice — independent review verdict

**Date:** 2026-10-17
**Reviewer:** independent review
**Target:** `issue/1-ephemeral-retention`, parent `ef88957`, 40 entries,
tree `e4cf5a0651d9324ae1a0ed8dff735d3f8978aeee`.
- Porcelain: 3,425-byte record,
  SHA-256 `df595f17cc65163fa97d6ce7797ea1ffef3c5aab7aabd78e32dc7a675be82c85`.
- Content record SHA-256: `a1a3ca6058d039f567e27d78a66aea355276e2a9c6d3b50ba541ff9eab8b7260`.
- Both were identical at the start and end of review.

## Verdict: APPROVE the ownership slice (tasks 3.1 and 3.2) for the task 3.3 checkpoint

There are no P1 findings. One P2 (F1, below) must be fixed before any pull request or integration.
It does not block this local checkpoint: CI runs only on pushes to `main`, `master` or `feature/**`
and on pull requests to master, not on `issue/**` pushes.

**Authorized now:**
1. Stage exactly the 40 manifest paths. Do not stage this verdict. Confirm that the tree is
   `e4cf5a06…` with parent `ef88957`, then commit.
2. Add a verdict-only direct child commit.
3. Leave the task checkboxes as they are. The existing validator requires 3.1 through 5.3 to stay
   open, so the boxes are ticked inside the next slice (section 3), not in a separate binding target.

## 1. What I verified

**Approved tests intact.** I extracted `task-3-1-approved-baseline-2026-10-17.zip`
(SHA-256 `4308f221…`). It is byte-identical to my own round-5 snapshot.
`PhysicalOwnershipTests.cs` differs from the approved file only by:
- seven `ExpectedRed`→`Implemented` disposition flips;
- one class-level `AC-535` trait;
- the three `Lifetime.WhenSettled` awaits that resolve my P3-1 race.

The two abort awaits are gated on the expected status, so a missing fence still fails on its
original assertion. No assertion, case or control was removed or weakened.

**Implementation.** The core is `EphemeralInstanceLifetime`, which reference-counts the instance's
outstanding work.
- **Ownership.** The provisional start owns the instance until acceptance or abort.
  `TryOwn`/`Own` refuse once the instance is retired, so a stale save, commit, router call, timer,
  retry or resource continuation cannot resurrect it.
- **Acceptance and abort.** Acceptance and abort are decided under one gate, and `TryAccept` respects
  the caller token.
- **Locking.** Cleanup actions, cancellation and the completion observer all run outside the gate.
  `TerminalCancellation` is read only inside `Lifetime.RunAsync`, so the token is never read after it
  is disposed.
- **Abort fence.** `AbortProvisionalStartAsync` signals cancellation and then fences to `Terminated`
  on the owning lane with `CancellationToken.None`. Existing terminal statuses are preserved. The
  original error is rethrown, and any cleanup failure is attached as `Data`. The abort is recorded
  as a provisional-abort lifecycle origin, not as a termination command (OB-026).
- **Retirement.** `EvictInstance`/`EvictTerminalInstances` now go through the same retirement path.
  There is no public API, friend, count/age policy or sweeper.

**Guards and fixtures.**
- The DAG guard now freezes only DAG and DAG.Hosting source, DAG deltas and the named contract
  fixtures, which resolves my 3.1 P3-3. The fingerprint, API, git-approval and canonical checks are
  retained.
- The inventory, package-digest and acceptance-deferral edits are consistent.

**Lanes.** I ran these in a fresh disposable copy, `E:/ocv201`, at `ef88957` with the 40 entries, and
reproduced the tree `e4cf5a06…`:

| Lane | Result |
|---|---|
| OpenSpec strict | 22/22 |
| Release `-warnaserror` | 0 warnings, 0 errors |
| Exact 12-package feed | built |
| Infrastructure | **248/248** |
| CI's six unit projects (Core, Ephemeral, Durable, Acceptance, Hosting, ProviderCertification), unfiltered | all green on the first pass |

## 2. F1 — P2 — The ephemeral unit project is now flaky under full-project load

Repeated unfiltered runs of `OrcaCore.Engine.Ephemeral.Tests`:

| Tree | Failed runs | Failing tests |
|---|---|---|
| Target | 4 of 18 | `ExactNamedStepThrottle_IsSharedAcrossInstances` (3×), `SelectedParallel_SaturatedPoolReleasesTurnForRunnableSiblingAndResumesOwner` (1×) |
| Base `ef88957` | 0 of 15 | — |

Both tests pass 40 of 40 times when run in isolation on either tree.

These are pre-existing test races. They assert completion, or non-completion, before awaiting the
asynchronous continuation. The added lifetime bookkeeping shifts that timing enough to expose them.
`master` already fixed the throttle test after this branch's base, in `d8ac81a` and then `c1ef90b`,
by awaiting completion notifications.

**Required before any pull request or integration:**
- Port `c1ef90b`'s `HostGovernanceExecutionTests` change.
- Replace `SelectedParallel_…`'s point-in-time `targetDelivery.IsCompleted.Should().BeFalse()` with a
  deterministic barrier on the pooled branch's governance park.
- Show 20 consecutive green unfiltered runs of the ephemeral project.

This is folded into the next slice.

## 3. Next slice: finish all of Section 4 in ONE target

The owner asked for larger steps and a shorter review loop. The next and only remaining
implementation review before consumer qualification covers all of the following in a single
uncommitted target on top of the 3.3 checkpoint and its verdict child:

1. **Bind and tick 3.x.** Move both registry records to an ownership-approved, policy-pending stage
   that binds the 3.3 checkpoint, its verdict child and this verdict's hash. Tick 3.1–3.3, and
   adjust the validator so 3.x must be complete and 4.x reflects its real state.
2. **Implement 4.1–4.5 in full:**
   - the public `EphemeralTerminalRetentionPolicy`, the `TerminalRetention` property, factory
     validation and hosting fingerprint agreement;
   - the count/age retirement coordinator and its non-overlapping sweep;
   - query and output-wait pins with the linearized not-found contract;
   - atomic key expiry;
   - the start-to-observe gap test (AC-539).

   In the same change, update the executable public-API baseline, the compile fixtures and the
   package-source records.
3. **Fold in the carried P3s:**
   - the doc 17 §17.4 signature guard, which closes transition P3-2;
   - removal of the `NormativeContractGuards` ownership-row shim (P3-4);
   - migration of the `EvictInstance` probes if they are isolated (3.1 P3-4);
   - mapping of an `Own()` loss-race on a retired handle to `WorkflowInstanceNotFoundException`
     (MG-057);
   - replacement of the guides' "implementation pending" and "draft" wording for the parts that now
     exist (P3-6, P3-7);
   - the ledger row 209 correction note (P3-1).
4. **Fix F1** as described above.

Leave 5.1–5.3 (the fresh package-consumer experiment) and the `master` integration merge for the
**final** review, which also covers the pull request. Issue #1 then needs exactly two more reviews:
this Section 4 target, and the final consumer-plus-integration target.

**Lean evidence is sufficient.** One short review request, covering:
- the base commit, tree, porcelain and content-record hashes;
- the list of tasks completed;
- the lane results: strict OpenSpec, Release `-warnaserror`, Infrastructure, CI's six unfiltered unit
  projects, and 20 consecutive green ephemeral runs;
- the provenance-gate counts;
- the line coverage for `OrcaCore.Engine.*`.

Drop the self-inclusive NUL captures, copied-index rehearsals, ZIP archives, per-case TSVs and large
mutation catalogues. The checkpoint commit and git history are the provenance, and the reviewer runs
the probes. Keep the "no commit while a review is pending" and "verdict-only child" rules, because
they are cheap.

## 4. Remaining carried items

The transition-verdict P3s are dispositioned in section 3. 3.1 P3-1 through P3-3 are resolved.
3.1 P3-4 carries to the Section 4 target.
