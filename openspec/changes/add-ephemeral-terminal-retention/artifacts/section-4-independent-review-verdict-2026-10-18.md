# Issue #1 Section 4 — independent review verdict

**Date:** 2026-10-18
**Target:** `issue/1-ephemeral-retention`, base `0b33d02`, 39 entries, candidate tree
`09fd5669a00ef3feae38bdf97375950010df70eb`. The porcelain SHA-256 is `0bb4e80b…` and the content
record SHA-256 is `23adf09c…`. Both were identical at the start and end of review.

## Verdict: APPROVE

There are no P1 findings. Two P2 items must be fixed in the final target, before any pull request.
Neither blocks this local checkpoint, because CI does not run on `issue/**` pushes.

**Authorized now:**
1. Stage exactly the 39 manifest paths, excluding this verdict. Confirm the tree is `09fd5669…` with
   parent `0b33d02`, then commit.
2. Add a verdict-only child commit. Task 4.5 may be ticked in that same child, or in the final target.
3. Then build the final target, which covers:
   - tasks 5.1–5.3, the fresh package-consumer experiment;
   - the master integration, as a merge commit, never a squash;
   - the two fixes below;
   - the pull request.

## What I verified

**Chain.**
- `c1459cc` has `ef88957` as its sole parent and the tree `e4cf5a06…`.
- `0b33d02` has `c1459cc` as its sole parent and adds only the 3.3 verdict, with SHA-256
  `ecfc02f2…`.
- Only 3.1–3.3 and 4.1–4.4 are newly ticked.
- No source outside `OrcaCore.Engine.Ephemeral` changes.

**Public surface.**
- `EphemeralTerminalRetentionPolicy` and `EphemeralEngineHostOptions.TerminalRetention` exactly match
  doc 17 and MG-054.
- Validation runs in parameter order and names the failing parameter.
- The role fingerprint carries `retention:disabled` or all three values.
- The API baseline changes only by the approved members.

**Retirement.**
- `EphemeralRetentionCoordinator` stamps settlement once, with a monotonic timestamp and ordinal. It
  retires expired units first, then the oldest by ordinal down to `MaxRetainedInstances`.
- Sweeps are a non-overlapping one-shot timer that re-arms itself, and disposal is safe. Cleanup
  actions run outside the gate.
- Facade operations pin under the facade gate. `TryPin` refuses retired units, and a lost race maps
  to `WorkflowInstanceNotFoundException`.
- Output waits hold their pin across subscribe-then-recheck.

**Tests.**
- The approved ownership tests are unchanged, apart from moving the `Entered` signal after the
  `PhysicalTask` assignment, which fixes a harness race.
- The flaky `SelectedParallel_…` timing assertion was replaced by a governance-park barrier plus the
  semantic check that the pooled branch has not run.
- Master's `c1ef90b` throttle fix is ported.

**Lanes.** I ran these in a fresh short-path copy, `E:/ocv203`, and reproduced the tree `09fd5669…`:

| Lane | Result |
|---|---|
| OpenSpec strict | 22/22 |
| Release `-warnaserror` | 0 warnings, 0 errors |
| Infrastructure | passed |
| CI's six unit projects, unfiltered | all passed |

## P2 items for the final target

- **F1 — A sweep failure can crash the host.** `Enforce` rethrows any cleanup failure. When the
  `Sweep` timer callback calls it, that exception escapes a `TimeProvider.System` timer callback,
  which terminates the process. The cleanup paths that can throw include `IndexSnapshot`, the
  diagnostics refresh and the observers. Fix: catch inside `Sweep`, record a bounded diagnostic, and
  keep re-arming. Add a test that injects a throwing cleanup during an idle sweep.
- **F2 — A rare flake remains under `dotnet test`.** 2 of 47 unfiltered `dotnet test` runs of
  `OrcaCore.Engine.Ephemeral.Tests` failed. Both failures came in the first batch, under load from
  the preceding lanes.
  - The failing tests were the new
    `PhysicalOwnershipTests.ExceptionAfterTimeout_PreservesTerminalAndProtectsRemainingAttempt(bounded: False)`
    and the existing `TimerEventRaceTests.TimeoutBeforeEvent_FailsAndCancelsWait`.
  - The next 37 runs were all green.
  - The author's 20/20 used the direct xUnit runner, not the `dotnet test`/VSTest path that CI uses.

  Fix: reproduce both under `dotnet test` with concurrent load, then add the missing barriers. Show
  50 consecutive green unfiltered `dotnet test` runs in the final target.

## Carried items

Integrating master resolves the known conflicts: the Task 7.3 row and pin, `production-readiness.md`,
and the test files master stabilized. The guides must describe the shipped state once 5.x evidence
exists. Lean evidence remains sufficient.
