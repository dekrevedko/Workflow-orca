# Issue #1 final consumer, integration and PR target — independent review verdict

**Date:** 2026-10-18
**Target:** a pending merge in `issue/1-ephemeral-retention` with these anchors:

| Anchor | Value |
|---|---|
| First parent | `8e10dd1` |
| Second parent | master `1c86c23` (equal to `origin/master` at review) |
| Entries | 36 |
| Candidate tree | `d4ab4a057de82328de532ab460ddf7070a8ba03c` |
| Native porcelain SHA-256 | `fdd9a90b…` |
| Content record SHA-256 | `a6d0b3a1…` (36 rows, 4,880 B) |

Both hashes were identical at the start and end of review.

## Verdict: APPROVE

There are no P1 or P2 findings. Both carried P2s are resolved. One P3 below is follow-up work and does
not block.

**Authorized now:**
1. **Merge checkpoint.** Commit the pending merge as a true two-parent merge commit: first parent
   `8e10dd1`, second parent `1c86c23`, tree `d4ab4a05…`. Do not squash and do not rebase. Exclude the
   `.artifacts/` request, the validation output and this verdict.
2. **Verdict child.** Add a verdict-only direct child commit.
3. **Completion binding.** One mechanical commit, with no further independent review:
   - Tick 5.3.
   - Move both registry records to their completed stage, naming this verdict and the two commits
     above as final-approval evidence.
   - Adjust the validator, schema version and task-accounting phrase to match.
   - Run OpenSpec strict and the Infrastructure lane, and record the results in the commit message.

   If the binding needs anything beyond this bookkeeping (source, test behavior or guide text), it
   returns for review.
4. **PR.** Push `issue/1-ephemeral-retention` and open the PR to master with the prepared description.
   The PR's CI must be green, including the provider lanes and the 75% engine coverage gate. Merge it
   with a merge commit, never a squash or rebase.

Merging the PR and closing issue #1 remain the owner's decision. Task 5.4 (issues #2 and #3) starts
only after that, on fresh branches.

## What I verified

**Chain.**
- `dd5dce3` has parent `0b33d02` and tree `09fd5669…`, the approved Section 4 tree.
- `8e10dd1` is its sole child and adds only the Section 4 verdict, with SHA-256 `112f844a…`.
- Schema 15 checks that chain through git.
- Only 4.5, 5.1 and 5.2 are newly ticked.

**F1, the sweep crash (resolved).**
- `Sweep` now catches failures from `Enforce`, records one bounded `cleanup-failed`/`retention-sweep`
  lifecycle measurement (also inside a catch) and always re-arms unless disposed.
- Synchronous callers still observe cleanup failures, and `Enforce` still runs every selected cleanup
  before rethrowing.
- The new regression test covers the escaping exception, the other selected cleanup, a throwing
  listener and a later idle sweep.
- With only the old coordinator restored, that test fails, so the test is sensitive.
- No public API or new instrument is added.

**F2, the flakes (resolved).**
- The step-timeout callback now calls `Cancel` before it signals `timeoutReached`, and tolerates
  `ObjectDisposedException` when physical completion has already won. That removes the real race in
  which the woken turn disposed the source mid-callback.
- In `ExceptionAfterTimeout…`, the added awaits (cancellation observed, failing commit reached) are
  barriers only. Every original assertion is kept, including the check that the physical task is
  still incomplete.
- `TimerEventRaceTests.WaitForStatusAsync` now subscribes to commits, then rechecks before waiting,
  replacing a 10,000-yield polling loop. Its outcome checks are unchanged.
- The `EphemeralTimerTests` change is master's `c1ef90b`.

**Merge.**
- Master's publishing workflow, scripts and docs come in as staged.
- In the conflicted files, master's text is retained and the retention additions are appended.
- The Task 7.3 table pins the combined bytes, and its guard digest moved with it.
- The guides now link the actual consumer results and state that the published `0.1.0-preview.1`
  packages predate this API.

**Consumer evidence (5.1 and 5.2).**
- The experiment uses public APIs only, and its diagnostic GC is labelled experiment-only.
- I packed a fresh 12-package feed from the reproduced tree, restored into an empty package cache,
  and built the consumer outside the repository (Release, warnings as errors). The restore resolved
  only `OrcaCore`, `OrcaCore.Core` and `OrcaCore.Engine.Ephemeral`.
- My runs matched the record:

| Run | Managed MiB at 512 / 1,024 jobs | Key reuse | Other results |
|---|---|---|---|
| `bounded` | 7.07 / 7.07 | fresh ID, effect repeated | — |
| `default` | 132.36 / 262.14 | same ID, no repeated effect | — |
| `behavior` | — | — | idle age, waiting and physical-work protection, protected replay and post-return release all true; 64 jobs, 66 evictions |

I did not repeat the SOS heap counts.

**Lanes.** I ran these in a disposable copy, `E:/ocv204`. It reproduced tree `d4ab4a05…` as a
simulated two-parent merge, so the ancestry guards ran against a real merge `HEAD`.

| Lane | Result |
|---|---|
| OpenSpec strict | 22/22 |
| Release `-warnaserror` | 0 warnings, 0 errors |
| Exact feed, then Infrastructure | 249/249 |
| CI's six unit projects, CI command and coverage collector | 834/834: 354 + 217 + 102 + 37 + 24 + 100 |
| Ephemeral, unfiltered `dotnet test -c Release` with coverage, run in concurrent pairs; the first eight overlapped the consumer runs | **30/30 green** |

I did not re-run the PostgreSQL, SQL Server or integration lanes. The PR's CI runs them.

## P3 (follow-up issue, non-blocking)

**A throwing step-token callback can still escape the timer.**
- The step's `executionToken` is `timeoutCancellation.Token`, so callbacks a step registers on it run
  inside `Cancel()` on the system timer thread.
- If one of them throws, `Cancel` raises `AggregateException`. The new code catches only
  `ObjectDisposedException`, so that exception escapes the timer callback.
- This behavior predates this branch and is not introduced by it.

Track it as a separate issue: contain it as `Sweep` does and record a diagnostic.

## Hygiene

I only read the target worktree, apart from writing this file outside the 36-path manifest.
The `E:/ocv204` worktree has been removed. Manual cleanup is still needed for the reviewer scratch
folders `E:/ocv204-consumer`, `E:/ocv204-logs` and `E:/ocv204-tr`, which a removal safety check
blocked, and for the author's `E:/oc5-lab`.
