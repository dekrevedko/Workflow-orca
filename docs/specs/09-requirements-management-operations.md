# 9. Management & Operations Requirements (MG)

Scope: the management/operator surface, lifecycle events, operational statistics, stuck
detection, resource lifecycle (activation, eviction), and resource governance. Management
commands are runtime/operator concerns — never workflow graph steps.

## 9.1 Fluent management model

### MG-001 Scope → filter → terminal
The management API SHALL be fluent and composable with three explicit stages:

1. **Scope selection** — engine-wide (`All()` / `Where(...)` at root), definition-scoped
   (`Engine<TDefinition>...`), instance-scoped (`Instance(id)`), step-scoped
   (`Instance(id).Step(stepId)`), saga-scoped (`Instance(id).Saga()`).
2. **Filtering** — canonical `Where(predicate)`; selection/filter operations have no side
   effects.
3. **Terminal operation** — either a query (`List`, `Count`, `Get`, `GetState<TState>`,
   `Statistics`, `GetActiveWaits`, `GetHistory`, `GetLifecycleEvents`,
   `GetCompensationState`) or a command (`Pause`, `Resume`, `RaiseEvent`, `Cancel`,
   `Terminate`, `Retry`, `Archive`, `Purge`).

Selection hidden in method names (`RetryAllFailed()`) is non-conforming. `All()` always means
"all instances in the current scope."

### MG-002 Constrained, translatable predicates
`Where(...)` SHALL accept expression-style predicates over a defined, query-safe model of
runtime metadata (status, definition, version, timestamps, stuck/timeout flags, correlation
fields). Arbitrary delegates, external calls, and non-translatable constructs are rejected.
The public shape is LINQ-like; the internal structured query model is the provider contract.

### MG-003 Query/command symmetry
Any selection expressible for a query SHALL be expressible for a command, subject to
mode/capability support: `Where(x => x.Status == Failed).Retry()` mirrors
`Where(x => x.Status == Failed).List()`.

### MG-004 Destructive breadth safety
Broad destructive commands (`All().Terminate()`, `All().Purge()`) SHALL have explicit safety
semantics (confirmation/force parameters, count-returning previews, or equivalent) defined by
the API contract.

### MG-005 Snapshots only
Management queries return immutable snapshots and copies (CR-021); commands return outcome
reports (affected counts, per-instance results), never live objects.

## 9.2 Command set and mode availability

### MG-010 Core command set
Minimum surface: `Start` (definition-scoped), `Instance(id)` retrieval, `List`, `Count`,
`Get`, `GetState<TState>`, `GetActiveWaits`, `RaiseEvent` (all three routing modes, EV-010),
`Cancel`, `Terminate`.

### MG-011 Durable-only commands
`StartOrGet` (strong semantics), `Pause`/`Resume`, `Retry` (failed-instance recovery),
step-level `Retry`, `GetHistory`, `Archive`, `Purge` SHALL be durable-facing; hidden from
ephemeral APIs where practical (DD-003 discipline), rejected fast otherwise.

### MG-012 Recovery is first-class
Recovery SHALL cover at least: wait-resume (events), failure-retry (instance and step level,
durable), and explicit terminal actions. Retry semantics (from where execution resumes, what
state is kept) SHALL be explicit.

### MG-013 Pause/Resume semantics (durable-only)
`Pause` SHALL stop advancement of an instance **without losing anything**:

- the instance moves to `Paused` at the next safe commit boundary; an in-flight step MAY run
  to completion and its result is committed, but no further advancement occurs;
- events and timer firings targeting a paused instance SHALL be accepted and durably
  buffered (mailbox/inbox per EV-030/DU-030) — never rejected, never lost — and SHALL NOT
  resume or advance the instance; an external event never un-pauses an instance (operator
  intent dominates);
- wait records remain registered, but matching is deferred while paused;
- paused state survives restart: after rehydration the instance is still `Paused`.

`Resume` SHALL be the only transition out of `Paused`, and SHALL offer explicit handling of
the deliveries buffered during the pause window:

- **Replay** (default) — process buffered deliveries in arrival order through the standard
  matching path (EV-020/EV-030), with deduplication still applying (EV-031);
- **Discard** — drop the buffered deliveries without matching. Discards SHALL be recorded
  durably for audit (inbox outcome such as `DiscardedOnResume`), and the discarded
  `EventId`s remain deduplicated — a later redelivery of the same event cannot match.

The initial contract is all-or-nothing over the pause window's buffer; selective per-event
discard MAY be added later as an extension.

## 9.3 Lifecycle events

### MG-020 Instance and step lifecycle events
The runtime SHALL publish first-class lifecycle events. Instance: created, activated,
evicted, started, suspended, resumed, completed, failed, timed out, cancelled, terminated,
archived, deleted, stuck-detected. Step: scheduled, started, completed, failed, retried,
timed out, cancelled, compensation started/completed, stuck-detected.

### MG-021 Durability of lifecycle events
The contract SHALL state which lifecycle events are durable (published via outbox,
survive restart) vs best-effort in-process, per mode. Terminal-state events SHALL be
consistent with actual state (no completion without its event; no event without the state).

## 9.4 Observability and statistics

### MG-030 Operational statistics
The engine SHALL support grouped operational queries: counts by definition, version, and
status; active/waiting/failed/timed-out/terminated counts; active waits by event name;
stuck steps/instances; oldest running/suspended/step ages. Exposed via the management
surface (`Statistics()`), backed by projections in durable mode.

### MG-031 Pressure metrics
Durable statistics SHALL include history/stream growth, checkpoint lag, outbox backlog, and
active-instance pressure (DU-052).

### MG-032 Lifetime tracking
Every instance SHALL track: created, last transition, last active execution, current-status
entry time, total age, current wait age, terminal time. Every executing step SHALL track:
start time, optional progress heartbeat, expected timeout, completion time, outcome.

## 9.5 Stuck detection and timeouts

### MG-040 Stuck detection
The engine SHALL detect apparently-stuck steps (execution beyond timeout, no heartbeat beyond
threshold, executing-after-crash without reconciliation) and instances (non-terminal without
progress beyond threshold, waiting past expected expiry without outcome). Detection SHALL
produce observable signals (lifecycle events + queryable flags), never silent internal state.
Thresholds are configurable at definition level.

### MG-041 Timeout enforcement
Step/scope timeout policies (EV-052) SHALL be enforced deterministically and reflected in
lifecycle events and statistics.

## 9.6 Resource lifecycle (activation & eviction)

### MG-050 Safe eviction
Idle in-memory instances SHALL be evictable without correctness loss: waiting instances with
no runnable work, idle instances between commands, and terminal instances after durable
terminal handling. Instances currently executing a step, mid-commit, or holding
non-delegated short-wait wake-ups SHALL NOT be evicted. The requirement is *safe eviction*
semantics; LRU or idle-timeout is an implementation strategy.

### MG-051 Eviction policy dimensions
Configurable: active idle timeout, short-wait residency window, max active instance count,
memory-pressure eviction. `WaitLong` registration is a natural eviction boundary (EV-041).

### MG-052 Eviction preserves single-mutator
Eviction releases ownership cleanly; rehydration reacquires it; at most one logical mutator
exists at any time, under any eviction/reactivation race (CR-040).

### MG-053 Terminal instances leave memory quickly
After terminal state and lifecycle handling are durably complete, instances SHALL be evicted
from active memory while remaining durably queryable per retention (DU-050).

## 9.7 Resource governance (optional capacity limits)

### MG-060 Concurrency limits
The engine SHALL support optional limits on concurrent workflow advancement and concurrent
step execution, complementing (never replacing) per-instance serialization: serialization is
correctness; governance is capacity.

### MG-061 Named shared pools (transient, in-process)
The engine SHALL support named resource pools shared across unrelated workflows (e.g.,
"at most 4 concurrent db-backed operations process-wide"). Steps/definitions MAY carry
optional, serializable pool-key hints; step code never acquires synchronization primitives
directly — hosts enforce pools. Transient pools bound **in-process step execution** only;
they do not survive restarts and their distributed (cross-process) enforcement is explicitly
out of scope. Capacity governance of work that outlives a step or a process is the job of
durable pools (MG-062).

### MG-062 Durable resource pools (tickets)
For work that consumes an external capacity-bounded resource for its whole lifetime — the
canonical case: an external job holding database connections while its owning instance is
cold-waiting — the engine SHALL support **durable named resource pools** with **ticket
(lease)** semantics:

- a pool is a durable, engine-wide entity: name, capacity, lease-expiry policy; multiple
  pools coexist (e.g. one per database);
- acquisition is declared as a resource-requirement decorator on a step, scope, or
  composite (CR-006) — step code never takes locks;
- acquisition behaves like a wait: with no ticket available, the instance suspends
  (`Waiting`, cold-capable) and resumes when granted; grants flow through the pool's own
  serialized durable command lane, FIFO by default;
- a held ticket is a durable record (pool, holder instance + node, acquired-at, expires-at)
  surviving restarts and cold waits; capacity is NEVER exceeded — across definitions,
  instances, and hosts sharing the store;
- release is symmetric and automatic at the guarded scope's exit on **every** terminal path
  (success, failure, timeout, cancellation, termination); explicit early release MAY be
  offered.

### MG-063 Multi-pool acquisition is all-or-nothing
A scope requiring tickets from several pools SHALL acquire them atomically: the allocator
grants all or none; holding a partial set while waiting for the rest is forbidden (no
hold-and-wait, therefore no deadlock). Grant decisions are deterministic; a waiter keeps one
queue position per request.

### MG-064 Ticket expiry, reconciliation, and pool operations
Ticket leaks are defended by explicit per-pool expiry policy: `expires-at` SHOULD align with
the guarded work's timeout plus a reporting margin. An expired ticket is handled per pool
policy (reclaim vs hold-for-operator) and **always signalled** — lifecycle event plus
queryable state — never silently reclaimed, since the external work may still be consuming
the resource. Pools SHALL be operable through the management surface: inspect capacity, held
tickets, queue depth, oldest waiter; resize capacity (shrinking never revokes held tickets —
it only reduces future grants); force-release a ticket as an explicit, audited operator
action.
