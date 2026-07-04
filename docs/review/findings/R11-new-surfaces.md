# R11 — New surfaces audit (DAG runner, durable saga/yield, definition facade, telemetry) — Findings

> **Scope:** the code added by the 2026-07-04 e2e push (`a3f55dd6…fd82feac`) that no earlier
> R-phase reviewed: `DurableDagRunner`, `DurableWorkflowRuntime` + `DurableDefinitionRegistry`,
> the durable **yield** and **saga** command handlers, and the hosting `OrcaCoreTelemetryObserver`
> / `IWorkflowRuntimeObserver`.
> **Baseline:** build clean under `-warnaserror`; the fixes below verified (Core 264, Durable 184,
> Hosting 10 stressed 8× under 4 concurrent heavy suites).
> **Lenses weighted:** correctness/concurrency (crash-safety, exactly-once), spec conformance
> (CR-017 yield, SG saga, JS-* DAG), API design.

## Findings

### [P1 — latent] `DurableDagRunner.ScheduleReadyAsync` re-schedules in-flight nodes on re-drive — `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableDagRunner.cs:22`
- **Requirement/convention:** JS-* DAG driving scenario crash-safety; CR-040/043 (exactly-once, no duplicate committed work)
- **Evidence:** The runner recomputed the ready set purely from `CompletedNodeIds`/`FailedNodeIds`. `WorkflowDagPlan.GetRunnableNodes` returns any node whose deps are complete and which is not itself completed/failed — so a **scheduled-but-not-yet-terminal** node is returned again. Each call also mints `CommandId.New()` per batch, so `groupId = command.CommandId` differs every call and the `DurableChildWorkflowState.HasActiveChildInGroup(groupId)` dedup guard (`DurableChildWorkflowCommandHandler.cs:94`) can never fire across runner calls; `DeterministicChildId(instanceId, commandId, index)` then produces *different* child ids for the same node.
- **Failure scenario:** a durable driver loop (or crash-restart) reconstructs `completed`/`failed` from child-completion events — in-flight nodes appear in neither set. Node A completes → wave schedules B, C. C completes, B still running → driver reconstructs `completed={A,C}` → `GetRunnableNodes` returns B again → **B is scheduled a second time** as a duplicate child workflow. Same on crash-restart mid-run.
- **Reachability:** latent — no production driver ships yet; every current caller (unit + JS integration tests) drives exactly one call per wave with strictly-growing `completed` sets and never overlaps, so nothing hits it today. The API simply had **no way to express "this node is in-flight."**
- **Fix applied (2026-07-04):** added an optional in-flight exclusion set — `WorkflowDagPlan.GetRunnableNodes(..., scheduledNodeIds)`, `WorkflowDagRunner.GetNextBatches(..., scheduledNodeIds)`, and `DurableDagScheduleRequest.ScheduledNodeIds` (reconstructed from the root's `WorkflowChildrenScheduledEvent` item snapshots minus completed/failed). Supplying it makes re-invocation idempotent. Tests: `DagBuilderTests.GetRunnableNodes_ExcludesScheduledButNotYetTerminalNodes`, `DurableDagRunnerTests.ScheduleReadyAsync_WhenNodesAreMarkedScheduled_DoesNotReDispatchInFlightWork`. The per-command `HasActiveChildInGroup` guard is unchanged (it correctly dedups *command-level* retries).
- **Confidence:** CONFIRMED (traced the guard-key and the runnable-set recomputation).

### [P3] Hosted-service retry test helper regressed the wall-clock guard — `v3-gpt/tests/OrcaCore.Hosting.Tests/OrcaCoreHostingServiceCollectionTests.cs:298`
- **Requirement/convention:** NF-020 / `RepositoryGuardTests.TestSources_DoNotUseWallClockTaskDelay`
- **Evidence:** the prior flake fix (commit `cbe821b5`) used `Task.Delay(25)` as a liveness budget in `AdvanceUntilObservedAsync`, tripping the wall-clock-delay guard (caught here, not before, because that commit re-ran only the Hosting suite, not the Core guard suite).
- **Fix applied:** replaced the fixed 100-iteration + real-delay loop with a `while (!observed.IsCompleted)` loop that yields and advances the fake clock, bounded by the test's own `CancellationToken`. Guard-compliant, deterministic (can't burn out early), verified 8× under 4 concurrent heavy suites. The original flake root cause was the *fixed iteration cap* burning out under saturation, not the absence of a real delay.
- **Confidence:** CONFIRMED.

## Positives verified (no finding)

- **Durable yield (CR-017):** `DurableYieldCommand` emits zero events + a checkpoint at the
  *current* (un-advanced) stream version; the commit no-op guard requires *both* empty events and
  null checkpoint (`DurableCommitPipeline.cs:24`), so the yield checkpoint is materialized and
  persisted (InMemory `AppendAsync` writes the checkpoint regardless of empty events; no stream
  advance). `INT_E2E_015_YieldCrashRecoveryOnPostgreSql` proves resume from the yielded checkpoint
  across a *store restart*. Correct.
- **Durable saga:** `CompleteSagaCompensation` gates the terminal `Compensated` event on
  `AllCompensationsCompleteAfter(scope, action)` — the R4 "terminates on first completed action"
  P1 is genuinely fixed. Idempotency guards present on forward-action (`HasForwardAction`) and
  compensation-request (`HasRequestedCompensation`) handlers; all handlers honor `IsTerminal`.
- **Telemetry / observer:** `DurableCommandProcessor` invokes `IWorkflowRuntimeObserver` **after**
  commit inside `try { … } catch (Exception) { }` (`DurableCommandProcessor.cs:582`), so a throwing
  or cancellation-observing observer cannot turn a committed command into a failure. Default is the
  null-object observer. `OrcaCoreTelemetryObserver` correlates logs to `Activity.Current`
  trace/span ids; metrics/log allocation happens only when an observer is registered.
- **Definition registry:** `Register` is idempotent for an identical (id, version, state-type)
  re-registration and throws `WorkflowDefinitionException` on a conflicting state type;
  `DurableWorkflowRuntime.StartOrGetAsync(definition, …)` re-registering on every call is therefore
  safe.

## Coverage note

Verified: JS-AC-001 (DAG scheduling + the new idempotency primitive), CR-017 (yield commit
boundary + crash recovery), SG compensation completion/idempotency, PR-040 observer isolation,
DU-040 definition version binding. Not reached in this phase: the `OrcaCore.Dashboard` project and
spec-15 `OB-*` observability requirements beyond the command/outbox observer (no dashboard or
metrics-export review yet — recommend a dedicated R12 for observability + dashboard).
