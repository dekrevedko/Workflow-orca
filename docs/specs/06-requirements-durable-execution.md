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

### DU-002 Feature matrix is explicit
The product SHALL publish a feature matrix declaring, per feature, its availability in each
axis combination (ephemeral/durable × workflow/saga). Silent downgrades are non-conforming.

## 6.2 Recovery model (accepted architecture)

### DU-010 Hybrid event-sourced core
Durable execution SHALL use a hybrid event-sourced model:

- **Append-only per-instance event stream** of workflow facts is the write-side source of
  truth.
- **Checkpoints** materialize aggregate state at a stream version; recovery = load checkpoint
  + replay stream tail. Replay-from-genesis is never required for routine operation.
- **Projections** derived from committed events serve queries and routing (instance
  summaries, active waits, pending events, history, saga compensation state).
- **Hot memory is a disposable cache** of stream + projections.

Rationale: this satisfies crash safety, restart-safe dedup, queryable metadata, durable wait
ownership, saga traceability, history-pressure control, and future multi-node ownership
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
entered/succeeded/failed, wait registered/matched, event buffered/consumed/duplicate-
discarded, timer scheduled/fired, branch started/completed, join satisfied, completed,
failed, deleted, compensation lifecycle). Business state SHALL remain a typed mutable model
inside the aggregate, persisted materialized in checkpoints. Users SHALL NOT be forced into
domain event sourcing.

### DU-013 Deterministic rehydration
Rehydration SHALL be deterministic for the same durable inputs, require no prior in-memory
references, and be possible after complete host loss. Inputs: checkpoint, stream tail, wait
records, definition identity/version. Output: an activation ready to resume from the last
committed point.

## 6.3 Crash safety and consistency

### DU-020 Committed state only
If a crash occurs mid-transition, recovery SHALL restore the last committed durable state
only; no partial mutation is ever observable (extends CR-043).

### DU-021 Safe-boundary persistence
Critical persistence and event publication SHALL happen at safe transition boundaries before
suspension/eviction — never in shutdown or deactivation hooks (best-effort cleanup only).

### DU-022 Serialized execution across processes
The per-instance serialized guarantee (CR-040) SHALL hold across process restarts and
concurrent hosts, enforced minimally by expected-version (optimistic) append; an optional
lease/ownership layer MAY strengthen it for multi-node (DU-060).

## 6.4 Inbox / outbox

### DU-030 Inbox (restart-safe dedup)
The engine SHALL durably record received external deliveries by `EventId` with states
`Received`, `Applied`, `DuplicateIgnored`, `Poisoned`. Duplicate events after restart MUST
NOT produce duplicate committed outcomes. The inbox also serves operational audit and replay
defense. This is the transactional-inbox pattern: event ingestion and state mutation commit
in one logical transaction (EV-032).

### DU-031 Outbox (consistent publication)
Outbound messages (external events, status messages, child-start commands) SHALL be derived
from committed workflow events as durable outbox records **in the same commit boundary** as
the events themselves. The engine SHALL never dispatch a message that was not first committed
as an outbox record.

### DU-032 Asynchronous at-least-once dispatch
Outbox dispatch SHALL run after commit: asynchronous, retryable, at-least-once, through the
pluggable dispatcher port. The dispatch pipeline SHALL support: manual dispatch, automatic
background pumping, poison/failure handling hooks, retry-delay strategy hooks, and
observability hooks. Consumers are expected to handle at-least-once delivery; the engine
SHALL make the guarantee explicit.

### DU-033 One unified outbox
A single logical outbox SHALL carry all outbound record kinds (external messages, child-start
commands, status messages) so ordering/backlog management and operational tooling are
uniform. Saga compensation dispatch extends the same outbox.

## 6.5 Versioning of long-running instances

### DU-040 Version binding
Every durable instance SHALL be permanently bound to `DefinitionId` + `DefinitionVersion`,
recorded as an early durable fact at start. Version SHALL be visible in snapshots,
projections, and management queries.

### DU-041 No silent corruption
When a definition changes while instances are active/suspended: compatible versions MAY
continue per documented rules; incompatible changes SHALL either fail explicitly with
versioning diagnostics or leave existing instances isolated on their bound version. Silently
resuming an instance under changed semantics is non-conforming. Zero-downtime deployment
guidance (side-by-side versions) SHALL be documented.

### DU-042 Continue-as-new
Long-lived instances SHALL have a history-control mechanism (continue-as-new or equivalent
checkpoint-lineage rollover) that bounds stream growth while preserving logical identity and
documented continuity semantics.

## 6.6 Retention and cleanup

### DU-050 Separated policies
The product SHALL separate: active-memory eviction, durable retention, archival, and hard
deletion. Terminal instances leave memory quickly but remain durably inspectable per
retention policy (e.g., keep failed longer than completed; archive after N days; delete after
M days).

### DU-051 Safe purge/archive
Archive and purge operations SHALL follow documented policy, never remove active instances,
and never break in-flight consumers or lifecycle handling. Application-facing retention uses
a declarative retention policy; raw cutoff purges remain operator/provider-oriented.

### DU-052 History pressure visibility
Stream length, checkpoint lag, outbox backlog, and payload-size pressure SHALL be observable
through operational statistics before they become outages.

## 6.7 Idempotent start

### DU-053 StartOrGet
Durable mode SHALL provide an idempotent start (`StartOrGet(key, input)`): retrying a lost
start with the same idempotency key returns the existing instance instead of creating a
duplicate. Ephemeral mode MAY offer a best-effort variant.

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
Operators SHALL be able to query durable instances by status, definition, version, wait
state, correlation, and timestamps from durable metadata/projections — without
business-payload deserialization — across hot and cold instances uniformly (see MG
requirements for the surface).

### DU-071 Durable history
Durable mode SHALL provide an operator-facing history/timeline per instance (derived from the
event stream) sufficient for debugging: what happened, in what order, with what outcome.
Whether full history retention is mandatory or checkpoint-plus-essential-events is a
provider/policy choice is tracked as [open question 2](13-phasing-and-open-questions.md#132-open-questions-to-resolve-during-implementation)
("History retention depth"); the inspection contract itself is mandatory regardless of how
that question resolves.
