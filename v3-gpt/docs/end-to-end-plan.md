# OrcaCore v3-gpt End-to-End Plan

This plan is scoped to the current `v3-gpt` implementation tree. It uses the
existing source, test projects, Testcontainers fixtures, and skipped integration
tests as the baseline. The root `src` and root `tests` trees are not part of
this plan.

## Current Baseline

The current end-to-end harness lives in:

- `tests/OrcaCore.Integration.Tests`
- `tests/OrcaCore.ProviderCertification`
- provider-specific test projects under `tests/OrcaCore.Providers.*.Tests`
- the sample host at `samples/OrcaCore.SampleHost`

The integration project already references the full provider and hosting stack:

- durable and ephemeral engines;
- hosting services;
- in-memory, PostgreSQL, SQL Server, Redis, RabbitMQ, and ZeroMQ providers;
- Testcontainers for PostgreSQL, SQL Server, Redis, and RabbitMQ;
- `OrcaIntegrationHost` as the shared host-level test harness.

Observed current baseline:

```powershell
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.E2E"
```

Result after Workstream 6 and the current scheduler slice: 18 passed, 0
skipped, 0 failed.

The original skipped `E2E` scenarios are now implemented:

- `INT_E2E_008`: durable saga compensation survives restart on PostgreSQL.
- `INT_E2E_011`: durable definition version binding survives deployment.
- `INT_E2E_013`: durable DAG runner schedules children and management
  reconstructs node status.
- `INT_E2E_015`: durable yield persists progress across processor restart.

The broader integration suite still contains deliberate skips for
cron/scheduled-start simulation, the slow scheduler soak, and provider
failure-injection hooks. Those are backlog or ownership markers, not flaky tests.
The current full integration project gate is 106 passed, 2 skipped, 0 failed.

Structured logging and runtime metrics are introduced in Workstream 3. The
current code now has source-generated command/outbox logs, BCL `Meter`
instruments at durable command and outbox pump boundaries, and `ActivitySource`
spans for durable command processing. Remaining telemetry breadth still follows
`docs/specs/15-requirements-observability-otel.md`: provider commit spans,
wait/timer/resource-pool metrics, and opt-in OpenTelemetry exporter wiring in
hosting.

## E2E Definition

For this implementation track, an OrcaCore end-to-end test should exercise the
library the way an application would consume it:

1. Build an `IHost` through public DI registration.
2. Register or resolve the workflow/runtime/provider surface from DI.
3. Start or resume workflow work through public entry points.
4. Persist state through a real provider when durability is part of the scenario.
5. Let hosted services execute outbox, timer, and operational sweep behavior.
6. Observe outcomes through management APIs, provider state, dispatched messages,
   and, for observability scenarios, structured logs and metrics.
7. Cover restart or multi-node behavior where the acceptance criterion requires it.

Manual `DurableCommandProcessor` calls remain valid lower-level integration
coverage, but they are not enough for the final e2e gate when a host-level path
should exist.

## Non-Goals

- Do not put Kubernetes or EKS adapters inside OrcaCore. The current
  `eks-scheduler-handoff.md` keeps Kubernetes dispatchers, watchers, cron, and
  tenant policy in the scheduler application.
- Do not create a parallel e2e harness. Extend `OrcaCore.Integration.Tests` and
  reuse its fixtures.
- Do not widen scope to the legacy root implementation.
- Do not require Docker for unit, acceptance, or provider-certification tests
  that do not need real external services.

## Workstream 1: Baseline And Gate Hygiene

Goal: make the current e2e surface explicit and repeatable before adding missing
runtime behavior.

Tasks:

1. Add a documented e2e gate command set:
   - `FullyQualifiedName~OrcaCore.Integration.Tests.E2E`
   - `FullyQualifiedName~OrcaCore.Integration.Tests.Hosting`
   - `FullyQualifiedName~OrcaCore.Integration.Tests.Stacks`
   - `FullyQualifiedName~OrcaCore.Integration.Tests.MultiNode`
   - `FullyQualifiedName~OrcaCore.Integration.Tests.JobScheduler`
   - `FullyQualifiedName~OrcaCore.Integration.Tests.Observability`
2. Record that broad integration runs require Docker.
3. Before broad e2e runs, use `dotnet build-server shutdown` if old MSBuild
   node-reuse processes are still resident.
4. Keep existing skipped tests as named backlog items until each blocker is
   implemented; do not delete or silently retag them.

Exit criteria:

- The current `E2E` namespace remains green with only deliberate skips.
- `tests/OrcaCore.Integration.Tests/README.md` identifies smoke, focused,
  observability, and full-stack integration commands.
- The observability filter is executable and covers `OB-AC-001` through
  `OB-AC-007`.

## Workstream 2: Host-Level Durable E2E

Goal: prove the durable stack through `IHost`, not only through manually created
processors and stores.

Current useful code:

- `OrcaIntegrationHost.Build`
- `OrcaIntegrationHost.BuildPostgreSqlAsync`
- `HostingPostgreSqlIntegrationTests`
- `ProviderStackIntegrationTests`
- PostgreSQL provider DI registration through `AddOrcaCorePostgreSql`

Tasks:

1. Promote representative command-level durable scenarios to host-level tests:
   - start -> step complete -> complete;
   - start -> wait -> event delivery -> resume;
   - start -> external job -> outbox dispatch;
   - start -> timer scheduled -> hosted timer sweep fires;
   - terminal instance -> archive or retention operation.
2. Ensure the tests resolve `DurableCommandProcessor`, `DurableManagement`,
   `IWorkflowEventStore`, `IResourcePoolStore`, `ITimerScheduler`, and
   `DurableOutboxPump` from the host service provider.
3. For scenarios with dispatch, prefer the hosted outbox path where possible;
   use `PumpOnceAsync` only when the scenario intentionally isolates a single
   cycle.
4. Add matching assertions through management projections and provider state,
   not only return values from command calls.

Exit criteria:

- Existing durable happy paths have host-level coverage with PostgreSQL.
- Host restart tests prove a waiting durable instance resumes after host rebuild.
- Outbox and timer tests prove hosted service wiring, not just direct class calls.

## Workstream 3: Structured Logging, Metrics, And Telemetry E2E

Goal: introduce the actual runtime telemetry surface after the host-level durable
path is proven, so logs and metrics are validated against real command,
provider, outbox, timer, and management behavior.

Current useful code:

- `OrcaCore.Abstractions.Diagnostics.OrcaCoreDiagnostics`
- `IWorkflowRuntimeObserver`
- `IOutboxPumpObserver`
- management `Statistics()` / durable projection statistics
- hosted outbox, timer, and operational sweep services

Current implemented slice:

- Source-generated structured `ILogger` calls are part of the e2e gate for
  durable command outcomes and outbox pump summaries.
- BCL `Meter` instruments are asserted by integration tests for durable command
  throughput/duration, active instances, and outbox dispatch health.
- `ActivitySource` spans are asserted for durable command processing and trace
  correlation on command logs.

Remaining gap:

- Provider append/commit, wait/timer, and resource-pool telemetry is not yet as
  broad as the command/outbox telemetry surface.
- No opt-in hosting extension wires OpenTelemetry exporters for OrcaCore meters,
  traces, and logs.

Implementation note:

- The first telemetry slice introduces source-generated structured logs and BCL
  metrics at the durable command and outbox pump boundaries, plus an
  `ActivitySource` span for durable command processing. The executable
  `Observability` integration gate now covers `OB-AC-001` through `OB-AC-007`.
  Provider-commit, wait/timer, resource-pool, and OpenTelemetry exporter wiring
  remain in this workstream's remaining task list.

Tasks:

1. Add structured logging on the high-value runtime boundaries:
   - durable command accepted, completed, no-op, conflict, poisoned, or failed
     (initial command-completed slice complete);
   - provider append/commit success and conflict;
   - wait registered and matched;
   - timer scheduled and fired;
   - outbox pump cycle summary (initial slice complete);
   - outbox record dispatched, retryable, or poisoned;
   - resource-pool acquire, wait, release, and expiry.
2. Use source-generated `[LoggerMessage]` partial methods on hot paths and keep
   log calls structured. Do not log business payloads or workflow state by
   default.
3. Add BCL `Meter` instruments for the required operator signals:
   - active instances by status and definition (initial slice complete);
   - active waits by event name;
   - commands processed and command duration (initial slice complete);
   - provider commit duration;
   - outbox pending/retryable/claimed counts;
   - outbox dispatch attempts and dispatch duration (initial slice complete);
   - resource-pool waiters and tickets.
4. Back observable gauges with the same projections used by management
   statistics where possible, so metrics and management answers stay aligned.
5. Add `ActivitySource` spans for command processing (initial slice complete),
   provider commit, step execution, outbox dispatch, and pump cycles. Logs
   emitted inside those spans must carry trace correlation through logging
   scopes.
6. Add opt-in hosting wiring such as `AddOrcaCoreOpenTelemetry(...)` in
   `OrcaCore.Hosting`. OpenTelemetry SDK package references belong in hosting
   only; engines, providers, core, and abstractions stay on BCL diagnostics.
7. Add observability e2e tests under `OrcaCore.Integration.Tests`:
   - metrics are emitted after starting workflows and creating outbox backlog;
   - a command produces a structured log with instance id, command type, and
     trace id when tracing is enabled;
   - a failed outbox dispatch increments the matching metric and emits a
     correlated structured error log;
   - `Statistics()` and `orca.instances.active` agree for the same fixture;
   - no `OpenTelemetry.*` packages appear outside hosting.

Exit criteria:

- `OB-AC-001` through `OB-AC-007` have scenario-traceable tests.
- Operators can answer the basic fleet-health, blocking, throughput, latency,
  and outbox-health questions from OrcaCore logs and metrics without adding
  custom host instrumentation.
- Telemetry failures never change workflow command outcomes.

## Workstream 4: Durable Workflow Facade

Goal: add the missing public durable workflow runtime surface needed for true
definition-driven e2e.

Current gap:

- The ephemeral engine can register a `WorkflowDefinition<TState>` and execute
  the node graph inline.
- The durable runtime now exposes a public definition registry and version-bound
  `StartOrGet` facade over the durable command path.
- Full durable workflow-node execution is not yet available.

Implementation note:

- `DurableDefinitionRegistry` registers immutable definition versions by
  `DefinitionId` and `DefinitionVersion`.
- `DurableWorkflowRuntime` exposes public version-bound `StartOrGet` over the
  existing durable start/idempotency implementation.
- `INT_E2E_011` now simulates deployment on PostgreSQL: an instance started on
  version 1 remains bound to version 1 after version 2 is registered, and a
  same-key start under version 2 fails explicitly.

Tasks:

1. Define the durable public entry point:
   - register definition version;
   - start or start-or-get an instance;
   - bind a running instance to its original definition version;
   - deliver events and timers through the durable runtime.
2. Add a durable definition registry that can resolve the bound definition after
   restart. (Initial registry/facade slice complete for `INT_E2E_011`.)
3. Translate durable-capable workflow nodes into existing durable commands:
   - start;
   - step completed or failed;
   - waits;
   - timers;
   - child workflow commands;
   - external job commands;
   - terminal commands.
4. Add version-compatibility diagnostics for incompatible resumed definitions.
5. Unskip and implement `INT_E2E_011`. (Complete.)

Exit criteria:

- A workflow definition can be registered, started, suspended, persisted,
  reloaded after host restart, and resumed through public durable APIs.
- Version binding is observable and tested.

## Workstream 5: Integrated DAG And Scheduler Runner

Goal: turn current DAG/job building blocks into an integrated durable runner for
the job-scheduler scenario.

Current useful code:

- DAG builder and acceptance tests.
- Durable child workflow command handlers.
- Durable external job command handlers.
- Resource-pool stores and certification tests.
- Outbox kind dispatching.

Current implemented slice:

- The durable runtime now has an initial `DurableDagRunner` that schedules ready
  `WorkflowDagPlan` batches through durable child workflow commands.
- Core-owned `INT-JS-*` tests now cover diamond DAG scheduling, failure-blocked
  dependents, queue quota, restart mid-job, cancellation stop outbox, pause with
  in-flight jobs, continue-as-new mid-DAG, heterogeneous node definitions, and
  duplicate completion deduplication.

Remaining gap:

- `INT_JS_013` remains skipped because cron/scheduled-start triggering belongs
  to the scheduler application, which should call OrcaCore `StartOrGet` with a
  canonical occurrence key.
- `INT_JS_018` remains skipped because the one-hour fake-clock soak belongs in a
  slow or nightly gate, not the default integration gate.

Implementation note:

- `DurableDagRunner.ScheduleReadyAsync(...)` turns ready DAG batches into
  `DurableRunChildrenCommand` commits.
- `INT_E2E_013` now runs against PostgreSQL by scheduling DAG children through
  the runner and reconstructing node status through durable management.
- The JobScheduler focused gate now passes with only the scheduler-app owned
  scheduled-start test and the slow soak skipped.

Tasks:

1. Add a runner that turns a DAG plan into durable execution commands. (Initial
   ready-batch runner complete for `INT_E2E_013` and `INT-JS-001`.)
2. Schedule ready nodes only when dependencies are satisfied. (Complete for the
   focused core gate.)
3. Use durable resource pools for external jobs. (Complete for the focused core
   gate.)
4. Emit normalized outbox records for job start and job stop. (Complete for the
   focused core gate.)
5. Consume normalized completion events with stable event IDs for inbox dedup.
   (Complete for the focused core gate.)
6. Preserve management visibility for node state, blocked dependents, active
   jobs, queued jobs, and completed nodes. (Complete for the focused core gate.)
7. Unskip DAG runner scenarios incrementally:
   - diamond DAG happy path;
   - failure blocks dependents;
   - restart mid-job;
   - queue quota across definitions;
   - cancel or pause with in-flight jobs where the durable API supports it.
     (Complete for the focused core gate.)

Exit criteria:

- Core JS acceptance criteria that belong to OrcaCore pass without Kubernetes.
- Scheduler-app responsibilities remain documented in `eks-scheduler-handoff.md`.

## Workstream 6: Durable Saga And Durable Yield

Goal: close the remaining `E2E` skips that require restart-safe advanced runtime
behavior.

Implementation note:

- `INT_E2E_008` now uses the durable saga command adapter against PostgreSQL to
  prove compensation is restart-safe and idempotent.
- `DurableYieldCommand` commits a checkpoint without appending a
  `WorkflowStepCompletedEvent`, allowing a restarted processor to complete the
  same logical step once.
- `INT_E2E_015` now proves yield checkpoint recovery and exactly-once step
  completion across PostgreSQL-backed processor restart.

Tasks:

1. Durable saga:
   - persist forward action audit;
   - persist compensation decisions;
   - resume compensation after restart;
   - expose manual recovery state through management.
2. Durable yield:
   - commit progress before yielding;
   - resume the same logical step after restart;
   - prevent duplicate side effects on retry;
   - expose progress in history/projections where required.
3. Unskip and implement:
   - `INT_E2E_008`; (Complete.)
   - `INT_E2E_015`. (Complete.)

Exit criteria:

- Durable saga and yield scenarios pass through the same provider and host-level
  harness as other e2e tests. (Complete for the E2E gate.)

## Workstream 7: Failure Injection And Resilience E2E

Goal: prove crash, cancellation, transient failure, and retry behavior at the
boundaries where production failures happen.

Current implemented slice:

- `PostgreSqlWorkflowStoreOptions.BeforeCommitAsync` exposes a deterministic
  hook inside the PostgreSQL append transaction after writes are staged and
  before commit.
- `INT_EP_002` proves a pre-commit failure leaves no events, checkpoint,
  projection, or outbox rows.
- `INT_HO_005` proves cancellation during an in-flight host command rolls back
  cleanly without relying on sleeps or process killing.

Remaining gap:

- Additional hooks for after-commit/before-return, outbox claim, timer claim,
  and resource-pool acquire/release would broaden provider failure testing.
- Several resilience scenarios remain covered by direct class tests rather than
  full provider-backed e2e tests.

Tasks:

1. Add test-only or internal injectable hooks around:
   - append before commit; (Complete for PostgreSQL.)
   - append after commit before return;
   - outbox claim;
   - outbox dispatch;
   - timer claim;
   - resource-pool acquire and release.
2. Use those hooks to prove:
   - no outbox dispatch before commit is visible;
   - no partial append; (Complete for PostgreSQL pre-commit failure.)
   - no timer leak on failed commit;
   - claimed outbox rows can be retried after cancellation;
   - host shutdown during a pump cycle does not lose rows;
   - host shutdown during a command leaves either a committed or cleanly absent
     state. (Complete for cancellation before PostgreSQL append commit.)
3. Unskip host and engine failure-injection tests only after the hook exists.
   (Complete for `INT_EP_002` and `INT_HO_005`.)

Exit criteria:

- Failure-path e2e tests are deterministic and do not rely on sleeps or process
  killing. (Complete for the default integration gate.)
- Provider invariants remain covered by certification tests.

## Workstream 8: Provider Matrix E2E

Goal: prove the same behavioral contract across providers without duplicating
all tests for every provider.

Tasks:

1. Keep the full scenario set on PostgreSQL as the primary durable e2e provider.
2. Keep SQL Server coverage focused on provider parity:
   - durable wait survives restart;
   - outbox and timer stack;
   - projections;
   - resource pools;
   - retention.
3. Keep Redis coverage focused on projection-cache behavior.
4. Keep RabbitMQ and ZeroMQ coverage focused on dispatcher behavior and outbox
   state transitions.
5. Put cross-provider invariants in `OrcaCore.ProviderCertification` when the
   behavior belongs to every provider.

Exit criteria:

- PostgreSQL proves full stack e2e.
- SQL Server proves equivalent provider semantics for implemented surfaces.
- Dispatcher providers prove retryable, permanent, and successful dispatch paths.

## Final E2E Gate

The final gate should run from `v3-gpt`:

```powershell
dotnet build OrcaCore.slnx
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build
dotnet test tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj --no-build
dotnet test tests/OrcaCore.Providers.PostgreSql.Tests/OrcaCore.Providers.PostgreSql.Tests.csproj --no-build
dotnet test tests/OrcaCore.Providers.SqlServer.Tests/OrcaCore.Providers.SqlServer.Tests.csproj --no-build
dotnet test tests/OrcaCore.Providers.RabbitMq.Tests/OrcaCore.Providers.RabbitMq.Tests.csproj --no-build
dotnet test tests/OrcaCore.Providers.Redis.Tests/OrcaCore.Providers.Redis.Tests.csproj --no-build
dotnet test tests/OrcaCore.Providers.ZeroMq.Tests/OrcaCore.Providers.ZeroMq.Tests.csproj --no-build
```

For local iteration, use narrower gates first:

```powershell
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.E2E"
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.Hosting"
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.Observability"
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.JobScheduler"
```

## Sequencing

1. Workstream 1: baseline and gate hygiene.
2. Workstream 2: host-level durable e2e promotion.
3. Workstream 3: structured logging, metrics, and telemetry e2e.
4. Workstream 4: durable workflow facade.
5. Workstream 5: integrated DAG and scheduler runner.
6. Workstream 6: durable saga and durable yield.
7. Workstream 7: failure injection and resilience e2e.
8. Workstream 8: provider matrix expansion and final e2e gate.

This order gives fast confidence first, then adds missing product surface area,
then fills advanced durable semantics and hardens resilience/provider breadth.
