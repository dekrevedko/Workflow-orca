# Full Audit - 2026-07-03

Scope: `v3-gpt/` after addressing the R8 .NET/C# quality findings and the
initial channel-substrate migration. The active
`v3-gpt/tests/OrcaCore.Integration.Tests/` folder was intentionally excluded.

Audit method:
- Read the implementation guidance, especially stack decisions, engineering
  conventions, and TDD workflow.
- Compared the old channel proof of concept under `src/` with the current
  `v3-gpt` execution lanes.
- Ran focused verification for R8 fixes, channel lanes, timeout determinism,
  analyzer output, and non-integration tests.
- Used parallel read-only audit agents for architecture/channel seams,
  security/reliability, and test/analyzer hygiene.

## Verification

Commands run from repository root:

| Check | Result |
| --- | --- |
| Non-integration project analyzer build loop with `-warnaserror`, `RunAnalyzersDuringBuild=true`, `EnforceCodeStyleInBuild=true` | Passed, 0 warnings, 0 errors |
| `OrcaCore.Engine.Ephemeral.Tests` filtered to `ExecutionLaneTests` | Passed, 5 tests |
| `OrcaCore.Engine.Durable.Tests` filtered to `DurableCommandPipelineTests` | Passed, 6 tests |
| `OrcaCore.Engine.Ephemeral.Tests` filtered to `TimeoutPolicyTests` | Passed, 1 test |
| `OrcaCore.Acceptance.Tests` filtered to `PolicyAcceptanceTests` | Passed, 2 tests |
| Non-PostgreSQL non-integration test projects | Passed: Acceptance 71, Core 169, Durable 114, Ephemeral 120, Hosting 6, ProviderCertification 36, RabbitMQ 8, Redis 6, SQL Server 29, ZeroMQ 5 |
| PostgreSQL service-collection test | Passed, 1 test |

Not run to completion:
- Full `v3-gpt/OrcaCore.slnx` test/build with integration tests included,
  because `v3-gpt/tests/OrcaCore.Integration.Tests/` is active development.
- Full PostgreSQL provider Testcontainers suite. A previous all-project test
  loop hung in provider/infrastructure tests before the timeout-test race was
  fixed. The audit recommends tagging container tests and isolating them in CI.

Follow-up implementation verification, 2026-07-03:
- Timer/outbox lease implementation pass ran all non-integration `v3-gpt/tests`
  projects plus `v3-gpt/tests/OrcaCore.Integration.Tests`.
- Analyzer build loop with `-warnaserror` passed for every `v3-gpt` project,
  including integration tests.
- Integration tests are now honored for implementation verification: passed 68,
  skipped 31, failed 0.
- PostgreSQL start-idempotency implementation pass reran the analyzer build
  loop for every `v3-gpt` project with `-warnaserror` and the full
  `v3-gpt/tests` sweep, including integration tests. Integration coverage is now
  passed 70, skipped 29, failed 0 because the PostgreSQL restart and two-host
  `StartOrGet` scenarios are active.
- SQL Server resource-pool implementation pass reran the non-integration
  analyzer build loop with `-warnaserror`, the full SQL Server provider suite,
  and the full `v3-gpt/tests` sweep including integration tests. Integration
  coverage is now passed 73, skipped 26, failed 0.
- SQL Server retention certification pass added table-backed
  `IWorkflowRetentionStore` behavior and reran focused SQL Server, PostgreSQL,
  and in-memory retention certification. Successful archive now has shared
  certification coverage in addition to active-instance and in-flight outbox
  safety checks.
- Outbox observer and interpreter-runner decomposition pass reran focused
  durable outbox tests, focused ephemeral interpreter/control-flow tests, the
  non-integration analyzer build loop with `-warnaserror`, and the full
  `v3-gpt/tests/OrcaCore.Integration.Tests` project. Integration coverage is
  now passed 81, skipped 18, failed 0.
- Yield continuation scheduler pass reran focused yield/wait/timer tests, full
  `OrcaCore.Engine.Ephemeral.Tests`, filtered acceptance yield/wait/timer
  coverage, the non-integration analyzer build loop with `-warnaserror`, the
  full integration project, and all non-integration `v3-gpt/tests` projects.
- Durable saga-state extraction pass added focused `DurableSagaState` tests and
  reran the saga, aggregate, and full durable test slices before broader
  verification.
- Durable child-workflow state extraction pass added focused
  `DurableChildWorkflowState` tests and reran focused child-state and full
  durable test slices. Broader verification passed the non-integration analyzer
  build loop with `-warnaserror`, the full non-integration `v3-gpt/tests`
  sweep, and `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 81, skipped
  18, failed 0.
- Durable resource-pool state extraction pass added focused
  `DurableResourcePoolState` tests and reran focused resource-pool/external-job
  tests plus the full durable test project before broader verification. The
  pass also fixed guarded resource tickets not being released when a durable
  step fails. Broader verification passed the non-integration analyzer build
  loop with `-warnaserror`, the full non-integration `v3-gpt/tests` sweep, and
  `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 81, skipped 18, failed
  0.
- Durable timer-state extraction pass added focused `DurableTimerState` tests
  for active timer tracking, buffered timer replacement, buffered replay
  planning, discard behavior, clearing, and checkpoint projection. Focused
  durable timer aggregate coverage passed before broader verification. Broader
  verification passed the non-integration analyzer build loop with
  `-warnaserror`, the full non-integration `v3-gpt/tests` sweep, and
  `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 83, skipped 16, failed
  0.
- Hosted-service transient-failure boundary pass added shared retry/backoff
  handling with source-generated logging for outbox pump, timer sweep, and
  operational sweep loops. Focused hosting tests cover first-cycle failures and
  subsequent retry cycles for all three hosted services. Broader verification
  passed the non-integration analyzer build loop with `-warnaserror`, the
  non-integration `v3-gpt/tests` sweep after replacing a wall-clock test yield
  flagged by repository guards, and `v3-gpt/tests/OrcaCore.Integration.Tests`
  with passed 83, skipped 16, failed 0.
- Durable checkpoint-mapper extraction pass added focused
  `DurableCheckpointMapper` tests for provider checkpoint runtime-state
  projection and payload copying. Focused mapper coverage, the full durable
  test project, the non-integration analyzer build loop with `-warnaserror`,
  and `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 83, skipped 16,
  failed 0 passed.
- Durable commit-materializer extraction pass added focused
  `DurableCommitMaterializer` tests and reran focused command-pipeline tests
  before broader verification. Broader verification passed the non-integration
  analyzer build loop with `-warnaserror`, the full non-integration
  `v3-gpt/tests` sweep, and `v3-gpt/tests/OrcaCore.Integration.Tests` with
  passed 81, skipped 18, failed 0.
- Durable resource-pool commit-effects extraction pass added focused
  `DurableResourcePoolCommitEffects` tests and reran the existing R4
  rollback/retry resource-pool tests before broader verification. Broader
  verification passed the non-integration analyzer build loop with
  `-warnaserror`, the full non-integration `v3-gpt/tests` sweep, and
  `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 81, skipped 18, failed
  0.
- Durable commit-pipeline extraction pass added focused
  `DurableCommitPipeline` tests for inbox-only poison commits, no-append
  eviction, append conflicts, and resource-pool side effects routed by append
  success or failure. Focused durable execution coverage passed before broader
  verification. Broader verification passed the non-integration analyzer build
  loop with `-warnaserror`, the full non-integration `v3-gpt/tests` sweep, and
  `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 81, skipped 18, failed
  0.
- Ephemeral wait-executor extraction pass added a focused public regression for
  wait correlation selector failure, then fixed a yield-drain snapshot race
  exposed by broad verification by taking initial yield snapshots through the
  per-instance lane. Focused wait/interpreter/loop/timer-race and yield/parallel
  coverage passed before broader verification. Broader verification passed the
  non-integration analyzer build loop with `-warnaserror`, the full
  non-integration `v3-gpt/tests` sweep, and
  `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 81, skipped 18, failed
  0.
- Durable wait-state extraction pass added focused `DurableWaitState` tests and
  reran the durable analyzer build and full durable test project before broader
  verification.
- Durable external-job-state extraction pass added focused
  `DurableExternalJobState` tests before broader analyzer, unit, provider, and
  integration verification.
- Durable inbox-preflight extraction pass added focused
  `DurableInboxPreflight` tests for absent, received, already-resolved, and
  poisoned inbox states. Focused inbox/commit-pipeline coverage, the full
  durable test project, the non-integration analyzer build loop with
  `-warnaserror`, the full non-integration `v3-gpt/tests` sweep, and
  `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 83, skipped 16,
  failed 0 passed.
- Shared instance-lane extraction pass added focused `InstanceLane` tests for
  serialization, cross-instance overlap, exception release, cancellation, and
  idle eviction. Focused core/ephemeral/durable lane coverage, the
  non-integration analyzer build loop with `-warnaserror`, the full
  non-integration `v3-gpt/tests` sweep, and
  `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 83, skipped 16,
  failed 0 passed.
- Workflow event codec hardening pass replaced repeated event-type switches
  with one typed descriptor table and added focused `WorkflowEventCodec` tests
  that round-trip every supported durable event type. Focused codec,
  PostgreSQL event-store, and SQL Server event-store coverage passed before the
  non-integration analyzer build loop with `-warnaserror`, the full
  non-integration `v3-gpt/tests` sweep, and
  `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 83, skipped 16,
  failed 0 passed.
- SQL Server history-projection parity pass added table-backed
  `AppendHistory` projection writes through migration `004_history_projections`
  and purges history rows during retention cleanup. Focused SQL Server
  projection, migration-journal, and retention certification checks passed
  before the non-integration analyzer build loop with `-warnaserror`, the full
  non-integration `v3-gpt/tests` sweep, and
  `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 83, skipped 16,
  failed 0.
- Deterministic concurrency-test probe pass added `AsyncSignalCounter` in test
  support and internal lane/governance observer hooks so tests can prove a
  contender reached the contested Seam before making negative assertions.
  Focused core, ephemeral, and acceptance slices passed before the
  non-integration analyzer build loop with `-warnaserror`, the full
  non-integration `v3-gpt/tests` sweep, and
  `v3-gpt/tests/OrcaCore.Integration.Tests` with passed 83, skipped 16,
  failed 0.

## Completed In This Pass

R8 quality and analyzer remediation:
- PostgreSQL connection-string constructors now validate before constructing
  `NpgsqlDataSource`.
- Strongly typed ID JSON converters are centralized in abstractions.
- Workflow event serialization now uses a shared source-generated STJ context.
- Closed internal switches use `UnreachableException` where appropriate.
- Ephemeral timer tokens use version-7 GUIDs.
- BCL diagnostics were introduced for durable outbox pumping.
- .NET analyzers are explicitly enabled.

Channel-substrate migration:
- `OrcaCore.Core.Concurrency.InstanceLane` owns the bounded
  `System.Threading.Channels` Implementation used by the durable and ephemeral
  engine adapters.
- Per-instance work is serialized by a single-reader channel.
- Idle lanes self-evict after the current batch drains.
- Tests cover same-instance serialization, cross-instance overlap, exception
  release, cancellation before enqueue, and idle-lane eviction.

Test hygiene:
- Timeout policy tests now wait until the never-completing step has registered
  its fake-time delay before advancing `FakeTimeProvider`.
- This removed a deterministic race that left xUnit test executables running
  and locking build outputs.

## Channel PoC Comparison

Old PoC:
- `src/OrcaCore.Runtime/Storage/InstanceCommandLane.cs:7`
- `src/OrcaCore.EventDrivenPrototype/Engine/PrototypeInstanceCommandLane.cs:7`

The PoC used an unbounded channel, `Task.Run(ProcessAsync)`, and a lane that
lived until disposal. That proved the mailbox shape but did not provide
backpressure or lane eviction.

Current `v3-gpt`:
- `v3-gpt/src/OrcaCore.Core/Concurrency/InstanceLane.cs:7`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/InstanceExecutionLane.cs:6`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableInstanceCommandLane.cs:6`

The current Implementation uses bounded channels with `FullMode = Wait`,
`SingleReader = true`, and self-eviction through the shared
`OrcaCore.Core.Concurrency.InstanceLane` Module. The engine-specific lane
classes are now adapters that preserve local engine seams while sharing the
channel behavior.

Refactoring candidate: introduce a shared internal `InstanceLane` Module with
the Interface `RunAsync(instanceId, operation, cancellationToken)`. Keep the
channel-backed Adapter internal. This increases Depth because ordering,
backpressure, cancellation, exception propagation, and eviction sit behind one
small Interface. It improves Locality because future lane bug fixes happen once.

Implementation update:
- 2026-07-03: introduced `OrcaCore.Core.Concurrency.InstanceLane` and routed
  `InstanceExecutionLane` and `DurableInstanceCommandLane` through it. The
  shared Module now owns ordering, backpressure, cancellation propagation,
  exception propagation, and idle eviction.

## P0 Findings

### Durable per-instance serialization is not process-wide

Evidence:
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs:13`
  owns `new DurableInstanceCommandLane()`.
- `v3-gpt/src/OrcaCore.Engine.Durable/Management/DurableManagement.cs:348`
  creates a fresh `DurableCommandProcessor` for management commands.

Risk: pause, resume, cancel, terminate, timer, and outbox-driven durable
commands can bypass each other's lane when they use different processor
instances. That weakens the serialized-execution invariant for one workflow
instance.

Refactoring candidate: `DurableCommandRuntime` Module. Its Interface is
"process one durable command through the instance lane and commit pipeline".
Hosting, timers, management, child workflows, and public durable entrypoints
should depend on this Seam. The Adapter can be the current channel-backed lane
plus processor logic. This adds Leverage because all durable callers share one
ordering point and Locality because correctness fixes land in one place.

Implementation update:
- 2026-07-03: Introduced `DurableCommandRuntime` as the process-owned durable
  lane/runtime Module. `DurableCommandProcessor` is now a compatibility adapter
  over that runtime, and hosting registers one singleton runtime shared by the
  processor, timer hosted service, and `DurableManagement`.
- Added focused tests proving two processors over the same runtime reuse one
  per-instance lane and that management commands can use the injected command
  processor instead of reconstructing one from stores.
- Remaining work: route any future in-process child-command/outbox dispatcher
  through `DurableCommandRuntime`; current child workflow commands are only
  materialized as outbox records.

### SQL Server provider cannot persist most durable events

Evidence:
- `v3-gpt/src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:1194`
  maps only started, continued-as-new, timer scheduled, and timer fired events.
- `SerializeEvent` and `DeserializeEvent` throw for common events such as step
  completion, waits, child workflows, resource pools, external jobs, pause/resume,
  terminal, and saga events.

Risk: non-trivial durable workflows fail at append or load time on SQL Server.

Refactoring candidate: move event type mapping and source-generated
serialization into a shared `WorkflowEventCodec` Module. Provider Adapters
should handle SQL dialect and transaction shape only. This improves Locality for
event schema changes and gives all providers the same event coverage.

Implementation update:
- 2026-07-03: Added shared `WorkflowEventCodec` in abstractions and routed
  PostgreSQL and SQL Server workflow stores through it for event type mapping,
  source-generated serialization, and deserialization.
- SQL Server now supports the same concrete durable workflow event catalog as
  PostgreSQL. Provider certification covers append/load round-trip for every
  concrete `WorkflowEvent`.

### Durable timers are removed before timer-fired commit succeeds

Evidence:
- PostgreSQL deletes due timers in
  `v3-gpt/src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs:603`.
- SQL Server deletes due timers in
  `v3-gpt/src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:726`.
- The hosted timer service claims first and processes later at
  `v3-gpt/src/OrcaCore.Hosting/Services/OrcaCoreTimerHostedService.cs:36`.

Risk: crash, cancellation, append conflict, or transient provider failure after
claim permanently loses the wake-up.

Refactoring candidate: represent timer claiming as a lease Module rather than
delete-on-claim. The Interface should include claim, complete, and release or
retry semantics. This creates Locality for crash-safety rules and lets provider
Adapters share the same lifecycle.

Implementation update:
- 2026-07-03: Added lease-aware `TimerClaimRequest` plus `CompleteAsync` and
  `ReleaseAsync` to `ITimerScheduler`.
- In-memory, PostgreSQL, and SQL Server schedulers now claim due timers without
  deleting them. Claimed timers become eligible again when their lease expires,
  can be explicitly released, and are deleted only after completion.
- `OrcaCoreTimerHostedService` now claims with the configured
  `TimerClaimLeaseDuration`, completes timers after `Committed` or `NoOp`
  durable results, and releases claims on conflicts, cancellation, and
  exceptions.
- Provider certification tests now cover timer lease expiry, release, and
  completion semantics across providers.

## P1 Findings

### Outbox records can remain claimed forever

Evidence:
- `v3-gpt/src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs:25` claims
  records and dispatches them.
- PostgreSQL marks records claimed at
  `v3-gpt/src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs:270`.
- SQL Server marks records claimed at
  `v3-gpt/src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:552`.

Risk: dispatcher exception, host crash, or mark-failed failure can strand a
record in `Claimed` until manual repair.

Recommendation: turn outbox claiming into a lease Module with retry-after,
attempt tracking, and explicit completion/failure. Add tests that simulate
dispatcher failure and process restart.

Implementation update:
- 2026-07-03: Added lease-aware `OutboxClaimRequest` and `ReleaseAsync` to
  `IWorkflowOutboxStore`.
- In-memory, PostgreSQL, SQL Server, and test-support outbox stores now claim
  pending/retryable records and expired claimed records with a lease deadline.
  Marking a terminal state clears the lease, and release returns claimed work to
  `Retryable`.
- `DurableOutboxPump` now claims with a recoverable lease, marks dispatcher
  exceptions as `Retryable`, and releases records on cancellation.
- PostgreSQL and SQL Server migrations add `claimed_until` for outbox records;
  provider certification tests cover lease expiry and explicit release.
- `IOutboxPumpObserver` now provides per-cycle claimed, attempted, success,
  retryable-failure, and permanent-failure counts; hosting can register the
  observer and integration coverage asserts the hosted pump summary.

### Hosted services lack transient-failure boundaries

Evidence:
- `v3-gpt/src/OrcaCore.Hosting/Services/OrcaCoreOutboxPumpHostedService.cs:21`
- `v3-gpt/src/OrcaCore.Hosting/Services/OrcaCoreTimerHostedService.cs:23`
- `v3-gpt/src/OrcaCore.Hosting/Services/OrcaCoreOperationalSweepHostedService.cs:21`

Risk: one provider or transport exception can terminate background processing
without structured diagnostics.

Recommendation: add source-generated logging and a retry/backoff policy at the
hosting Seam. Keep policy options in hosting, not provider Adapters.

Implementation update:
- 2026-07-03: Added `HostedServiceFailureBoundary` with shared transient
  failure handling and source-generated warning logging.
- `OrcaCoreHostedServiceOptions.TransientFailureBackoff` controls retry delay in
  hosting. Provider Adapters remain unchanged.
- Outbox pump, timer sweep, and operational sweep hosted services now catch
  non-cancellation cycle failures, delay through the injected `TimeProvider`,
  and continue processing instead of letting one provider or transport exception
  terminate the background loop.
- Hosting tests simulate first-cycle outbox claim, timer claim, and resource
  pool expiry failures and verify the next cycle still runs.

### DurableCommandProcessor is too shallow for its current responsibility

Evidence:
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs:29`
  begins a long overload list.
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs:510`
  adds start idempotency writes.
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs:676`
  starts child workflow outbox materialization.
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs:789`
  starts lifecycle projection mapping.

Risk: command routing, rehydration, commit, materialization, idempotency,
resource pool side effects, timer scheduling, and projection writes are all in
one Module. The Interface exposes nearly as much complexity as the
Implementation.

Refactoring candidate: split internal Modules for `DurableCommitPipeline`,
`OutboxMaterializer`, `TimerScheduleMaterializer`,
`LifecycleProjectionMaterializer`, and `ResourcePoolCoordinator`. The external
Interface should stay a small "process durable command" Seam.

Implementation update:
- 2026-07-03: extracted `DurableCommitMaterializer` for provider commit-batch
  materialization, including inbox operations, lifecycle/child/residual/external
  job outbox writes, timer schedules, start-idempotency writes, and projection
  writes. `DurableCommandProcessor` still owns command routing, rehydration,
  lane execution, append result handling, and resource-pool side effects.
- 2026-07-03: extracted `DurableResourcePoolCommitEffects` for resource-pool
  rollback on append conflict, committed-ticket release, and transient release
  retry. `DurableCommandProcessor` still owns command routing, rehydration, lane
  execution, and append result branching.
- 2026-07-03: extracted `DurableCommitPipeline` for no-op/eviction decisions,
  inbox-only poison commits, provider append, conflict result mapping, and
  commit-success/failure resource-pool effect routing. `DurableCommandProcessor`
  still owns command routing, rehydration, lane execution, inbox preflight, and
  resource-pool acquire command planning.
- 2026-07-03: extracted `DurableCheckpointMapper` for provider checkpoint to
  aggregate checkpoint projection, including runtime-state collections and
  payload copying. `DurableCommandProcessor` still owns command routing,
  per-instance lane execution, inbox preflight, and commit pipeline invocation.
- 2026-07-03: extracted `DurableInboxPreflight` for inbound-delivery
  idempotency decisions before aggregate hydration. `DurableCommandProcessor`
  still owns command routing, per-instance lane execution, rehydration, and
  commit pipeline invocation.
- 2026-07-03: extracted `DurableAggregateLoader` for checkpoint/tail provider
  read sequencing and aggregate rehydration. `DurableCommandProcessor` still
  owns command routing, per-instance lane execution, inbox preflight, and commit
  pipeline invocation.

### DurableWorkflowAggregate is a feature sink

Evidence:
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
  contains wait, timer, child workflow, resource pool, external job, saga,
  projection, and replay behavior.

Risk: changes for one durable concept require loading and editing a broad
Implementation. Tests become broad and bug Locality is weak.

Refactoring candidate: keep `DurableWorkflowAggregate` as the aggregate Module,
but add internal slice Modules for wait state, child workflow state, resource
pool state, external job state, and saga compensation state. The public/internal
aggregate Interface remains stable while the Implementation gains Locality.

Implementation update:
- 2026-07-03: extracted `DurableSagaState` for saga forward-action tracking,
  compensation planning, compensation action replay, manual recovery replay, and
  saga audit projection. `DurableWorkflowAggregate` still owns lifecycle
  decisions and terminal status transitions; saga compensation state is no
  longer spread across aggregate collections and helper methods.
- 2026-07-03: extracted `DurableWaitState` for active-wait storage,
  buffered-delivery storage, branch-scoped matching, buffered-delivery replay
  planning, and active-wait snapshot/checkpoint projection. The aggregate still
  owns lifecycle status transitions and command-level event emission.
- 2026-07-03: extracted `DurableExternalJobState` for active external-job
  lookup, replay, stop-request planning, and checkpoint projection. The
  aggregate still owns run/complete/timeout decisions, resource release, and
  terminal status transitions.
- 2026-07-03: extracted `DurableChildWorkflowState` for active child/group
  state, child completion follow-up planning, residual intent and resume-token
  tracking, child compensation planning, replay effects, and checkpoint
  projection. The aggregate still owns lifecycle terminal checks and terminal
  event emission.
- 2026-07-03: extracted `DurableResourcePoolState` for active ticket storage,
  acquire/queue event planning, release event planning, queued-wait replay
  effects, active-ticket replay, and checkpoint projection. The aggregate still
  owns lifecycle status transitions and external-job orchestration. During this
  extraction, `DecideStepFailed` was corrected to emit resource-pool release
  events before terminal failure so provider-held tickets are released by the
  post-commit side-effect path.
- 2026-07-03: extracted `DurableTimerState` for active timer storage,
  buffered-timer storage, timer-fired replay effects, buffered timer replay
  planning, clearing, and checkpoint projection. The aggregate still owns
  lifecycle status transitions and timer command event emission.

### PostgreSQL start idempotency is not durable

Evidence:
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs:15`
  uses `IWorkflowStartIdempotencyStore` only when the event store implements it.
- `v3-gpt/src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs:18`
  does not implement that port.

Risk: `StartOrGet` can duplicate workflow starts across restarts or nodes when
PostgreSQL is the durable provider.

Recommendation: implement the idempotency port in PostgreSQL and add provider
certification coverage.

Implementation update:
- 2026-07-03: PostgreSQL now implements `IWorkflowStartIdempotencyStore` and
  persists start idempotency keys in `orcacore_start_idempotency` in the same
  transaction as the accepted start event.
- Added migration `003_start_idempotency` and fresh-schema coverage in
  `001_initial.sql`. PostgreSQL DI now exposes the idempotency store port.
- `DurableStartService` now re-reads the durable idempotency mapping after a
  start conflict so concurrent hosts return the winning instance instead of
  surfacing a duplicate-start failure.
- Provider tests cover restart persistence and duplicate-key rollback; integration
  tests now execute PostgreSQL restart and two-host `StartOrGet` scenarios.

### SQL Server resource pools were in-memory

Status: fixed 2026-07-03 in `v3-gpt` after PostgreSQL start-idempotency parity.

Evidence:
- `v3-gpt/src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:25`
  implements `IResourcePoolStore`.
- The store keeps pool state in private in-memory collections near
  `v3-gpt/src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:37`.

Risk: resource tickets disappear on restart and split across store instances.

Recommendation: persist pool definitions, tickets, waiters, expiry, and audit
records in SQL Server or do not expose the durable resource-pool Adapter.

Follow-up result:
- Added SQL Server migration `003_resource_pools` and fresh-schema resource-pool
  tables for pool definitions, tickets, FIFO waiters, expired-ticket audit, and
  operator audit records.
- Extracted internal `SqlServerResourcePoolStore` so `SqlServerWorkflowStore`
  still exposes `IResourcePoolStore` but no longer owns process-local pool
  collections.
- Acquisition, release, expiry, and force-release now execute through SQL Server
  transactions with a transaction-owned application lock, so separate
  `SqlServerWorkflowStore` instances share one durable capacity lane.
- Added SQL Server provider tests for restart persistence, split-store capacity
  sharing, split-store release/grant behavior, resource-pool table creation, and
  migration journal coverage.

## P2 Findings

### Provider Adapters duplicate event serialization

Evidence:
- PostgreSQL has event constants and switches in
  `v3-gpt/src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs:29`,
  `:1363`, `:1419`, and `:1464`.
- SQL Server has separate switches in
  `v3-gpt/src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:1194`.
- Source generation context lives at
  `v3-gpt/src/OrcaCore.Abstractions/Serialization/OrcaCoreJsonSerializerContext.cs:8`.

Recommendation: create `WorkflowEventCodec` and make provider Adapters depend
on that Interface. This is a high-Leverage Module because every new event type
currently has multiple edit points.

Implementation update:
- 2026-07-03: Completed with shared `WorkflowEventCodec`; provider Adapters now
  keep SQL dialect and transaction behavior local while delegating event codec
  behavior to one Module.

### Ephemeral runtime is facade-heavy

Evidence:
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs` owns
  definition registration, waits, correlation routing, saga runtime state,
  governance, registry, timers, and management behavior.
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs:766` handles
  timeout/retry/governance in the same step execution path.

Recommendation: introduce internal Modules for `EphemeralRoutingIndex`,
`TimerCoordinator`, `SagaRuntime`, and node execution. Avoid class-per-node
ceremony; choose Interfaces that express behavior and concentrate Locality.

### Container/infrastructure tests are not isolated

Evidence:
- `v3-gpt/OrcaCore.slnx` includes the active integration project.
- Provider test projects contain Testcontainers-backed tests without consistent
  category isolation.
- PostgreSQL provider tests use `Testcontainers.PostgreSql` across multiple
  test classes.

Recommendation: tag container tests consistently, split default unit tests from
  infrastructure jobs, and keep active integration tests out of default solution
  runs until stable.

### Some concurrency tests use scheduler-yield negative assertions

Evidence:
- Examples include execution-lane, governance, and operations acceptance tests
  that assert a second operation has not completed after `Task.Yield()`.

Risk: the test may pass without proving that the contender reached the contested
Seam.

Recommendation: add deterministic "attempted enter" probes to test-support
helpers or expose a test-only observer on the lane/governance Module.

Implementation update:
- 2026-07-03: added `AsyncSignalCounter` to test support plus internal
  lane/governance observer hooks. Core instance-lane, ephemeral execution-lane,
  and governance tests now wait for explicit contender signals before asserting
  protected work has not entered. The operations acceptance test now asserts
  observable max concurrency rather than scheduler timing.

### Redis projection updates are non-atomic

Evidence:
- `v3-gpt/src/OrcaCore.Providers.Redis/RedisProjectionStore.cs` removes old
  index memberships, writes the snapshot, and adds new index memberships as
  separate operations.

Risk: cancellation, network failure, or process death can leave query indexes
missing or stale.

Recommendation: use a Redis transaction or Lua script Adapter for projection
upsert.

Implementation update:
- 2026-07-03: Redis projection upserts now use an optimistic Redis transaction
  with snapshot-key conditions. Snapshot writes and secondary-index removals/adds
  commit together, and contended writers retry against the winning envelope.
- Redis provider tests now cover metadata-index movement after an update and
  guard the Adapter's transaction boundary.

### PostgreSQL shared data-source ownership is unclear

Evidence:
- DI overloads can receive an existing `NpgsqlDataSource`, but PostgreSQL store
  disposal disposes the data source.

Risk: disposing one store can tear down a host-owned or shared pool.

Recommendation: split owned-data-source and borrowed-data-source Adapters, or
track ownership explicitly.

Implementation update:
- 2026-07-03: PostgreSQL workflow and resource-pool stores now track data-source
  ownership explicitly. Connection-string constructors own and dispose their
  internally-created `NpgsqlDataSource`; `NpgsqlDataSource` constructors borrow
  caller-owned pools and leave disposal to the caller or container owner.
- PostgreSQL service-collection tests now cover direct borrowed-store disposal
  and borrowed-data-source DI provider disposal without tearing down the
  caller-owned source.

## Large Module And Switch Hotspots

This is the first refactoring lens to apply after the P0/P1 correctness fixes.
The goal is not to split by file size alone. The goal is to find shallow
Modules where a caller or test must understand a large switch, many feature
branches, or a long list of operation modes to use the Module safely.

### `Interpreter<TState>` should be treated as a priority decomposition target

Evidence:
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs:13` declares
  `internal sealed class Interpreter<TState>`.
- The file is currently about 1,215 lines.
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs:82` starts a
  large node-dispatch switch over `InitNode`, `BusinessStepNode`, `EndNode`,
  `IfNode`, `WhileNode`, `ParallelNode`, `WhenFirstNode`, `ForEachNode`,
  child-workflow nodes, `WaitNode`, and `DelayNode`.
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs:766` starts
  step execution, combining timeout, retry, governance, step factory, result
  handling, stuck-step detection, and failure deferral.

Refactoring direction:
- Keep `Interpreter<TState>` as the orchestration Module and preserve its
  external Interface.
- Extract internal Modules by behavior, not one class per syntax node:
  `StepExecutor`, `WaitExecutor`, `DelayScheduler`, `ParallelJoinCoordinator`,
  `WhenFirstCoordinator`, `LoopExecutor`, and `YieldContinuationScheduler`.
- Each extracted Module should hide ordering, cancellation, and continuation
  invariants behind a small Interface. The test surface should move to those
  Interfaces where bugs currently require large integration-style tests.

Acceptance signal for this refactor:
- The large dispatch switch becomes a shallow routing table.
- Timeout/retry/governance behavior can be tested through `StepExecutor`
  without constructing a full interpreter run.
- Wait/delay/parallel behavior can be tested through their own Modules with
  fake time and deterministic probes.

Implementation update:
- 2026-07-03: extracted `StepExecutor<TState>` and `StepExecutionResult` from
  `Interpreter<TState>`. Step execution now owns timeout cancellation, retry
  attempts, resource-governance leases, stuck-step detection, resumed event
  delivery, lifecycle event recording, and step-result mapping behind one
  internal Module. `Interpreter<TState>` still owns recursive node orchestration
  and consumes the step outcome.
- 2026-07-03: extracted `ConditionEvaluator<TState>`,
  `SuspensionScheduler<TState>`, `WhileNodeRunner<TState>`,
  `ParallelNodeRunner<TState>`, `WhenFirstNodeRunner<TState>`,
  `ForEachNodeRunner<TState>`, `ParallelJoin`, `WhenFirstJoin`,
  `WorkflowFailureHandler<TState>`, and `WorkflowLifecycleTransition`. The
  interpreter now stays closer to orchestration and delegates condition
  handling, wait/delay suspension, loop execution, parallel joins, when-first
  joins, foreach scheduling, failure recording, and lifecycle transitions.
- 2026-07-03: extracted `YieldContinuationScheduler` after confirming that a
  scheduler-only wrapper would be pass-through glue. The extracted Module owns
  both yield continuation scheduling and lane/governance-aware draining from
  start, timer, and event-resume entry points.
- 2026-07-03: hardened `YieldContinuationScheduler` so its initial snapshot and
  continuation checks run through the per-instance lane; broad ephemeral
  verification had exposed an intermittent concurrent snapshot race while
  parallel wait branches were resuming.
- 2026-07-03: extracted `WaitExecutor<TState>` for explicit `WaitNode`
  correlation selector evaluation, selector-failure recording, and wait
  suspension registration. `SuspensionScheduler<TState>` still owns the shared
  wait/delay timer mechanics used by wait nodes and step wait results.
- Remaining interpreter decomposition should stop unless another behavior can be
  made directly testable without turning the interpreter into pass-through glue.

### Other switch-heavy or oversized Modules to prioritize

| Module | Current shape | Refactoring direction |
| --- | --- | --- |
| `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs` | About 2,603 lines before decomposition; one aggregate owned timers, projections, lifecycle transitions, and replay orchestration. Saga compensation state has been extracted to `DurableSagaState`; active-wait and buffered-delivery state has been extracted to `DurableWaitState`; external-job state has been extracted to `DurableExternalJobState`; child workflow state has been extracted to `DurableChildWorkflowState`; resource-pool state has been extracted to `DurableResourcePoolState`; timer state has been extracted to `DurableTimerState`. | Keep the aggregate Interface. Further extraction should target projection/replay orchestration only when it creates behavior-testable Locality rather than pass-through glue. |
| `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs` | About 979 lines before decomposition; one processor handled command overloads, rehydration, and append result branching. `DurableCommandRuntime` owns per-instance lanes, `DurableCommitMaterializer` owns provider commit-batch materialization, `DurableResourcePoolCommitEffects` owns resource-pool commit side effects, `DurableCommitPipeline` owns no-op/poison/append-result behavior, `DurableCheckpointMapper` owns provider checkpoint projection, `DurableInboxPreflight` owns inbound idempotency short-circuit decisions, and `DurableAggregateLoader` owns checkpoint/tail read sequencing plus aggregate rehydration. | Continue only where another behavior can become testable Locality; avoid splitting command overloads into pass-through classes. |
| `v3-gpt/src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs` | About 1,515 lines; event mapping now routes through shared `WorkflowEventCodec`, which uses a single descriptor table for type names, serialization, and deserialization. Projection operation switching remains in the store Adapter. | Move projection writes into a projection store Module; keep this Adapter focused on PostgreSQL SQL and transaction shape. |
| `v3-gpt/src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs` | About 1,369 lines; event mapping now routes through shared `WorkflowEventCodec`, which uses a single descriptor table for type names, serialization, and deserialization. Projection operation switching remains in the store Adapter, with `AppendHistory` now covered by a table-backed projection. | Split durable event store, projection store, timer store, outbox store, and resource pool Adapter responsibilities. |

Avoid replacing these switches with many tiny pass-through classes. A new Module
only earns its keep when deleting it would push real invariants and branching
back into several callers. The useful seams here are the ones that create
Locality for behavior that is currently hard to test without loading the whole
class.

## P3 Findings

### Thin test stubs could use a mocking framework

High-fidelity domain fakes such as `FakeWorkflowEventStore` should stay. For
one-off DI substitutions that only throw `NotSupportedException`, NSubstitute or
FakeItEasy would reduce boilerplate and make test intent clearer.

### Source-text assertions should stay temporary

Repository guards are useful for architectural regression checks, but several
tests inspect source text for behavior-like claims. Prefer behavior tests at the
Module Interface and keep source-text checks explicitly named as guard tests.

## Recommended Refactoring Order

1. Completed 2026-07-03: create `DurableCommandRuntime` and route
   management/timer/public durable entrypoints through one process-wide
   per-instance lane Seam.
2. Completed 2026-07-03: fix timer and outbox claim semantics with lease
   Modules before expanding clustered durable execution.
3. Completed 2026-07-03: create `WorkflowEventCodec` and complete SQL Server
   event coverage.
4. Completed 2026-07-03: PostgreSQL start idempotency, SQL Server resource-pool
   persistence, SQL Server retention certification parity, and SQL Server
   history-projection parity.
5. Continue behavior-preserving `Interpreter<TState>` decomposition only where
   it adds locality or testability.
6. Completed 2026-07-03: internal durable aggregate slices for wait state, saga
   compensation state, external-job state, child workflow state, and
   resource-pool state.
7. Introduce a shared `InstanceLane` Module to remove duplicated channel-lane
   Implementation.
8. Split test execution into unit, container, and active integration lanes in CI.
9. Completed 2026-07-03: replace scheduler-yield negative assertions with
   deterministic test-support probes for lane/governance tests and keep public
   acceptance coverage on observable concurrency.

## Current Risk Summary

The R8 quality issues, immediate channel-substrate drift, timer/outbox claim
leases, PostgreSQL start idempotency, SQL Server event coverage, SQL Server
resource-pool persistence, SQL Server retention certification parity, SQL
Server history-projection parity, and hosted-service transient-failure
boundaries are addressed. The scheduler-yield negative assertion audit item now
has deterministic lane/governance probes, and Redis projection updates now use
one optimistic transaction for snapshot/index movement. PostgreSQL
data-source ownership is explicit for direct and DI-created stores. The next
refactoring pass should prioritize durable aggregate, durable command-processor,
and interpreter decomposition without weakening the provider certification
surface.
