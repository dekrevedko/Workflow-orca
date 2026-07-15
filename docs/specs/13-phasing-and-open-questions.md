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
  (pause/resume incl. discard-on-resume). AC-513 gates here for its **event-buffering
  clause**; its timer-firing clause is re-verified in Slice 3 once durable timers exist.
  Excludes AC-312 (pressure metrics — gated in Slice 3 alongside statistics), AC-313
  (continue-as-new — gating depends on open question 10 below), and AC-315 (multi-node —
  gated in Slice 6 as advanced).

### Slice 3 — Time, policies, and operations

- Timer primitive + timer/event races; step/scope timeout policies; structured retry.
- Lifecycle event publication with documented durability; stuck detection; statistics and
  pressure metrics; resource governance (limits, named pools).
- `WhenFirst` with residual policies.
- Gate: AC-111…113, AC-204/205, AC-507…511, AC-312, plus AC-513's timer-firing clause
  (deferred from Slice 2).

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
- Gate: JS-AC-001…007, JS-AC-009…013, AC-518…522 — JS-AC-007 lands here because durable
  pool quota enforcement is library behavior, verified against in-memory/reference pool
  stores and a fake dispatcher. (JS-AC-008 and the real EKS adapters land with Slice 6.)

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

1. **RESOLVED (2026-07-02): `Wait` and `WaitLong` stay separate public concepts.**
   `WaitLong` remains durable-only and represents cold-evictable, restart-safe waits.
   `Wait` remains the regular wait concept for public surfaces that do not make durable
   residency guarantees. Implementations may share internals, but the API-level durable
   separation is permanent.
2. **RESOLVED (2026-07-02): durable history is retention-policy governed.** Durable
   providers MUST expose and document a retention policy; the library does not require
   providers to keep complete stream history forever. DU-071 inspection remains mandatory
   within the active retention window and for essential operational facts required by
   management, recovery, audit, and compliance-oriented queries.
3. **Provider emulation** — Must providers offer true event-store semantics, or may they
   emulate append/expected-version over relational tables? (PR-020/021 invariants must hold
   either way.)
4. **RESOLVED (2026-07-02): fanout may use provider-native mechanics when available.**
   The library defines fanout semantics and correlation guarantees; providers may implement
   large fanout through native capabilities such as broker publish/routing in RabbitMQ when
   that preserves OrcaCore delivery, deduplication, batching, and observability contracts.
   Providers without native fanout use projection-driven command emission with pagination.
5. **Business-state typing** — Strongly typed only, or hybrid with schema/versioning help
   from the engine? How much state-migration responsibility belongs to the engine vs the
   application?
6. **RESOLVED (2026-07-02): fluent builder calls plus explicit metadata objects.**
   Retry, timeout, cancellation, and resource hints are authored through fluent builder
   methods and represented as explicit definition metadata. Attributes and reflection-based
   policy discovery are not part of this implementation track.
7. **RESOLVED (2026-07-02): compile-time separation as far as practical without doubling every abstraction.**
   Durable-only and ephemeral-only features SHOULD be absent from the wrong public surface
   through distinct authoring/runtime entry points where that keeps APIs clear and does not
   duplicate every shared contract. Shared abstractions remain shared when the semantic
   contract is genuinely common; runtime validation is acceptable for edge cases and
   provider/runtime dispatch paths that would otherwise force parallel abstraction trees.
8. **Eviction policy shape** — Plain idle timeout, LRU-like pressure eviction, or hybrid
   (MG-050 fixes the safety semantics only).
9. **Lifecycle event durability split** — Exactly which lifecycle events are durable vs
   best-effort per mode (MG-021 requires the split be documented; the split itself is open).
10. **RESOLVED (2026-07-02): continue-as-new belongs with the durable core.**
    AC-313 gates Slice 2 because durable history growth exists as soon as event-sourced
    execution exists. Later phases may add archival and production-readiness polish, but
    the core DU-042 rollover contract is an early durable requirement, not a Slice 6-only
    feature.
11. **Synchronous completion bridge shape** — Await-handle vs poll-to-terminal vs callback
    (CR-016 fixes the requirement, not the shape).
12. **RESOLVED (2026-07-02): compensation-heavy saga track comes first.** Phase 5
    implements saga as the compensation-aware semantic kind: forward actions,
    compensation bindings/scopes, reverse deterministic compensation, saga terminal
    states, durable audit/operator recovery, child compensation, and in-process-only
    ephemeral saga mode. A process-manager-style, message-driven saga without
    compensation is not a prerequisite track.
13. **RESOLVED (2026-07-02): Ephemeral saga is in-process only.** Ephemeral saga support
    is a clearly labeled limited mode for one process lifetime only: no durable recovery,
    no durable compensation audit, and no post-restart operator remediation claims. Public
    XML documentation and implementation docs MUST state this limitation anywhere the
    ephemeral saga surface is introduced. SG-030 remains the minimum contract.
14. **RESOLVED (2026-07-01): `Yield` is committed.** CR-017 (cooperative checkpoint) is a
    mandatory part of the step-result contract, and AC-013 gates Slice 1 (ephemeral
    behavior) with its crash-survival clause re-verified in Slice 2. Rationale: the
    loop/`ForEach`/`Parallel` fairness uses listed in CR-017 were confirmed as real product
    scenarios. (Number retained to keep cross-references stable.)
15. **RESOLVED (2026-07-02): DAG compile target is child-instance-per-node.** JS-001
    DAG definitions compile each node as a durable child workflow instance using
    `RunChildren`-style orchestration. This preserves per-node identity, lineage,
    retry isolation, and restart-safe joins without creating a second in-instance DAG
    runtime.
16. **Durable pool extensions** — MG-062 fixes concurrency-cap (ticket) semantics only.
    Open extensions: rate-based pools (token refill per interval), priority queues instead
    of FIFO, revocation policy beyond expiry, and sharing one logical pool across engines
    that do **not** share a store (would require an external arbiter — likely out of scope
    for the library).

**Resolution for question 15 (2026-07-02): child-instance-per-node.** Each DAG node
executes as a durable child workflow instance using `RunChildren`-style orchestration.
This preserves isolation, lineage, per-node retry, and restart-safe joins through the
existing Phase 4 model while avoiding a second in-instance DAG runtime.

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
- **`Yield` defined and committed**: cooperative checkpoint — commit progress, release the
  lane, reschedule the same step. Mandatory in the step-result contract; question 14 is
  resolved. (CR-017, AC-013.)
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
- **Local branch concurrency**: local `Parallel`, `WhenFirst`, and ephemeral `ForEach`
  branches execute as deterministic cooperative fibers over isolated input/private state;
  only one local step body per instance runs at a time. This supersedes the earlier target of
  concurrently executing local branch bodies. True concurrent work uses external jobs or
  child workflow instances, while all parent-instance commits remain serialized.
- **`WhenAll`/`WhenFirst`**: specified as explicit primitives with policies, superseding
  "join semantics only implied by tests."
- **Child workflows**: the v3 split (ephemeral `ForEach` vs durable `RunChild`/`RunChildren`)
  supersedes earlier symmetric child-workflow designs.
- **`CorrelationKey` → `CorrelationId`**: the rename is final across all contracts.
- **Where-predicates**: expression-based LINQ-like public shape over an internal structured
  query model (not structured-query-first, not arbitrary delegates).
