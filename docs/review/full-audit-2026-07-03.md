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
- `InstanceExecutionLane` and `DurableInstanceCommandLane` now use bounded
  `System.Threading.Channels` instead of unbounded semaphore dictionaries.
- Per-instance work is serialized by a single-reader channel.
- Idle lanes self-evict after the current batch drains.
- Tests cover same-instance serialization, cross-instance overlap, exception
  release, and idle-lane eviction.

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
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/InstanceExecutionLane.cs:64`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableInstanceCommandLane.cs:47`

The current Implementation uses bounded channels with `FullMode = Wait`,
`SingleReader = true`, and self-eviction. This is closer to the documented
channel substrate, but the Module is duplicated between engines and the Seam is
still concrete.

Refactoring candidate: introduce a shared internal `InstanceLane` Module with
the Interface `RunAsync(instanceId, operation, cancellationToken)`. Keep the
channel-backed Adapter internal. This increases Depth because ordering,
backpressure, cancellation, exception propagation, and eviction sit behind one
small Interface. It improves Locality because future lane bug fixes happen once.

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

### Hosted services lack transient-failure boundaries

Evidence:
- `v3-gpt/src/OrcaCore.Hosting/Services/OrcaCoreOutboxPumpHostedService.cs:21`
- `v3-gpt/src/OrcaCore.Hosting/Services/OrcaCoreTimerHostedService.cs:23`
- `v3-gpt/src/OrcaCore.Hosting/Services/OrcaCoreOperationalSweepHostedService.cs:21`

Risk: one provider or transport exception can terminate background processing
without structured diagnostics.

Recommendation: add source-generated logging and a retry/backoff policy at the
hosting Seam. Keep policy options in hosting, not provider Adapters.

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

### SQL Server resource pools are in-memory

Evidence:
- `v3-gpt/src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:25`
  implements `IResourcePoolStore`.
- The store keeps pool state in private in-memory collections near
  `v3-gpt/src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:37`.

Risk: resource tickets disappear on restart and split across store instances.

Recommendation: persist pool definitions, tickets, waiters, expiry, and audit
records in SQL Server or do not expose the durable resource-pool Adapter.

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

### Redis projection updates are non-atomic

Evidence:
- `v3-gpt/src/OrcaCore.Providers.Redis/RedisProjectionStore.cs` removes old
  index memberships, writes the snapshot, and adds new index memberships as
  separate operations.

Risk: cancellation, network failure, or process death can leave query indexes
missing or stale.

Recommendation: use a Redis transaction or Lua script Adapter for projection
upsert.

### PostgreSQL shared data-source ownership is unclear

Evidence:
- DI overloads can receive an existing `NpgsqlDataSource`, but PostgreSQL store
  disposal disposes the data source.

Risk: disposing one store can tear down a host-owned or shared pool.

Recommendation: split owned-data-source and borrowed-data-source Adapters, or
track ownership explicitly.

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

1. Create `DurableCommandRuntime` and route management/timer/public durable
   entrypoints through one process-wide per-instance lane Seam.
2. Fix timer and outbox claim semantics with lease Modules before expanding
   clustered durable execution.
3. Create `WorkflowEventCodec` and complete SQL Server event coverage.
4. Persist PostgreSQL start idempotency and SQL Server resource pools, or remove
   those durable-provider claims until true.
5. Extract internal durable aggregate slices for waits, children, resource pools,
   external jobs, and saga compensation.
6. Introduce a shared `InstanceLane` Module to remove duplicated channel-lane
   Implementation.
7. Split test execution into unit, container, and active integration lanes in CI.
8. Replace scheduler-yield negative assertions with deterministic test-support
   probes.

## Current Risk Summary

The R8 quality issues and immediate channel-substrate drift are addressed. The
remaining high-risk items are not analyzer warnings; they are durable execution
and provider semantics. The next refactoring pass should prioritize durable
ordering, timer/outbox crash safety, and provider parity before adding more
composition features.
