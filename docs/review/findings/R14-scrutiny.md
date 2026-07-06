# R14 — Fresh scrutiny pass (2026-07-05)

> **Remediation status (same day):** all code findings below are **fixed** in the working tree.
> The fix pass surfaced one deeper fact the original finding understated: PostgreSQL and SQL
> Server checkpoints persisted **no `RuntimeState` at all** (no column — waits, timers, children,
> tickets, and jobs were also lost across checkpoint rehydration on SQL providers, not just saga
> state; only InMemory honored the contract). Fixes applied:
>
> - **P1-1**: `WorkflowRuntimeCheckpointState` extended with saga records + resume-token sets;
>   aggregate checkpoint/rehydrate/mapper wired; continue-as-new checkpoints carry saga state
>   (matching replay semantics); PG migration `005_checkpoint_runtime_state.sql` (jsonb) and
>   SQL Server `006_checkpoint_runtime_state.sql` (varbinary) add persistence; old rows load as
>   Empty. Guarded by 4 regression tests (`DurableCheckpointStateSurvivalTests`) and a new
>   certification test (`CheckpointUpsert_RoundTripsFullRuntimeState`) that gates every provider.
> - **P1-2**: `EphemeralManagement.Evict(instanceId)` / `EvictTerminal()` remove terminal
>   instances and saga runtime state from process memory (named *Evict*, not *Purge* — the
>   `EphemeralManagement_DoesNotExposePauseResumeRetryHistoryArchivePurge` pin reserves durable
>   retention vocabulary). Active instances refuse eviction.
> - **P2-1**: post-commit ticket-release failures no longer surface as command failures
>   (lease-expiry sweep is the recovery path); rollback-path semantics unchanged.
> - **P2-2**: the Kubernetes sample uses a stable `DefinitionId`, documents the external-job
>   pattern, and explains why its DAG loop is hand-rolled.
> - **P3**: resume replay now reports `Waiting` while timers are pending; all repository-guard
>   scans exclude `obj`/`bin`; `StepDuration` semantics documented on the observation record.
>
> Not addressed here (feature work): the durable definition driver — now specified as
> [spec 16 (`DR-`)](../../specs/16-requirements-durable-driver.md), which defines the
> engine-agnostic durable interpreter plus the default in-process lane host as the primary
> durable engine (the Orleans package re-hosts the same interpreter) — and the RMQ consumer
> bridge and cron scheduling surface.

Scope: `v3-gpt/` working tree on `feature/v3-rebuild`, one day after the R11–R13 post-cycle
audits. Method: independent re-read of the durable kernel (command processor, aggregate,
checkpoint/rehydrate, commit pipeline, outbox, timers), the ephemeral engine, the concurrency
lane, the messaging providers, and the **uncommitted** dashboard/Kubernetes sample work; plus
behavioral verification of one suspected defect via throwaway repro tests (reproduced, then
removed — code embedded below). Gap analysis is against the owner's stated product goals
(ephemeral speed, durable "never lose an event", DAG, WorkflowCore replacement, ETL/k8s/RMQ
integration, resource limits, cron timers).

**Concurrent-edit caveat:** the tree was being modified by another session during this audit
(`ObservabilityIntegrationTests.cs` changed at 14:29 local, removing a wall-clock `Task.Delay`
that failed `RepositoryGuardTests.TestSources_DoNotUseWallClockTaskDelay` in this pass's first
run; the guard passes after that edit). Baseline numbers below are from the state at audit time.

## 1. Verified baseline

| Check | Result |
|---|---|
| `dotnet build OrcaCore.slnx -warnaserror` | ✅ 0 warnings / 0 errors |
| Core.Tests | 263/264 → **264/264 after the concurrent fix above** (single failure was the wall-clock guard) |
| Engine.Ephemeral.Tests | ✅ 130/130 |
| Engine.Durable.Tests | ✅ 183/183 |
| Hosting.Tests | ✅ 10/10 |
| Acceptance.Tests | ✅ 71/71 |
| ProviderCertification | ✅ 48/48 |
| PostgreSQL + Integration suites | run against real containers this pass (see session log) |
| R12 P1 №1 (`orca.instances.active` process-static) | **Fixed** — `OrcaCoreTelemetryInstruments.UpdateFleetGauges` is projection/statistics-backed observable gauges |
| R12 P1 №2 (`AddOrcaCoreOpenTelemetry` missing) | **Fixed** — exists in Hosting and the dashboard consumes it |

## 2. New findings, ranked

### P1-1 (correctness, durable saga): checkpoint schema drops saga state and parent-resume-token dedup — **reproduced**

`DurableAggregateCheckpoint` / `WorkflowRuntimeCheckpointState` capture timers, waits,
buffered deliveries, children, groups, resource tickets, and external jobs — but **not**
`DurableSagaState` (completed forward actions, compensation actions, recovery interventions,
requested scopes) and **not** `DurableChildWorkflowState`'s
`RecordedParentResumeTokens`/`ConsumedParentResumeTokens`
([DurableWorkflowAggregate.cs:497](../../v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs),
`Rehydrate` passes `[]` for all six at
[DurableWorkflowAggregate.cs:212](../../v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs)).

Checkpoints are written on **every** `DurableStepCompletedCommand` at `StreamVersion.Next()`
([DurableLifecycleCommandHandler.cs:43](../../v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableLifecycleCommandHandler.cs)),
and `DurableAggregateLoader.LoadAsync` replays only the tail **after** the checkpoint version.
Because `DurableCommandProcessor.ProcessCoreAsync` reloads the aggregate per command, the loss
does **not** require a restart or eviction — it happens mid-flight in a single process the
moment any step checkpoint lands after a saga event.

Reproduced with in-memory provider (both asserts fail on current code; control without the
interleaved checkpoint passes):

1. Start → `RecordSagaForwardActionCompleted(scope-1, reserve-stock, release-stock)`
   → `DurableStepCompleted` (checkpoint) → `RequestSagaCompensation(scope-1)`
   ⇒ **no `SagaCompensationStartedEvent` is planned** — the completed forward action is
   invisible, compensation silently does nothing, yet the scope is marked
   compensation-requested.
2. Same prefix, then redeliver the same `RecordSagaForwardActionCompleted`
   ⇒ **duplicate `SagaForwardActionCompletedEvent`** (dedup in
   `DurableSagaCommandHandler.Handle` consults the empty rehydrated state).

The same mechanism plausibly breaks `ConsumeParentResumeTokenCommand` dedup (token sets lost
across a checkpoint) — not separately reproduced, same root cause.

This directly violates the product goal "never lose track of execution". It defeats the point
of durable sagas: the failure mode is silent (Committed outcome, empty compensation plan).

**Fix direction:** add the six missing collections to the checkpoint schema (provider
`CheckpointWrite.RuntimeState` + `DurableCheckpointMapper` + `Rehydrate`), with provider
migrations; or exclude saga-bearing instances from checkpoint fast-path (load full stream when
saga events exist); or keep a saga-state hash in the checkpoint and fail loudly on mismatch.
Schema extension is the honest fix. Add certification tests: every provider must round-trip
saga state through checkpoint+tail rehydration; add EDGE scenarios for "saga action …
checkpoint … compensate" and "redelivery after checkpoint".

Repro used (was `tests/OrcaCore.Engine.Durable.Tests/Recovery/AuditR14ReproTests.cs`, removed
after confirming; core of it):

```csharp
await processor.ProcessAsync(StartCommand(instanceId), ct);
await processor.ProcessAsync(new RecordSagaForwardActionCompletedCommand {
    CommandId = CommandIdValue(2), InstanceId = instanceId, RequestedAt = Timestamp(2),
    ScopeId = "scope-1", ActionKey = "reserve-stock", CompensationKey = "release-stock" }, ct);
await processor.ProcessAsync(new DurableStepCompletedCommand(
    CommandIdValue(3), instanceId, Timestamp(3), "root/1",
    "application/octet-stream", [1]), ct);            // <- checkpoint truncates saga memory
await processor.ProcessAsync(new RequestSagaCompensationCommand {
    CommandId = CommandIdValue(4), InstanceId = instanceId, RequestedAt = Timestamp(4),
    ScopeId = "scope-1", Reason = "business failure" }, ct);
// events.OfType<SagaCompensationStartedEvent>() is EMPTY — should contain release-stock
```

### P1-2 (resource leak, ephemeral): completed instances and saga runtimes are retained forever

`InMemoryInstanceRegistry` has `Save/TryGet/GetMany/List` and **no removal path**;
`EphemeralWorkflowEngine.sagaRuntimeStates` only ever adds. Completed/failed/terminated
instances (including their state objects, lifecycle event lists, pending-event buffers) stay
reachable for process lifetime. `EphemeralManagement` exposes no purge either. The docs pin
"queryable while process-owned runtime state exists" as a feature, but there is no bound and
no opt-out. For the stated use case — a long-lived service running many fast workflows — this
is unbounded memory growth, and `RaiseEventByDefinitionAsync`'s full `List()` scan also
degrades linearly with the garbage. Needs a retention policy (evict on terminal + optional
keep-last-N/TTL for queryability) or at minimum a `Purge` management API. WorkflowCore
parity note: WC has persistence-backed cleanup; an in-memory engine needs its own answer.

### P2-1 (kernel, commit path): post-commit release failure masks a successful commit

`DurableCommitPipeline.CommitAsync` → `ReleaseCommittedTicketsAsync` retries 3× then
**throws after the events were already appended**
([DurableResourcePoolCommitEffects.cs](../../v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableResourcePoolCommitEffects.cs)).
The caller observes an exception for a command that durably committed; a retrying caller then
gets `Conflict`. The orphaned ticket is bounded by lease expiry + operational sweep — but only
when the ticket carries `ExpiresAt`; a null-expiry ticket orphaned this way (or by a crash
between pool-store acquire and append) is held until force-release. Suggest: swallow-and-log
release failures post-commit (sweep is the recovery mechanism anyway) and require/strongly
default ticket expiries.

### P2-2 (uncommitted sample): Kubernetes sample teaches unstable definition identity and bypasses the DAG machinery

[KubernetesWorkflowSampleService.cs](../../v3-gpt/samples/OrcaCore.Dashboard/Workflows/KubernetesWorkflowSampleService.cs)
(uncommitted): (a) `static readonly Definition = … .Build(DefinitionId.New(), …)` — a fresh
random `DefinitionId` every process start, so durable instances persisted by previous runs
reference definitions that no longer exist; start-idempotency records span restarts while the
definition id does not. Samples must model stable, deterministic definition ids. (b) the
"DAG" mode hand-rolls readiness/fan-out with `Task.WhenAll` in the sample instead of using
`WorkflowDagBuilder`/`DurableDagRunner` — the flagship k8s scenario doesn't exercise the
library's own DAG path (see §3 architecture note — symptomatic, not accidental). (c) the
workflow definition is `Init → End` with external jobs attached; no step of the definition
actually runs, which is fine for the external-job pattern but worth an explicit comment so
readers don't conclude that's how DAG workflows are meant to be built.

### P3

- `WorkflowResumedEvent` replay sets `Running` when only timers (no waits) are pending
  (`DurableWorkflowReplayApplier.TryApplyLifecycle` checks `WaitState.HasActiveWaits` only,
  vs. the timer-fired handler which checks both). Status-drift only.
- `DurableCommandProcessor.RecordEventSpans`/`RecordStepSpans` emit zero-duration
  start-and-dispose activities after the fact, and `CreateEventObservations` stamps the whole
  command duration as `StepDuration` for every step event. Telemetry fidelity, not correctness.
- Repository guard scans (`RepositoryGuardTests`) `Directory.EnumerateFiles(..., AllDirectories)`
  include `obj/` — harmless today but can produce phantom hits on generated sources.

## 3. Architecture / goal-fit assessment (no code defect, decision-level)

1. **The durable engine is a kernel without a driver — by design, and the design gap is now
   the critical path.** Nothing in `src/` interprets a `WorkflowDefinition` durably: no
   production component issues `DurableStepCompletedCommand`, wait registrations, or saga
   commands from a definition — only tests and samples do, by hand. `DurableWorkflowRuntime`
   covers register + `StartOrGetAsync` only, and delivery/resume/complete is `internal`
   (acknowledged in [docs/orleans-engine/README.md](../../docs/orleans-engine/README.md) as the
   OT1 seam work). Consequence today: every consumer must hand-roll the execution loop (as the
   k8s sample does), which is exactly what the WorkflowCore-replacement promise says users should not have to do. The
   Orleans-engine plan is the right shape; until it (or a lane-based driver) lands, "durable
   engine" should be described as an event-sourced workflow kernel, not a usable engine.
2. **RabbitMQ integration is outbound-only.** `OrcaCore.Providers.RabbitMq` is an outbox
   dispatcher seam (publish with confirm mapping). There is no consumer/ingestion bridge
   (nothing binds a queue to `RaiseEvent`/`DeliverEventCommand`), so the goal "react on events
   sent by 3rd party via RMQ" is unimplemented; same for MassTransit/Rebus adapters. The
   inbox/dedup machinery on the engine side is ready for it — the missing piece is a hosted
   consumer that maps broker messages to `EventEnvelope` + correlation.
3. **No cron/scheduling surface.** Goal "simple timer scheduled tasks with cron-compatible
   syntax" has no support in `src/` (no cron parser, no recurring-schedule API; the closed
   integration skip covers *host-driven* scheduler starts via `StartOrGetAsync` idempotency,
   i.e. bring-your-own scheduler). Durable timers are one-shot instance timers. Decide:
   in-library recurring schedules (cron string → `ContinueAsNew` loop or scheduler store) vs.
   documented "use k8s CronJob / Quartz + StartOrGetAsync" pattern. Today only the latter
   exists, undocumented as a named pattern.
4. **Cross-host serialization is optimistic-concurrency only** (correct, but worth stating):
   `InstanceLane` serializes per-instance within a process; across hosts the expected-version
   append is the only guard, so multi-host deployments will see `Conflict` outcomes under
   contention that callers must treat as retry signals. Fine for the Orleans plan (single
   activation), fine for single-writer hosts; must be documented for anyone running two plain
   hosts against one store.

Positives worth recording: `InstanceLane` close/drain race is sound (write-after-close retries
onto a fresh lane; post-`TryComplete` drain covers the in-flight window); outbox pump lease +
mark/release semantics are clean at-least-once; timer sweep claim/complete/release handles
conflict outcomes correctly; wait/timer/child/resource state that *is* in the checkpoint
round-trips faithfully; `RunChildren` group dedup is checkpoint-safe (group id = command id,
active children are checkpointed); the R12 observability P1s are genuinely fixed.

## 4. Verdict

The R0–R13 arc holds up — everything those passes claimed fixed was verified fixed here. But
this pass found one silent-data-loss P1 in the durable saga path (reproduced), one unbounded
ephemeral retention P1, and confirmed that three of the owner's headline use cases (durable
definition execution, RMQ ingestion, cron) are not yet implemented surfaces at all. Priority
order: fix P1-1 (small, well-localized schema fix with certification coverage), decide the
ephemeral retention policy, then the Orleans-engine driver work — the RMQ consumer and cron
story both naturally sit on top of that driver.
