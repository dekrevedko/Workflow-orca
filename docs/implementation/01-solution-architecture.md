# 01. Solution Architecture

## 1. Projects and module boundaries

```text
OrcaCore.slnx
src/
  OrcaCore.Abstractions          ← contracts ONLY: step/result/envelope/wait/status models,
                                   snapshots, provider ports, dispatch models, serialization
                                   seams, functional primitives (Result/Option/Validation)
  OrcaCore.Core                  ← engine-agnostic internals shared by both engines:
                                   definition graph model, builder + validation, lifecycle
                                   state machine, wait matching, correlation index,
                                   management query model
  OrcaCore.Engine.Ephemeral      ← quick engine (in-memory instances, per-instance lanes)
  OrcaCore.Engine.Durable        ← event-sourced engine (commands, aggregate, checkpoints,
                                   projections pipeline, inbox/outbox pump, timer service,
                                   durable pools)
  OrcaCore.Providers.InMemory    ← reference implementation of ALL provider ports
  OrcaCore.Hosting               ← Microsoft.Extensions integration: AddOrcaCore(),
                                   hosted services for pumps/schedulers
  plugins/
    OrcaCore.Providers.PostgreSql   (Phase 2)
    OrcaCore.Providers.RabbitMq     (Phase 6)
    OrcaCore.Providers.SqlServer / .Redis / .DynamoDb / .ZeroMq   (Phase 6+)
tests/
  OrcaCore.TestSupport           ← hand-rolled port fakes, FakeTimeProvider harness,
                                   builders for envelopes/definitions (shared library)
  OrcaCore.Core.Tests
  OrcaCore.Engine.Ephemeral.Tests
  OrcaCore.Engine.Durable.Tests
  OrcaCore.Acceptance.Tests      ← AC-xxx catalog tests against PUBLIC API only
  OrcaCore.ProviderCertification ← abstract test classes (a library, not a test project):
                                   the PR-024 suite any provider must pass
  OrcaCore.Providers.PostgreSql.Tests   ← inherits certification suite + Testcontainers
  OrcaCore.Providers.RabbitMq.Tests
```

## 2. Dependency rules (enforced; violations fail review)

```text
Abstractions  ← depends on nothing (BCL only)
Core          ← Abstractions
Engines       ← Core, Abstractions
Providers.*   ← Abstractions ONLY (never Core, never an engine)
Hosting       ← everything above (composition root)
TestSupport   ← Abstractions (+ Core where needed)
```

- **Nothing depends on a plugin.** Engines consume ports (`IWorkflowEventStore`,
  `IMessageDispatcher`, `ITimerScheduler`, …) from Abstractions; the host wires concrete
  providers via DI. This is the "program to interfaces" rule made structural.
- Plugins are **explicitly registered** (an `AddOrcaCorePostgres(...)`-style extension per
  plugin, living in the plugin). No assembly scanning, no reflection discovery.
- `Providers.InMemory` is a first-class provider, not test code: it is the executable
  documentation of port semantics and must pass the certification suite like any plugin.

## 3. Public vs internal

- Public surface: Abstractions contracts, builders, the two engine facades, management
  scopes/snapshots, hosting extensions. Everything else `internal`, `sealed` by default.
- Each production project grants `InternalsVisibleTo` to exactly one matching unit-test
  project. `OrcaCore.Acceptance.Tests` gets **no** internals — it proves the public API is
  sufficient (spec CR-021/MG-005 depend on this discipline).

## 4. Port catalog (interfaces allowed here; signatures indicative, not code)

Defined in Abstractions, consumed by the durable engine, implemented by providers
(spec PR-010…016):

- `IWorkflowEventStore` — `AppendAsync(streamId, expectedVersion, events, ct) → Result<AppendOutcome>`;
  `ReadTailAsync(streamId, afterVersion, ct) → IAsyncEnumerable<StoredEvent>`;
  `LoadCheckpointAsync(...) → Option<Checkpoint>`; `SaveCheckpointAsync(...)`.
- `IWorkflowInboxStore` — record/query/mark by `EventId` (`Received/Applied/DuplicateIgnored/Poisoned/DiscardedOnResume`).
- `IWorkflowOutboxStore` — append-in-commit, claim/lease batch, mark dispatched/failed/poisoned, backlog stats.
- `IWorkflowProjectionStore` — upsert/query instance summaries, active waits, pending
  events, history, saga compensation, DAG lineage.
- `ITimerScheduler` — schedule/cancel durable wake-ups → `FireTimer` commands.
- `IResourcePoolStore` — pool records, ticket acquire (all-or-nothing)/release/expire (MG-062…064).
- `IMessageDispatcher` — `DispatchAsync(DispatchMessage, ct) → DispatchOutcome` (success/retryable/permanent).
- `IPayloadSerializer` / `IPayloadSchemaResolver` — serialization seams (PR-016).

The **atomic commit boundary** (PR-020) is expressed as one port-level unit-of-work
operation on the store family (a provider composes events + checkpoint + inbox + outbox +
projection work into one transaction or a documented transactional chain); its exact shape
is designed in Phase 2 under IOQ-1/IOQ-3.

## 5. Concurrency model (Channels + TPL)

- **Per-instance execution lane**: one bounded `Channel<InstanceWork>` (or semaphore lane in
  the ephemeral engine) serializes all mutations of an instance — the CR-040 guarantee.
- **Outbox pump**: bounded channel of claimed batches → dispatcher; retry-delay strategy and
  observer hooks as small interfaces (DU-032).
- **Timer service**: min-heap over `TimeProvider` + channel of due firings.
- All loops: `async Task` with cooperative `CancellationToken`; no `Thread`, no `Timer`
  callbacks mutating state directly, no `async void`, no blocking waits (`.Result`/`.Wait()`).

## 6. Module growth rule

New capability = new internal module (folder with an internal interface + implementation)
inside the owning project, or a new plugin project if it touches external infrastructure.
Adding a cross-cutting concern into an existing class instead of a seam is the primary
architecture smell reviewers reject.
