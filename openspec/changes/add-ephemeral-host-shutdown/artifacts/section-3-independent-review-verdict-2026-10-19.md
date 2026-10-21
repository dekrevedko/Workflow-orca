# Issue #2 Section 3 source, consumer and PR target — independent review verdict

**Date:** 2026-10-20 (request dated 2026-10-19)
**Target:** base `6253f6c`, 57 entries (38 modified, 19 added, 0 deleted), tree
`fac02f62a3ef0fdebb975f496001b3e27b00ed5f`, porcelain SHA-256 `964af63d…`. HEAD, the porcelain hash
and the tree all reproduced, and nothing drifted during review.

## Verdict: APPROVE

There are no P1 findings. F1 (P2, test only) is fixed in the publication target, and the P3 notes are
optional.

**Authorized now:**
1. Stage exactly the 57 manifest paths, excluding this verdict. Confirm tree `fac02f62…` with parent
   `6253f6c`, then commit.
2. Add a verdict-only child commit.
3. Prepare **one** publication target (task 4.1), committed without another independent review. It
   contains:
   - the F1 fix;
   - the mechanical binding: the records move to source-approved and name this checkpoint, its child
     and this verdict; tick 3.5;
   - the final PR text (P3-1).

   Record in the commit message:
   - OpenSpec strict and Infrastructure results;
   - 20 or more consecutive unfiltered ephemeral `dotnet test` runs, run in concurrent pairs, all
     green.

   If the target needs anything beyond this scope, especially any product-source change, it comes
   back for review.
4. Then push the branch and open the PR to master.
   - The PR's CI must be green.
   - Merge it with a merge commit, never a squash or rebase.
   - Merging and closing issue #2 remain the owner's decision.

## What I verified

**Chain.**
- `2e51fbd` has parent `4bd7c3e` and the approved tree `0f4ad1fb…`.
- `6253f6c` adds only the transition verdict (`12f99ea8…`).
- Schema 18 and the transition guards check both through git.

**Implementation.**
- **Ledger and observers.** `EphemeralHostLifetime` is a conserved ledger.
  - Stable zero is final: `Own` refuses once the ledger is done.
  - Observers release their own admission before waiting, so they never wait on themselves.
  - Finite observer timers are owned during setup and handed to cleanup exactly once.
  - Decision precedence (fault, completed, cancelled, deadline) matches the contract.
  - A pre-cancelled public call has no effect. The hosted path still closes and enters Stopping.
- **Phases.** `ApplicationStopping` only closes admission: it is read from the token state and via an
  unsafe registration.
- **Acceptance election.** `TryAcceptStart` checks Stopping and accepts under the visibility gate.
  `stopping` is set before the stop worker starts, and the worker's `TryOwn` and `IsAccepted` reads
  take the same gate, so acceptance and abort are atomic.
- **Stop worker.** The worker seals retention and timers first, and records a fault on any cleanup
  failure. It then cancels accepted instances and fences unaccepted ones through their lanes.
- **Lane.** The lane holds host ownership from creation until final removal.
- **Registration.** The reserved-descriptor check runs only after the idempotent same-role return, so
  idempotent registration still works.

**Tests.**
- No existing runtime test was modified.
- Four new shutdown test files cover the planned 3.1 matrix with barriers and fake clocks, including:
  - an initialized start accepted while Closed;
  - a queued pre-Init start refused;
  - the Stopping abort;
  - ingress, delay and retry progress while Closed;
  - first-dispatch cancellation;
  - sticky faults;
  - post-Completed rejection.

  There are no sleeps.
- The guard edits move the registry from `ContractApproved…` to `CanonicalApprovedSourcePending` and
  bind the canonical evidence through git. The source-scope assertion is narrowed to
  `src/OrcaCore.Engine.Ephemeral/` only.

**Consumer.** I copied the consumer outside the repository and restored it from my own freshly packed
feed into an empty cache. It resolved only `OrcaCore`, `OrcaCore.Core` and
`OrcaCore.Engine.Ephemeral`, and passed 48/48:
- all three recipes, each on three fresh roots;
- output-wait cancellation;
- the concurrent-stop caveat;
- the RunAsync negative.

**Lanes,** in a disposable copy at `E:/ocv207` (a simulated commit on `6253f6c`, since removed):

| Lane | Result |
|---|---|
| OpenSpec strict | 23/23 |
| Release `-warnaserror` | 0 warnings, 0 errors |
| Exact feed, then Infrastructure | 256/256 |
| CI's six unit projects, CI command with coverage | 969/969: 354 + 352 + 102 + 37 + 24 + 100 |
| Ephemeral, unfiltered, run in concurrent pairs | 19/20; the first 2 overlapped the consumer run (F1) |

I did not re-run PostgreSQL, SQL Server, integration or coverage. The PR's CI runs and gates them.

## F1 — P2 — A test race in issue #1's bounded slot test is exposed under load

**Failing test.** Run 5 failed
`PhysicalOwnershipTests.TimedOutPhysicalAttempt_HoldsItsSlotUntilAnotherInstanceCanEnter(bounded: True, capacity: "pool")`
with `WorkflowInstanceNotFoundException` at line 420, `secondInstance.GetSnapshotAsync`. This target
does not modify that file.

**Cause.** The bounded profile retains only one instance.
- The first instance's capacity release lets the second instance enter before the first instance's
  own lifetime settlement is recorded.
- Under load the second can therefore settle first and receive the earlier ordinal. Count pressure
  then retires the second instance and keeps the first.
- That is correct product behavior: retirement follows settlement order. The test's own comment
  already expects count pressure to retire the first handle.

**Fix,** test only, in the publication target. Remove the ordering assumption. Either:
- make the bounded assertion read the second runtime object directly, as the test already does for
  the first;
- or await the first instance's `Lifetime.WhenSettled` before starting the second.

Keep every slot and attempt assertion.

## P3 (optional)

- **P3-1 — PR text.** Before publishing, drop the closing "review material only…" paragraph and add
  the issue reference the owner wants (for example "Addresses #2").
- **P3-2 — Duplicated lane code.** `InstanceExecutionLane` now carries a local copy of Core's
  `InstanceLane` channel logic with ownership hooks, so that Core and durable source stay unchanged.
  Future fixes to Core's lane must be mirrored there. Track a later follow-up that moves optional
  ownership hooks into Core's `InstanceLane` and deletes the copy.
