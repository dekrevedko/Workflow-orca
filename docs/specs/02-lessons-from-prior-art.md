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
   changes; steps express intent through results; side effects happen in dedicated execution
   paths. This is the single most important borrowed rule — it is the prerequisite for safe
   retries, deduplication, and any replay-based recovery.
2. **External event waits are a first-class primitive** with payload delivery, correlation,
   and natural composition with timeouts (event/timer races).
3. **Durable timers are a core primitive**, not a generic sleep; timer wake-up must survive
   process loss in durable mode.
4. **Fan-out/fan-in is explicit**: branch execution may be concurrent, join semantics are
   deliberate and acceptance-tested.
5. **Instances are version-bound.** Long-running instances are permanently associated with a
   definition version; incompatible deployments must fail explicitly, never silently corrupt.
6. **Unresolved runtime-owned work blocks completion.** A workflow must not silently complete
   while runtime-owned waits/timers remain unresolved, unless policy explicitly permits it.
7. **History/checkpoint growth is an operational concern**: pressure must be observable, and
   a continue-as-new/compaction mechanism must exist for long-lived instances.
8. **Sub-orchestrations (child workflows)** are the sanctioned way to decompose large
   workflows and bound history growth.

### From Orleans

9. **Stable logical identity, disposable activation.** `InstanceId` identifies the logical
   execution, never the in-memory object; rehydration from durable state must be possible at
   any time.
10. **One logical mutator per instance by default**; reentrancy, if ever allowed, is an
    explicit advanced feature.
11. **Transient vs durable wake-up are different concepts** — this motivates the
    `Wait` / `WaitLong` split and the explicit statement of what a durable wake-up does and
    does not replay.
12. **Wait subscriptions are engine-owned persisted records** (analogous to Orleans stream
    pub/sub subscription state), not improvised broker subscriptions.
13. **Never rely on shutdown/deactivation hooks for critical persistence.** Persist at
    business-safe transition boundaries; treat deactivation hooks as best-effort cleanup.
14. **Activation collection**: idle instances are evictable; memory presence is neither
    identity nor durability.
15. **Duplicate-activation defense**: any future multi-node mode needs an explicit ownership
    model (optimistic concurrency now, leases later) and tests for duplicate executors.

### From Dapr, Elsa, MassTransit, Stateless, Workflow Core

16. **A stable, complete management surface** (start, query, raise event, pause/resume,
    cancel, terminate, retry, archive, purge) is part of the product, not an add-on.
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
13. **Recovery limited to wait-resume.** Failure retry must be a supported recovery path,
    distinct from suspend/resume. (Workflow Core #829.)
14. **Unbounded/arbitrary predicates as the public query contract.** In-memory-only
    predicates break the moment a durable provider must translate them.
15. **Deleting completed instances out from under in-flight consumers or lifecycle hooks**
    without defined provider invariants. (Workflow Core Redis NRE pattern.)
16. **Introducing a serialized DSL before code-first semantics are stable** — or accepting
    that DSL parity must be exhaustively tested as a second contract.

## 2.4 Cross-cutting conclusions

1. Durable suspension, correlation, idempotency, and recovery are **core behavior**, not
   enhancements.
2. Queryability and inspection are **product requirements**, not infrastructure conveniences.
3. Terminal states and lifecycle events need **precise contracts** (users notice every gap).
4. Branch, join, wait, and cancel interactions must be **acceptance-tested from day one**.
5. Users often expect a **synchronous completion bridge** for effectively-immediate
   workflows; the product must define how (and whether) a caller can await a short-running
   workflow's completion, distinct from long-running durable execution.
