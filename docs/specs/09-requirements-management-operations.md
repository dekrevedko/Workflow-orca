# 9. Management & Operations Requirements (MG)

Scope: the management/operator surface, lifecycle events, operational statistics, stuck
detection, resource lifecycle (activation, eviction), and resource governance. Management
commands are runtime/operator concerns — never workflow graph steps.

## 9.1 First-release instance management

### MG-001 Typed handles, not bulk fluent management
V1 SHALL expose the typed definition and instance handles in document 17 rather than a broad
engine-wide `All().Where(...).Command()` surface. Registration/start is definition-scoped;
inspection and lifecycle commands are instance-scoped. Handles carry validated identities and
grant no access to live runtime objects. Bulk fluent selection remains future design-visible
work and has no first-release interface, extension method, or placeholder.
Runtime-owned keyed correlation lookup and trusted lease-obligation diagnostics are separate
internal/advanced capabilities; neither creates public instance enumeration, multi-ID retrieval,
filter/count, or bulk mutation.

### MG-002 Immutable instance and wait snapshots
`WorkflowInstanceHandle.GetSnapshotAsync` SHALL return `WorkflowInstanceSnapshot` with instance,
mode, definition identity/version/structural fingerprint, exact `WorkflowInstanceStatus`,
timestamps, optional fixed outcome/failure, and all `ActiveWaitSnapshot` values. Wait snapshots
carry `WaitId`, `AuthoredLocation`, `EventName`, `CorrelationId`, registration time, and optional
deadline. A terminal workflow timeout is represented by the instance `Status` and `Failure`; an
active structural wait deadline is represented only by its `ActiveWaitSnapshot`. The v1
application snapshot intentionally omits the absolute workflow deadline and active step-attempt
ordinal/deadline/outcome. Those in-flight facts remain runtime state surfaced through
logs/traces and advanced operational diagnostics, while any capacity-reserving quarantine is
reported separately by `IDurableResourceLeaseDiagnostics`. Snapshot construction grants no
authority and values are fixed-codec detached.

### MG-003 Committed state and typed output only
`GetStateAsync<TState>` SHALL return a detached copy of the last committed **root** state and
throw `WorkflowStateTypeMismatchException` on a mismatched requested type. It SHALL never expose
private branch/item state or an in-flight attempt copy. Only resultful handles expose
`GetOutputAsync<TOutput>`, returning closed `Pending`, `Available`, or `Unavailable` variants;
resultless handles have no output member. Resultful handles additionally expose
`WaitForOutputAsync(CancellationToken)` returning `TOutput`, and typed start results expose the
same success helper after `GetHandleOrThrow()`. Waiting SHALL subscribe before one authoritative
recheck, never poll, throw `WorkflowOutputUnavailableException` for terminal-without-output, and
treat caller cancellation as local to the wait.

### MG-004 Bulk/destructive management is deferred
Any future broad selection, retry, archive, purge, or destructive breadth contract must define
authorization, previews/confirmation, per-instance outcomes, and provider/reference safety.
V1 does not predeclare `All`, `Where`, bulk `Terminate`, or bulk `Purge` members.

### MG-005 Snapshots and closed outcomes only
Queries return immutable detached snapshots/copies; commands return closed outcome values,
never live objects or mutable runtime authority. `DagRunHandle.WaitForTerminalAsync` SHALL return
one committed terminal `DagRunSnapshot` through the same notification/recheck, no-poll,
caller-cancellation-local contract.

## 9.2 First-release commands and deferred operations

### MG-010 Exact v1 command and event set
Each `WorkflowInstanceHandle` SHALL expose `RequestCancellationAsync` and `TerminateAsync`.
Cancellation returns `Requested`, `AlreadyRequested`, or `AlreadyTerminal`; termination returns
`Terminated` or `AlreadyTerminal`. `IWorkflowEventClient` separately exposes only instance and
correlation delivery from EV-010/012. Definition handles expose typed `StartOrGetAsync` in both
modes, with durable mode providing the restart-safe binding.

### MG-011 Durable management commands are deferred
Public `Pause`, `Resume`, failed-instance/step management `Retry`, `GetHistory`, `Archive`, and
`Purge` are deferred in both modes. They SHALL be absent from assemblies and compile fixtures.
Durable provider retention, telemetry, and internal remediation do not imply those application
members.

### MG-012 Recovery boundaries are explicit
V1 recovery comprises wait/timer/event continuation, bounded step retry on the same logical
step operation, crash rehydration, resource reconciliation, cancellation request, and
termination. It never reopens `Completed`, `Failed`, `TimedOut`, `Cancelled`, or `Terminated`.
Failed-instance retry requires a future new-generation identity/state/output/lineage amendment.

### MG-013 Pause/resume reservation (deferred)
`Paused`, `Pause`, `Resume`, pause-window buffering policy, and resume-with-discard are not v1
status or API surface. Their former proposed semantics are historical only. A future amendment
must specify admission, in-flight attempts, wait matching, timer/event buffering, restart,
authorization, and interaction with leases before adding them.

### MG-014 Internal poison/remediation is not an application handle API
Hosts/providers SHALL surface malformed continuation or runtime-state faults operationally and
must never hot-loop or guess execution position. Any advanced compare-and-act repair remains in
runtime-protocol/operator tooling. V1 instance handles expose no `Rearm`, raw stream version,
diagnostic ticket, `Retry`, or `Resume` member.

## 9.3 Lifecycle events

### MG-020 Instance and step lifecycle events
The runtime SHALL record first-class lifecycle events. V1 instance events include created,
activated, evicted, started, suspended/woken, cancellation-requested, completed, failed,
timed-out, cancelled, terminated, and stuck-detected. Step events include scheduled, started,
completed, failed, retried, timed-out, cancellation-signalled, late-result-discarded, and
stuck-detected. Step events carry `StepOperationId` and `AttemptNumber` where applicable without
treating either as a routine metric label. Archive/delete events are reserved for the deferred
public retention capability, though providers may emit internal retention audit records.

### MG-021 Durability of lifecycle events
The contract SHALL state which lifecycle events are durable (published via outbox,
survive restart) vs best-effort in-process, per mode. Terminal-state events SHALL be
consistent with actual state (no completion without its event; no event without the state).

## 9.4 Observability and statistics

### MG-030 Operational statistics
Host/operator projections SHALL support grouped operational queries: counts by definition, version, and
status; active/waiting/failed/timed-out/terminated counts; active waits by event name;
stuck steps/instances; oldest running/suspended/step ages. Exposed via the management
and telemetry integration, backed by projections in durable mode. A public application
`Statistics()` member is not part of the v1 instance-handle surface.

### MG-031 Pressure metrics
Durable statistics SHALL include history/stream growth, checkpoint lag, outbox backlog, and
active-instance pressure (DU-052).

### MG-032 Lifetime tracking
Every instance SHALL track: created, last transition, last active execution, current-status
entry time, total age, current wait age, terminal time. Every executing step SHALL track:
start time, optional progress heartbeat, expected timeout, completion time, outcome.
Application operations SHALL obtain transition timestamps from the runtime `TimeProvider`.
Caller-supplied timestamps are limited to protocol/custom-host seams and deterministic test
fixtures.

## 9.5 Stuck detection and timeouts

### MG-040 Stuck detection
The engine SHALL detect apparently-stuck steps (execution beyond timeout, no heartbeat beyond
threshold, executing-after-crash without reconciliation) and instances (non-terminal without
progress beyond threshold, waiting past expected expiry without outcome). Detection SHALL
produce observable signals (lifecycle events + queryable flags), never silent internal state.
Thresholds are validated host/operator policy; v1 exposes no author-level global options object.

### MG-041 Timeout enforcement
Per-attempt `WithStepTimeout` and whole-workflow `CompleteWithin` deadlines (EV-052) SHALL be
enforced deterministically and reflected in lifecycle events and statistics. A workflow
timeout with unresolved protected work SHALL expose quarantine/cleanup state rather than
pretending terminal status released its lease.

## 9.6 Resource lifecycle (activation & eviction)

### MG-050 Safe eviction
Idle in-memory instances SHALL be evictable without correctness loss: waiting instances with
no runnable work, idle instances between commands, and terminal instances after durable
terminal handling. Instances currently executing a step, mid-commit, or holding
non-delegated short-wait wake-ups SHALL NOT be evicted. The requirement is *safe eviction*
semantics; LRU or idle-timeout is an implementation strategy.

### MG-051 Eviction policy dimensions
Configurable: active idle timeout, optional wait-residency optimization, max active instance
count, and memory-pressure eviction. Every committed durable `Wait` is a safe cold-eviction
boundary; authors do not select residency (EV-040).

### MG-052 Eviction preserves single-mutator
Eviction releases ownership cleanly; rehydration reacquires it; at most one logical mutator
exists at any time, under any eviction/reactivation race (CR-040).

### MG-053 Terminal instances leave memory quickly
After terminal state and lifecycle handling are durably complete, instances SHALL be evicted
from active memory while remaining durably queryable per retention (DU-050).

## 9.7 Concurrency and durable resource governance

### MG-060 Concurrency limits
The engine SHALL support a host-owned per-instance structured-path ceiling and host-local
exact-step-type execution throttles, complementing (never replacing) per-instance serialized
commit authority: serialization is correctness; governance is capacity. V1 SHALL NOT expose an
independent host-wide workflow-instance/advancement ceiling or an untyped general-body ceiling.

The public taxonomy SHALL distinguish: (1) a per-step execution throttle held only around one
attempt, (2) an ephemeral named cross-instance transient pool that is host-local and
re-evaluated after restart, (3) the host-owned
`MaxConcurrentExecutionPathsPerInstance`, (4) DAG-host `MaxConcurrentNodes`, and (5) a
persisted durable resource lease owned by an exact lexical scope occurrence. These names and
lifetimes SHALL NOT be aliases. Mode availability is normative in
[document 17](17-selected-mode-capability-matrix.md).

`StructuredExecutionHostOptions` owns the positive path ceiling and copied
`StepExecutionThrottle` list. `EphemeralEngineHostOptions` combines it with copied
`TransientPoolDefinition` values; `DurableEngineHostOptions` combines it with
`DurableResourcePoolOptions`. Every `StepExecutionThrottle.For<TStep>` is keyed solely by exact
step type; v1 has no untyped global, per-definition, or step-category scope. Duplicate step
types/pool names and non-positive capacities fail startup. Options for one engine role never
silently configure the other. Saturation parks the exact requesting owner until grant or
governing cancellation after releasing the instance turn; it is not workflow failure. V1 has no
fail-fast/capacity-wait-timeout policy or custom transient-governance SPI. Driver segment
budgets such as `MaxSegmentDuration` remain a separate fairness mechanism, not a capacity-wait
exit.

Authors SHALL NOT configure a definition-wide execution-path ceiling. The host owns
`MaxConcurrentExecutionPathsPerInstance`; fixed root `Parallel` branches are admitted in authored
order. Root `ForEach` may declare a positive node `MaxConcurrency`, whose effective limit is
the lower of the node value and host ceiling. `MaxConcurrentNodes` governs DAG child-instance
admission separately, counts every started nonterminal child including one parked in a workflow
wait, and cannot override resource pools.

The execution-path ceiling SHALL use one countable token model. A runnable root, branch, or item
owns one token; it releases that token when parked on a wait, delay, resource request, or join,
and reacquires one before progress. A parent releases its token before admitting branches/items
and reacquires one only for merge/continuation, so ceiling one cannot deadlock fanout because its
parent is waiting. Root-fanout branches/items share the same pool and queue by authored ordinal/item
index. `ForEachOptions.MaxConcurrency` separately counts admitted nonterminal item scopes,
including parked items. A fenced token-ignoring attempt owns no logical path token but keeps its
physical step-throttle/transient slot until it returns.

### MG-061 Named shared pools (ephemeral only)
The ephemeral engine SHALL support host-local pools shared across unrelated ephemeral
workflows. `WithTransientPool(TransientPoolName)` guards the immediately preceding ephemeral
business step and may appear at most once for that step; multiple pool definitions may coexist
on the host, but one step cannot stack them. No durable builder exposes it. Step code never acquires synchronization
primitives directly. `TransientPoolName` is not interchangeable with `ResourcePoolName`.
Transient pools bound in-process execution only, vanish on process exit, and make no restart or
cross-process claim. Durable workflows use per-step throttles and/or MG-062 durable pools.

When an ephemeral fiber cannot obtain transient capacity, it SHALL record an owned
blocked obligation, end its quantum, and release the instance mutation turn. It SHALL NOT
await capacity while retaining the instance turn.
Pending transient admission cancels with its owner, but a granted slot remains counted until
the actual guarded step body returns or stops. Forced workflow termination SHALL NOT free a
slot from a still-running body; process exit resets host-local capacity only after in-process
work has stopped.

### MG-062 Durable resource pools (tickets)
For work that consumes an external capacity-bounded resource for its whole lifetime — the
canonical case: an external job holding database connections while its owning instance is
cold-waiting — the engine SHALL support **durable named resource pools** with **ticket
(lease)** semantics:

- a pool is a durable, provider-partition-wide entity created by
  `DurableResourcePoolDefinition.Create(ResourcePoolName, capacity, reviewAfter)`; capacity and
  review interval are positive, and multiple distinct names may coexist;
- acquisition is a durable scoped structural `AcquireResources(request, body)` node on the
  durable root, a root `If`/`While` nested body, or an independent root-`Parallel` branch/
  root-`ForEach` item builder when no live ancestor lease is active — step code never takes
  locks and no ephemeral builder exposes it; a leased body may sequence and decorate steps,
  nest `If`, wait, and delay, but exposes no `Parallel`, `ForEach`, or `While` and cannot acquire
  another lease;
- one immutable `ResourceLeaseRequirement` is created only through
  `Require(ResourcePoolName, units)` and carries a positive integer unit count;
  `ResourceLeaseRequest.Create(first, additional)` copies its inputs, is non-empty by shape,
  and rejects null requirements or duplicate `ResourcePoolName` values;
- acquisition behaves like a wait: with no ticket available, only the requesting fiber parks
  (cold-capable), unrelated siblings remain runnable, and the instance is globally `Waiting`
  only when no runnable fiber remains; grants flow through the pool's serialized durable
  command lane, FIFO by committed request sequence with no v1 queue-policy plug-in;
- a persistent owned-lease record follows `Queued -> PendingCommit -> Held -> ReviewMarked ->
  AmbiguousHeld -> Quarantined -> Released`, with cancelled-before-grant and `LeaseLost` terminal
  side paths, and carries a runtime-generated
  `LeaseObligationId`, instance, continue-as-new generation, authored node, fiber
  occurrence, scope-entry occurrence, exact ticket set, and provider ownership generation;
  `AuthoredLocation` or a reusable holder string alone is not sufficient;
- pending-commit reservations and held tickets survive restarts and cold waits and reserve
  units across definitions, instances, and hosts sharing the store;
- the ticket remains held while the lexical body executes or is parked in `Wait`; release is
  symmetric and automatic before the parent builder resumes after normal body completion or a
  causally definite failure/cancellation path. Forced/ambiguous cleanup follows the
  fenced/quarantined rule in MG-064 rather than treating terminal status alone as proof that
  protected work ended.

`DurableEngineHostOptions` SHALL contain `StructuredExecutionHostOptions` plus one
`DurableResourcePoolOptions` with a validated `ResourceGovernancePartitionId` and copied pool
definitions. Startup rejects default partition identity, missing/non-positive values, duplicate
pool names, duplicate step-throttle types, and an incomplete durable provider role set. These
host safety values are required; v1 has no `WorkflowAuthoringOptions`.

The public node has no TTL, lease duration, expiry-backstop parameter, holder identity, or
renewal callback. Static requirements are copied and validated immediately at the fluent call.
A dynamic selector is deterministic and side-effect-free; the runtime MAY reevaluate
it after a crash before any selection commit, but it SHALL normalize and durably commit one
non-empty immutable request before the first pool mutation and reuse that committed value on
replay/retry.

After normalization and before queue, reservation, or provider mutation, every named durable
pool SHALL exist in the configured partition. A request containing one or more well-formed but
unconfigured names fails as non-retryable `ResourcePoolNotConfiguredException` with stable code
`WF-RESOURCE-POOL-NOT-CONFIGURED` and the complete missing-name set; the whole request creates no
queue entry, ticket, or operation. This applies equally to static and selector-produced requests,
so an unknown pool never parks indefinitely.

If capacity is unavailable, the acquisition wait persists until the atomic request is granted
or its exact owning path is canceled, fails, or terminates. The baseline public node has no
acquisition-timeout overload; an enclosing authored timeout may end its owner through the
normal cancellation/failure protocol.

The ticket owner SHALL be the requesting lexical lease-scope occurrence on its fiber.
Deterministic release occurs when that body exits normally or on causally definite pre-effect
failure/cancellation. A retryable step timeout, ambiguous submit, or recovered in-flight attempt
while the owner remains recoverable transitions to `AmbiguousHeld`, retaining the same
`StepOperationId`, `LeaseProtectionToken`, ticket set, provider generations, and reserved units.
A later retry cannot overlap a still-running prior body in the same process; host-loss recovery
may retry because that process body is gone. Retry success alone does not erase ambiguity.
Ambiguous scope exit, retry exhaustion, cancellation, workflow deadline, forced termination, or
abandonment transfers to `Quarantined`. Release or quarantine transfer SHALL commit before a
branch/item return, merge, parent continuation, or terminal progression.
`ContinueAsNew` is absent from the leased builder and runtime-rejected while any obligation is
owned, but is legal at a quiescent durable root after every lexical lease scope has exited.

### MG-063 Durable acquisition is atomic and ancestry-safe
A fiber requiring tickets from several pools SHALL acquire them atomically: the allocator
grants all or none; holding a partial set while waiting for the rest is forbidden. All
resources needed concurrently SHALL be requested by one node.

At most one live durable acquisition may exist along an active fiber's inclusive lease-scope
ancestry. A descendant cannot acquire while an ancestor scope is `PendingCommit`, `Held`,
`ReviewMarked`, or `AmbiguousHeld`;
dedicated leased builders therefore omit `AcquireResources`. Independent sibling fibers MAY
acquire independently. The compiler SHALL enforce the rule path-sensitively: mutually
exclusive `If` arms are analyzed separately; sequential same-fiber scopes are legal; one
lexical scope fully contained in a root `While` iteration is legal because it releases before
the next iteration; parent acquisition after a child scope/join is valid only after exact
release commits. All resources needed concurrently belong in one atomic request.

The runtime SHALL repeat the inclusive-ancestry check before queue or pool mutation for
hand-built, legacy, or stale compiled plans. It SHALL inspect both pending owned-lease records
and held active tickets; checking only a transient wait obligation is insufficient.
`ContinueAsNew` SHALL inspect pending, held, review-marked, ambiguous-held, and quarantined lease state for the
whole generation and reject before rollover. No point-acquisition or portable
`StepResult.AcquireResources` path exists in v1.

### MG-064 Ticket review, reconciliation, and pool operations
Ticket leaks are defended by an explicit per-pool review/reconciliation policy. A provider
`review-at` value is a **review deadline**, not a ticket-validity cutoff. A known runtime
deadline such as an external-job timeout MAY ensure review is scheduled no earlier than the
deadline plus a pool-owned margin, but no author supplies a resource TTL. Different pools in
one atomic request MAY schedule different review deadlines.

When a review becomes due, the system SHALL atomically record and signal the review mark as a
lifecycle/audit fact plus queryable state. Marking for review keeps the ticket capacity held and
does not advance waiters. A reconciler compares the exact lease obligation, tickets, owner
generation, and committed workflow state:

- an exact active or reconstructable owner remains held and may receive a later review time;
- a queued or pending-commit grant is preserved, confirmed, or cancelled only from causal
  owner evidence;
- a committed release or causally proven never-committed acquisition releases the exact whole
  obligation idempotently and records a recovered-release audit;
- a terminal owner releases only when the same causal history also proves required lease
  cleanup and protected work stopped (or an end-to-end fence makes the old work unable to use
  the resource); bare `Terminated` status is not that proof and remains reserved/quarantined;
- unavailable or `AmbiguousHeld` owner state remains reserved for operator action;
- an active owner whose expected ticket/fence is missing receives a typed `LeaseLost` outcome
  and MUST NOT continue or silently reacquire.

Every confirm, cancel, release, or reconciliation mutation SHALL compare-and-act on exact
ticket ID, `LeaseObligationId`, and provider ownership generation so stale cleanup cannot
release a successor occurrence. Holder-driven periodic renewal is not required and is absent
from the baseline. Automatic elapsed-time reclaim of a logically active owner requires a
separately approved, provider-certified renewal protocol **and** end-to-end fencing enforced by
the protected resource; renewal alone is insufficient.

Trusted in-process advanced host management SHALL expose
`IDurableResourceLeaseDiagnostics.EnumerateOutstandingAsync` plus token-targeted `GetAsync`.
Its immutable projections correlate the exact `LeaseProtectionToken`, workflow/definition owner,
generation/authored scope, obligation and confirmation states, defensively copied pool/unit/
provider-generation tickets, review marks/deadlines, quarantine time, and accepted confirmation
when retained. Enumeration includes queued, capacity-reserving, reconciliation-required, and
`LeaseLost` obligations, including crash-before-external-label cases; ordinary released/cancelled
history is excluded. This is a trusted diagnostic seam rather than ordinary application bulk
management. A remote adapter supplies authorization and redaction.

The runtime-created `LeaseProtectionToken` available inside a leased step context binds
protected external work to the exact obligation. A trusted host/integration reconciler MAY
submit caller-created idempotent `StopConfirmationId` through the advanced generic
`IDurableResourceLeaseRecovery.ConfirmProtectedWorkStoppedAsync` seam only after every
token-bound work item is terminal, absent after causally sufficient observation, or end-to-end
fenced. The serialized result matrix is normative and ordered. The runtime first evaluates
confirmation identity binding, then retained accepted-confirmation state, then current token
lifecycle; the first matching row wins:

| Token state | Result | Capacity effect |
|---|---|---|
| confirmation ID already bound to another token, regardless of target-token state | `ConfirmationConflict` | none |
| confirmation ID already accepted for this token, or token already released by another accepted confirmation | `AlreadyConfirmed` | none |
| live `PendingCommit`/`Held`/`ReviewMarked`/`AmbiguousHeld` lexical obligation | `NotConfirmable` | none; author scope still owns it |
| quarantined token, unused confirmation ID | `Released` | bind the ID and release exact tickets once |
| normally released, never existed, or retention-purged token | `TokenNotFound` | none |

Normal release racing confirmation SHALL serialize to one outcome: if normal release wins, a
later confirmation is `TokenNotFound`; if confirmation wins, later retry is
`AlreadyConfirmed`. Tombstones for accepted confirmations SHALL be retained at least through
the workflow/provider deduplication window. Elapsed time, workflow status, delete acknowledgement,
or infrastructure label alone is never confirmation.

`IDurableResourcePoolManagement` SHALL expose typed `ListAsync`, `GetAsync`, and idempotent
`ResizeAsync` using caller-stable `ResourcePoolOperationId`. `DurableResourcePoolSnapshot`
reports configured capacity, reserved units, resize debt, queued request count, and oldest review
deadline. `ResizeAsync` returns the closed `DurableResourcePoolResizeResult.Applied` or
`.Conflict`: first application persists `Applied`; exact same-ID/pool/capacity replay returns the
originally recorded `Applied`; changed pool or capacity returns `Conflict` containing recorded
and attempted facts and changes no state. Shrink never revokes reservations; debt is
`max(0, reserved - configured)` and blocks new grants until release/upward resize makes the next
request fit. `GetAsync` and `ResizeAsync` fail with `ResourcePoolNotConfiguredException` and code
`WF-RESOURCE-POOL-NOT-CONFIGURED` when the well-formed pool name is absent. `ResizeAsync` requires
positive capacity; zero or negative capacity throws `ArgumentOutOfRangeException`. Both checks
occur before operation-ID binding, aggregate append, waiter grant, or any other mutation. V1 has
no force-release API; unsafe metadata deletion is non-conforming.

### MG-065 Lease races and capacity accounting are exact
One request identity and the **single serialized resource-governance aggregate for the configured
provider partition** SHALL make grant versus owner
cancel/fail/terminal races choose one durable outcome. A canceled request cannot leave a ghost
grant; a concurrently granted set is committed to the still-active exact owner, released
exactly once before normal owner removal, or retained once in forced-stop quarantine. Release
facts preserve the acquisition's `LeaseObligationId`, instance, fiber, scope occurrence, ticket
IDs, pool names, units, and provider generation.

The winner lifecycle is exact. Cancellation committed while the obligation is still `Queued`
wins as cancelled-before-grant, reserves zero, removes its queue eligibility, and makes any later
grant command a stale no-op. If governance reservation commits first, the obligation is
`PendingCommit` and its exact tickets remain capacity-reserving; cancellation cannot relabel it
cancelled-before-grant or erase the reservation. Cancellation before workflow activation then
performs one exact compensating release because author code was never admitted. Cancellation
after activation follows normal causal cleanup: definite cleanup releases once, while possibly
live protected work transfers to capacity-reserving quarantine before owner progression. Restart
at any boundary preserves the winning state and delayed grant/cancel commands are idempotent
no-ops.

Capacity contribution is state-defined: `Queued`, `Released`, and cancelled-before-grant reserve
zero; `PendingCommit`, `Held`, `ReviewMarked`, `AmbiguousHeld`, and `Quarantined` reserve their
exact units. Before each grant, the serialized pool lane SHALL prove
that each affected pool's reserved units plus its proposed units do not exceed that pool's
configured capacity.
The only permitted `reserved > configured` state is explicit over-capacity debt created by a
downward operator resize. It cannot produce a new grant and is recomputed after exact release
or resize; an upward resize may reduce/clear it.

The global invariant is exact obligation/ticket/unit conservation and grant-time admission,
not equality of an `AvailableCapacity` snapshot before and after one release. Freed capacity MAY
transfer immediately to queued waiters. Exact pre-acquisition numeric restoration is required
only in an isolated no-waiter/no-resize test; contended tests instead prove exact ticket
release, unit conservation, and eligibility of the next waiter.

The aggregate SHALL own every configured pool, atomic multi-pool request, queue position,
reservation/ticket, review mark, resize operation, confirmation binding, and tombstone in the
partition. One expected-version append grants every requirement or none. The workflow/resource
handoff is an idempotent protocol: workflow commits the normalized pending obligation;
governance reserves all exact tickets; workflow commits activation; governance confirms active
ownership. Crashes leave capacity reserved until exact recovery completes or compensates the
occurrence. `IDurableResourceGovernanceStore` SHALL load the partition stream and atomically
append records at an expected aggregate version. A provider that can append only per-workflow
streams is non-conforming for durable leasing.

`DurableResourcePoolDefinition.Capacity` is the immutable creation capacity stored with the pool
definition. `DurableResourcePoolSnapshot.ConfiguredCapacity` is the current capacity obtained by
replaying that seed plus committed resize operations. On later startup, hosts compare name,
creation capacity, and `ReviewAfter` against the persisted creation definition; they never compare
host configuration with the replayed current capacity and never overwrite it. Therefore a host
started with the original definition after a resize succeeds and preserves the resized capacity
and debt, while changing the creation definition fails startup even when the supplied value equals
the current resized capacity.

Certification SHALL expose exactly four friend-only post-commit barriers:
`WorkflowPendingObligationCommitted`, `GovernanceReservationCommitted`,
`WorkflowActivationCommitted`, and `GovernanceOwnershipConfirmed`. Each immutable barrier fact
contains partition, obligation, instance, generation, fiber occurrence, scope-entry occurrence,
protection token, workflow/governance versions, and every then-known ticket ID/pool/units/provider
generation. The barrier signals and blocks after its durable commit and before the next protocol
command, enabling deterministic host stop/restart without timing sleeps. Recovery SHALL preserve
equal ownership facts and create neither a ghost nor duplicate grant.

`ResourceGovernanceRecord.FromPersisted` validates one positive sequence, supported protocol
format, checksum, and copied payload. `ResourceGovernanceStream.Create(version, records)`
rejects negative/mismatched version, null collection/entry, gap, duplicate, or reordering;
defensively copies the collection; requires version zero exactly when empty; and validates the
complete v1 sequence `1..Version`. `AppendAsync` copies one non-empty batch numbered exactly
`expectedVersion + 1..expectedVersion + count` and commits all or returns expected-version
`Conflict`; partial or empty append is non-conforming.
