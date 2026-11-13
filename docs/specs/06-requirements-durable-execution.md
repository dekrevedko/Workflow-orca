# 6. Durable Execution Requirements (DU)

Scope: everything the durable execution mode adds — persistence, the recovery model,
rehydration, crash safety, versioning, inbox/outbox, retention, and multi-node direction.
Durable mode changes **guarantees**, not business semantics.

## 6.1 Mode contract

### DU-001 Durability is explicit
Durable mode SHALL be active only when a persistence provider is configured. Ephemeral mode
SHALL be an explicit, honest mode: instances are lost on process exit; durable rehydration,
durable waits/timers, durable history, and post-restart inspection are unavailable and
absent from ephemeral-facing APIs where practical. Requests for durable-only behavior in
ephemeral mode SHALL fail fast with clear diagnostics.

An in-memory implementation of durable ports MAY support development, tests, and executable
documentation, but SHALL be named and documented as non-restart-durable, SHALL emit a clear
startup diagnostic outside explicitly selected development/test use, and SHALL NOT appear in
production-readiness examples.

### DU-002 Feature matrix is explicit
The product SHALL publish a feature matrix declaring, per feature, its availability in
ephemeral and durable workflow mode. Deferred semantic kinds are not v1 matrix axes and expose
no placeholder members. Silent downgrades are non-conforming.

The normative DU-002 matrix and signature baseline is
[document 17](17-selected-mode-capability-matrix.md).

## 6.2 Recovery model (accepted architecture)

### DU-010 Hybrid event-sourced core
Durable execution SHALL use a hybrid event-sourced model:

- **Append-only per-instance event stream** of workflow facts is the write-side source of
  truth.
- **Checkpoints** materialize aggregate state at a stream version; recovery = load checkpoint
  + replay stream tail. Replay-from-genesis is never required for routine operation.
- **Projections** derived from committed events serve queries and routing (instance
  summaries, active waits, accepted-event audit, history, typed outputs, and DAG lineage).
- **Hot memory is a disposable cache** of stream + projections.

Rationale: this satisfies crash safety, restart-safe dedup, queryable metadata, durable wait
ownership, DAG lineage, history-pressure control, and future multi-node ownership
better than mutable-snapshot-only persistence, while checkpoints avoid pure-replay costs.

### DU-011 Command → events write path
Every instance mutation SHALL be requested as a command and processed as:
receive command → resolve/create instance → load checkpoint + stream tail → rebuild aggregate
deterministically → decide new events → append with expected stream version → update
inbox/outbox/projections within the same durability boundary or a clearly defined
transactional chain. Commands are not durable truth; events are; projections are derived
truth.

### DU-012 Engine facts, not business event sourcing
The engine SHALL event-source its own orchestration facts (started, version-bound, step
entered/succeeded/failed, wait registered/matched, event accepted/consumed/duplicate-
  discarded, timer scheduled/fired, branch started/completed, join satisfied, completed,
  failed, and deleted). Business state SHALL remain a typed mutable model
inside the aggregate, persisted materialized in checkpoints. Users SHALL NOT be forced into
domain event sourcing.

### DU-013 Deterministic rehydration
Rehydration SHALL be deterministic for the same durable inputs, require no prior in-memory
references, and be possible after complete host loss. Inputs: checkpoint, stream tail, wait
records, definition identity/version/structural fingerprint, and the fixed codec format. Output:
an activation ready to resume from the last committed point. Opaque code compatibility is
established by `DefinitionVersion`, not by fingerprinting code.

## 6.3 Crash safety and consistency

### DU-020 Committed state only
If a crash occurs mid-transition, recovery SHALL restore the last committed durable state
only; no partial mutation is ever observable (extends CR-043).

### DU-021 Safe-boundary persistence
Critical persistence and runtime-owned outbound-record creation SHALL happen at safe transition boundaries before
suspension/eviction — never in shutdown or deactivation hooks (best-effort cleanup only).

### DU-022 Serialized execution across processes
The per-instance serialized guarantee (CR-040) SHALL hold across process restarts and
concurrent hosts, enforced minimally by expected-version (optimistic) append; an optional
lease/ownership layer MAY strengthen it for multi-node (DU-060).

## 6.4 Inbox / outbox

### DU-030 Inbox (restart-safe pre-wait ownership and dedup)
Durable ingress SHALL atomically record every accepted external event before a matching wait is
required. Global `EventId` binds the full normalized-envelope fingerprint before route evaluation;
fanout adds stable per-target ownership under one membership snapshot. Records progress through
pending/received, applied/duplicate, or observably poisoned terminal states. Restart and host
replacement MUST NOT lose, duplicate, or silently expire an accepted event.

### DU-031 Outbox (consistent runtime dispatch)
Runtime-owned continuations, lifecycle/status messages, timer work, internal DAG child-start
commands, and workflow-authored outbound events SHALL be derived from committed workflow facts as
durable outbox records in the same commit boundary. Every accepted operation that makes an instance
runnable SHALL commit its continuation in that boundary even when the accepting host lacks the
definition. A workflow-authored `Publish` SHALL commit its fixed-codec public event in that same
boundary.

### DU-032 Asynchronous at-least-once dispatch
Dispatch SHALL be asynchronous, retryable, and at-least-once after commit. Public `workflow-event`
records SHALL reach only `IWorkflowEventDispatcher.DispatchAsync(WorkflowOutboundEvent, ct)` with
closed success/retryable/permanent outcomes. Internal continuation/provider records SHALL use the
internal dispatcher path and SHALL never cross the public event boundary. Retry reuses the committed
identity; cancellation releases the claim; permanent failure records stable poison detail. The
dispatch pipeline SHALL retain manual dispatch, automatic background pumping, poison/failure
handling, retry-delay strategy, and observability hooks for both application and internal paths.

### DU-033 One store, disjoint dispatch kinds
One logical provider outbox SHALL retain every approved record kind, while selector-aware claims
partition public workflow events from internal continuations and other host/provider work. The
public dispatcher never receives internal records, and the internal pump never claims public
workflow events. Uniform storage does not collapse these application and runtime boundaries.

## 6.5 Versioning of long-running instances

### DU-040 Version binding
Every durable instance SHALL be permanently bound to `DefinitionId` + `DefinitionVersion`,
compiled-format version, fixed codec format, and deterministic structural plan fingerprint,
recorded as early durable facts at start. Version and fingerprint SHALL be visible in snapshots
and projections.

### DU-041 No silent corruption
When a definition changes while instances are active/suspended, existing instances remain on
their bound identity/version/fingerprint. Registration of the same identity/version with a
different **structural** fingerprint SHALL fail with a typed conflict. Selector, projector,
merge/output body, step configuration, DAG mapping logic, and external-request construction are
opaque to v1 hashing, so changing any of them SHALL use a new version and side-by-side
deployment. Silently deploying changed opaque behavior under the same version is
non-conforming even when the structural fingerprint is unchanged.

### DU-043 Typed output commits with completion
A successful durable root `End` SHALL serialize and commit its declared typed output, optional
fixed `WorkflowOutcomeName`, final state checkpoint, and terminal fact atomically. A crash
cannot expose completion without output or output without completion. Resultless definitions
have no phantom output payload. Typed outputs are available to typed handles and internal DAG
dependency mapping without exposing child business state.

### DU-042 Continue-as-new
Long-lived durable root workflows SHALL expose `ContinueAsNew(replacementState)`. The winning
commit starts a new generation under the same `InstanceId`, definition identity/version,
structural fingerprint, original `CompleteWithin` absolute deadline, and documented lineage,
using the supplied fixed-codec-normalized replacement root state and a fresh execution position.
It is legal only at a quiescent root after every lexical lease scope has exited; it never clears
waits, tickets, quarantine, or other owned obligations implicitly.

## 6.6 Retention and cleanup

### DU-050 Separated policies
The product SHALL separate: active-memory eviction, durable retention, archival, and hard
deletion. Terminal instances leave memory quickly but remain durably inspectable per
retention policy (e.g., keep failed longer than completed; archive after N days; delete after
M days).

### DU-051 Safe purge/archive is a deferred public capability
Provider retention and physical cleanup SHALL never remove active instances, live continuation/
inbox/outbox references, lease obligations, confirmation tombstones, or required audit state.
Public `Archive` and `Purge` commands are deferred and have no v1 handle, fluent selection,
alias, or placeholder. Their future amendment must define reference safety, authorization,
retention policy, and provider certification before exposing application/operator commands.

### DU-052 History pressure visibility
Stream length, checkpoint lag, outbox backlog, and payload-size pressure SHALL be observable
through operational statistics before they become outages.

## 6.7 Idempotent start

### DU-053 StartOrGet
Both typed definition-handle families SHALL provide
`StartOrGetAsync(input, StartIdempotencyKey)`: compatible reuse returns the existing instance
instead of creating a duplicate. `StartIdempotencyKey` is distinct from
`InstanceId`, `DefinitionId`, and step-operation identities; it is encoded as one scalar string,
uses exact ordinal case-sensitive equality, and rejects default/whitespace/surrounding
whitespace. The repository baseline keeps one provider-global key
namespace across definitions; changing that namespace requires an explicit migration decision.
Every provider SHALL enforce the same equality even when its default collation is
case-insensitive. The durable binding also records definition identity/version, structural
fingerprint, and `PayloadFingerprint` of the exact fixed-codec input bytes. V1 uses the
nonreplaceable System.Text.Json-based format identifier `orcacore-json-v1`; every host validates
the supported type graph at registration and the same supported graph/order produces the same
bytes. Authors SHALL normalize unordered sets/maps before crossing the boundary. Changing the
codec is a future format migration, not a provider registration option. Reuse through another
definition/version or with a different structural or payload fingerprint
returns stable `StartIdempotencyConflict` and never returns an incompatible instance through
the typed handle. Durable mode persists the binding across restart; ephemeral mode preserves
the same within-process semantics without claiming restart survival.

### DU-054 Explicit host-scoped registration
Definition registration SHALL be an explicit host-scoped operation through
`IWorkflowDefinitionRegistry` and SHALL return a typed definition handle whose
`StartOrGetAsync` method infers input/output types without phantom generic parameters. V1 has no
start-by-raw-identity overload, so start cannot register a definition as a side effect.

### DU-055 Split-host continuation contract
Every accepted event delivery SHALL atomically record deduplication, the delivery outcome, and
an at-least-once continuation handoff. A definition-owning host MAY progress inline; a
definition-less callback host records the accepted delivery and leaves progression to a
definition-owning pump. The application receives the exact `EventDeliveryResult` status from
EV-012; it does not depend on or observe whether progression happened inline.

### DU-056 Stable identity for bounded external API calls
A durable business-step context SHALL expose one runtime-created `StepOperationId` for its
logical visit and a separate positive `AttemptNumber`. `AttemptNumber` is the ordinal of one
durable retry-policy attempt, not a count of physical CLR invocations. Before the first physical
invocation of that attempt, the runtime SHALL commit or deterministically derive the operation ID,
attempt ordinal, any absolute attempt deadline, and the in-flight dispatch marker. If a host is lost before a winning transition
commits, recovery re-invokes the same attempt coordinate without consuming another `maxAttempts`
slot or resetting its deadline. Only a committed retry transition after a retryable failure or
timeout advances `AttemptNumber`; consequently `maxAttempts = 1` still permits at-least-once crash
replay of attempt one but permits no second policy attempt. The operation ID survives retry,
timeout reconciliation, replay, expected-version conflict, process replacement, and competing
drivers; a new loop visit, item, branch, or continue-as-new generation receives a new ID.
Application adapters MAY use that ID to implement idempotent create-or-observe calls. OrcaCore
guarantees stable identity and at-least-once invocation, not exactly-once external effects.

An external watcher reports a normalized event through the ordinary typed event-delivery
surface and reuses one caller-stable `EventId` for redelivery of the same logical report.
Inbox deduplication and DU-055 progression apply regardless of which host accepted it. Public
`RunExternalJob`, `ExternalJobKey`, `ExternalJobId`, job-specific completion/failure methods,
and job-system policy types are deferred; no v1 runtime facade or protocol placeholder exposes
them. Kubernetes/AWS/job integration belongs to an outward companion project (PR-005, JS-003).

## 6.8 Multi-node direction (advanced)

### DU-060 Ownership model
Single-host correctness relies on optimistic append (DU-022). "Multi-node execution" here
means concurrent multi-mutator execution of **one** logical instance across hosts; when
introduced it SHALL define explicit per-instance ownership (time-bound leases or partition
ownership) preserving one-logical-mutator across hosts, with tests for duplicate-activation
defense. Until then, single-instance multi-node execution is out of scope and SHALL be
documented as such.

Amendment (R14, spec 16 DR-035): **independent-instance distribution** — multiple hosts
running the same durable lane driver against one store, distributing work across *distinct*
instances via claim-based pumps (timers, outbox, continuation) with expected-version append
as the cross-process guard — is permitted and is NOT the out-of-scope case above. It carries
no per-instance ownership; hot single-instance contention across hosts is resolved by
conflict-retry, not ownership. Cluster-wide single activation of one instance remains the
Orleans host's territory.

## 6.9 Durable inspection

### DU-070 Queryable durable metadata
Hosts SHALL maintain queryable durable metadata/projections by status, definition, version,
wait state, correlation, and timestamps without business-payload deserialization, across hot
and cold instances uniformly. V1 application handles expose the exact per-instance snapshot,
last committed root state, active waits, and typed successful output from document 17; broad
list/filter/statistics queries remain a host/operator projection concern until separately
approved.

### DU-071 Durable history projection, public query deferred
The durable stream SHALL retain or project enough ordered facts for host/operator debugging and
provider certification. The exact retention depth remains provider/host policy as tracked in
[document 13](13-phasing-and-open-questions.md#132-open-questions-to-resolve-during-implementation).
A public application `GetHistory` member is deferred and SHALL NOT appear in v1; telemetry and
advanced operator tooling may consume provider projections without enlarging instance handles.
