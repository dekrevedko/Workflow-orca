# 01. Orleans Engine — Scope & Architecture

## 1. Scope and positioning

**In scope**: a third engine, `OrcaCore.Engine.Orleans`, that executes **durable** workflows
(regular and saga semantics, exactly as the durable engine defines them) with Orleans grains
providing per-instance serialized execution, activation lifecycle, and multi-silo
distribution. It is **opt-in**: hosts that don't reference it get zero Orleans dependencies
(spec PR-003 stays intact for the rest of the product).

**Out of scope (v1, registered as OOQs where revisitable)**:

- replacing the ephemeral engine — grains add nothing to in-process ephemeral orchestration;
- Orleans grain persistence, `JournaledGrain`, Orleans transactions, Orleans streams;
- dispatched/offloaded long-step execution (OOQ-3) — v1 allows only bounded inline work
  inside a grain turn; production long external work uses a scheduled/outboxed dispatch
  pattern completed by a later durable command;
- Orleans reminders as the timer mechanism (OOQ-1) — v1 reuses the claim-based
  `ITimerScheduler` pump, which is already restart-safe and certified.

**The one-sentence design**: *a grain is the distributed host for one durable advancement
segment.* Everything below the concurrency/hosting layer — the durable interpreter from document 16,
`DurableCommandProcessor`, the aggregate, ports, inbox/outbox, projections, PostgreSQL
provider — is reused **semantically unchanged**. The early Orleans tasks first prove the
low-level command transport; once the durable interpreter exists, the instance grain hosts a
durable advancement segment rather than implementing a second interpreter.

The Orleans package still requires exactly two review-gated seam additions the current code
does not yet expose:

1. **Public delivery dispatch** — the delivery/resume/complete/fail `ProcessAsync`
   overloads are `internal` today; an external engine cannot legally call them. OT1-01a
   implements the approved public seam (per the OT1-00 SEAMS.md decision).
2. **Atomic start reservation** — `IWorkflowStartIdempotencyStore` is lookup-only today;
   OT1-03a adds reserve semantics with the reservation persisted **atomically in the start
   commit boundary** (commit materializer), never as a separate post-start write.

No other `Engine.Durable`/port surface changes are authorized by this package. Durable
interpreter/driver changes belong to document 16 and must be reviewed there before Orleans
rehosts them.

## 2. Execution model: one grain turn = one durable advancement segment

Orleans grains are non-reentrant: one request at a time, held until its `Task` completes,
with a caller-side response timeout (default 30 s). Therefore a grain call **never** runs an
unbounded workflow and **never** awaits an external signal. Each mutating call processes one
durable advancement segment: deserialize input → load checkpoint + tail → invoke the shared
durable interpreter/command kernel → commit each kernel command with expected version and
provider-owned inbox/outbox/projections → return when the segment reaches suspension,
terminal state, poison, or budget.

A segment may contain one kernel command or several sequential kernel command cycles. Each
kernel command remains its own DU-011 atomic commit. The direct command-envelope calls used
by the first Orleans tasks are the degenerate case: one grain turn, one kernel command, one
commit. Reaching a `Wait`/`WaitLong` appends the wait fact and **returns**; the activation
goes idle and may be collected. Wake-ups (matched events, due timers) arrive as new grain
calls.

Consequences (normative — see OE-011..013, OE-030):

- no `[Reentrant]` on the instance grain, ever — it would break CR-040;
- read-only queries use `[ReadOnly]` methods or (preferred) go straight to projections;
- no `Task.Run`/detached continuations touching grain-reachable state;
- no `await Task.Delay` for waits; `TimeProvider` discipline stays in force;
- every grain method takes a `CancellationToken` and flows it through codec → interpreter /
  processor → ports (OE-014) — Orleans propagates caller cancellation to grain calls;
  implementations must observe it;
- Orleans calls are **at-most-once**: a timed-out call may or may not have committed, so
  every command reachable through a grain must be retry-safe (inbox dedup, StartOrGet
  reservation, timer claim/reclaim, continuation idempotence, expected-version append).

## 3. Grain topology and components

```text
Client / host code
  └─ OrleansWorkflowEngine (facade; mirrors durable engine public surfaces 1:1)
       ├─ StartOrGetAsync ─► StartIdempotencyGrain(key) ─► IWorkflowInstanceGrain(instanceId)
       ├─ RaiseEventAsync   (correlation-targeted: resolves to EXACTLY the instances the
       │                     durable matching rules name — via IWorkflowProjectionStore)
       ├─ fanout/broadcast  (definition-targeted: separate surface, mirrors the durable
       │                     engine's own fanout API — never merged with RaiseEvent)
       ├─ queries (status/waits/history) ► IWorkflowProjectionStore (no grain activation)
       └─ mutating management ops (pause/resume/cancel) ► commands through the instance grain

Silo
  ├─ WorkflowInstanceGrain : IGrainWithGuidKey            (one per InstanceId; non-reentrant)
  │    └─ shared durable interpreter / DurableCommandProcessor ─► ports (event store,
  │                                                               inbox, outbox,
  │                                                               projections,
  │                                                               resource pools)
  ├─ StartIdempotencyGrain : IGrainWithStringKey  (key = idempotency key; its single-threaded
  │                          turn makes reserve-or-return-winner atomic cluster-wide, with
  │                          IWorkflowStartIdempotencyStore as the durable backstop)
  ├─ TimerPumpService      (ILifecycleParticipant<ISiloLifecycle>, started at
  │                         ServiceLifecycleStage.Active — NOT a plain BackgroundService;
  │                         ITimerScheduler.ClaimDueAsync → FireTimer command → grain call;
  │                         claim semantics make multi-silo pumping safe)
  └─ OutboxPumpService     (same lifecycle-participant hosting; unchanged at-least-once dispatch)
```

`ITimerScheduler` is the **single durable source of truth for workflow time**: Orleans
grain timers stop on deactivation/silo loss, and Orleans reminders neither replay missed
ticks nor suit sub-minute schedules — neither may carry durable wait semantics (OE-031).

- **Grain key**: the `InstanceId` GUID. Identity = grain identity; activation = cache.
- **Activation (`OnActivateAsync`)**: lightweight — no eager replay; the processor
  rehydrates from checkpoint + stream tail per command (DU-013). Nothing critical in
  `OnDeactivateAsync` (DU-021) — every turn commits before returning, so deactivation has
  nothing pending.
- **Storage is the hot path**: because the grain holds no state between turns, every turn
  pays checkpoint + tail I/O. This is accepted only while per-turn I/O stays bounded
  (OE-023): checkpoint cadence keeps tails short, and Phase O5 load tests must prove the
  provider is not the bottleneck before production. Regaining Orleans' warm-state leverage
  (aggregate cached in the activation, guarded by expected-version append) is OOQ-6.
- **Duplicate-activation defense**: the grain directory may briefly allow two activations
  under cluster instability; correctness rests on expected-version append (DU-022/PR-021) —
  one winner, the loser observes a version conflict and fails its call cleanly.
- **Caller retries**: a timed-out grain call may be retried after the original committed;
  inbox dedup by `EventId` (DU-030) and `StartOrGet` idempotency (DU-053) make this safe.

## 4. Serialization boundary

Domain types (commands, envelopes, results) stay Orleans-free. Grain interfaces carry only
thin **transport records** defined inside `OrcaCore.Engine.Orleans` and annotated
`[GenerateSerializer]` **with explicit `[Id(n)]` on every member** (Orleans version-tolerance
rule: ids are stable forever, members are only ever added, never renumbered); the
command/result payload inside them is a JSON string/bytes produced by the engine's existing
System.Text.Json serialization (PR-016 seams). Orleans codegen therefore never touches
`OrcaCore.Abstractions`/`Core`/`Engine.Durable`, and the banlist for core projects is
untouched. Transport records carry a schema-version field; decoding must handle unknown
command kinds and newer payload versions explicitly (fail fast, never silently drop), and
the test suite includes rolling-upgrade shape tests (OE-060).

C# note: inside namespace `OrcaCore.Engine.Orleans`, qualify framework types as
`global::Orleans.*` where a bare `Orleans.` qualifier would bind to the project namespace.

## 5. Project layout and dependency rules

```text
src/OrcaCore.Engine.Orleans        ← grains, transport envelopes, OrleansWorkflowEngine,
                                            TimerPumpService, silo builder extension
tests/OrcaCore.Engine.Orleans.Tests ← TestingHost-based unit/behavior tests
tests/OrcaCore.Acceptance.Tests     ← gains OE-AC trait tests (Phase O5)
tests/OrcaCore.Integration.Tests    ← gains Postgres multi-silo e2e (Phase O5)
```

Dependency rules (violations fail review):

```text
Engine.Orleans  ← Engine.Durable, Core, Abstractions, Microsoft.Orleans.* (server/sdk/stj)
```

- nothing outside `Engine.Orleans` (+ its tests) may reference an Orleans package;
- `Engine.Orleans` never references a provider project — ports arrive via DI, exactly like
  the other engines;
- no change to any existing port or `Engine.Durable` public type without a review gate
  (lane-bypass entry point is OOQ-2, not an implicit change).

## 6. Stack delta (whitelist addition; everything else in 00-stack-decisions stands)

| Package | Confined to | Purpose |
|---------|-------------|---------|
| `Microsoft.Orleans.Server` | Engine.Orleans | silo hosting, grain runtime |
| `Microsoft.Orleans.Sdk` | Engine.Orleans | codegen for transport records |
| `Microsoft.Orleans.Serialization.SystemTextJson` | Engine.Orleans | STJ integration for envelope payloads |
| `Microsoft.Orleans.TestingHost` | Engine.Orleans.Tests, Integration.Tests | in-memory multi-silo test cluster |
| `Microsoft.Orleans.Clustering.AdoNet` | Integration/e2e only (OOQ-4) | production-shaped clustering over PostgreSQL (`Npgsql` invariant; Orleans 10 ADO.NET guidance uses `Microsoft.Data.SqlClient` only for SQL Server) |

Version: **Orleans 10.x — pin the exact reviewed version (10.2.1 as of 2026-07-04) in
`Directory.Packages.props`; never “latest”.** Version bumps go through the
new-dependency review gate. Dev/test clustering: TestingHost / localhost clustering — no
infrastructure needed. Production requires ≥2 silos and reliable clustering with the
provider's DB artifacts provisioned (membership tables) and matching client configuration —
this is a pre-production architecture gate (OOQ-4), not an afterthought.

## 7. Decisions & open questions register (OOQ)

Protocol: identical to the main register — agents never resolve an OOQ inside a task; a
resolution is recorded here with one line of rationale and a date, at the noted gate.

| # | Question | Resolve at | Constraint while open |
|---|----------|-----------|----------------------|
| OOQ-1 | Replace/augment the claim-based timer pump with Orleans reminders (per-instance durable wake-up, ≥1 min granularity, no missed-tick replay) | Phase O2 exit | Timer pump only; no `Microsoft.Orleans.Reminders` reference in Engine.Orleans |
| OOQ-2 | Add a lane-bypass/segment-host entry point to `DurableCommandProcessor` or the Durable Driver host (grain turn serialization already provides the per-instance host lane; an internal `InstanceLane` pass may be redundant overhead) | Phase O1 exit | Call the existing public durable dispatch path as-is; correctness is unaffected, only queue overhead |
| OOQ-3 | Dispatched long-step execution (`[StatelessWorker]` activity pattern: step-scheduled fact + outbox job + completion command) | **Pre-production gate** — design doc before any production workload with long steps | Inline steps must be bounded and idempotent; long external work is represented by durable scheduled/outboxed work plus a later completion command |
| OOQ-4 | Production clustering provider (AdoNet/PostgreSQL vs Redis), membership DB artifacts, client config | **Phase O3 exit (pre-production architecture gate)**, informed by the OT3-05 spike | TestingHost everywhere except the designated clustering spike/e2e tasks |
| OOQ-5 | Grain-directory strong-consistency options vs default distributed directory | With OOQ-4 | Default directory + expected-version backstop (OE-021) |
| OOQ-6 | Warm aggregate cache inside the activation between turns (regain Orleans state leverage; staleness guarded by expected-version append) | Phase O5, informed by OE-AC-060 load data | Grain holds no state between turns |

## 8. What Orleans explicitly does NOT provide here (agents: do not assume)

Correlation matching, event buffering, inbox dedup, transactional outbox, saga
compensation, versioning/continue-as-new, retention, history projections, management
queries, and definition interpretation — all of these remain the durable core/driver's
responsibility and are reused, not reimplemented. Orleans contributes exactly:
per-instance turn serialization, activation lifecycle/eviction, cluster-wide instance
addressing, and horizontal placement.
