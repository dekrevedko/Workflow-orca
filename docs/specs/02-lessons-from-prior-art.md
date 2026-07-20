# 2. Lessons from Prior Art

This document records the practices OrcaCore deliberately **adopts** from established systems
and the **anti-patterns** it deliberately avoids. These are binding design inputs: later
documents turn them into numbered requirements.

## 2.1 What was studied

| System | Role in the research |
|--------|----------------------|
| **Temporal** | Gold standard for durable execution, orchestration/side-effect separation, signals/queries, versioning |
| **Azure Durable Functions / Durable Task** | External-event waits, durable timers, fan-out/fan-in, replay history, versioning and history-pressure guidance |
| **Orleans** | Identity vs activation, single-threaded grains, timers vs reminders, activation collection, lifecycle discipline |
| **Dapr Workflow** | Stable management surface (start/query/pause/resume/raise-event/terminate/purge), child workflows, pluggable building blocks |
| **Elsa Workflows** | Bookmarks as first-class suspension records, correlation indexing, blocking vs trigger activities |
| **MassTransit** | Transport-agnostic messaging, saga repositories, routing slips with compensation, correlation (`CorrelatedBy`) |
| **Stateless** | Explicit transition tables, guards, named triggers, rejected-vs-ignored discipline |
| **Workflow Core** | Embeddable code-first authoring — and a rich catalog of issue patterns showing where simple engines fail |

## 2.2 Practices adopted (normative)

### From Temporal and Durable Functions

1. **Separate orchestration logic from side effects.** The runtime owns orchestration state
   changes. A durable step may perform one short bounded idempotent create-or-observe call using
   stable `StepOperationId`; long-running work remains external and reports through events.
   Stable identity plus at-least-once invocation is honest; exactly-once external effects are
   not claimed.
2. **External event waits are a first-class primitive** with payload delivery, correlation,
   and natural composition with timeouts (event/timer races).
3. **Durable timers are a core primitive**, not a generic sleep; timer wake-up must survive
   process loss in durable mode.
4. **Fan-out/fan-in is explicit**: fixed branches and bounded dynamic items have isolated state,
   stable ordering, host admission ceilings, and deliberate all-terminal joins. Local fibers
   are cooperative; true concurrency occurs across instances/external systems.
5. **Instances are version-bound.** Long-running instances are permanently associated with a
   definition version; incompatible deployments must fail explicitly, never silently corrupt.
6. **Unresolved runtime-owned work blocks completion.** A workflow must not silently complete
   while runtime-owned waits/timers remain unresolved, unless policy explicitly permits it.
7. **History/checkpoint growth is an operational concern**: pressure must be observable, and
   a continue-as-new/compaction mechanism must exist for long-lived instances.
8. **Sub-orchestrations are valuable but need a typed boundary.** V1 uses an internal durable
   child protocol for `OrcaCore.Dag` node instances; this lesson does not justify a provisional
   public `RunChild`/`RunChildren` API.

### From Orleans

9. **Stable logical identity, disposable activation.** `InstanceId` identifies the logical
   execution, never the in-memory object; rehydration from durable state must be possible at
   any time.
10. **One logical mutator per instance by default**; reentrancy, if ever allowed, is an
    explicit advanced feature.
11. **Transient vs durable wake-up guarantees must be explicit** — one public `Wait` has the
    same business meaning in both modes; durable mode persists it and may cold-evict/rehydrate
    under host residency policy. A second `WaitLong` authoring concept is unnecessary.
12. **Wait subscriptions are engine-owned persisted records** (analogous to Orleans stream
    pub/sub subscription state), not improvised broker subscriptions.
13. **Never rely on shutdown/deactivation hooks for critical persistence.** Persist at
    business-safe transition boundaries; treat deactivation hooks as best-effort cleanup.
14. **Activation collection**: idle instances are evictable; memory presence is neither
    identity nor durability.
15. **Duplicate-activation defense**: any future multi-node mode needs an explicit ownership
    model (optimistic concurrency now, leases later) and tests for duplicate executors.

### From Dapr, Elsa, MassTransit, Stateless, Workflow Core

16. **A stable management boundary is part of the product, not an add-on.** V1 deliberately
    starts with typed registration/start handles, snapshots, committed-root-state/output reads,
    active-wait inspection, event delivery, cooperative cancellation, and termination. Public
    pause/resume, failed-instance retry, archive, purge, and bulk fluent selection remain
    documented future amendments rather than placeholder members.
17. **Bookmark-style suspension**: each wait has identity, correlation metadata, cancellation
    semantics, and is indexed for efficient resume.
18. **Correlation is a first-class request-reply identity** (`CorrelationId`), the same value
    flowing out with a request and back with its response — not merely a lookup filter.
19. **Transport-agnostic event providers**; the engine must not leak broker semantics into
    definitions, and messaging must be optional.
20. **Explicit transition tables** for instance lifecycle: named triggers, legal transitions,
    illegal triggers rejected (not ignored), terminal states reject everything.
21. **Compact, embeddable, code-first authoring** — approachable API shape matters; the
    engine must remain a library.
22. **Definition code is versioned behavior.** The compiler fingerprints only observable
    structure and static authored values. Selectors, projectors, merge bodies, step configuration,
    request construction, and other opaque code are not honestly hashable in v1; changing any of
    them requires a new `DefinitionVersion` even when the structural fingerprint is unchanged.
23. **Protected external capacity needs proof, not a clock.** Scoped durable leasing holds
    across waits when external work consumes the resource, has no author TTL/renewal, and
    quarantines ambiguous work until causally sufficient stop/fence confirmation.
24. **Integration dependencies point outward.** Kubernetes/AWS/job-system code belongs to a
    companion project; provider-neutral OrcaCore types must not expose infrastructure SDKs.

## 2.3 Anti-patterns to avoid (normative prohibitions)

Drawn primarily from the Workflow Core issue-pattern review and design synthesis:

1. **Opaque JSON blob as the only persisted shape.** Runtime metadata (status, waits,
   correlation, versions, timestamps, errors) must remain queryable without deserializing the
   business payload. (Workflow Core #377 — reporting impossible over a JSON field.)
2. **Concurrent mutation of one instance.** Direct path to branch/wait race bugs.
3. **Ad hoc waits without first-class runtime records.** Waits need identity, correlation,
   cancellation, inspection.
4. **Critical persistence in shutdown/deactivation hooks.** Must happen at safe boundaries.
5. **Pretending in-memory mode is durable.** Ephemeral mode must not claim restart safety.
6. **Completion with unresolved runtime-owned waits/timers and no policy.** Hidden leaks and
   undefined semantics.
7. **Implicit feature interactions.** Branch/join/wait/cancel/retry/timeout interactions must
   be specified and tested; graph-shape-sensitive behavior (Workflow Core #273 — adding a
   no-op step changed join behavior) is a correctness bug class to design out.
8. **Binding product semantics to one provider's implementation details.**
9. **Invisible history/checkpoint growth.** Pressure must surface before it becomes an outage.
10. **Saga as a boolean flag on a regular workflow.** Compensation changes failure semantics;
    it deserves a distinct definition kind.
11. **Method-name explosion in the management API** (`PauseAllRunningFailedFoo`). Selection
    must be composable (`Where(...)`), not encoded in verb names.
12. **Start without an idempotency contract.** Callers lose responses; retry-safe initiation
    (`StartOrGet`) is a correctness boundary. (Workflow Core #828.)
13. **Recovery semantics left implicit.** Wait continuation, step retry, terminal-state
    immutability, and any future failed-instance retry must remain distinct. V1 intentionally
    never reopens a terminal instance; a later retry feature requires a new-generation contract.
14. **Unbounded/arbitrary predicates as the public query contract.** In-memory-only
    predicates break the moment a durable provider must translate them.
15. **Deleting completed instances out from under in-flight consumers or lifecycle hooks**
    without defined provider invariants. (Workflow Core Redis NRE pattern.)
16. **Introducing a serialized DSL before code-first semantics are stable** — or accepting
    that DSL parity must be exhaustively tested as a second contract.
17. **Primitive execution identities.** Raw strings, reusable authored locations, fiber IDs, or
    retry attempt numbers are not stable external-effect identity; use validated domain values
    and runtime-created `StepOperationId`.
18. **Time-only lease reclaim.** Elapsed time, terminal workflow status, delete acknowledgement,
    or infrastructure labels do not prove protected work stopped and must not free capacity.
19. **Speculative public integration SPIs.** A concrete application integration may stay
    application-owned until repeated implementations prove a provider-neutral contract.
20. **Shared mutable attempt state.** A failed, timed-out, or token-ignoring late attempt must
    not leak mutations into a retry or committed state; every attempt needs a codec-detached copy
    and only the winning successful attempt may replace committed state.
21. **Time or per-workflow streams as distributed resource allocation.** Durable multi-pool
    grants require one serialized resource-governance aggregate per provider partition. A provider
    that appends only per-workflow streams cannot make an atomic cross-pool grant.

## 2.4 Cross-cutting conclusions

1. Durable suspension, correlation, idempotency, and recovery are **core behavior**, not
   enhancements.
2. Queryability and inspection are **product requirements**, not infrastructure conveniences.
3. Terminal states and lifecycle events need **precise contracts** (users notice every gap).
4. Branch, join, wait, and cancel interactions must be **acceptance-tested from day one**.
5. Users often expect a **synchronous completion bridge** for effectively-immediate
   workflows; the product must define how (and whether) a caller can await a short-running
   workflow's completion, distinct from long-running durable execution.
6. Greenfield simplicity is a correctness tool: removed provisional members disappear, while
   deferred capabilities remain documented with explicit amendment gates rather than aliases or
   placeholder APIs.
