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

## 16.0 Audit baseline (2026-07-05, current implementation)

This section is a historical baseline, not a current implementation ledger. Subsequent
implementation evidence and remaining gaps are tracked in
[`docs/durable-driver-audit.md`](../../docs/durable-driver-audit.md).

What exists and is reused. The kernel processor, aggregate, persistence, wake-up
sources, facade, and DAG runner are reused without behavioral forks. The kernel
**command contracts are extended, not reused unchanged**: several existing commands did
not carry the post-step business state and durable execution position that DR-011 and
DR-012 require, so their record shapes SHALL change (additive fields). This is called
out explicitly in DR-011a; "reused" below means the components and decision logic are
kept, not that every command DTO is frozen.

| Layer | Component | Status |
|---|---|---|
| Kernel | `DurableCommandProcessor` + `DurableWorkflowAggregate` + handlers | Historical implementation covered start, step results, yield, waits, timers, events, children, resource pools, external jobs, saga, and lifecycle. Yield/job/saga/public-child records are provenance only and do not approve v1 authoring members. Active contracts are replaced per DR-010/011a. |
| Persistence | Checkpoint (incl. full runtime state after R14 fix), streams, inbox, outbox, projections | Complete; certified per provider |
| Wake-up sources | `OrcaCoreTimerHostedService` (claim-based timer pump), `OrcaCoreOutboxPumpHostedService`, `OrcaCoreOperationalSweepHostedService` | Complete |
| Facade | `DurableWorkflowRuntime` (`RegisterDefinition`, `StartOrGetAsync`) | Start-only |
| Composition | `DurableDagRunner.ScheduleReadyAsync` | Exists but nothing calls it in production |

What was missing at the audit baseline (this document's subject):

1. **No durable interpreter** — nothing maps definition nodes and step results to kernel
   commands (`Interpreter<TState>` exists only in `Engine.Ephemeral`).
2. **No execution position persistence** — checkpoints carry `LastStepPath` (diagnostic)
   and an opaque business-state payload, but not the CR-015 structured fiber/scope state; a
   rehydrated instance cannot know where to resume.
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

### DR-003 Reviewed host dispatch seam, not application/provider API
Delivery/resume/complete/fail entry points needed by driver hosts SHALL live behind one named,
versioned durable-hosting contract. Lane and Orleans hosts consume the same contract. It is not
an application API, provider SPI, or general child-management back door; the DAG bridge is
internal/friend-scoped exactly as document 17 defines.

## 16.2 Durable interpreter

### DR-010 Definition interpretation in durable mode
The interpreter SHALL support every definition shape valid for **durable** execution:
typed `Init`, named business steps, root/nested `If`, root `While`, root `Parallel`
with `WhenAll`/`WhenAllOutcomes`, root bounded `ForEach` with the same joins, structural and
dynamic event waits (including the structural timeout race), timers/delays, scoped
`AcquireResources`, typed/resultless `End`, and root continue-as-new after every lease scope
has exited. Durable lambda steps are absent.

Nested `Parallel`, nested `While`, and nested `ForEach`, `WhenFirst`, `WaitLong`, author `Yield`, public
`RunChild`/`RunChildren`, public `RunExternalJob`, and saga definitions SHALL NOT be accepted by
the v1 durable driver. DAG-owned internal child scheduling is handled by DR-041 and does not
make a public child node valid. Any unsupported definition shape SHALL fail fast
at registration with a capability diagnostic (DU-002 feature matrix), never at mid-flight
runtime.

### DR-011 Step results map to kernel commands
Step outcomes SHALL map onto kernel commands exactly. `StepResult` objects still carry
control intent only (CR-011); the driver-generated command emitted after step execution
carries the post-step business state and complete versioned structured execution envelope
whenever user step code may have mutated state. The envelope records fibers, recursive
scopes, scheduler position, typed results, ownership, and plan binding rather than a
frame-stack cursor.

| Step result / node | Command(s) |
|---|---|
| `Completed` at a commit boundary | `DurableStepCompletedCommand` carrying serialized business state + structured execution envelope; produces the checkpoint |
| `Failed` / unhandled exception after policy exhaustion | `DurableStepFailedCommand` carrying the last committed business state + structured execution envelope and failure diagnostics; the detached failed-attempt copy is discarded |
| `WaitForEvent` / builder `Wait` | `DurableWaitRegisteredCommand` carrying post-step business state + structured execution envelope when the wait is produced by step code or follows a state-mutating node |
| Timer/delay node | `ScheduleTimerCommand` carrying the current structured execution envelope; state is checkpointed if the preceding node mutated it |
| Parallel/`ForEach` branch or item return/join | Internal scope commands carrying ordered typed result/outcome, complete replacement parent state at merge, ownership, and join-once position |
| `AcquireResources(request, body)` | Internal lease queue/grant/release or quarantine commands carrying exact lexical-scope obligation, selected request, tickets, protection token, and execution position |
| `End` | `DurableCompleteCommand` carrying terminal state metadata, typed output when declared, optional fixed outcome, final business-state checkpoint, and structured execution envelope |
| Continue-as-new | `ContinueAsNewCommand` carrying the replacement state envelope, incremented generation, and lineage metadata |

### DR-011a Required kernel contract changes
The mapping in DR-011 requires command records to carry post-step business state and the
format-2 structured execution envelope. These contracts SHALL be replaced or extended as
needed for the active-development refactor; compatibility with provisional format-1 cursor
envelopes is not required. This document is the authority that supersedes any "commands
unchanged" reading of 16.0:

- `StartWorkflowCommand` and `WorkflowStartedEvent` SHALL carry the serialized start input
  and its content type so `Init` can reconstruct input after a crash between the start commit
  and the first interpreter segment (DR-018).
- `DurableStepCompletedCommand` already carries `StepPath` + serialized state; `StepPath`
  SHALL become descriptive metadata only and the command SHALL carry the versioned
  structured execution envelope (DR-012).
- `DurableStepFailedCommand` SHALL carry the last committed business state, execution
  envelope, and failure diagnostics after applicable policies are exhausted. Mutations made
  by an unsuccessful attempt SHALL NOT become the input to a retry or terminal checkpoint
  unless a future explicitly-authored policy defines different semantics (DR-019).
- `DurableWaitRegisteredCommand` SHALL carry post-step business state + execution envelope
  when the wait follows a state-mutating node.
- `ScheduleTimerCommand` SHALL carry the current execution envelope (the baseline shape
  carried only `TimerId`, `FireAt`, `WakeupName`), and the preceding node's state checkpoint
  when it mutated state.
- `DurableCompleteCommand` SHALL carry terminal state metadata + typed output when declared +
  optional fixed outcome + final business-state checkpoint + execution envelope (the baseline
  shape carried only `OutcomeName`).
- Scope-return/join and lease commands SHALL carry the business state + structured execution
  envelope described in the DR-011 table. Provisional public yield/child/job commands do not
  define v1 authoring nodes.

The envelope carried by these commands is the same fixed-codec, versioned checkpoint payload
envelope defined in DR-012, so state and position always commit atomically. Every invocation
operates on a codec-detached attempt-local root/branch/item copy. Only the winning success may
commit; a failed, timed-out, fenced, or token-ignoring late attempt cannot mutate the command's
committed state. `ReplaceState` supplies immutable/value-state replacement.

### DR-012 Execution position is durable
The interpreter SHALL persist the complete CR-015 structured execution state at every commit
boundary, versioned and serialized alongside business state inside the fixed
`orcacore-json-v1` checkpoint payload envelope. Format 2 SHALL include structural plan
fingerprint and compiler format, root and child fiber
records, recursive scope records, scheduler position, loop iteration, next scope-entry
sequence, scope-plan/entry identity, pending typed results, owned-obligation references, and
step-operation/attempt diagnostics. Runtime scope/fiber identities SHALL derive from committed
generation, plan identity, parent fiber, and scope-entry sequence. Rehydration SHALL resume
without re-executing steps whose completion committed or reconstructing ownership from
cursor paths. The envelope format SHALL remain versioned for forward evolution (DU-041
applies).

The persisted position SHALL distinguish one logical business-step visit from a repeated visit
to the same authored instruction. It supplies one stable `StepOperationId` across retry,
timeout reconciliation, replay, host replacement, and optimistic conflict, and creates a new
ID for another loop entry, branch/item occurrence, or continue-as-new generation. The public
context exposes only workflow instance ID, operation ID, and positive attempt number — never
fiber/scope coordinates.

If a checkpoint without the required execution-position envelope is loaded, the driver SHALL
NOT guess. It SHALL either migrate deterministically under an explicitly coded rule or poison/
quarantine automatic advancement with a clear runtime-state-version diagnostic in advanced
operator tooling. It SHALL NOT add `Parked` to the public workflow status or expose public rearm.

### DR-013 Advancement segment and commit cadence
One interpreter invocation SHALL process one **advancement segment**: it starts from the
persisted position and may execute zero or more kernel command cycles sequentially until it
reaches a suspension point (wait, timer, lease admission, branch/item join), terminal state,
poison
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
inbox dedup). A runtime-created `StepOperationId` remains stable for that logical step visit
across re-invocation, but does not make the external effect exactly-once. A durable step may
perform one short bounded idempotent create-or-observe call using that identity; long-running
work executes outside the step and reports through ordinary events.

Before author code is invoked, the driver SHALL durably commit or derive from committed facts the
step-operation coordinate, current policy-attempt ordinal, and applicable attempt deadline. After
uncertain host loss, recovery reuses that same coordinate, ordinal, and deadline. Such a physical
re-invocation does not consume another `maxAttempts` slot; only a committed policy-retry
transition advances the attempt ordinal.

### DR-015 Determinism contract at the driver boundary
Interpreter decisions SHALL be deterministic functions of (definition version, committed
facts, persisted position, business state) — CR-012 restated at this layer. Wall-clock
reads go through `TimeProvider`; identifiers created during a segment are committed with
it; the interpreter never consults mutable ambient state. The structural fingerprint binds
graph shape, strong authored values, referenced types, static requests, and codec format.
Selectors, projectors, merges, step configuration, mapping logic, and request construction are
opaque code whose change requires a new `DefinitionVersion`.

### DR-016 Version binding at resume
Every resume SHALL bind to the definition version and structural fingerprint recorded at start
(DU-040). A host without the definition SHALL leave the committed continuation retryable for a
definition-owning host; it SHALL NOT invent a workflow lifecycle transition. Registration of
the same identity/version with another structural fingerprint conflicts before use. Opaque code
compatibility relies on the mandatory version bump (DU-041).

### DR-017 No public parked/rearm lifecycle in v1
The exact first-release `WorkflowInstanceStatus` set contains neither `Parked` nor `Paused`.
Missing host registration, incompatible runtime-state format, and poison continuation handling
are host/operator conditions, not workflow-authored lifecycle states. The driver SHALL never
guess position or hot-loop: it leaves a recoverable continuation retryable when another
definition-owning host can progress it, or quarantines/poisons malformed work with an
operator-readable diagnostic in advanced runtime/provider tooling. V1 instance handles expose
no `Rearm`, `Resume`, raw stream version, or diagnostic repair ticket. Any future public repair
operation requires a document-17 lifecycle/authorization amendment.

### DR-018 Start input survives the first-segment crash window
The start commit SHALL durably record the fixed-codec start input, `orcacore-json-v1` format,
and payload fingerprint before
`Init` can execute. The same atomic start commit SHALL leave a continuation signal when the
instance is runnable. A replacement host SHALL reconstruct the exact input from committed
facts and execute `Init` without caller-held memory. Input serialization/version failures
SHALL reject start before a runnable instance is committed. Retention or redaction of start
input after the first business-state checkpoint is an explicit retention-policy concern and
MUST NOT weaken replay within the active recovery window.

### DR-019 Durable policy execution
The durable interpreter SHALL apply the policy decorators required by CR-006 before mapping
a final step result through DR-011. At minimum:

- `WithRetry(maxAttempts, fixedDelay?)` is the complete v1 retry API: positive `maxAttempts`
  counts durable policy attempts and includes the initial attempt, not physical re-invocations
  needed to recover one uncertain in-flight attempt; optional `fixedDelay` is the same
  non-negative delay before every policy retry; no predicate, backoff family, terminal callback,
  or definition-wide retry exists;
- a fixed delay crossing a segment boundary uses a durable timer/continuation deadline and
  survives host replacement without resetting the attempt count;
- each `WithStepTimeout` deadline bounds one durable policy attempt, including any physical
  host-loss replay of that in-flight attempt, fences a late result, and uses `TimeProvider`;
  committed retry increments `AttemptNumber` and gets a new attempt deadline while retaining
  `StepOperationId`; timeout reports `StepAttemptTimeoutException` and discards the attempt-local
  copy;
- one `CompleteWithin` deadline is anchored at workflow start, persisted, and includes
  admission, retry/fixed delay, delay, wait, lease queueing, every `ContinueAsNew` generation,
  and cleanup/quarantine; it never resets on restart or rollover and commits terminal
  `TimedOut` with `WorkflowDeadlineExceededException` when it wins;
- a structural wait-timeout winner fails the current root/branch/item with typed
  `WorkflowWaitTimeoutException`, cancels the event obligation, and becomes failure data for an
  enclosing `WhenAllOutcomes`; no timeout callback builder executes;
- cooperative cancellation is delivered through the step's `CancellationToken`, then
  committed according to CR-031; Polly/application retries do not define these transitions;
- `StepOperationId` derives from one committed logical step-visit coordinate and is distinct
  across loop, branch, item, and generation occurrences. Attempt number is diagnostic and does
  not change it. This does not upgrade inline external side effects beyond DR-014.
- `maxAttempts = 1` still permits physical replay of its one uncertain in-flight policy attempt,
  using the same `AttemptNumber` and deadline, but permits no committed policy retry after that
  attempt reaches a retryable failure/timeout outcome.

Operation ID, policy attempt number, optional deadline, and in-flight dispatch marker SHALL be
durable before the first author-code invocation of that attempt; pending fixed-delay position
SHALL be durable whenever it crosses a segment boundary. Recovery reuses an in-flight attempt's ordinal and deadline, and if that deadline has
already expired transitions through the authored timeout policy before any re-invocation. A host
restart MUST NOT grant extra attempts, reset a deadline, or change the terminal policy outcome;
only a committed policy retry creates the next ordinal and deadline.

Every invocation receives a fixed-codec-detached copy of the last committed root/branch/item
state. A failed/timed-out attempt's mutations are discarded, and retry starts from the last
committed copy. A token-ignoring timed-out body may overlap physically with its retry but has no
commit authority or logical path token; it keeps its physical throttle/transient slot until
return. The workflow deadline stops admission, signals tokens, cancels runtime obligations, and
suppresses merges without waiting forever for such bodies.

## 16.3 Default in-process lane host (the primary durable engine)

### DR-030 Lane-serialized advancement
The lane host SHALL serialize all advancement per instance through `InstanceLane`
(CR-040/041 layering: in-process lane first, expected-version append as the cross-process
guard, DU-022). Conflict outcomes from concurrent hosts SHALL be handled as retry/reload
signals, never surfaced as instance failures.

### DR-031 Advancement triggers
The lane host SHALL schedule an interpreter invocation when:

- a start commits (first segment),
- a committed command unblocks the instance: wait matched, timer fired, internal DAG child
  completed, normalized external event applied, or resource-pool grant,
- a runtime segment budget leaves a successor continuation,
- the continuation pump (DR-034) claims a runnable instance after a crash.

### DR-032 Exact application facade
The durable engine SHALL expose `IWorkflowDefinitionRegistry`, typed durable definition handles
with `StartOrGetAsync`, typed instance handles with snapshot/root-state/output/cancellation/
termination, and `IWorkflowEventClient` with only instance-targeted and correlation-targeted
delivery. Definition fanout, `DurableManagement`, pause/resume, rearm, failed-instance retry,
history, archive, and purge are absent. Public signatures SHALL match document 17 rather than a
provisional monolithic `DurableWorkflowRuntime` facade.

### DR-033 Role-based host lifecycle integration
`AddOrcaCoreDurableEngine(DurableEngineHostOptions)` SHALL register the lane driver, ingress,
timer/reconciliation loops, and bounded workers against exactly one complete certified provider
role set. Callback-only `AddOrcaCoreDurableEventIngress()` registers inbox/continuation handoff
and `IWorkflowEventClient` but no definitions or execution workers. Graceful drain finishes the
current commit, admits nothing new, and uses `TimeProvider` throughout. There is no separate
`AddOrcaCoreHostedServices` toggle or catch-all mode registration.

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
| Progress committed, suspended, terminal, or conclusive stale/no-op | Mark the claimed record `Dispatched` |
| Segment budget exhausted while still runnable | Mark the claimed record `Dispatched` only after the progress commit atomically created a successor continuation |
| Expected-version conflict, host cancellation, lost lease, or transient infrastructure failure | Return the record to `Retryable` (or let its lease expire) without consuming the retry-safety signal |
| Unreadable continuation payload | Mark the record `Poisoned`; report the malformed record even when no instance identity can be recovered |
| Repeated interpreter failure at the DR-036 threshold | Mark/quarantine the triggering continuation `Poisoned` with an operator diagnostic; do not invent a workflow status or public rearm operation |

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
A continuation that repeatedly fails because of a malformed payload/runtime state or persistent
interpreter fault SHALL follow a bounded advanced poison/quarantine path with diagnostics, never
a hot crash loop. A definition merely absent on an ingress/worker host remains retryable for a
definition-owning host. Same-version structural conflict is rejected at registration. Neither
condition adds `Parked`/`Paused` status or a public repair member (DR-017).

Retry accounting and next-eligible-at timestamps SHALL be durable and shared across hosts,
keyed by instance and unresolved committed position (or an equivalently precise continuation
generation). Process-local counters are non-conforming: host replacement, claim expiry, or a
different competing consumer MUST NOT reset the attempt count. A successful advancement or
conclusive stale/no-op clears the unresolved failure count. Poison/quarantine diagnostics SHALL
record the durable attempt count and failed position required by DR-017. Stuck detection
(MG-040) includes instances stranded by operational faults through host/operator projections.

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

### DR-038 Scoped durable lease occurrences are committed, ancestry-safe state machines
Each structural `AcquireResources(request, body)` occurrence SHALL create one runtime-generated
`LeaseObligationId` for the exact instance generation, authored scope, fiber occurrence, and
scope-entry occurrence. One persistent owned record SHALL span `Pending` and `Held` until a
committed normal release or transfer into marked/ambiguous/quarantined ownership and eventual
exact release/cancellation; a pending wait disappearing after
grant while tickets live in a separate unowned collection is non-conforming. The workflow
state and every provider reservation/ticket SHALL carry the same obligation, ticket, and
provider ownership generation. A pending-commit reservation consumes the same configured units
as a held ticket; queued/released/cancelled states consume none.

A capacity wait has no independent author-configured timeout. It remains pending until the
whole atomic request is granted or the exact scope is cancelled/fails/terminates; only that
requesting fiber parks, while unrelated siblings may remain runnable. Workflow deadline or
other cancellation uses the same exact-owner cleanup/quarantine path.

For selector-based requests, the driver SHALL normalize and commit the immutable selected
requirements before the first provider mutation. A crash may cause re-evaluation only before
that commit; replay/retry afterward uses the committed selection. After normalization, every
pool named by a static or selector-produced request SHALL be checked before selection commit,
queueing, reservation, or provider mutation. Any missing name fails the whole request as
non-retryable `ResourcePoolNotConfiguredException`, stable code
`WF-RESOURCE-POOL-NOT-CONFIGURED`, carrying the complete missing-name set; it never parks.
Provider grants begin as exact pending-commit reservations. Guarded work cannot start until the
workflow acquire fact commits and the reservation is confirmed active. Recovery confirms or
cancels a reservation only from causal owner-stream evidence; ambiguity remains held.

Grant/cancel resolution has one durable winner. Cancellation committed while an obligation is
still queued wins as cancelled-before-grant, reserves zero, removes queue eligibility, and makes
a later grant command a stale no-op. If the governance reservation commits first, the obligation
is `PendingCommit` and its exact tickets reserve capacity; cancellation cannot relabel it as
cancelled-before-grant or erase that reservation. Cancellation before workflow activation then
performs one exact compensating release because author code was never admitted. Cancellation
after activation requires proven exact release or transfer to capacity-reserving quarantine.
Restart preserves the winning state, and a delayed loser command is an idempotent stale no-op.

Before enqueue or grant, the driver SHALL reject a pending/held lease on any active ancestor
lease scope and SHALL exclude siblings from that ancestry scan. Both pending records and active
tickets count. Sequential scopes and one fully lexical scope per root-loop iteration are legal;
the earlier exact release must commit before the next scope can acquire. The lease remains held
across `Wait` inside its lexical body and releases before branch/item return, merge, or parent
resume after normal completion. Dedicated leased builders omit acquisition and
`ContinueAsNew`. Rollover after every scope has exited is legal; rollover SHALL NOT clear any
remaining obligation without matching provider facts.

Forced workflow termination SHALL fence the exact owner from resuming but SHALL NOT infer that
protected external work stopped. Until committed cleanup plus confirmed stop, or an end-to-end
protected-resource fence, proves reuse safe, the lease record transitions to a capacity-
reserving quarantine rather than releasing tickets from bare terminal status.

The scope supplies a runtime-created round-trippable `LeaseProtectionToken`. A trusted
integration reconciler uses the generic `IDurableResourceLeaseRecovery` seam and one
caller-stable `StopConfirmationId` to confirm all token-bound work terminal, absent under
causally sufficient observation, or end-to-end fenced. Confirmation is idempotent and
compare-and-acts on exact obligation/ticket/provider generation; elapsed time, delete
acknowledgement, workflow terminal status, or infrastructure labels are insufficient.

Confirmation uses total ordered precedence: (1) a confirmation ID already bound to another token
returns `ConfirmationConflict` before target-token state is evaluated; (2) an ID already accepted
for the same token, or a token released by another accepted confirmation, returns
`AlreadyConfirmed`; (3) live `PendingCommit`/`Held`/`ReviewMarked`/`AmbiguousHeld` lexical
ownership returns `NotConfirmable`; (4) a quarantined token with an unused ID returns `Released`
and releases its exact tickets once; (5) a normally released, unknown, or retention-purged token
returns `TokenNotFound`. Normal release and confirmation serialize, and accepted-confirmation
tombstones survive at least the workflow/provider deduplication window.

Pool review deadlines trigger the mark-and-reconcile protocol in MG-064, not holder renewal
or time-only reclaim. Reconciliation and deterministic release compare exact obligation,
ticket, and ownership generation so stale cleanup cannot affect a successor occurrence.
All pools/requests/tickets/reviews/resizes/confirmations/tombstones in one configured partition
are serialized through the single expected-version resource-governance aggregate in MG-065 and
PR-017; the driver never constructs a cross-pool grant from independent per-workflow writes.

`DurableResourcePoolDefinition.Capacity` is immutable creation capacity. The current
`DurableResourcePoolSnapshot.ConfiguredCapacity` is reconstructed by replaying that seed plus
committed resizes. On later startup, hosts compare their definitions with the persisted creation
definition, never with the replayed current capacity, and never overwrite current capacity. Thus
the original definition after resize from N to M succeeds while preserving M and any resize debt;
a changed creation definition fails even if its capacity equals M. Pool-manager `GetAsync` and
`ResizeAsync` calls for a well-formed unknown name fail with
`ResourcePoolNotConfiguredException`/`WF-RESOURCE-POOL-NOT-CONFIGURED`. Resize capacity SHALL be
positive; zero or negative throws `ArgumentOutOfRangeException`. Both validations occur before
operation-ID binding, append, waiter grant, or any other mutation.

## 16.4 Typed DAG driving and deferred saga

### DR-040 Saga driving is deferred
The v1 driver SHALL expose no saga definition, compensation command path, adapter, or public
placeholder. Historical kernel records do not approve the feature. A future implementation is
gated by the complete amendment in document 07 and document 17.

### DR-041 Typed DAG driving
The driver SHALL own the `OrcaCore.Dag` execution loop through an internal child-start/join
protocol: pure mapping may reevaluate before one fixed-codec mapped-input commit; one child
workflow starts per admitted resultful or resultless node; typed output is consumed only for
resultful success; newly ready nodes schedule until terminal. Concurrent completions cannot
duplicate a child. The driver persists the closed run/node statuses, child-failure mapping,
`DependencyBlocked` closure, authored-ordinal admission/snapshots, and exact DAG cancellation
progression from CP-024/027/028. Host `MaxConcurrentNodes` is distinct from workflow path tokens
and resources and counts every started nonterminal child, including one parked in a wait, delay,
or lease queue, until its node is terminal. A child does not free DAG admission merely by
releasing an in-instance path token. No caller pumps sets and no public child-workflow node is
exposed.

## 16.5 Observability and options

### DR-050 Driver telemetry
The driver SHALL emit through the existing observer/OB surfaces: segment duration
(distinct from OB step-command duration), continuation lag (commit → continuation start —
  the direct health signal for DR-034), poison/quarantine counts, registration conflicts, and
separate backlog gauges for internal continuation records and external outbox records.
Instrument names follow OB-020 naming.

Minimum distinct signals:

| Instrument | Type | Required low-cardinality attributes |
|---|---|---|
| `orca.driver.segment.duration` | Histogram (`s`) | `definition.id`, `definition.version`, `outcome` |
| `orca.continuation.pending.count` | ObservableGauge | `provider.name`, `state` |
| `orca.continuation.lag` | Histogram (`s`) | `provider.name`, `outcome` |
| `orca.outbox.external.pending.count` | ObservableGauge | `provider.name`, `state` |
| `orca.driver.poison.count` | Counter | `definition.id`, `source` |
| `orca.driver.registration_conflict.count` | Counter | `definition.id`, `definition.version` |

Instance IDs, correlation IDs, and error messages SHALL NOT be metric attributes; they belong
in traces/logs under OB cardinality rules.

### DR-051 Options
Worker concurrency, continuation pump interval/batch/lease, poison retry policy, and segment
admission budgets SHALL be options on the existing hosted-service options surface, validated
on start. Required budgets:

- `MaxCommandsPerSegment`
- `MaxSegmentDuration`
- `MaxConcurrentExecutionPathsPerInstance`

`MaxCommandsPerSegment` is a hard command-admission limit. `MaxSegmentDuration` is a hard
elapsed-time **admission deadline checked between commands/steps**: after it expires the host
SHALL start no additional command or step in that segment. It is not a forcible wall-clock
preemption of user code already executing. An admitted operation may overshoot the deadline
and finishes, times out, or observes cooperative cancellation under DR-019; arbitrary user
code cannot be safely interrupted by the segment scheduler.

`MaxConcurrentExecutionPathsPerInstance` is a positive host-owned ceiling. Authors cannot
raise it. Fixed root-parallel branches admit in authored order; root `ForEach` uses the lower of its
optional node cap and this ceiling. DAG `MaxConcurrentNodes` is owned by the DAG host and
remains separate.

One runnable root/branch/item owns one path token. Parking on wait/delay/resource/join releases
it; progress reacquires it. A parent releases before fanout and reacquires for merge/continuation,
so ceiling one cannot deadlock on the waiting parent. Root-fanout paths share the pool and queue by
authored ordinal/item index. `ForEachOptions.MaxConcurrency` separately counts admitted
nonterminal item scopes. A fenced token-ignoring body owns no logical token but retains its
physical throttle slot until return.

When either budget prevents another command and the instance is still runnable, the driver
SHALL atomically leave a successor continuation with the last progress commit, release the
lane/host turn, and let the next pump/host turn resume from the persisted position. A warning
threshold MAY be lower than either admission budget, but warning-only budgeting is not
sufficient for production hosts or Orleans rehosting.

## 16.6 Parity and migration

### DR-060 Semantic parity with the ephemeral engine
A definition using only mode-shared shapes SHALL produce the same terminal outcome and
observable lifecycle sequence on both engines (differences only in guarantees: durability,
restart survival, and host-managed cold residency for ordinary waits). The feature matrix
(DU-002) SHALL gain a "durable driver"
column; every gap is explicit.

### DR-061 Public-surface replacement fitness
With DR-032 complete, registration, typed start, event delivery, snapshot/state/output reads,
cancellation request, and termination SHALL require no hand-written kernel command code. The
samples SHALL include one durable end-to-end sample
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
- **DR-AC-005** Runtime-owned segment budget: a runnable definition reaches its command/time
  admission budget; committed progress remains, one successor continuation is left, the lane
  is released, and execution resumes without an author `Yield` surface (CR-017, DR-051).
- **DR-AC-006** Position fidelity: a root `Parallel` has one branch whose nested `If` enters a
  wait while another branch completes; after restart the instance resumes only the exact
  branch/conditional position and joins once (DR-012).
- **DR-AC-007** Deferred saga absence: public assemblies, registration, and driver compile
  fixtures expose no saga/compensation surface or placeholder; a manual saga graph fails the
  selected-mode capability check (DR-010/040).
- **DR-AC-008** Typed DAG: a diamond including a resultless prerequisite maps direct typed
  outputs and is driven to completion with closed statuses, authored-order snapshots, and
  cancellation semantics; no public child node/manual pumping is used (DR-041).
- **DR-AC-009** Version binding: a callback host without the registered definition accepts an
  event and leaves a continuation; a definition-owning host later progresses it. Same-version
  structural drift conflicts at registration, and no `Parked`/rearm lifecycle is exposed
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
- **DR-AC-016** Bounded durable `ForEach`: pure item selection may repeat before one fixed-codec
  snapshot commit,
  stable item indices/operation IDs and ordered outcomes survive restart, the lower host/node
  limit holds, and merge commits once. Nested `Parallel` and nested `ForEach` reject at build (DR-010,
  CP-010..014).
- **DR-AC-017** Segment budget yields fairly: a workflow with many synchronous steps reaches
  `MaxCommandsPerSegment` or the `MaxSegmentDuration` admission deadline; the driver starts no
  additional work in that segment, commits progress with a successor continuation, releases
  the lane, and later resumes from the persisted position. A separately admitted long-running
  step may overshoot the segment deadline and is governed by its own timeout/cancellation
  policy rather than forcible interruption (DR-019/051).
- **DR-AC-018** Checkpoint envelope migration behavior: a checkpoint without the required
  position envelope either migrates deterministically or automatic progression is poisoned/
  quarantined with a diagnostic; it never guesses position or adds public rearm (DR-012/017).
- **DR-AC-019** Timer/delay resume: a definition with a timer/delay node registers a durable
  timer, the host is replaced while the timer is pending, and after the timer fires the
  driver resumes the next step exactly once from the persisted position (DR-010/031,
  ScheduleTimerCommand position carriage per DR-011a).
- **DR-AC-020** Structural wait timeout race: ordinary `Wait(..., timeout)` registers event and
  timer obligations; timeout wins once, cancels the event obligation, and fails the current
  root/branch/item with `WorkflowWaitTimeoutException`. An enclosing `WhenAllOutcomes` observes it as
  data; a distinct late event cannot double-resume and no callback builder runs (EV-044/051).
- **DR-AC-021** Normalized external report resume: a bounded typed step create-or-observes
  external work using `StepOperationId` and then waits; after host restart a normal event with
  one caller-stable `EventId` resumes exactly once. When the external lifetime is protected by a
  lease, the immediate first post-resume step remains inside that lease and validates operation
  ID, protection token, external-work incarnation, and terminal state before release; malformed,
  stale, or unproven reports cannot release. No external-job-specific command/facade is used
  (DR-014/031/038, DU-056).
- **DR-AC-022** Durable resource-pool grant resume: an instance blocks on a resource-pool
  requirement, the grant is advanced by a concurrently released ticket, and the driver
  resumes the guarded lexical scope exactly once after the grant commits — including across a host
  restart between grant and continuation (DR-031 resource-pool grant trigger).
- **DR-AC-023** Start-input crash window: commit a start with non-trivial typed input, stop
  the host before `Init` begins, then replace the host. `Init` receives the exact committed
  input and completes without caller-held state; an unserializable input commits no runnable
  instance (DR-018).
- **DR-AC-024** Durable retry policy: with `maxAttempts = 1`, host loss after an external effect
  but before outcome commit replays create-or-observe under the same operation ID, attempt ordinal,
  and persisted deadline without gaining a policy retry. Separately, with `maxAttempts = 2` and
  fixed delay, a committed retryable failure advances to attempt 2 across host replacement, starts
  from the last committed state, preserves operation ID, and commits one terminal outcome after
  exactly two policy attempts (DR-014/019).
- **DR-AC-025** Durable timeout/cancellation policy: a persisted timeout deadline survives
  host replacement, applies the authored EV-052 outcome once, and does not reset elapsed
  time. Cooperative operator cancellation reaches the running step token and commits the
  CR-031 lifecycle outcome without an extra retry (DR-019).
- **DR-AC-026** Continuation disposition: conflicts, host cancellation, lease loss, and a
  transient store failure leave the continuation retryable; stale/no-op and committed
  suspension/terminal outcomes mark it dispatched; budget exhaustion marks the old record
  dispatched only after a successor continuation commits (DR-034).
- **DR-AC-027** Durable poison threshold: failed continuation accounting survives alternating
  hosts; the threshold poisons/quarantines the malformed continuation once with durable
  count/position diagnostics and no hot loop or invented workflow status (DR-036).
- **DR-AC-028** No public repair lifecycle: runtime-state/poison recovery uses advanced
  operator/provider tooling only; public assemblies contain no `Parked`, `Paused`, `Rearm`,
  `Resume`, stream-version, or diagnostic-ticket member (DR-017).
- **DR-AC-029** Outbox kind isolation: provider certification proves include/exclude claims
  for every shipped provider; the external pump never passes `continue` to
  `IMessageDispatcher`, while the continuation pump never claims external records
  (DR-034/037).
- **DR-AC-030** Complete public facade: typed registry/definition/instance handles plus
  instance/correlation `IWorkflowEventClient` flows drive durable definitions with inbox dedup.
  Definition fanout and deferred management are absent; no test-issued kernel commands are used
  (DR-003/032/061).
- **DR-AC-031** Telemetry and options: a hosted driver exports every DR-050 instrument with
  the required dimensions; internal and external backlog gauges differ correctly. Invalid
  worker/pump/retry/budget options fail startup, while valid overrides reach the running host
  (DR-033/050/051).
- **DR-AC-032** Durable all-terminal joins: branch completions across host replacement produce
  one authored-order join. `WhenAll` fails without merge after all success/failure when any
  fails; `WhenAllOutcomes` commits one success/failure merge; ancestor deadline/cancellation
  suppresses both merges, and neither failure path automatically cancels a sibling.
  `WhenFirst` is absent (DR-010, CP-002..005).
- **DR-AC-033** Driver-owned continue-as-new: a registered definition requests
  continue-as-new through its public authoring surface; replacement state, lineage, bound
  definition version, structural fingerprint, original absolute workflow deadline, and fresh
  execution position commit atomically, and restart resumes the generation (DR-010/011/012).
- **DR-AC-034** Lease selector commit boundary: crash before selection commit may repeat a
  deterministic selector and causes no provider effect; crash after commit reuses the exact
  normalized selection and acquires at most one obligation. A normalized selection containing
  any unconfigured name fails with `ResourcePoolNotConfiguredException`/
  `WF-RESOURCE-POOL-NOT-CONFIGURED`, reports the complete missing-name set, and creates no queue,
  reservation, ticket, operation binding, or other mutation (DR-038).
- **DR-AC-035** Lease ancestry analysis and runtime defense: mutually exclusive `If` scopes,
  sequential same-fiber scopes, one lexical root-loop scope per iteration, and independent
  siblings can acquire; acquisition under an active ancestor and manual-plan equivalents reject
  before pool mutation (DR-038, MG-063).
- **DR-AC-036** Lease retry and scope exit preserve capacity: normal lexical-body completion or
  definite pre-effect failure/cancellation commits exact release before branch/item return, merge,
  or parent acquisition. Retryable ambiguity remains `AmbiguousHeld` under the same operation,
  protection token, tickets, and capacity with no overlapping in-process leased retry; host-loss
  recovery may retry the same operation. Ambiguous scope exit, exhaustion, workflow cancellation,
  deadline, termination, or abandonment transfers atomically to quarantine before parent progress,
  and restart preserves that ordering. A queued cancellation wins with zero reservation and makes
  a later grant stale; a reservation-first race remains `PendingCommit` and capacity-held until
  exact pre-activation compensation or post-activation proven release/quarantine. Restart preserves
  the winner and the loser command is a stale no-op (DR-038).
- **DR-AC-037** Continue-as-new requires quiescence: it is absent inside the leased builder and
  runtime-rejected while any obligation remains, but succeeds at root after all lexical scopes
  release; rollover never clears provider capacity implicitly (DR-038).
- **DR-AC-038** Expiry review is safe: a due live owner is marked and remains capacity-held
  without renewal, a proven orphan/release gap is recovered once by exact fence, ambiguous
  state is held, and a missing expected ticket yields `LeaseLost` (DR-038, MG-064).
- **DR-AC-039** Step operation coordinate is stable: crash, retry, timeout reconciliation,
  optimistic conflict, and host replacement retain one `StepOperationId`. Host-loss replay of an
  in-flight policy attempt reuses its `AttemptNumber` and deadline; only a committed policy retry
  increments the attempt. Loop/item/branch/generation repetition receives another ID
  (DR-012/014/019).
- **DR-AC-040** Typed output is atomic and structurally fingerprint-bound: a successful
  `End<TOutput>` commits output, optional fixed outcome, final state, and terminal fact together.
  Same-version structural drift conflicts; opaque output-selector changes require a new version
  (DR-011/012/016).
- **DR-AC-041** Generic protected-work confirmation is exact: live lexical ownership is
  `NotConfirmable`; first quarantine confirmation is `Released`; retry or another accepted
  confirmation after release is `AlreadyConfirmed`; and normal-release/unknown is
  `TokenNotFound`. A confirmation ID bound to another token takes precedence as
  `ConfirmationConflict` before target-token state evaluation and cannot release a successor
  (DR-038).
- **DR-AC-042** Durable resource-governance provider: conflict/crash injection across the
  partition-wide expected-version aggregate proves atomic multi-pool FIFO grants, workflow
  handoff recovery, resize debt, confirmation tombstones, and exact conservation. After resize
  from creation capacity N to current capacity M, restart with the original definition preserves
  M and debt; a changed creation definition fails even if it supplies M. Unknown `GetAsync` and
  `ResizeAsync` fail with `ResourcePoolNotConfiguredException` and the stable code, while
  non-positive resize fails with `ArgumentOutOfRangeException`; all fail before binding or
  mutation (DR-038, MG-065, PR-017).
- **DR-AC-043** Hosting roles: durable engine and callback-only ingress register exactly their
  document-17 services; incomplete/conflicting provider roles and legacy catch-all/hosted-service
  toggles fail or are absent (DR-032/033).

## 16.8 Phasing

Fits document 13 as the durable execution work behind the first-release typed workflow,
composition, leasing, and DAG slices. Alternative hosts re-use the same interpreter:

1. **DR-P1 Interpreter + position envelope** (DR-010..019, incl. the DR-011a command
   contract changes, detached state/`ReplaceState`, typed input/output, operation identity,
   structural fingerprint,
   and durable deadlines/policies) — hardest correctness work; gate:
   DR-AC-001/002/005/006/009/013/018/023/024/025/028/033/039/040 on the
   in-memory provider.
2. **DR-P2 Lane host + continuation signal** (DR-030..037, DR-003 seam, DR-037
   kind-partitioned claims) — gate: DR-AC-003/004/010/011/014/015/017/019/020/021/022/
   026/027/029, with provider certification on every shipped provider and the restart/
   multi-host scenarios on PostgreSQL.
3. **DR-P3 Composition + scoped leasing** (DR-010/012/019/038) — gate:
   DR-AC-016/020/022/032/034…038/041/042 plus provider certification.
4. **DR-P4 Typed DAG driving** (DR-041) — gate: DR-AC-008 and JS-AC-001…017. Saga absence is
   guarded by DR-AC-007; saga implementation is not a v1 phase.
5. **DR-P5 Facade, parity, samples, telemetry** (DR-032, DR-050/051, DR-060/061) — gate:
   DR-AC-012/030/031/043, sample e2e, feature-matrix update.

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
