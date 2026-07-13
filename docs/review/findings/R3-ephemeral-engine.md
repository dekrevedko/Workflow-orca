# R3 — Ephemeral Engine — Findings

> Phase scope: `src/OrcaCore.Engine.Ephemeral/**` and matching tests in
> `tests/OrcaCore.Engine.Ephemeral.Tests/**`. Reviewed against CR-010…044, EV (all),
> CP-001…013, MG-001…005/010/060/061. Primary lenses: correctness/concurrency, spec
> conformance. Code review only — no build run in this session (see R0 baseline placeholder).

## Findings

### [P1] Parallel-branch wait matching ignores branch identity — shared correlation breaks second branch — `WorkflowInstance.cs:123`
- **Requirement/convention:** CP-001 / EV-020 / AC-110
- **Evidence:**
  ```csharp
  var wait = activeWaits.FirstOrDefault(candidate => candidate.Matches(envelope));
  // RuntimeWaitRecord.Matches — EventName + CorrelationId only; BranchId not consulted
  ```
- **Failure scenario:** `Parallel` registers two active waits on the same `(EventName, CorrelationId)` in branches `0:a` and `1:b` (see `ParallelTests.Run_ParallelBranchWaits_RecordDistinctBranchIds`). The first `RaiseEventAsync` matches `FirstOrDefault` (branch `a` only). A second event with the same correlation is then dropped by `HasConsumedWait` (below) because consumption is keyed without branch — branch `b` never resumes and the join stalls or the instance hangs in `Waiting`.
- **Recommendation:** Include `BranchId` in `Matches` when the envelope is instance-targeted (or require disambiguation metadata on instance-targeted delivery). Key `consumedWaits`/`HasConsumedWait` with branch identity so one branch consuming a correlation does not block siblings.
- **Confidence:** CONFIRMED (traced `RaiseEventAsync` → `ResumeWaitAsync` → `HasConsumedWait`)

### [P1] `HasConsumedWait` is branch-blind — blocks legitimate cross-branch delivery — `WorkflowInstance.cs:638`
- **Requirement/convention:** CP-001 / EV-023 / AC-110
- **Evidence:**
  ```csharp
  private bool HasConsumedWait(string eventName, CorrelationId correlationId)
  {
      return consumedWaits.Any(wait =>
          string.Equals(wait.EventName, eventName, StringComparison.Ordinal) &&
          wait.CorrelationId == correlationId);
  }
  ```
  (`consumedWaits` entries *do* carry `BranchId` at line 297, but the lookup ignores it.)
- **Failure scenario:** After branch `a` consumes correlation `C`, any later event for `(EventName, C)` returns `ToSnapshot()` immediately (line 112–115) even when branch `b` still has an `Active` wait on `C`.
- **Recommendation:** Scope `HasConsumedWait` to `(EventName, CorrelationId, BranchId?)` or drop the early-return and rely on active-wait matching with branch-aware rules.
- **Confidence:** CONFIRMED

### [P1] Wait-timeout path never records consumed correlation — stale mailbox can resume a later wait — `WorkflowInstance.cs:204`
- **Requirement/convention:** EV-023 / EV-030 / EV-043
- **Evidence:** `ResumeWaitAsync` adds to `consumedWaits` on success (line 297); `FireWaitTimeoutAsync` removes the wait and runs the continuation but never updates `consumedWaits` or `consumedEventIds`.
- **Failure scenario:** A wait on correlation `C` loses the timer/event race (timeout wins). An event for `C` already buffered in `pendingEvents` (or arriving before the next wait registers) is consumed by `MatchPendingEventAsync` when a later step registers a new wait on the same `C`, producing an extra resume (event path) after the timeout path already advanced — violating exactly-once semantics for that wait point.
- **Recommendation:** On successful timeout fire, record the same `WaitSignature` / `EventId` consumption rules as event resume; reject or discard mailbox entries for that signature before registering the next wait on the same correlation.
- **Confidence:** CONFIRMED (timeout and event paths diverge; mailbox bidirectional matching at line 134–146)

### [P1] Graceful cancel does not signal in-flight steps via `CancellationToken` — `EphemeralWorkflowEngine.cs:339`
- **Requirement/convention:** CR-031 / AC-014 / EV-044
- **Evidence:** `CancelInstanceAsync` calls `instance.Cancel(timeProvider.GetUtcNow())` inside the execution lane. Steps receive only the caller's token passed into `StartAsync`/`RaiseEventAsync`, not a per-instance cancel source. `Cancel` clears waits/timers but never cancels an executing step's token.
- **Failure scenario:** A long-running step started via `StartAsync` continues after `Management.Instance(id).CancelAsync()` until it finishes naturally; cooperative cancellation required by CR-031 is impossible for the caller.
- **Recommendation:** Link each instance activation to a `CancellationTokenSource` cancelled on `Cancel` (not on `Terminate`), and flow that linked token into `ExecuteStepAsync` / `StepContext`.
- **Confidence:** CONFIRMED (no instance-level CTS in engine or interpreter; `TerminalCommandTests.CancelAsync_RunningInstance` only covers a *waiting* instance, not in-flight work — AC-014 untested)

### [P1] `EphemeralTimerService` uses an unsynchronized `List<>` across instances — `EphemeralTimerService.cs:8`
- **Requirement/convention:** CR-040 / NF-010 (thread-safe public engine)
- **Evidence:**
  ```csharp
  private readonly List<ScheduledTimer> scheduledTimers = [];
  // Schedule: scheduledTimers.Add(...)
  // ClaimDueTimers: scheduledTimers.RemoveAll(...)
  ```
  One `EphemeralTimerService` is shared by all instances (`EphemeralWorkflowEngine.cs:79`).
- **Failure scenario:** Thread A runs `FireDueTimersAsync` (mutating the list) while threads B/C complete `StartAsync` on different instances and call `Schedule` from `RegisterDelay`/`RegisterWaitAsync` — undefined behavior / lost timers / `ArgumentOutOfRangeException` on the shared list.
- **Recommendation:** Guard `scheduledTimers` with a lock or replace with `ConcurrentDictionary` + ordered due-time structure; add a concurrent stress test (schedule on N instances while firing).
- **Confidence:** CONFIRMED

### [P2] `FireDueTimersAsync` does not drain yield continuations — `EphemeralWorkflowEngine.cs:275`
- **Requirement/convention:** CR-017 / CR-042
- **Evidence:** `RaiseEventAsync` and `StartAsync` call `DrainYieldContinuationsAsync` after the lane operation; `FireDueTimersAsync` only appends the snapshot from `timer.FireAsync` (lines 292–298).
- **Failure scenario:** A delay/timer resume runs a step that returns `Yield`; the yield continuation is scheduled on the instance but not executed until some later unrelated command, leaving the instance logically mid-step while the timer API reports a terminal/waiting snapshot.
- **Recommendation:** Mirror `RaiseEventAsync`: after each timer fire, call `DrainYieldContinuationsAsync` when the registry holds the instance.
- **Confidence:** CONFIRMED (asymmetric code paths); impact PLAUSIBLE until a yielding step follows a timer (no test today)

### [P2] Correlation routing scans all instances — no correlation index — `EphemeralWorkflowEngine.cs:502`
- **Requirement/convention:** EV-011 / EV-013 / AC-115 (partial)
- **Evidence:** `RaiseEventByCorrelationAsync` uses `instanceRegistry.List().OfType<...>().Where(instance => instance.HasActiveWait(...))` — O(instances × waits) on every delivery.
- **Failure scenario:** Large in-process populations make correlation-targeted routing linear in total instance count; violates “efficient lookup” intent for hot paths.
- **Recommendation:** Maintain `(EventName, CorrelationId) → InstanceId` multi-map updated on wait register/match/cancel (as durable mode will require); keep `Instances(ids)` bulk path as implemented.
- **Confidence:** CONFIRMED (implementation choice); severity P2 for ephemeral single-host scale

### [P2] `Statistics()` exposes only definition/version/status counts — `EphemeralManagement.cs:183`
- **Requirement/convention:** MG-030
- **Evidence:** `Statistics()` groups `List()` snapshots by `(DefinitionId, DefinitionVersion, Status)` only; no active-wait-by-name, stuck, or age aggregates required by MG-030.
- **Failure scenario:** Operators cannot query “how many instances waiting on event X” or “oldest running instance age” through the management surface without full `List()` + client-side aggregation.
- **Recommendation:** Extend `WorkflowStatistics` (or add focused query terminals) for the MG-030 minimum dimensions; back with tests tagged `AC-503` where applicable.
- **Confidence:** CONFIRMED

### [P2] Step-level lifetime tracking not implemented — `Interpreter.cs:791`
- **Requirement/convention:** MG-032
- **Evidence:** Steps record `StepCompleted` / `StepFailed` lifecycle events with timestamps but no per-step start time, heartbeat, expected timeout, or completion outcome snapshot on the query model.
- **Failure scenario:** Management cannot answer “how long has step X been running?” or “what is the expected timeout for the current step?” from engine metadata alone.
- **Recommendation:** Record step execution frames in runtime metadata (start, optional heartbeat, policy timeout, completion) and expose via `GetLifecycleEvents` or a dedicated query field.
- **Confidence:** CONFIRMED

### [P2] Ephemeral AC coverage thin — most behavioral ACs lack `[Trait("AC",...)]` tests — `tests/OrcaCore.Engine.Ephemeral.Tests/**`
- **Requirement/convention:** 03 §2 TDD discipline / AC catalog traceability
- **Evidence:** Only ForEach (`AC-601`…`605`) and saga (`AC-401`…) tests carry AC traits. Core/event/management ACs (e.g. `AC-006`, `AC-110`, `AC-112`, `AC-014`, `AC-516`) are exercised behaviorally in places but not tagged; `AC-110` is not validated for same-correlation parallel waits (only distinct correlations in `ParallelTests.RaiseEventAsync_ParallelBranchWait_ResumesOnlyMatchingBranch`).
- **Failure scenario:** Regression of spec-linked behavior is hard to gate in CI; gaps (cancel in-flight, parallel same-correlation) go unnoticed.
- **Recommendation:** Add AC traits to existing tests where they map 1:1; add missing AC tests called out in this review (especially `AC-110` same-correlation, `AC-014` in-flight cancel).
- **Confidence:** CONFIRMED (file survey)

### [P2] `YieldTests` polls with wall-clock `Task.Delay` — `YieldTests.cs:134`
- **Requirement/convention:** NF-020 / 03 §2
- **Evidence:** `WaitForSingleSnapshotAsync` loops with `await Task.Delay(10, timeout.Token)` waiting for registry state.
- **Failure scenario:** Slow CI machines can flake; violates “no sleeps/timing races” test-quality lens.
- **Recommendation:** Drive synchronization with `TaskCompletionSource` gates (pattern already used elsewhere in `YieldTests`) or inject a controllable scheduler; avoid wall-clock polling.
- **Confidence:** CONFIRMED

### [P3] `WhenFirst` “concurrent completions” test does not exercise concurrency — `WhenFirstTests.cs:16`
- **Requirement/convention:** CP-004 / AC-204
- **Evidence:** `WhenFirst_ConcurrentCompletions_SelectsDeterministicWinner` uses two synchronous branches; `Interpreter.RunWhenFirstAsync` awaits each branch sequentially (lines 418–434), so branch `a` always completes first.
- **Failure scenario:** None today (deterministic by definition order), but the test name/documents a race policy without proving behavior under true concurrent branch completion.
- **Recommendation:** Add a test with two waiting branches and concurrent `RaiseEventAsync` calls to validate winner policy under real races; document winner rule (e.g. lowest branch ordinal on simultaneous completion).
- **Confidence:** CONFIRMED

### [P3] Management fluent surface omits step/saga scopes — `EphemeralManagement.cs:14`
- **Requirement/convention:** MG-001
- **Evidence:** Surface provides `All()`, `ForDefinition`, `Instance(id)` only — no `Instance(id).Step(stepId)` or `.Saga()` selectors from MG-001.
- **Failure scenario:** Callers cannot compose filters at step/saga granularity on ephemeral (may be intentional deferral).
- **Recommendation:** Add scoped selectors or document ephemeral subset explicitly in phase README.
- **Confidence:** CONFIRMED

## Coverage note

### Requirements verified (implementation + tests reviewed)

| Area | IDs | Verdict |
|------|-----|---------|
| Interpreter / control flow | CR-010, CR-011, CR-014, CR-015 (implicit index walk), CR-016 (`AwaitCompletionAsync`), CR-017, CR-020, CR-021, CR-032 | **Mostly met**; cancel token gap (CR-031) |
| Serialized execution | CR-040, CR-041, CR-042, CR-043, CR-044 | **Met** via `InstanceExecutionLane` + join atomics; timer list race (above) |
| Events / waits / mailbox | EV-001, EV-002, EV-010, EV-012, EV-020, EV-021, EV-022, EV-030, EV-031, EV-040, EV-043, EV-044, EV-050, EV-051 | **Mostly met**; branch isolation + timeout consumption gaps |
| Composition | CP-001…005, CP-010…013 | **Mostly met**; CP-001 branch matching defect; `maxConcurrency` honored when items suspend on wait (`ForEachTests`, AC-603) |
| Management | MG-001 (partial), MG-002, MG-004, MG-005, MG-010 (Start on engine), MG-040, MG-041, MG-060, MG-061 | **Mostly met**; MG-030/MG-032 partial |
| Policies | CR-006 retry/timeout | **Met** (`RetryPolicyTests`, `TimeoutPolicyTests`) |

### Acceptance criteria cross-walk (ephemeral-relevant)

| AC | Covered by tests? | Notes |
|----|-------------------|-------|
| AC-001…004, AC-005, AC-009…013 | Yes (interpreter, yield, terminal, management) | Untagged |
| AC-006, AC-007 | Yes (`WaitMatchingTests`, `ParallelTests`, `ExecutionLaneTests`) | Untagged |
| AC-010 | Yes (`MailboxTests`) | Untagged |
| AC-011, AC-012 | Yes (`TerminalCommandTests`) | Untagged |
| AC-014, AC-015 | **Partial** | Cancel/terminate on waiting instances only; no in-flight cancel/terminate |
| AC-101…105, AC-109, AC-111…113 | Yes | Untagged |
| AC-106…108 | Yes (`RoutingTests`) | Untagged |
| AC-110 | **Gap** | Distinct correlations only; same-correlation parallel waits broken |
| AC-112 | Yes (`TimerEventRaceTests`, `Clock`) | Untagged |
| AC-115 | Partial | `Instances(ids)` bulk path tested; correlation scan not |
| AC-201…203, AC-205 | Yes (`ParallelTests`, `WhenFirstTests`) | Untagged |
| AC-204 | Weak | No true concurrent completion race |
| AC-501, AC-503, AC-507, AC-508, AC-509, AC-511, AC-516 | Yes (management/governance/stuck) | Untagged |
| AC-601…605 | Yes, tagged | ForEach |
| AC-114, AC-504…506, AC-512+ | N/A ephemeral / provider | Correctly out of scope |

### In-scope code areas reviewed (complete for R3)

- `EphemeralWorkflowEngine.cs` — start, events, timers, saga entrypoints, yield drain
- `Execution/` — `Interpreter`, `WorkflowInstance`, lane, registry, wait/timer records
- `Management/EphemeralManagement.cs` — queries, commands, predicates
- `Governance/ResourceGovernanceCoordinator.cs`
- `Timers/EphemeralTimerService.cs`
- All 22 test files under `OrcaCore.Engine.Ephemeral.Tests`

### Not reached / deferred

- **CR-022 instance epoch:** No epoch/version field on ephemeral instances (likely cross-cutting with R1 abstractions); not re-litigated here beyond snapshot inspection.
- **EV-060 `Publish` outbound events:** No publish primitive in ephemeral interpreter scope (not observed in this project).
- **EV-041 `WaitLong`:** Correctly absent; builder rejection is R2 scope.
- **Saga ephemeral mode (`StartSagaAsync`):** Code present; SG requirements are R6 — skimmed only via `EphemeralSagaTests`.
- **MG-050+ eviction:** Not implemented in ephemeral engine (durable concern); correctly N/A.

**Stop boundary:** Full R3 scope completed in one session; no sub-area truncation.
