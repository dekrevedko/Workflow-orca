# 16. Durable Driver Requirements (DR)

Scope: the component layer that **executes registered workflow definitions in durable
mode** — the durable interpreter (run-to-suspension executor) and the default in-process
driver host that owns instance wake-up and advancement. This document was opened to close
the gap recorded in the R14 audit (2026-07-05): at that audit baseline the durable engine
shipped a complete command kernel (command processor, aggregate, ports, inbox/outbox, pumps,
DAG runner, management), but no production component walked a `WorkflowDefinition` and
drove it. Sections 16.1 onward are normative regardless of subsequent implementation status.

Positioning against the engine family (normative):

- The **durable lane host defined here is the primary durable engine**. It runs in any
  plain .NET host with no cluster dependency, using the same `InstanceLane` concurrency
  the ephemeral engine proves (CR-040/041) plus optimistic-concurrency appends across
  hosts (DU-022).
- The **Orleans engine** ([orleans-engine package](../orleans-engine/README.md)) is an
  **alternative host for the same driver contract** — grains replace the in-process lane
  and may satisfy the continuation signal through grain calls; it is opt-in and never a
  prerequisite. Its `OE-011` turn model SHALL be stated as "one grain turn = one durable
  advancement segment". Individual kernel commands remain the atomic commit unit inside
  that segment.
- The **ephemeral engine** is unaffected; parity expectations are stated in DR-060.

## 16.0 Audit baseline (2026-07-05, v3-gpt)

This section is a historical baseline, not a current implementation ledger. Subsequent
implementation evidence and remaining gaps are tracked in
[`v3-gpt/docs/durable-driver-audit.md`](../../v3-gpt/docs/durable-driver-audit.md).

What exists and is reused. The kernel processor, aggregate, persistence, wake-up
sources, facade, and DAG runner are reused without behavioral forks. The kernel
**command contracts are extended, not reused unchanged**: several existing commands did
not carry the post-step business state and durable execution position that DR-011 and
DR-012 require, so their record shapes SHALL change (additive fields). This is called
out explicitly in DR-011a; "reused" below means the components and decision logic are
kept, not that every command DTO is frozen.

| Layer | Component | Status |
|---|---|---|
| Kernel | `DurableCommandProcessor` + `DurableWorkflowAggregate` + handlers | Complete; commands cover start, step-completed/failed, yield, waits, timers, events, children, resource pools, external jobs, saga, lifecycle. Command **contracts extended** per DR-011a to carry state + execution position |
| Persistence | Checkpoint (incl. full runtime state after R14 fix), streams, inbox, outbox, projections | Complete; certified per provider |
| Wake-up sources | `OrcaCoreTimerHostedService` (claim-based timer pump), `OrcaCoreOutboxPumpHostedService`, `OrcaCoreOperationalSweepHostedService` | Complete |
| Facade | `DurableWorkflowRuntime` (`RegisterDefinition`, `StartOrGetAsync`) | Start-only |
| Composition | `DurableDagRunner.ScheduleReadyAsync` | Exists but nothing calls it in production |

What was missing at the audit baseline (this document's subject):

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
The interpreter SHALL support every definition shape valid for **durable** execution:
`Init`, business steps, `If`/`While`, `Parallel`/`WhenAll`/`WhenFirst`, `Wait`/`WaitLong`
(with timeout races), timers/delays, `Yield`, `RunChild`/`RunChildren`, saga definitions
(16.4), `End` outcomes, and continue-as-new.

Lightweight `ForEach` is the ephemeral engine's in-instance fanout primitive (CP-010..013)
and SHALL NOT be accepted by the durable driver until a separate durable `ForEach` semantic
is specified. Durable fanout is expressed through `RunChild`/`RunChildren` and DAG-owned
child scheduling. Any definition shape not supported by the durable driver SHALL fail fast
at registration with a capability diagnostic (DU-002 feature matrix), never at mid-flight
runtime.

### DR-011 Step results map to kernel commands
Step outcomes SHALL map onto kernel commands exactly. `StepResult` objects still carry
control intent only (CR-011); the driver-generated command emitted after step execution
carries the post-step business state and next durable execution position whenever user step
code may have mutated state.

| Step result / node | Command(s) |
|---|---|
| `Completed` at a commit boundary | `DurableStepCompletedCommand` carrying serialized business state + execution position; produces the checkpoint |
| `Failed` / unhandled exception after policy exhaustion | `DurableStepFailedCommand` carrying the last committed business state + execution position and failure diagnostics; uncommitted mutations from a failed attempt are discarded; saga scopes enter the compensation path per SG rules instead |
| `WaitForEvent` / builder `Wait` | `DurableWaitRegisteredCommand` carrying post-step business state + execution position when the wait is produced by step code or follows a state-mutating node |
| Timer/delay node | `ScheduleTimerCommand` carrying the current execution position; state is checkpointed if the preceding node mutated it |
| `Yield` | `DurableYieldCommand` carrying post-chunk business state + same-step execution position, plus continuation signal (DR-034) |
| `RunChild`/`RunChildren` node | `DurableRunChildCommand` / `DurableRunChildrenCommand` carrying parent state + group/join execution position |
| `End` | `DurableCompleteCommand` carrying terminal state metadata, final business-state checkpoint, and execution position |
| Continue-as-new | `ContinueAsNewCommand` carrying the replacement state envelope and lineage metadata |

### DR-011a Required kernel contract changes
The mapping in DR-011 requires additive fields on command records that did not carry
post-step business state or execution position at the audit baseline. These contracts SHALL
be extended (backward-compatibly where a versioned envelope allows), and this document is
the authority that supersedes any "commands unchanged" reading of 16.0:

- `StartWorkflowCommand` and `WorkflowStartedEvent` SHALL carry the serialized start input
  and its content type so `Init` can reconstruct input after a crash between the start commit
  and the first interpreter segment (DR-018).
- `DurableStepCompletedCommand` already carries `StepPath` + serialized state; it SHALL
  additionally carry the versioned execution-position envelope (DR-012).
- `DurableStepFailedCommand` SHALL carry the last committed business state, execution
  position, and failure diagnostics after applicable policies are exhausted. Mutations made
  by an unsuccessful attempt SHALL NOT become the input to a retry or terminal checkpoint
  unless a future explicitly-authored policy defines different semantics (DR-019).
- `DurableWaitRegisteredCommand` SHALL carry post-step business state + execution position
  when the wait follows a state-mutating node.
- `ScheduleTimerCommand` SHALL carry the current execution position (the baseline shape
  carried only `TimerId`, `FireAt`, `WakeupName`), and the preceding node's state checkpoint
  when it mutated state.
- `DurableCompleteCommand` SHALL carry terminal state metadata + the final business-state
  checkpoint + execution position (the baseline shape carried only `OutcomeName`).
- `DurableYieldCommand` and `DurableRunChild`/`DurableRunChildrenCommand` SHALL carry the
  business state + execution position described in the DR-011 table.

The envelope carried by these commands is the same versioned checkpoint payload envelope
defined in DR-012, so state and position always commit atomically.

### DR-012 Execution position is durable
The interpreter SHALL persist the CR-015 execution position (frame stack: current node path
plus enclosing container frames, branch progress, loop iteration state, child-group join
state, saga scope/compensation cursor, and continuation/yield cursor) at every commit
boundary, versioned and serialized alongside the business state inside the checkpoint
payload envelope. Rehydration SHALL resume from the persisted position without
re-executing steps whose completion committed. The envelope format SHALL be versioned for
forward evolution (DU-041 applies).

If a checkpoint without the required execution-position envelope is loaded, the driver SHALL
NOT guess. It SHALL either migrate the envelope deterministically under an explicitly coded
migration rule or park the instance with a clear runtime-state-version diagnostic visible
through management.

### DR-013 Advancement segment and commit cadence
One interpreter invocation SHALL process one **advancement segment**: it starts from the
persisted position and may execute zero or more kernel command cycles sequentially until it
reaches a suspension point (wait, timer, yield, child dispatch), terminal state, poison
condition, or configured segment budget (DR-051). Each kernel command still has exactly one
atomic durable commit. A multi-step segment therefore produces one commit per completed
step or suspension-producing node, all inside one host-side scheduling unit.

An invocation SHALL never await external signals in memory. Reaching a wait registers the
fact, commits, and returns. A blocked wait does not emit a continuation signal; only a commit
that leaves the instance runnable does so (DR-034). From the Orleans side this same rule is
stated as `OE-011`: one grain turn equals one durable advancement segment, not one whole
workflow.

### DR-014 Execution semantics are at-least-once; commits are exactly-once
The driver SHALL document and preserve the kernel guarantee: step **effects** are
at-least-once (a crash after side effects but before commit re-runs the step from the last
committed position); committed **transitions** are exactly-once (expected-version append +
inbox dedup). `Yield` is safe for chunked work only when each chunk is idempotent or guarded
by an application-level idempotency key. Non-idempotent externally observable work belongs
in external jobs/outbox-backed dispatch, not inline step code.

### DR-015 Determinism contract at the driver boundary
Interpreter decisions SHALL be deterministic functions of (definition version, committed
facts, persisted position, business state) — CR-012 restated at this layer. Wall-clock
reads go through `TimeProvider`; identifiers created during a segment are committed with
it; the interpreter never consults mutable ambient state.

### DR-016 Version binding at resume
Every resume SHALL bind to the definition version recorded at start (DU-040). If the
registered definition for that version is absent or incompatible, the instance SHALL park
with a version-binding diagnostic (DU-041: no silent corruption), visible via management.

### DR-017 Parked is a defined lifecycle state
"Park" is a first-class, durable-only, **non-terminal** lifecycle state — distinct from the
cooperative `Paused` state (which resumes by replaying buffered deliveries) because a parked
instance is held on an unresolved fault and is not automatically runnable. It SHALL be
represented by a dedicated `Parked` value added to `WorkflowStatus` (durable-only, like
`Paused`; the ephemeral engine never produces it). This is one of the additive kernel
contract changes tracked under DR-011a.

Parking SHALL be entered only from the three sites named in this document — checkpoint
runtime-state-version mismatch (DR-012), version-binding failure (DR-016), and poison /
repeated continuation failure (DR-036) — and SHALL record a required **park diagnostic**:

- a park reason category (`RuntimeStateVersion`, `VersionBinding`, `Poison`),
- an operator-readable error summary,
- the park timestamp and the failed-attempt count that preceded parking,
- the persisted execution position/version at park time (never a guessed position).

Behavior and retry semantics:

- A parked instance SHALL NOT be claimed for automatic advancement. The continuation pump
  (DR-034) SHALL skip parked instances so parking never produces a hot retry loop.
- The park diagnostic SHALL be surfaced through `DurableManagement` and be queryable by
  status (a `Parked` filter on the projection query, DU-070).
- Re-arming a parked instance SHALL be an explicit, expected-version management operation
  (`RearmAsync` or an equivalent reviewed public seam) that lowers to one kernel command and
  commits the unpark fact plus a fresh continuation signal atomically. Registering a
  definition or installing a migration only satisfies a precondition; neither action alone
  mutates parked instances.
- Re-arm preconditions SHALL depend on the park reason: `VersionBinding` requires the bound
  definition version to resolve compatibly; `RuntimeStateVersion` requires a completed,
  validated envelope migration; `Poison` requires an operator acknowledgement after the
  underlying fault or configuration is addressed. The kernel SHALL reject re-arm while the
  applicable precondition is false. A successful re-arm clears the park diagnostic and
  resumes from the persisted position, never a guessed one.
- Stuck detection (MG-040) SHALL include parked instances (DR-036).

### DR-018 Start input survives the first-segment crash window
The start commit SHALL durably record the serialized start input and its content type before
`Init` can execute. The same atomic start commit SHALL leave a continuation signal when the
instance is runnable. A replacement host SHALL reconstruct the exact input from committed
facts and execute `Init` without caller-held memory. Input serialization/version failures
SHALL reject start before a runnable instance is committed. Retention or redaction of start
input after the first business-state checkpoint is an explicit retention-policy concern and
MUST NOT weaken replay within the active recovery window.

### DR-019 Durable policy execution
The durable interpreter SHALL apply the policy decorators required by CR-006 before mapping
a final step result through DR-011. At minimum:

- a retry's `MaxAttempts` includes the initial attempt; retry predicates and terminal
  conditions are deterministic, and failed-attempt mutations are discarded so each retry
  starts from the last committed business state;
- retry backoff that crosses a segment boundary uses a durable timer/continuation deadline,
  not an in-memory delay, and survives host replacement without resetting the attempt count;
- timeout deadlines and outcomes follow EV-052, use `TimeProvider`, and remain anchored to
  committed time across restart; cooperative cancellation is delivered through the step's
  `CancellationToken`, then committed according to CR-031 and the authored policy;
- idempotency metadata derives a stable logical-operation key from committed workflow
  generation + execution position; retry attempt number is separate diagnostic metadata and
  does not change that key. This does not upgrade inline external side effects beyond the
  at-least-once contract in DR-014.

Policy attempt number, deadline, and pending backoff position SHALL be durable whenever they
must survive a segment boundary. A host restart MUST NOT grant extra attempts, reset a
deadline, or change the terminal policy outcome.

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
inside the same commit boundary, so that a host crash between commit and continuation never
strands an instance. The signal SHALL be claim-based with lease recovery and SHALL be
satisfied cheaply in-process on the happy path (the committing host continues immediately;
the claim is then a no-op).

RECOMMENDED mechanism: a dedicated outbox record kind (`continue`) consumed by a
continuation pump, reusing the certified at-least-once outbox machinery — a new port is NOT
justified. Continuation records are **internal outbox records**. External message
dispatchers SHALL ignore them. A dedicated continuation pump claims only continuation
records. Duplicate or stale continuations SHALL be harmless: the interpreter reloads
committed position, performs no duplicate step execution when no work remains, and the pump
marks the record processed.

Continuation disposition SHALL depend on the durable advancement outcome:

| Advancement outcome | Required record disposition |
|---|---|
| Progress committed, suspended, terminal, already parked, or conclusive stale/no-op | Mark the claimed record `Dispatched` |
| Segment budget exhausted while still runnable | Mark the claimed record `Dispatched` only after the progress commit atomically created a successor continuation |
| Expected-version conflict, host cancellation, lost lease, or transient infrastructure failure | Return the record to `Retryable` (or let its lease expire) without consuming the retry-safety signal |
| Unreadable continuation payload | Mark the record `Poisoned`; report the malformed record even when no instance identity can be recovered |
| Repeated interpreter failure at the DR-036 threshold | Commit the instance park first, then mark the triggering record `Poisoned`; a crash between those operations leaves a retryable record that resolves as an already-parked no-op |

No implementation may mark a continuation processed merely because it invoked the driver.
The durable result above must be known. Lease recovery and every retryable disposition SHALL
preserve enough information for another host to attempt the same committed position.

### DR-035 Multi-host scale-out story
The multi-host story here is **independent-instance distribution**, not general multi-node
execution of a single instance. Multiple lane hosts against one store SHALL be correct by
construction (expected-version append, DU-022) and SHALL distribute work via the
claim-based pumps (timers, outbox, continuation) as competing consumers across *distinct*
instances. This is explicitly narrower than the "multi-node execution" DU-060 holds out of
scope: no host claims cluster-wide single activation of one instance, and there is no
per-instance ownership lease.

The documentation SHALL state the contention model honestly: hot single-instance
contention across hosts resolves by conflict-retry (append rejection → reload → retry),
not ownership, and remains bounded by the in-process lane only within a single host.
Workloads needing cluster-wide single activation of one instance are the Orleans host's
territory (DU-060 direction). DU-060 is amended (see 06-requirements-durable-execution.md)
to record that claim-based independent-instance distribution over optimistic append is
permitted and is distinct from the still-out-of-scope single-instance multi-node case.

### DR-036 Poison and stuck handling
A continuation that repeatedly fails because of an interpreter exception or transient
definition-resolution/infrastructure fault SHALL follow the poison path: bounded retries
with backoff, then park the instance (DR-017, reason `Poison`) with diagnostics, never a hot
crash loop in the pump. A missing or incompatible bound definition is not poison; it follows
the immediate `VersionBinding` park path in DR-016.

Retry accounting and next-eligible-at timestamps SHALL be durable and shared across hosts,
keyed by instance and unresolved committed position (or an equivalently precise continuation
generation). Process-local counters are non-conforming: host replacement, lease expiry, or a
different competing consumer MUST NOT reset the attempt count. A successful advancement or
conclusive stale/no-op clears the unresolved failure count. Parking SHALL record the durable
attempt count and failed position required by DR-017. Stuck detection (MG-040) SHALL cover
parked instances.

### DR-037 Kind-partitioned outbox claims (continuation isolation)
DR-034 places continuation records in the certified outbox under a dedicated `continue`
record kind. `OutboxWrite` already carries a `Kind` discriminator, but the current claim
port (`IWorkflowOutboxStore.ClaimAsync` / `OutboxClaimRequest`) and the outbox pump claim
records **without a kind selector**, so external dispatchers and the continuation pump
would compete for the same records. To make DR-034 safe, the outbox contract SHALL provide
kind-partitioned claiming:

- `OutboxClaimRequest` SHALL carry a kind selector (an include set or an exclude set) and
  providers SHALL honor it, claiming only matching records. Providers SHOULD index on
  `Kind` so a partitioned claim does not scan the whole outbox.
- The **external message dispatcher pump** (`OrcaCoreOutboxPumpHostedService`) SHALL claim
  with the `continue` kind **excluded**, so it never delivers internal continuation records
  to a transport. `IMessageDispatcher` SHALL never receive a `continue` record.
- The **continuation pump** (DR-034) SHALL claim with the selector restricted to the
  `continue` kind only, and dispose each claim according to the DR-034 outcome table.
- The two pumps therefore operate on disjoint kind partitions of one outbox store; no new
  provider port is introduced (the DR-034 decision), only the claim selector is added.

This closes DR-OQ-2 in favor of the outbox-kind mechanism; a dedicated runnable-queue port
remains a future performance escape hatch only (see 16.8).

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
the direct health signal for DR-034), park/poison counts, version-binding failures, and
separate backlog gauges for internal continuation records and external outbox records.
Instrument names follow OB-020 naming.

Minimum distinct signals:

| Instrument | Type | Required low-cardinality attributes |
|---|---|---|
| `orca.driver.segment.duration` | Histogram (`s`) | `definition.id`, `definition.version`, `outcome` |
| `orca.continuation.pending.count` | ObservableGauge | `provider.name`, `state` |
| `orca.continuation.lag` | Histogram (`s`) | `provider.name`, `outcome` |
| `orca.outbox.external.pending.count` | ObservableGauge | `provider.name`, `state` |
| `orca.driver.park.count` | Counter | `definition.id`, `reason` |
| `orca.driver.poison.count` | Counter | `definition.id`, `source` |
| `orca.driver.version_binding.failure.count` | Counter | `definition.id`, `definition.version` |

Instance IDs, correlation IDs, and error messages SHALL NOT be metric attributes; they belong
in traces/logs under OB cardinality rules.

### DR-051 Options
Worker concurrency, continuation pump interval/batch/lease, poison retry policy, and segment
admission budgets SHALL be options on the existing hosted-service options surface, validated
on start. Required budgets:

- `MaxCommandsPerSegment`
- `MaxSegmentDuration`

`MaxCommandsPerSegment` is a hard command-admission limit. `MaxSegmentDuration` is a hard
elapsed-time **admission deadline checked between commands/steps**: after it expires the host
SHALL start no additional command or step in that segment. It is not a forcible wall-clock
preemption of user code already executing. An admitted operation may overshoot the deadline
and finishes, times out, or observes cooperative cancellation under DR-019; arbitrary user
code cannot be safely interrupted by the segment scheduler.

When either budget prevents another command and the instance is still runnable, the driver
SHALL atomically leave a successor continuation with the last progress commit, release the
lane/host turn, and let the next pump/host turn resume from the persisted position. A warning
threshold MAY be lower than either admission budget, but warning-only budgeting is not
sufficient for production hosts or Orleans rehosting.

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
  during compensation; all N compensation transitions commit once in reverse order.
  Repeating an action after a crash-before-commit is allowed and proves the SG-012
  idempotency contract rather than claiming exactly-once external effects (DR-014/040).
- **DR-AC-008** DAG: diamond plan driven to completion by the driver alone; a node failure
  marks dependents blocked via transitive closure; no manual pumping (DR-041).
- **DR-AC-009** Version binding: resume with the definition version unregistered parks the
  instance with a diagnostic. Registering the compatible version alone leaves it parked;
  an explicit expected-version re-arm then clears the diagnostic, emits a continuation, and
  resumes it. Re-arm is rejected while the version remains absent or incompatible
  (DR-016/017).
- **DR-AC-010** Two lane hosts, one store, 100 instances: all complete, no duplicated step
  commits, conflicts resolved internally (DR-030/035).
- **DR-AC-011** Graceful shutdown mid-segment: current commit finishes, nothing new
  claimed, instance completes on the surviving host (DR-033).
- **DR-AC-012** Ephemeral/durable parity: the same definition produces the same outcome
  and lifecycle event names on both engines (DR-060).
- **DR-AC-013** Step wait persists state: a step mutates business state and returns
  `WaitForEvent`; after host restart and event resume, the following step observes the
  mutation exactly once (DR-011/012).
- **DR-AC-014** Crash after runnable commit: a step completion commit leaves the instance
  runnable, then the host crashes before the next step starts; a new host claims the
  continuation and executes the next step exactly once (DR-034).
- **DR-AC-015** Stale continuation no-op: a continuation record remains after another host
  already advanced the instance to a later position; the pump claims it, performs no
  duplicate step execution, and marks it processed (DR-034).
- **DR-AC-016** Durable rejects ephemeral-only `ForEach`: a definition containing
  lightweight `ForEach` is registered against the durable driver; registration fails fast
  with a capability diagnostic unless durable `ForEach` has been explicitly specified
  (DR-010, CP-010..013).
- **DR-AC-017** Segment budget yields fairly: a workflow with many synchronous steps reaches
  `MaxCommandsPerSegment` or the `MaxSegmentDuration` admission deadline; the driver starts no
  additional work in that segment, commits progress with a successor continuation, releases
  the lane, and later resumes from the persisted position. A separately admitted long-running
  step may overshoot the segment deadline and is governed by its own timeout/cancellation
  policy rather than forcible interruption (DR-019/051).
- **DR-AC-018** Checkpoint envelope migration behavior: a checkpoint without the required
  position envelope is loaded; the instance either migrates deterministically or parks with
  a clear diagnostic, and it never resumes under a guessed position (DR-012).
- **DR-AC-019** Timer/delay resume: a definition with a timer/delay node registers a durable
  timer, the host is replaced while the timer is pending, and after the timer fires the
  driver resumes the next step exactly once from the persisted position (DR-010/031,
  ScheduleTimerCommand position carriage per DR-011a).
- **DR-AC-020** WaitLong timeout race: a `WaitLong` with a timeout registers both the wait
  and the timeout timer; the timeout fires before any event arrives; the driver resumes on
  the timeout branch exactly once and atomically cancels/removes the losing wait. A distinct
  late matching event follows the documented no-active-wait/mailbox policy — it is not
  mislabeled as an `EventId` duplicate — and cannot double-resume (EV-044/051).
- **DR-AC-021** External-job completion resume: a step dispatches an external job and waits;
  the completion is reported (`CompleteExternalJobCommand`) after a host restart; the driver
  resumes the following step exactly once (DR-031 external-job completed trigger).
- **DR-AC-022** Durable resource-pool grant resume: an instance blocks on a resource-pool
  requirement, the grant is advanced by a concurrently released ticket, and the driver
  resumes the guarded holder exactly once after the grant commits — including across a host
  restart between grant and continuation (DR-031 resource-pool grant trigger).
- **DR-AC-023** Start-input crash window: commit a start with non-trivial typed input, stop
  the host before `Init` begins, then replace the host. `Init` receives the exact committed
  input and completes without caller-held state; an unserializable input commits no runnable
  instance (DR-018).
- **DR-AC-024** Durable retry policy: with `MaxAttempts = 2`, a step mutates state and fails
  its first attempt, then succeeds after durable backoff across host replacement. The retry
  starts from the last committed state, preserves its attempt number/deadline, and commits
  one terminal outcome after exactly two attempts (DR-019).
- **DR-AC-025** Durable timeout/cancellation policy: a persisted timeout deadline survives
  host replacement, applies the authored EV-052 outcome once, and does not reset elapsed
  time. Cooperative operator cancellation reaches the running step token and commits the
  CR-031 lifecycle outcome without an extra retry (DR-019).
- **DR-AC-026** Continuation disposition: conflicts, host cancellation, lease loss, and a
  transient store failure leave the continuation retryable; stale/no-op and committed
  suspension/terminal outcomes mark it dispatched; budget exhaustion marks the old record
  dispatched only after a successor continuation commits (DR-034).
- **DR-AC-027** Durable poison threshold: each failed continuation attempt runs on a newly
  constructed alternating host against one store. The shared attempt count and backoff
  survive every replacement; the configured threshold parks the instance once with the
  durable count/position diagnostic and poisons the triggering record (DR-036).
- **DR-AC-028** Reason-specific re-arm: `RuntimeStateVersion` and `Poison` parks reject a
  generic resume and remain unclaimable. A validated migration or acknowledged operator
  repair followed by expected-version re-arm commits one unpark fact and one continuation;
  concurrent re-arm attempts cannot both commit (DR-017).
- **DR-AC-029** Outbox kind isolation: provider certification proves include/exclude claims
  for every shipped provider; the external pump never passes `continue` to
  `IMessageDispatcher`, while the continuation pump never claims external records
  (DR-034/037).
- **DR-AC-030** Complete public facade: instance-targeted, correlation-targeted, and
  definition-fanout `RaiseEventAsync` flows drive registered durable definitions with inbox
  dedup, and the same facade exposes management queries and re-arm without test-issued kernel
  commands (DR-003/032/061).
- **DR-AC-031** Telemetry and options: a hosted driver exports every DR-050 instrument with
  the required dimensions; internal and external backlog gauges differ correctly. Invalid
  worker/pump/retry/budget options fail startup, while valid overrides reach the running host
  (DR-033/050/051).
- **DR-AC-032** Durable `WhenFirst`: two branches race across separate hosts; exactly one
  deterministic winner commits, the authored residual policy resolves losing waits/timers,
  and restart cannot select a second winner (DR-010, CP-004, EV-051).
- **DR-AC-033** Driver-owned continue-as-new: a registered definition requests
  continue-as-new through its public authoring surface; replacement state, lineage, bound
  definition version, and fresh execution position commit atomically, and a restart resumes
  the new generation without test-issued kernel commands (DR-010/011/012).

## 16.8 Phasing

Fits document 13 as the missing bridge between Slice 2 (durable core — done) and the
"engine complete" claim; the Orleans package's O1+ phases then re-host the same
interpreter:

1. **DR-P1 Interpreter + position envelope** (DR-010..019, incl. the DR-011a command
   contract changes, `Parked`, start input, and durable policies) — hardest correctness
   work; gate: DR-AC-001/002/005/006/009/013/016/018/023/024/025/028/032/033 on the
   in-memory provider.
2. **DR-P2 Lane host + continuation signal** (DR-030..037, DR-003 seam, DR-037
   kind-partitioned claims) — gate: DR-AC-003/004/010/011/014/015/017/019/020/021/022/
   026/027/029, with provider certification on every shipped provider and the restart/
   multi-host scenarios on PostgreSQL.
3. **DR-P3 Saga + DAG driving** (DR-040/041) — gate: DR-AC-007/008.
4. **DR-P4 Facade, parity, samples, telemetry** (DR-032, DR-050/051, DR-060/061) — gate:
   DR-AC-012/030/031, sample e2e, feature-matrix update.

Resolved decisions (were open; closed in this revision so implementation is not blocked on
contradictions):

- **DR-OQ-1 (RESOLVED)** Position envelope format: **extend the checkpoint payload** —
  business state + execution position in one versioned envelope, per the normative
  requirement DR-012. A parallel `RuntimeState` field is rejected because position and
  business state must commit and be read atomically. Residual work is only the envelope
  version/migration rules (DR-012, DU-041), not the shape choice.
- **DR-OQ-2 (RESOLVED)** Continuation signal: **outbox `continue` kind with
  kind-partitioned claims** (DR-034 + DR-037), reusing certified outbox machinery; no new
  provider port. A dedicated runnable-queue port is retained only as a future performance
  escape hatch, to be reopened solely if measured outbox contention warrants it — it is not
  an open design question for DR-P2.
- **DR-OQ-3 (RESOLVED 2026-07-12)** Segment admission defaults are
  `MaxCommandsPerSegment = 256` and `MaxSegmentDuration = 30s`, matching the reviewed driver
  budget. Positive per-host overrides are allowed through the validated hosted-service
  options surface. Warning thresholds are optional diagnostics and do not replace either
  admission limit. Unbounded production segments remain unsupported.
