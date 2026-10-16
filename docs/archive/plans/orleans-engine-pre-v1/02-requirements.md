# 02. Orleans Engine Requirements (OE)

Normative spec for `OrcaCore.Engine.Orleans`. Where a requirement restates a product-spec
guarantee, the original ID is cited; the original remains authoritative for semantics.

## 2.1 Positioning

### OE-001 Opt-in engine, zero contamination
The Orleans engine SHALL be a separate project consumed only by hosts that choose it.
No existing project (`Abstractions`, `Core`, `Engine.Durable`, `Engine.Ephemeral`,
providers, `Hosting`) SHALL gain an Orleans dependency, attribute, or conditional branch.

### OE-002 Guarantee parity
Workflows executed on the Orleans engine SHALL observe exactly the durable-mode semantics:
DU-010..013 (event-sourced core, command write path, deterministic rehydration), DU-020..022
(committed-state-only recovery, safe-boundary persistence, serialized execution), DU-030..033
(inbox/outbox), DU-040/053 (version binding, StartOrGet), EV wait/correlation rules. Any
behavioral divergence is a defect, not an engine characteristic. Parity cuts both ways:
capabilities that are incomplete in the durable core today (e.g. open saga/DAG remediation
items) are equally incomplete here — this package does not close durable-core gaps, and no
Orleans task may quietly implement a durable feature the durable engine lacks. Once document
16's durable interpreter and lane-host contract exists, Orleans SHALL host that same
interpreter rather than inventing a second definition interpreter.

### OE-003 Dependency confinement
Orleans packages SHALL appear only in `OrcaCore.Engine.Orleans` and test projects, per the
whitelist in [01-architecture.md §6](01-architecture.md). The repo banlist stays in force.

## 2.2 Grain execution model

### OE-010 One grain per instance
Each workflow instance SHALL be represented by exactly one logical grain keyed by the
`InstanceId` GUID. All mutating operations for an instance SHALL flow through its grain.

### OE-011 Turn = one durable advancement segment
Every mutating grain call SHALL process one durable **advancement segment** for one
instance and return after the segment reaches a suspension point, terminal state, poison
condition, or configured budget. A segment MAY execute zero or more kernel command cycles
sequentially; each kernel command still commits through `DurableCommandProcessor` with its
own atomic durable commit boundary. A direct command-envelope call is the degenerate case:
one segment containing one kernel command.

A grain call SHALL NOT await external signals, hold in-memory waits, or run an unbounded
workflow. Reaching `Wait`/`WaitLong` registers the wait fact, commits it, and returns; a
matched event, fired timer, or management resume arrives as a later grain call.

### OE-012 Turn budget
A turn SHALL complete well inside the Orleans response timeout. The budget applies to the
**whole advancement segment**, not per individual step or per individual kernel command.
Inline step execution is permitted for **bounded** steps only (guideline: p99 < 5 s per
turn); long-running external work is a poor fit for grain turns and SHALL, for production
workloads, use the dispatched-step pattern (scheduled/outboxed job completed by a later
command — OOQ-3 gates production readiness).

The Orleans host SHALL respect the durable-driver hard segment budgets (`MaxCommandsPerSegment`
and `MaxSegmentDuration`) when the shared interpreter exposes them. When a budget is
reached and the instance remains runnable, the turn yields by leaving a durable continuation
rather than increasing the Orleans response timeout. The engine SHALL emit a diagnostic when
a warning threshold or hard budget is reached.

### OE-013 No reentrancy on mutation paths
The instance grain SHALL NOT be `[Reentrant]`. Interleaving is permitted only on read-only
query methods explicitly marked `[ReadOnly]`. Grain code SHALL NOT schedule work outside the
Orleans task scheduler that touches grain-reachable state.

### OE-014 Cancellation propagation
Every grain method SHALL accept a `CancellationToken` and observe it: the token flows
through codec, processor, provider calls, and pump deliveries. Cancellation mid-turn SHALL
never produce a partially observable mutation (the commit boundary already guarantees this;
the token must not be swallowed before it).

### OE-015 At-most-once calls, retry-safe commands
Because Orleans grain calls are at-most-once (a timed-out call may or may not have
committed), every command reachable through a grain SHALL be safe to retry: start via the
idempotency reservation (OE-042), deliveries via inbox dedup (OE-041), timers via the
claim contract (OE-031), everything via expected-version append (OE-021).

## 2.3 Persistence and truth

### OE-020 Ports are the only truth
Workflow state SHALL persist exclusively through the existing provider ports
(`IWorkflowEventStore`, inbox/outbox/projection/idempotency/retention stores). Orleans grain
persistence (`IPersistentState`, `JournaledGrain`) SHALL NOT store workflow truth. In-memory
grain fields are disposable cache only (DU-010 “hot memory is a disposable cache”).

### OE-021 Expected-version backstop
Correctness under duplicate activations, split membership, or racing callers SHALL rest on
expected-version append (DU-022, PR-021): exactly one winner per version; the losing turn
surfaces a version-conflict result and SHALL NOT retry blindly inside the grain.

### OE-022 Cheap activation
`OnActivateAsync` SHALL NOT perform eager stream replay or other heavy I/O; rehydration
happens per command through the aggregate loader (checkpoint + tail, DU-013).

### OE-023 Bounded per-turn I/O
Because the grain holds no state between turns, storage is on every turn's path. Per-turn
I/O SHALL stay bounded: checkpoint cadence keeps stream tails short (existing DU-042/052
pressure controls apply), and a load test (OE-AC-060) SHALL demonstrate sustained
throughput without the provider becoming the bottleneck before any production sign-off.
Caching the aggregate inside the activation is OOQ-6, not an implicit optimization.

## 2.4 Waits, timers, wake-up

### OE-030 Waits are records, never awaits
Reaching `Wait`/`WaitLong` SHALL append the wait fact, return the turn, and leave the
activation eligible for idle collection. Resume SHALL occur only via a new grain call
carrying a command (matched event, fired timer, or a management command such as resume
after pause).

### OE-031 Timer scheduler is the source of truth for durable time
`ITimerScheduler` (claim-based, exactly-once claim per due timer) SHALL be the **single
durable source of truth** for workflow timers and wait timeouts. A silo-hosted pump SHALL
claim due `FireTimerCommand`s and deliver each to its instance grain. Multiple silos MAY
pump concurrently; the claim contract prevents double firing. Orleans grain timers
(activation-local, lost on deactivation/crash) and Orleans reminders (no missed-tick
replay, unsuitable for high-frequency schedules) SHALL NOT carry durable wait semantics;
reminders remain excluded entirely while OOQ-1 is open.

### OE-032 Restart-safe wake-up
Timers and cold waits SHALL survive: single-silo restart, full-cluster restart, and grain
deactivation. After downtime, due timers fire late-but-once (existing scheduler contract).

### OE-033 Claimed timers must not be lost
A wake-up claimed from `ITimerScheduler` but not yet committed as a `FireTimer` outcome
SHALL NOT be lost when the pump crashes or the grain call fails/times out between claim
and commit. The mechanism (claim lease/expiry with reclaim, or pump retry-until-commit)
follows the existing scheduler contract — OT2-00 SHALL verify what the port/provider
actually guarantees and escalate a seam gap if the answer is "nothing". Because delivery
may therefore repeat, applying a `FireTimer` command SHALL be idempotent (a second apply
for the same timer is a no-op, consistent with OE-015).

## 2.5 Routing, delivery, idempotency

### OE-040 Correlation routing outside the grain; split delivery surfaces
Inbound external events SHALL be resolved to target instance ids via projections
(EV-011 routing data) **before** any grain call; the grain receives only commands addressed
to its own instance. Unmatched events follow existing buffering/discard semantics unchanged.
The facade SHALL keep the durable engine's delivery surfaces **separate**:
correlation-targeted delivery resolves to exactly the instances the durable matching rules
name (never an implicit broadcast), and definition-targeted fanout — where the durable
engine exposes such a surface — is a distinct operation mirroring it 1:1 (where it does
not, the Orleans facade SHALL NOT invent one). One combined "raise event" method that
mixes both semantics is non-conforming.

### OE-041 Inbox dedup unchanged
Delivery through grains SHALL preserve DU-030: duplicate `EventId` after any combination of
retries, activations, or silo moves yields `DuplicateIgnored`, never a second committed outcome.

### OE-042 Cluster-safe idempotent start
`StartOrGetAsync` SHALL be idempotent **atomically across silos and clients** — the
existing `DurableStartService` process-local lock/cache is insufficient under Orleans'
at-most-once calls and multi-silo hosting. Two layers, both mandatory:

- **Serialization**: a `StartIdempotencyGrain` keyed by the idempotency key serializes
  racing callers cluster-wide (single-threaded turn = reserve-or-return-winner).
- **Durability**: the reservation SHALL be persisted **atomically within the start commit
  boundary** (materialized alongside the started/version-binding facts by the commit
  pipeline) — never as a separate write before or after the start commit, which is
  crash-prone. The store port must therefore support atomic reserve-or-return (OT1-03a);
  a lookup-then-record sequence is non-conforming.

A duplicate activation of the idempotency grain, a crash between call and commit, or any
retry ordering SHALL still converge every caller on one `InstanceId` (DU-053).

## 2.6 Lifecycle and operations

### OE-050 No critical work in deactivation
`OnDeactivateAsync` SHALL contain best-effort cleanup only (DU-021). The engine SHALL be
correct if the hook never runs.

### OE-051 Idle collection is normal operation
Activation collection of waiting/idle instances SHALL remain enabled (Orleans default
policy, or the idle age configured via the OT4-01 options) and SHALL NOT affect observable
behavior (OR-001 rehydration).

### OE-052 Management queries bypass grains
Instance queries, wait inspection, history, and statistics SHALL read projections directly
(DU-070) and SHALL NOT force grain activations.

### OE-053 Observability parity
The engine SHALL emit diagnostics per existing conventions (BCL `ActivitySource`/`Meter`
naming as resolved in IOQ-5): per-turn command outcome, turn duration, activation counts,
timer-pump and version-conflict counters.

## 2.7 Serialization boundary

### OE-060 Transport envelopes with version tolerance
Grain interfaces SHALL exchange only transport records defined in `Engine.Orleans`,
annotated `[GenerateSerializer]` with explicit, permanently stable `[Id(n)]` on every
member (add-only evolution), carrying command/result payloads as JSON produced by the
engine's existing STJ serialization (PR-016). Envelopes SHALL carry a schema-version field;
unknown command kinds and newer payload versions SHALL fail fast with diagnostics; the test
suite SHALL include rolling-upgrade shape tests (old-shape envelope decoded by new code).
Domain types SHALL NOT gain Orleans attributes.

## 2.8 Hosting and composition

### OE-070 Single composition entry point
One silo-builder extension (`UseOrcaCoreOrleans(...)`-style) SHALL register grains, the
shared `DurableCommandProcessor`, both pumps, and options. Ports are supplied by the host
exactly as for the durable engine (in-memory or PostgreSQL providers).

### OE-072 Pumps are silo lifecycle participants
The timer and outbox pumps SHALL be hosted as `ILifecycleParticipant<ISiloLifecycle>`
starting at `ServiceLifecycleStage.Active` and stopping through the silo lifecycle — never
as plain `BackgroundService`s (which can run before the silo can serve grain calls and
outlive its shutdown). The existing reusable pump internals are wrapped, not modified.

### OE-071 Facade surface parity
`OrleansWorkflowEngine` SHALL mirror the durable engine's public operation surface
(start-or-get, raise event, management operations) with identical result types, so switching
engines changes guarantees/deployment, not the programming model (spec 1.3 promise).

## 2.9 Testing

### OE-080 TestingHost-first
Engine behavior tests SHALL run on `Microsoft.Orleans.TestingHost` clusters with in-memory
providers; no test may require external infrastructure except the designated Testcontainers
tasks (PostgreSQL e2e).

### OE-081 Certification parity subset
The durable acceptance criteria that express engine-observable semantics (wait/resume,
dedup, StartOrGet, rehydration, version binding) SHALL be executed against the Orleans
engine and pass unmodified in expectation (test plumbing may differ).

### OE-082 Capstone e2e
The package is complete only when [OE-AC-050](03-acceptance-criteria.md) and
[OE-AC-051](03-acceptance-criteria.md) pass: the
customer-approval driving scenario on a restarted multi-silo cluster, in-memory and
PostgreSQL-backed variants.
