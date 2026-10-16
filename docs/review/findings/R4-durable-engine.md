# R4 — Durable Engine — Findings

> Phase scope: `src/OrcaCore.Engine.Durable/**` and matching tests in
> `tests/OrcaCore.Engine.Durable.Tests/**`. Reviewed against DU (all), EV under
> durability, MG-011…013/030…032/062…064. Primary lenses: correctness/concurrency, spec
> conformance. Code review only. Cross-reference: R3 findings (ephemeral branch-wait bugs)
> apply to the shared wait-matching model in `DurableWorkflowAggregate`.

## Findings

### [P0] `StartOrGet` idempotency is process-local only — restart creates duplicate instances — `DurableStartService.cs:10`
- **Requirement/convention:** DU-053 / AC-311
- **Evidence:**
  ```csharp
  private readonly Dictionary<string, StartedInstance> startedInstances = [];
  // ...
  if (startedInstances.TryGetValue(request.IdempotencyKey, out var existing))
  ```
  No durable write of `(IdempotencyKey → InstanceId)` in `ProviderCommitBatch`.
- **Failure scenario:** Client calls `StartOrGet("order-123", …)`, host crashes after commit. A new process has an empty `startedInstances` map; a retry with the same key mints a new `InstanceId` and appends a second `WorkflowStartedEvent` — duplicate logical work with no dedup.
- **Recommendation:** Persist idempotency keys in the provider (projection or dedicated store) in the same commit as `WorkflowStartedEvent`; on `StartOrGet`, load by key before creating a new instance.
- **Confidence:** CONFIRMED (no durable key path; tests only cover in-process duplicate calls in `DurableVersioningTests`)

### [P1] Early inbound events (no active wait, not paused) are poisoned instead of mailbox-buffered — `DurableWorkflowAggregate.cs:1204`
- **Requirement/convention:** EV-030 / AC-104 / DU-030
- **Evidence:** `DecideDeliverEvent` returns `DurableDecision.Empty` when `FindActiveWait` is null (lines 1204–1207). `DurableCommandProcessor.ProcessCoreAsync` maps empty inbox deliveries to `InboxRecordState.Poisoned` (lines 433–442). Buffering exists only when `Status == Paused` (lines 1187–1201).
- **Failure scenario:** Event arrives while instance is `Running` (or `Waiting` on a different correlation) before `WorkflowWaitRegisteredEvent` commits. Delivery is permanently poisoned; when the wait later registers, the event cannot be consumed — spec requires bidirectional mailbox matching.
- **Recommendation:** Emit a durable buffer fact (e.g. extend `WorkflowDeliveryBufferedEvent` or add `WorkflowDeliveryMailboxBufferedEvent`) when no active wait matches and status is non-terminal/non-paused; replay on wait registration like ephemeral `MatchPendingEventAsync`.
- **Confidence:** CONFIRMED (`DurableInboxTests.PoisonedDelivery` codifies poison-on-no-wait; no AC-104 test)

### [P1] Resource-pool acquire runs before durable commit — crash leaves ghost tickets — `DurableCommandProcessor.cs:114`
- **Requirement/convention:** DU-020 / MG-062 / MG-063
- **Evidence:**
  ```csharp
  var acquireResult = await RequiredResourcePoolStore().AcquireAsync(...);
  return aggregate.DecideResourcePoolAcquire(command, acquireResult);
  // append only after decide returns
  ```
  Same pattern for `RunExternalJobCommand` (lines 141–150).
- **Failure scenario:** `AcquireAsync` grants a ticket, `AppendAsync` fails (conflict/crash). Pool capacity is consumed with no matching `WorkflowResourcePoolAcquiredEvent` in the stream — capacity leak violating “capacity NEVER exceeded” intent across failure modes.
- **Recommendation:** Two-phase pattern: decide queued/granted as events first, commit, then have provider-side pool grant driven from committed facts (or participate in the same transactional boundary as `AppendAsync`).
- **Confidence:** CONFIRMED

### [P1] Resource-pool release runs after commit — partial failure desyncs pool from stream — `DurableCommandProcessor.cs:483`
- **Requirement/convention:** DU-020 / MG-062 / AC-520
- **Evidence:** `ReleaseResourcePoolTicketsAsync` is invoked only after successful `AppendAsync` (line 483), outside the provider commit batch.
- **Failure scenario:** `WorkflowResourcePoolReleasedEvent` commits, but `ReleaseAsync` throws or process dies before release completes. Stream says tickets released; pool still holds them — future grants blocked until operator intervention.
- **Recommendation:** Make release part of the provider commit transaction, or drive release from an outbox consumer idempotently from committed release events.
- **Confidence:** CONFIRMED

### [P1] Paused timer firings are not replayed on resume — timer silently dropped — `DurableWorkflowAggregate.cs:1244`
- **Requirement/convention:** MG-013 / AC-513
- **Evidence:** `DecideTimerFired` when paused emits `WorkflowTimerBufferedEvent` and `Apply` removes the timer from `activeTimers` (lines 1687–1689). `DecideResume` replays only `bufferedDeliveries` (events), not buffered timers (lines 1267–1305). No `WorkflowTimerFiredEvent` is produced on resume for buffered timers.
- **Failure scenario:** Instance paused with scheduled timeout; timer fires → buffered. Operator resumes with `Replay` — timeout never fires; instance stuck past expected expiry without timeout policy outcome.
- **Recommendation:** Track buffered timer firings separately (or keep timer active while paused) and emit `WorkflowTimerFiredEvent` during resume replay in order with buffered deliveries.
- **Confidence:** CONFIRMED (`DurableTimerAggregateTests.FireTimer_WhenPaused_BuffersWithoutAdvancing` stops at buffer; no resume replay test)

### [P1] `CompleteSagaCompensation` terminates the saga on the first completed action — `DurableWorkflowAggregate.cs:986`
- **Requirement/convention:** SG saga compensation sequencing (multi-step plan from `CreateSagaCompensationPlanEvents`)
- **Evidence:** `DecideRequestSagaCompensation` can emit multiple `SagaCompensationStartedEvent` records (lines 966–981). `DecideCompleteSagaCompensation` always appends `WorkflowTerminalEvent` with `Compensated` (lines 1007–1017) after a single `SagaCompensationCompletedEvent`.
- **Failure scenario:** Scope has forward actions A→B→C with compensations; compensation plan starts `release-B` and `release-A`. Completing `release-B` commits terminal `Compensated`; `release-A` never runs — partial compensation with false “success” terminal.
- **Recommendation:** Terminal only when all `SagaCompensationStartedEvent` actions for the scope are `Completed` (or failed terminal per policy); track per-action completion in aggregate state.
- **Confidence:** CONFIRMED (unit tests cover single-action completion only in `CompensationFailureTests`)

### [P1] Wait matching ignores branch identity (shared with R3) — `DurableWorkflowAggregate.cs:1993`
- **Requirement/convention:** CP-001 / EV-020 / AC-110
- **Evidence:**
  ```csharp
  return activeWaits.FirstOrDefault(wait =>
      wait.EventName == envelope.EventName &&
      wait.CorrelationId == envelope.CorrelationId);
  ```
  `DurableActiveWait` has no `BranchId`; `DecideResume` replay uses the same correlation-only match (lines 1284–1286).
- **Failure scenario:** Parallel durable waits on the same `(EventName, CorrelationId)` — first match wins; second branch never resumes; replay after pause can match the wrong wait.
- **Recommendation:** Add branch identity to durable wait facts and include it in matching (align with R3 fix).
- **Confidence:** CONFIRMED

### [P2] Checkpoint rehydration discards runtime collections from provider checkpoint — `DurableCommandProcessor.cs:491`
- **Requirement/convention:** DU-010 / DU-013
- **Evidence:** `ToAggregateCheckpoint` maps `CheckpointWrite` to `DurableAggregateCheckpoint` with empty `ActiveTimers`, `ActiveWaits`, `BufferedDeliveries`, `ActiveChildren`, etc. (lines 507–512).
- **Failure scenario:** Correct only if tail replay always contains every fact after checkpoint version. A provider that compacts tail without embedding wait/timer state in checkpoint materialization would lose active waits on load.
- **Recommendation:** Either persist runtime collections in checkpoint payload or document/enforce provider invariant “tail after checkpoint is authoritative for active waits”; add certification test for checkpoint+tail with active wait and empty tail slice.
- **Confidence:** CONFIRMED (by design); failure mode PLAUSIBLE if provider mis-implements compaction

### [P2] Durable management surface is query-only — operator commands not exposed — `DurableManagementQuery.cs:10`
- **Requirement/convention:** MG-001 / MG-010 / MG-011
- **Evidence:** `DurableManagementQuery` exposes `ListAsync`, `CountAsync`, `GetActiveWaitsAsync`, `StatisticsAsync` only. Pause/Resume/Cancel/Terminate/Retry/`GetHistory` exist on `DurableCommandProcessor` as `internal` commands but not on `DurableManagement`.
- **Failure scenario:** Host integrators must bypass the documented management fluent API and call processor types directly — durable-only command separation (MG-011) is incomplete at the public boundary.
- **Recommendation:** Add command terminals on `DurableManagement` / instance scope that enqueue the existing commands through the processor.
- **Confidence:** CONFIRMED

### [P2] `DU-071` history inspection not implemented on management surface — `DurableManagement.cs:11`
- **Requirement/convention:** DU-071 / MG-010
- **Evidence:** No `GetHistory` on `DurableManagement` or `DurableManagementQuery` (test `DurableManagementTests` only asserts ephemeral lacks it).
- **Failure scenario:** Operators cannot retrieve per-instance event timeline through the management API contract without direct store access.
- **Recommendation:** Add `GetHistory` terminal reading committed stream (with retention policy awareness).
- **Confidence:** CONFIRMED

### [P2] In-process per-instance lane does not substitute for cross-host serialization — `DurableCommandProcessor.cs:378`
- **Requirement/convention:** DU-022 / DU-060
- **Evidence:** `ConcurrentDictionary<InstanceId, SemaphoreSlim> lanes` serializes within one process; cross-host relies solely on `ExpectedVersion` on append.
- **Failure scenario:** Acceptable if providers enforce OCC (tested in `DurableCommandPipelineTests`). Without provider certification on all plugins, duplicate activations are possible — document as host+provider contract.
- **Recommendation:** Keep lane as optimization; ensure R5 provider certification tests concurrent append conflicts; document DU-060 out-of-scope for multi-node until lease layer lands.
- **Confidence:** CONFIRMED (design); cross-host duplicate activation PLAUSIBLE without provider enforcement

### [P3] `DurableOutboxPump` uses an unbounded channel for already-claimed records — `DurableOutboxPump.cs:15`
- **Requirement/convention:** none — general / NF-030
- **Evidence:** `Channel.CreateUnbounded<OutboxWrite>()` wraps a synchronous `foreach` over `records` — adds allocation without concurrency benefit.
- **Failure scenario:** None functional; minor unnecessary overhead on large claim batches.
- **Recommendation:** Dispatch directly over `records` or use a bounded channel sized to `maxCount`.
- **Confidence:** CONFIRMED

## Coverage note

### Requirements verified (implementation + tests reviewed)

| Area | IDs | Verdict |
|------|-----|---------|
| Event-sourced write path | DU-010, DU-011, DU-012, DU-013, DU-020 (partial) | **Strong** command→decide→append; pool acquire/release gaps |
| Inbox / outbox | DU-030 (partial), DU-031, DU-032, DU-033 | Dedup/crash-before-commit **good**; early-event mailbox **missing** |
| Versioning | DU-040, DU-041, DU-042 | Version binding **good**; `StartOrGet` **not durable** |
| Retention / pressure | DU-050, DU-051, DU-052 | Archive/purge on management; pressure metrics tested |
| Recovery / OCC | DU-021, DU-022, CR-040 | Checkpoint+tail, expected-version conflict, concurrent resume |
| EV durable | EV-030 (partial), EV-031, EV-032, EV-041, EV-051 (pause buffer) | Pause event buffer **good**; general mailbox **gap**; timer replay **gap** |
| MG pause/resume | MG-013, MG-011 (partial) | Aggregate logic **good**; timer replay + public API **gaps** |
| MG pools | MG-062, MG-063, MG-064 | Acquire-as-wait, release on terminal, expiry ops tested |
| Composition / children | CP-022…024 (via aggregate) | Outbox child-start, resume token, throttling tests present |

### Acceptance criteria cross-walk (durable-engine tests)

| AC | Covered? | Notes |
|----|----------|-------|
| AC-301, AC-302, AC-316 | Yes | `DurableRecoveryTests` |
| AC-303, AC-304 | Yes | `DurableWaitTests` |
| AC-305, AC-114 | Yes | `DurableInboxTests` |
| AC-306, AC-307, AC-311 | Partial | AC-311 in-process only; no restart |
| AC-309 | Yes | `DurableCommandPipelineTests` |
| AC-310 | Yes | `DurableOutboxTests` |
| AC-312 | Yes | `DurableLifecycleEventTests` (not fully read; referenced by processor) |
| AC-313 | Yes | `ContinueAsNewAggregateTests` |
| AC-504…506 | Yes | `DurableWaitTests` |
| AC-512…515, AC-517 | Yes | `DurableManagementTests` |
| AC-519…522 | Yes | `PoolAcquisitionTests`, `PoolOperationsTests` |
| AC-601…605, AC-610, AC-611 | Yes | Composition/saga tests (RunChild area — aggregate commands) |
| AC-104 | **Gap** | No durable out-of-order mailbox test |
| AC-110 | **Gap** | Same as R3 branch isolation |
| AC-401…409 | Partial | Saga aggregate tests; multi-step compensation completion gap |

### In-scope code areas reviewed (complete for R4)

- `Aggregates/DurableWorkflowAggregate.cs` — full decide/apply/rehydrate paths
- `Execution/DurableCommandProcessor.cs`, `DurableStartService.cs`
- `Outbox/DurableOutboxPump.cs`
- `Management/` — `DurableManagement`, `DurableManagementQuery`, predicate translator
- `Versioning/DurableVersionCompatibility.cs`
- `Building/DurableWorkflowBuilder.cs` (surface skim)
- All 28 test files under `OrcaCore.Engine.Durable.Tests` (key tests read in full; remainder surveyed via grep/traits)

### Not reached / deferred

- **Full interpreter / activation runtime** — durable engine is command processor + aggregate; step execution loop likely in hosting or another package (out of R4 folder scope).
- **Provider implementations** — OCC, inbox, outbox atomicity certified in R5 (`InMemoryWorkflowProvider` used heavily in tests).
- **RunChild / DAG / external-job orchestration above aggregate** — commands exist in aggregate; end-to-end activation is hosting scope.
- **DU-060 multi-node lease** — explicitly future; not implemented.

### Cross-phase patterns (for R3 remediation)

- Branch-blind wait matching (`FindActiveWait`) affects **both** engines — fix once in matching rules / wait facts.
- Early-event mailbox semantics differ: ephemeral buffers, durable poisons — align when fixing either side.

**Stop boundary:** Full R4 `OrcaCore.Engine.Durable` scope completed in one session.
