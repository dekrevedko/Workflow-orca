# 16. Durable Driver Requirements (DR)

Scope: the component layer that **executes registered workflow definitions in durable
mode** — the durable interpreter (run-to-suspension executor) and the default in-process
driver host that owns instance wake-up and advancement. This document closes the gap
recorded in the R14 audit (2026-07-05): the durable engine ships a complete command kernel
(command processor, aggregate, ports, inbox/outbox, pumps, DAG runner, management) but no
production component walks a `WorkflowDefinition` and drives it — every durable e2e today
issues kernel commands by hand.

Positioning against the engine family (normative):

- The **durable lane host defined here is the primary durable engine**. It runs in any
  plain .NET host with no cluster dependency, using the same `InstanceLane` concurrency
  the ephemeral engine proves (CR-040/041) plus optimistic-concurrency appends across
  hosts (DU-022).
- The **Orleans engine** ([orleans-engine package](../orleans-engine/README.md)) is an
  **alternative host for the same driver contract** — grains replace lanes and the
  continuation signal; it is opt-in and never a prerequisite. Its `OE-011` "one turn =
  one command; run-to-suspension is one command cycle" is this document's DR-013 stated
  from the grain side.
- The **ephemeral engine** is unaffected; parity expectations are stated in DR-060.

## 16.0 Current-state review (2026-07-05, v3-gpt)

What exists and is reused unchanged:

| Layer | Component | Status |
|---|---|---|
| Kernel | `DurableCommandProcessor` + `DurableWorkflowAggregate` + handlers | Complete; commands cover start, step-completed/failed, yield, waits, timers, events, children, resource pools, external jobs, saga, lifecycle |
| Persistence | Checkpoint (incl. full runtime state after R14 fix), streams, inbox, outbox, projections | Complete; certified per provider |
| Wake-up sources | `OrcaCoreTimerHostedService` (claim-based timer pump), `OrcaCoreOutboxPumpHostedService`, `OrcaCoreOperationalSweepHostedService` | Complete |
| Facade | `DurableWorkflowRuntime` (`RegisterDefinition`, `StartOrGetAsync`) | Start-only |
| Composition | `DurableDagRunner.ScheduleReadyAsync` | Exists but nothing calls it in production |

What is missing (this document's subject):

1. **No durable interpreter** — nothing maps definition nodes and step results to kernel
   commands (`Interpreter<TState>` exists only in `Engine.Ephemeral`).
2. **No execution position persistence** — checkpoints carry `LastStepPath` (diagnostic)
   and an opaque business-state payload, but not the CR-015 frame stack; a rehydrated
   instance cannot know where to resume.
3. **No advancement loop** — after `WorkflowWaitMatchedEvent` or `WorkflowTimerFiredEvent`
   commits, nothing runs the next step.
4. **No restart-safe continuation signal** — if a host dies between committing a resuming
   event and running the continuation, no other host discovers the runnable instance.
5. **Delivery/resume/complete command overloads are `internal`** — acknowledged by the
   Orleans package as seam OT1-01a; the same public seam serves this driver.

## 16.1 Layering

### DR-001 Driver is layered: interpreter vs. host
The durable driver SHALL be two separable layers with a documented boundary:

- **Durable interpreter** — an engine-agnostic component that, given a definition, the
  rehydrated aggregate facts, the persisted execution position, and the business state,
  executes step code to the next suspension point and emits kernel commands. It SHALL NOT
  own threads, timers, queues, or distribution.
- **Driver host** — owns concurrency, scheduling, wake-up, and distribution, and invokes
  the interpreter. The default host is the in-process lane host (16.3). Alternative hosts
  (Orleans grains) SHALL reuse the interpreter unchanged.

### DR-002 Kernel reuse without semantic forks
The driver SHALL drive execution exclusively through the existing durable command kernel
(`DurableCommandProcessor` decisions, DU-011 command → events → commit). The driver SHALL
NOT append events, write checkpoints, or touch provider ports directly, and SHALL NOT
duplicate aggregate decision logic.

### DR-003 Public dispatch seam
The delivery/resume/complete/fail command entry points needed by any driver host SHALL be
exposed through one reviewed public seam (the OT1-01a decision). The lane host and the
Orleans host SHALL consume the same seam; no host gets private kernel access.

## 16.2 Durable interpreter

### DR-010 Definition interpretation in durable mode
The interpreter SHALL support every definition shape the builder can produce for durable
execution: `Init`, business steps, `If`/`While`, `Parallel`/`WhenAll`/`WhenFirst`,
`Wait`/`WaitLong` (with timeout races), timers/delays, `ForEach`, `Yield`,
`RunChild`/`RunChildren`, saga definitions (16.4), `End` outcomes, and continue-as-new.
Shapes not yet supported SHALL fail fast at registration with a capability diagnostic
(DU-002 feature matrix), never at mid-flight runtime.

### DR-011 Step results map to kernel commands
Step outcomes SHALL map onto kernel commands exactly:

| Step result / node | Command(s) |
|---|---|
| `Completed` at a commit boundary | `DurableStepCompletedCommand` (carries serialized business state + execution position; produces the checkpoint) |
| `Failed` / unhandled exception | `DurableStepFailedCommand` (saga scopes: compensation path per SG rules instead) |
| `WaitForEvent` / builder `Wait` | `DurableWaitRegisteredCommand` |
| Timer/delay node | `ScheduleTimerCommand` |
| `Yield` | `DurableYieldCommand` + continuation signal (DR-034) |
| `RunChild`/`RunChildren` node | `DurableRunChildCommand` / `DurableRunChildrenCommand` |
| `End` | `DurableCompleteCommand` |
| Continue-as-new | `ContinueAsNewCommand` |

### DR-012 Execution position is durable
The interpreter SHALL persist the CR-015 execution position (frame stack: current node
path plus enclosing container frames, branch progress, loop iteration state, ForEach
dispatch cursor) at every commit boundary, versioned and serialized alongside the business
state inside the checkpoint payload envelope. Rehydration SHALL resume from the persisted
position without re-executing steps whose completion committed. The envelope format SHALL
be versioned for forward evolution (DU-041 applies).

### DR-013 Run-to-suspension is one command cycle
One interpreter invocation SHALL run from the current position through consecutive
synchronous-completing steps to the next suspension point (wait, timer, yield, child
dispatch, terminal) and commit once per completed step (the step-completed checkpoint is
the commit boundary; a multi-step segment produces one commit per step, all inside one
host-side scheduling unit). An invocation SHALL never await external signals in memory:
reaching a wait registers the fact and returns (matches OE-011 from the grain side).

### DR-014 Execution semantics are at-least-once; commits are exactly-once
The driver SHALL document and preserve the kernel guarantee: step **effects** are
at-least-once (a crash after side effects but before commit re-runs the step from the last
committed position); committed **transitions** are exactly-once (expected-version append +
inbox dedup). The step-authoring contract SHALL state that non-idempotent external work
belongs in external jobs or Yield-chunked steps.

### DR-015 Determinism contract at the driver boundary
Interpreter decisions SHALL be deterministic functions of (definition version, committed
facts, persisted position, business state) — CR-012 restated at this layer. Wall-clock
reads go through `TimeProvider`; identifiers created during a segment are committed with
it; the interpreter never consults mutable ambient state.

### DR-016 Version binding at resume
Every resume SHALL bind to the definition version recorded at start (DU-040). If the
registered definition for that version is absent or incompatible, the instance SHALL park
with a version-binding diagnostic (DU-041: no silent corruption), visible via management.

## 16.3 Default in-process lane host (the primary durable engine)

### DR-030 Lane-serialized advancement
The lane host SHALL serialize all advancement per instance through `InstanceLane`
(CR-040/041 layering: in-process lane first, expected-version append as the cross-process
guard, DU-022). Conflict outcomes from concurrent hosts SHALL be handled as retry/reload
signals, never surfaced as instance failures.

### DR-031 Advancement triggers
The lane host SHALL schedule an interpreter invocation when:

- a start commits (first segment),
- a committed command unblocks the instance: wait matched, timer fired, child completed
  (resume token consumed), resume from pause, external job completed, resource-pool grant,
- a yield commits (continuation of the same step),
- the continuation pump (DR-034) claims a runnable instance after a crash.

### DR-032 Facade completes the engine surface
`DurableWorkflowRuntime` SHALL grow to the full engine facade: `RegisterDefinition`,
`StartOrGetAsync` (existing), `RaiseEventAsync` (instance-targeted, correlation-targeted,
definition-fanout — EV routing modes with durable inbox dedup), and access to
`DurableManagement`. Public API shape SHOULD mirror the ephemeral engine where semantics
allow, so a WorkflowCore-style migration swaps engines without redesign.

### DR-033 Host lifecycle integration
`AddOrcaCoreHostedServices` SHALL host the lane driver: bounded worker concurrency,
graceful drain on shutdown (in-flight segment finishes its current commit, nothing new is
claimed), failure boundary + backoff consistent with existing pumps, `TimeProvider`
discipline throughout.

### DR-034 Restart-safe continuation signal
Every commit that leaves an instance runnable SHALL produce a durable continuation signal
inside the same commit boundary, so that a host crash between commit and continuation
never strands an instance. The signal SHALL be claim-based with lease recovery and SHALL
be satisfied cheaply in-process on the happy path (the committing host continues
immediately; the claim is then a no-op). RECOMMENDED mechanism: a dedicated outbox record
kind (`continue`) consumed by a continuation pump, reusing the certified at-least-once
outbox machinery — a new port is NOT justified. Duplicate continuations SHALL be harmless
(the interpreter reloads committed position and finds nothing to do).

### DR-035 Multi-host scale-out story
Multiple lane hosts against one store SHALL be correct by construction (expected-version
append) and SHALL distribute work via the claim-based pumps (timers, outbox, continuation).
The documentation SHALL state the contention model honestly: hot single-instance
contention across hosts resolves by conflict-retry, not ownership; workloads needing
cluster-wide single activation are the Orleans host's territory (DU-060 direction).

### DR-036 Poison and stuck handling
A continuation that repeatedly fails (interpreter exception, definition resolution
failure) SHALL follow the poison path: bounded retries with backoff, then park the
instance with diagnostics (status + error summary via existing management), never a hot
crash loop in the pump. Stuck detection (MG) SHALL cover parked instances.

## 16.4 Saga and DAG driving

### DR-040 Saga driving
For saga definitions the driver SHALL execute forward actions and record each via
`RecordSagaForwardActionCompletedCommand`; on a forward failure it SHALL request
compensation (`RequestSagaCompensationCommand`), execute compensation steps in the
kernel-planned reverse order, and record each outcome
(`CompleteSagaCompensationCommand`/`FailSagaCompensationCommand`) — all SG semantics stay
kernel-owned; the driver contributes only step execution and command sequencing.
Compensation SHALL survive restart at any point (the R14 checkpoint fix plus DR-034 make
this testable end-to-end).

### DR-041 DAG driving
The driver SHALL own the DAG drive loop: on each committed child completion for a DAG
root, invoke `DurableDagRunner.ScheduleReadyAsync` with node status reconstructed from
committed facts, until the plan is complete or blocked (transitive failure closure).
No test or host application code should need to pump a DAG manually.

## 16.5 Observability and options

### DR-050 Driver telemetry
The driver SHALL emit through the existing observer/OB surfaces: segment duration
(distinct from OB step-command duration), continuation lag (commit → continuation start —
the direct health signal for DR-034), park/poison counts, and version-binding failures.
Instrument names follow OB-020 naming.

### DR-051 Options
Worker concurrency, continuation pump interval/batch/lease, turn/segment budget warning
threshold, and poison retry policy SHALL be options on the existing hosted-service options
surface, validated on start.

## 16.6 Parity and migration

### DR-060 Semantic parity with the ephemeral engine
A definition using only mode-shared shapes SHALL produce the same terminal outcome and
observable lifecycle sequence on both engines (differences only in guarantees: durability,
restart survival, cold waits). The feature matrix (DU-002) SHALL gain a "durable driver"
column; every gap is explicit.

### DR-061 WorkflowCore replacement fitness
With DR-032 complete, the documented migration story (register definitions, start by id,
raise events, manage instances) SHALL be achievable against the durable engine without
hand-written kernel command code. The samples SHALL include one durable end-to-end sample
(start → wait → event → complete across a host restart) using only public surfaces.

## 16.7 Acceptance criteria

Trait format: `[Trait("AC", "DR-AC-0xx")]`, consistent with document 12.

- **DR-AC-001** Register + start a step→wait→step→end definition on the lane host; deliver
  the event; instance completes. No kernel commands issued by the test.
- **DR-AC-002** Same flow, host process replaced between wait registration and event
  delivery (new host, same store): completes identically (DU-013).
- **DR-AC-003** Kill the host after the wait-matched commit but before continuation; a
  second host's continuation pump completes the instance within the pump interval
  (DR-034).
- **DR-AC-004** Redeliver the same event and duplicate a continuation claim: exactly one
  step execution commits (inbox dedup + DR-034 idempotence).
- **DR-AC-005** Yield-chunked step: crash mid-chunk; committed chunks are not re-executed,
  the step resumes from the last committed chunk (CR-017 in durable mode).
- **DR-AC-006** Position fidelity: nested `Parallel`-inside-`If` with one branch waiting;
  restart; the resumed instance continues the correct branch only (DR-012).
- **DR-AC-007** Saga: forward failure after N committed forward actions with a restart
  during compensation; all N compensations run in reverse order exactly once (DR-040).
- **DR-AC-008** DAG: diamond plan driven to completion by the driver alone; a node failure
  marks dependents blocked via transitive closure; no manual pumping (DR-041).
- **DR-AC-009** Version binding: resume with the definition version unregistered parks the
  instance with a diagnostic; registering the version and retrying resumes it (DR-016).
- **DR-AC-010** Two lane hosts, one store, 100 instances: all complete, no duplicated step
  commits, conflicts resolved internally (DR-030/035).
- **DR-AC-011** Graceful shutdown mid-segment: current commit finishes, nothing new
  claimed, instance completes on the surviving host (DR-033).
- **DR-AC-012** Ephemeral/durable parity: the same definition produces the same outcome
  and lifecycle event names on both engines (DR-060).

## 16.8 Phasing

Fits document 13 as the missing bridge between Slice 2 (durable core — done) and the
"engine complete" claim; the Orleans package's O1+ phases then re-host the same
interpreter:

1. **DR-P1 Interpreter + position envelope** (DR-010..016) — hardest correctness work;
   gate: DR-AC-001/002/005/006 on the in-memory provider.
2. **DR-P2 Lane host + continuation signal** (DR-030..036, DR-003 seam) — gate:
   DR-AC-003/004/010/011 incl. PostgreSQL.
3. **DR-P3 Saga + DAG driving** (DR-040/041) — gate: DR-AC-007/008.
4. **DR-P4 Facade, parity, samples, telemetry** (DR-032, DR-050/051, DR-060/061) — gate:
   DR-AC-009/012, sample e2e, feature-matrix update.

Open questions (register per document 13 conventions before DR-P1 starts):

- **DR-OQ-1** Position envelope format: extend the checkpoint payload (business state +
  position in one versioned envelope) vs. a parallel `RuntimeState` field. Leaning:
  payload envelope — position and business state must commit atomically and are read
  together.
- **DR-OQ-2** Continuation signal: outbox kind (recommended, reuses certified machinery)
  vs. dedicated runnable-queue port (only if outbox contention measurably hurts).
- **DR-OQ-3** Inline step budget on the lane host: adopt the Orleans turn-budget guidance
  (OE-012) as a warning threshold, or leave unbounded with diagnostics only.
