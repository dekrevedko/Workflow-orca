# Issue #1 task 3.1 ownership test-first slice — independent review verdict

**Date:** 2026-10-17
**Reviewer:** independent review
**Request:** `artifacts/task-3-1-ownership-independent-review-request-2026-10-17.md` (not modified by
this review)
**Target:** branch `issue/1-ephemeral-retention`, base `ef88957af4c41bf18def689ba81c71e1a1d18696`. The
target is the 14 entries named by `task-3-1-ownership-dirty-manifest-2026-10-17.txt`: 9 tracked
modifications and 5 untracked files.

## Verdict: APPROVE, as the frozen red test baseline for task 3.2

I approve the test-first design, its red evidence and its minimal scaffolding:
- the ownership tests;
- the internal three-value carrier and the `private`→`internal` constructor change;
- the guard and inventory prerequisites.

There are no P1 or P2 findings. Four P3 notes govern task 3.2 (section 5).

### What this approval authorizes, and what it does not

1. **Do not commit this red state.** CI's "Unit tests with coverage" step runs
   `OrcaCore.Engine.Ephemeral.Tests` **unfiltered**. On this target that is 52 failed and 110 passed,
   so a checkpoint commit would make the branch CI red. Task 3.3 already names the ownership-slice
   checkpoint as the point after 3.2 implementation. This approval is that slice's intermediate TDD
   gate, not a separate phase checkpoint. That is why it does not conflict with CLAUDE.md's rule
   against accumulating approved phases.
2. **Task 3.2 may now begin** in this worktree, on top of these exact bytes. This verdict stays an
   untracked file beside the request. Include it as evidence in the 3.2/3.3 target.
3. **The 3.3 review target must preserve this approval's provenance:**
   - Present the diff from the approved red tree `aed8ba3fea6a66e59f0a1cc92e90291385ac5333` to its own
     target.
   - Enumerate every change to `PhysicalOwnershipTests.cs` beyond flipping `Disposition` from
     `ExpectedRed` to the green disposition, and justify each one as strengthening or determinism
     (P3-1). Assertions must not be weakened.
   - All 52 cases and all 22 controls must pass, with unfiltered `OrcaCore.Engine.Ephemeral.Tests`
     green.
   - Keep a byte copy of these 14 files with the author's evidence. The tree object is unreachable
     until a commit references it, so routine garbage collection can eventually prune it.
4. **Not authorized:**
   - ticking 3.1;
   - any change to source approval;
   - policy execution or public API, which stay in 4.2;
   - executable API, compile or package baselines beyond the single recorded internal-source digest;
   - integrating `master`, a push or a pull request;
   - issue changes;
   - issue #2 or #3 work.

## 1. Freeze anchors

| Anchor | Value | Result |
|---|---|---|
| HEAD / index | `ef88957…`; nothing staged | Unchanged at start and end of review |
| Raw porcelain (`core.quotepath=false`, `--untracked-files=all`, ordinal, LF + final LF) | 14 lines, 1,167 B, `ea4b1b603430909594c9d09f0306a8e21f290a5b7adb750f3f3e626c0f738a5c` | Matches the self-inclusive manifest |
| Content record (`status\tpath\tlength\tsha256`, LF + final LF) | 14 rows, 2,158 B, `7dcc22e7488e12f647ec948475da8825058d21665789844d65165944da2a2aa6` | Identical at start and end of review |
| Candidate tree, staged in a disposable copy at the base | `aed8ba3fea6a66e59f0a1cc92e90291385ac5333`; `git diff --cached --check` clean | Matches the author's report |

`origin/master` is still `1c86c23`. No `ocv` worktrees from earlier rounds remain registered.

## 2. Scope review

**Product source.** Two files change, both minimal and internal:
- `EphemeralWorkflowEngineOptions.cs` adds the internal nullable property `TerminalRetention` and the
  internal record `EphemeralTerminalRetentionSettings(int, TimeSpan, TimeSpan)`.
- `EphemeralWorkflowEngine.cs` changes only the five-argument constructor, from `private` to
  `internal`.

There is no behavior, public member or friend change. The user's documented choice to include the
minimal internal carrier covers this scaffolding, which lets both profiles exist before the public
policy lands in 4.2.

**DAG tripwire (`DagStreamIdentitySliceGuards`).** `src/` may now contain only those two files, each
exactly equal to a fixed transformation of base. DAG-delta and PublicApi edits stay forbidden. The
canonical-append check now resolves the delta through the archive-aware `ResolveChangeRecord`, made
`internal` for this. That closes transition-verdict P3-5, the active-path coupling. I verified the
code change but did not re-run the author's archive-move probe.

**Accounting.** The changes are exact and narrow:
- `section-07-r-declaration-crosswalk.json` adds one active source with 11 declarations. Physical
  counts go from 344/1,424 to 345/1,435, and active counts from 196/736 to 197/747.
- `RecoveryCrosswalkGuards` updates only the two active totals.
- Reshape task 7.20 is a `[x]` maintained-inventory note. It gains only its established
  "and the … slice at" attribution and the new totals; the checkbox is unchanged.
- `TaskAccountingGuards` updates the matching exact phrase.

**Package source.** Only the `OrcaCore.Engine.Ephemeral` source digest changes, which records the
actual internal edit. The other 11 entries and all PublicApi and compile fixtures are unchanged.

## 3. Test design review (`Ownership/PhysicalOwnershipTests.cs`, 11 theories, 74 cases)

The harness runs the real engine through its full constructor, along with the real facade registry,
definitions, starts, handles, commands and router. `RemovalObservation` delegates all storage to the
real `InMemoryInstanceRegistry` and signals only after a successful `Remove`. Time and timeouts come
from the fake clock. Barriers cover these points:
- entry, retry entry and cancellation observation;
- the governance wait;
- a blocked commit observer.

Every physical body is released and awaited in `finally`. The 10-second limits are deadlock guards
only.

| Task 3.1 obligation | Coverage | Status |
|---|---|---|
| Start-token cancellation during the first physical step after `Init` | `CancelledStartAfterInit…`, both profiles | Red; checks the exact caller token, no binding or handle, and fresh-ID replay |
| Exception escaping after `Init` before acceptance | `ExceptionAfterInit…`, both profiles × {active, prior `Completed`} | Red; checks exception identity and that the prior terminal status is preserved |
| Root, branch and item × no capacity, throttle or pool × single or bulk removal | `TimedOutRootBranchOrItem…`, 36 cases | Red; terminality is not settlement, and late completion cannot overwrite the fence |
| Pending commit callback before acceptance | `PendingTerminalCommitCallback…`, 4 cases | Red |
| Retries | `RetryWinner…`; `AbortedStartWithUnsettledPhysicalBody…` | Red |
| Exception after timeout with a physical attempt outstanding | `ExceptionAfterTimeout…` | Red |
| Acceptance wins over a later start-token cancellation | Control, wait or one-day delay, with replay | Green |
| Synchronous completion and replay | Control | Green |
| Accepted cancellation or termination protects the body until it returns | Control, 12 cases | Green |
| A timed-out attempt holds its physical slot | Control, throttle or pool | Green, and sensitive: my probe T1 released the slot at timeout and turned all 4 cases red |

I checked the control expectations against the future 4.2 bounded profile: count 1, age 1 minute,
and a 30-second fake-clock advance. They stay valid. The slot control deliberately reads the runtime
object rather than a handle that count pressure might retire.

## 4. Validation reproduced independently

I used a fresh short-path disposable detached worktree, `E:/ocv198`, at `ef88957`, holding a byte copy
of the 14 entries. Its content record reproduced as `7dcc22e7…`.

| Check | Result |
|---|---|
| `openspec validate --all --strict` | 22 passed, 0 failed |
| `dotnet build OrcaCore.slnx -c Release -warnaserror` | 0 warnings, 0 errors |
| Exact package feed, then the full `Disposition=Infrastructure` lane | **247 / 247 passed** |
| `OrcaCore.Engine.Ephemeral.Tests`, `Disposition!=ExpectedRed`, three consecutive runs | 110 / 110 each time |
| `Slice=Issue1Task3.1&Disposition=ExpectedRed`, two runs | 52 failed / 0 passed each time |
| Same 52 cases, per case | First-failure messages identical on both runs and equal to the author's TSV for all 52 cases. Every one is a behavior assertion: no timeout, setup or exception-type failure |
| Unfiltered owning project | 52 failed / 110 passed, which is why this state cannot be committed |

### Probes

Each probe was applied in the copy, then restored and rebuilt. The final guards were 7/7 and
non-red 110/110, and the content record returned to `7dcc22e7…`.

| Probe | Mutation | Result |
|---|---|---|
| G1 | extra member on the carrier record | red (DAG source tripwire) |
| G2 | constructor made `public` | red |
| G3 | engine body drift elsewhere | red |
| G4 | unrelated `src/` edit (timer service) | red |
| G5 | crosswalk active-declaration count reverted | red (recovery crosswalk) |
| T1 | governance lease releases at logical timeout | slot control red, 4/4 |

No commit, stage or ref was created in the reviewed repository.

## 5. P3 notes for task 3.2 (non-blocking; the 3.3 review will check them)

- **P3-1 — Post-return assertions have no settlement barrier.** These assertions run immediately after
  the test's own `await`, with no barrier for settlement or eager release:
  - in the 36 timeout cases, `TimedOutRootBranchOrItem…` lines 130–134 await `body.PhysicalTask` and
    then expect removal to succeed;
  - `CancelledStartAfterInit…` line 43 and `ExceptionAfterInit…` line 89 expect the unit to be
    released as soon as the start returns.

  They pass reliably only if 3.2 records settlement and eager release synchronously before those
  continuations resume. Other valid designs would be flaky, for example lane-posted settlement or
  release after the error surfaces. `AbortedStartWithUnsettledPhysicalBody…` and
  `ExceptionAfterTimeout…` already await `RemovalObservation`. In 3.2, do one of two things:
  - add an internal settlement or release observation barrier and await it in these assertions;
  - or record the synchronous-settlement requirement explicitly as a design decision.

  Either change must be listed in the 3.3 diff.
- **P3-2 — Callback and continuation coverage is narrow.** "Pending callback" is exercised only
  through a blocked `InstanceCommitted` observer. MG-055 also names continuations and callbacks. Task
  3.2's own "prevent late callback/save resurrection" tests should add removal-refusal cases for:
  - a terminal unit with a pending retry-backoff continuation (`AwaitRetryAndResumeAsync`);
  - a governance-grant continuation (`AwaitResourceGrantAndResumeAsync`);
  - a due-timer dispatch.
- **P3-3 — The DAG tripwire exemption cannot scale.** Exact-transformation exemptions cannot express
  3.2's real engine changes. Replace this growing exemption with a properly scoped rule, for example
  restricting the DAG no-source tripwire to DAG-owned paths. Do it as an explicit, separately
  justified part of the 3.2/3.3 target, not a silent widening.
- **P3-4 — The removal probes depend on the old helpers.** The tests use internal `EvictInstance` and
  `EvictTerminalInstances` as removal probes, so 3.2 must make them settlement-aware. If 4.2 later
  routes or isolates those helpers, migrate these tests to the coordinator's retirement path in the
  same slice, with an equal or stronger oracle.

The seven transition-verdict P3s carry forward as the evidence record lists, and P3-5 is now
resolved. Its reminder about abort-fence telemetry still applies to 3.2.

## 6. Hygiene

The target worktree was only read, apart from writing this file. This file sits outside the frozen
14-path manifest. All probes ran in `E:/ocv198`, and every script asserted that location.
