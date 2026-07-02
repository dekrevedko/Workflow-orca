# 13. Delivery Phasing and Open Questions

## 13.1 Recommended delivery slices

The matrix (workflow/saga × ephemeral/durable × features) is delivered in slices, not as one
finished grid. Each slice is gated by its acceptance criteria (document 12) going green on
public surfaces.

### Slice 1 — Ephemeral regular core (the semantic proving ground)

Smallest slice that proves the runtime model while covering the highest-risk correctness
areas (waits, loops, branches, joins, routing, serialized execution):

- Primitives: `Init`, business step, `End`, `If`, `While`, `Parallel`, `WhenAll`, `Wait`.
- Event system: envelope, three routing modes, matching, mailbox buffering, `EventId` dedup,
  correlation index with routing-time uniqueness.
- Core guarantees: serialized execution, transactional event consumption, explicit lifecycle
  table, runtime/business state separation, snapshot-only APIs.
- Management: fluent baseline — `Start`, `Instance(id)`, `All()`, `Where(...)`, `List()`,
  `Count()`, `Get()`, `GetState<TState>()`, `GetActiveWaits()`, `RaiseEvent(...)`,
  `Cancel()`, `Terminate()`.
- Explicitly excluded: `WaitLong`, `WhenFirst`, timers, persistence, replay, saga, retry/
  timeout policies, lifecycle publication, retention.
- Gate: AC-001…015 (AC-013's crash-survival clause re-verified in Slice 2), AC-101…110,
  AC-115, AC-201…203, AC-501…503, AC-516.

### Slice 2 — Durable execution core

- Hybrid event-sourced core: per-instance streams, checkpoints, projections, command
  pipeline, expected-version append.
- Rehydration, crash safety, restart-safe dedup (inbox), durable waits, `WaitLong` +
  cold eviction, version binding, `StartOrGet`.
- Outbox with async at-least-once dispatch, pump hooks, unified record kinds.
- Durable management: pause/resume, retry, history, delete/purge/retention baseline.
- Gate: AC-301…AC-311, AC-314, AC-316, AC-114, AC-504…506, AC-512…515, AC-517
  (pause/resume incl. discard-on-resume).
  Excludes AC-312 (pressure metrics — gated in Slice 3 alongside statistics), AC-313
  (continue-as-new — gating depends on open question 10 below), and AC-315 (multi-node —
  gated in Slice 6 as advanced).

### Slice 3 — Time, policies, and operations

- Timer primitive + timer/event races; step/scope timeout policies; structured retry.
- Lifecycle event publication with documented durability; stuck detection; statistics and
  pressure metrics; resource governance (limits, named pools).
- `WhenFirst` with residual policies.
- Gate: AC-111…113, AC-204/205, AC-507…511, AC-312.

### Slice 4 — Composition at scale

- Ephemeral `ForEach` (items, batching, bounded concurrency, join/failure policies).
- Durable `RunChild`/`RunChildren` (lineage, outbox spawning, durable throttling,
  exactly-once resume token, tree queries).
- Gate: AC-6xx (non-saga).

### Slice 4b — DAG front-end & external-job composite (scenario-driven)

Derived from the EKS job-scheduler driving scenario (document 14); depends on Slices 2–4.

- DAG definition input compiling to standard primitives (JS-001).
- `RunExternalJob`-style submit-and-await composite with timeout/retry/cancellation
  (JS-002); DAG run observability from lineage/projections (JS-005); run-cancellation
  propagation (JS-006).
- Durable resource pools with ticket semantics (MG-062…064) and their binding to external
  jobs (JS-007).
- Gate: JS-AC-001…006, JS-AC-009…013, AC-518…522. (JS-AC-007/008 and the EKS adapters land
  with Slice 6.)

### Slice 5 — Saga

- Saga definition kind, compensation scopes/handlers, reverse-order compensation, saga
  terminal states, timeout/cancellation policy; durable compensation tracking, audit,
  operator recovery; child compensation.
- Ephemeral saga as labeled limited mode.
- Gate: AC-4xx, AC-616.

### Slice 6 — Production readiness (explicit gate, not a default promise)

- Real provider adapters (relational store; at least one broker dispatcher) passing the
  certification suite; hosting integration package.
- Continue-as-new; archival policy; multi-node lease model (if pursued).
- Versioning/upgrade story, breaking-change policy, security review, performance targets,
  samples, published packages with semver.
- Gate: AC-315 (multi-node, if pursued); AC-313 (continue-as-new) only if open question 10
  resolves to "deferred" — otherwise AC-313 gates Slice 2 instead; all `[provider]`-tagged
  criteria across documents 06/07/08/12, re-run against each shipped adapter.

## 13.2 Open questions (to resolve during implementation)

Decisions intentionally left open, with the constraint each answer must respect:

1. **Wait unification** — Do `Wait`/`WaitLong` remain separate public concepts forever, or
   converge into one wait with a residency policy? (Must preserve EV-040/041 semantics and
   API-level durable separation.)
2. **History retention depth** — Is full stream history mandatory in durable mode, or is
   checkpoint-plus-essential-operational-events an allowed provider profile? (DU-071's
   inspection contract is mandatory either way.)
3. **Provider emulation** — Must providers offer true event-store semantics, or may they
   emulate append/expected-version over relational tables? (PR-020/021 invariants must hold
   either way.)
4. **Fanout mechanics at scale** — Definition fanout as projection-driven command emission
   vs broker-native publish with instance-side correlation; batching/pagination for large
   fanout.
5. **Business-state typing** — Strongly typed only, or hybrid with schema/versioning help
   from the engine? How much state-migration responsibility belongs to the engine vs the
   application?
6. **Decorator representation** — Attributes, fluent builder calls, metadata objects, or
   hybrid (CR-006 fixes semantics, not syntax).
7. **Compile-time vs runtime mode separation** — How much durable/ephemeral separation is
   enforced by distinct types vs runtime checks (DD-003 direction: as much compile-time as
   practical without doubling every abstraction).
8. **Eviction policy shape** — Plain idle timeout, LRU-like pressure eviction, or hybrid
   (MG-050 fixes the safety semantics only).
9. **Lifecycle event durability split** — Exactly which lifecycle events are durable vs
   best-effort per mode (MG-021 requires the split be documented; the split itself is open).
10. **Continue-as-new timing** — Introduced with the durable core (Slice 2) or deferred to
    production readiness (Slice 6), given history-growth requirements already exist. This
    decision directly determines which slice gates AC-313 — see the Slice 2 and Slice 6
    gate notes above.
11. **Synchronous completion bridge shape** — Await-handle vs poll-to-terminal vs callback
    (CR-016 fixes the requirement, not the shape).
12. **Saga track order** — Whether a process-manager-style (message-driven, no compensation)
    saga precedes the compensation-heavy track.
13. **Ephemeral saga depth** — Fully supported limited mode vs explicitly "advanced/at your
    own risk" labeling (SG-030 minimum stands).
14. **Keep or cut `Yield`** — CR-017 defines `Yield` as a cooperative checkpoint
    (commit-and-reschedule). If no real use case materializes during Slice 1, it MAY be cut
    from the step-result contract (removing AC-013) before the surface stabilizes; the
    decision must be made no later than the end of Slice 2, when the durable commit-boundary
    behavior would otherwise need implementing. Note: loop/`ForEach`/`Parallel` fairness
    (CR-017's listed uses) already argues for keeping it.
15. **DAG compile target** — JS-001 (document 14) compiles DAG definitions onto existing
    primitives. Open: does each DAG node execute as a durable child workflow instance
    (recommended — isolation, lineage, per-node retry via `RunChildren`) or as in-instance
    join/group state (lighter, no per-node identity)? Must be decided when Slice 4b starts;
    both must satisfy JS-AC-001…003 identically.
16. **Durable pool extensions** — MG-062 fixes concurrency-cap (ticket) semantics only.
    Open extensions: rate-based pools (token refill per interval), priority queues instead
    of FIFO, revocation policy beyond expiry, and sharing one logical pool across engines
    that do **not** share a store (would require an external arbiter — likely out of scope
    for the library).

## 13.3 Post-review decisions (recorded)

Decisions made during spec review, now normative:

- **Status tiers**: `Cancelled` and `Terminated` are shared statuses in both modes (their
  commands are core, MG-010); only `Paused` is durable-only. (CR-030, glossary.)
- **Pause buffering, no auto-resume**: events/timer firings during `Paused` are durably
  buffered and never lost, but never advance or un-pause the instance; only explicit
  `Resume` exits `Paused`, replaying buffered deliveries in arrival order. (MG-013.)
- **Cancel vs Terminate**: cancel is graceful/cooperative (token, wait cancellation,
  policies); terminate is forced (no cooperation, no policies, no compensation). (CR-031.)
- **Ephemeral timers are transient**: kept (timeout patterns need them) but explicitly
  in-process, activation-local, lost on exit — the Orleans timer/reminder split. (EV-050.)
- **`Yield` defined**: cooperative checkpoint — commit progress, release the lane,
  reschedule the same step. (CR-017; retention tracked as open question 14.)
- **Named End outcomes**: `End` may carry an outcome name recorded as queryable metadata and
  carried on the end-of-life lifecycle event/publication; no new statuses. (CR-008.)
- **Resume delivery handling**: `Resume` offers Replay (default) or auditable Discard of the
  pause-window buffer. (MG-013, AC-517.)
- **Durable resource pools**: global capacity budgets (e.g. DB connections consumed by
  external jobs) are durable ticket pools — a decorator-declared requirement, acquired
  all-or-nothing before dispatch, held across cold waits/restarts, released on every
  terminal path, expiry always signalled. Distinct from transient in-process pools.
  (MG-062…064, JS-007.)

## 13.4 Conflict resolutions recorded in this package

Where source documents disagreed, this package resolved:

- **Recovery model**: the hybrid event-sourced core (stream + checkpoint + projections) is
  adopted for durable mode, superseding mutable-snapshot-first persistence as the durable
  architecture. The ephemeral engine remains a lightweight in-memory runtime.
- **`Parallel` concurrency**: branches MAY execute concurrently (with serialized commits);
  earlier interim behavior (coordinated sequential branches) is not the specified target.
- **`WhenAll`/`WhenFirst`**: specified as explicit primitives with policies, superseding
  "join semantics only implied by tests."
- **Child workflows**: the v3 split (ephemeral `ForEach` vs durable `RunChild`/`RunChildren`)
  supersedes earlier symmetric child-workflow designs.
- **`CorrelationKey` → `CorrelationId`**: the rename is final across all contracts.
- **Where-predicates**: expression-based LINQ-like public shape over an internal structured
  query model (not structured-query-first, not arbitrary delegates).
