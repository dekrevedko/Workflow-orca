# Developer-facing interface v1 simplification amendment (2026-07-18)

Status: **planning amendment applied; ready for independent planning review; Phase 0 guard
retargeting remains pending**.

This record closes the first-release surface discussion that followed the independent public
API, lease, and primitive-obsession reviews. It supersedes their proposed future contract where
the decisions differ, but does not rewrite any dated historical review or the 2026-07-16 lease
amendment. This planning record neither authorizes nor claims product/guard implementation.

The authoritative signature/capability baseline is
[`17-selected-mode-capability-matrix.md`](../specs/17-selected-mode-capability-matrix.md).
Exact authoring declarations are mirrored in
[`17-public-authoring-contract.cs`](../specs/17-public-authoring-contract.cs).

## 1. First-release outcome

The first release keeps the features needed by the in-process engine and advanced Kubernetes
scheduler while removing provisional surface area that would force multiple unfinished state
machines into one release.

Ship:

- staged typed input/state/output workflows with resultless and typed definitions/references;
- immutable definition identity/version/fingerprint binding and optional fixed completion
  outcome metadata;
- codec-detached per-attempt state through `StepContext<TState>`/`ReplaceState` and the fixed
  certified `orcacore-json-v1` workflow-state codec;
- named steps in both engines and synchronous/asynchronous inline lambda steps in ephemeral
  mode only;
- root `If`/`While`, nested `If`, nested `Parallel`, structural `Wait`, `Delay`, durable root
  `ContinueAsNew`, step retry/cancellation, `WithStepTimeout`, and `CompleteWithin`;
- `Parallel(...).WhenAll(...)` and `.WhenAllOutcomes(...)` in both engines;
- finite root `ForEach(...).WhenAll*` in both engines, including durable committed item
  snapshots, item bounds, and optional node-local concurrency;
- runtime-created stable `StepOperationId` and diagnostic `AttemptNumber`;
- instance-targeted event deduplication and unique correlation wait routing;
- scoped-only durable `AcquireResources(request, body)` with exact protection/quarantine;
- separate first-release `OrcaCore.Dag` typed planning and sole `OrcaCore.Dag.Hosting` bridge to
  internal child-instance execution;
- role-specific engine/event-ingress/DAG hosting entry points with no catch-all registration or
  second hosted-service toggle;
- a separate outward-dependent Kubernetes/AWS/job scheduler companion that may share the
  solution.

Defer without a public member, placeholder, alias, or source task:

- `WhenFirst`;
- Saga;
- public `RunExternalJob`;
- public `RunChild`/`RunChildren`;
- nested `While`;
- nested `ForEach`/dynamic fan-out;
- durable lambda steps;
- definition-wide retry.

Remove rather than defer:

- `WaitLong`: durable `Wait` is automatically cold-capable according to host/runtime policy;
- author `Yield`: execution quanta and checkpoint scheduling are runtime-owned.

Because nothing has shipped, provisional APIs are deleted. There are no obsolete tombstones,
compatibility overloads, aliases, or no-op members.

## 2. Decision record

| ID | Decision | First-release resolution |
|---|---|---|
| V1-01 | Typed workflow contract | `Init<TInput>` creates private state; `End<TOutput>` atomically commits typed output. Resultless workflows use one-arity definitions/references. |
| V1-02 | Definition stability | Validated immutable reference `DefinitionId`/`DefinitionVersion` values reject empty/default/non-positive identity. One identity/version has one deterministic compiled fingerprint; any behavior-affecting change requires a new version. |
| V1-03 | Completion classification | Optional `WorkflowOutcomeName` is fixed authored metadata. Dynamic business classification belongs in `TOutput`. |
| V1-04 | Step authoring | `Then<TStep>()` is portable; inline lambda bodies are ephemeral-only. Durable closures are absent. |
| V1-05 | External idempotency | Runtime creates `StepOperationId` for one logical step visit and retains it across retry/replay/restart/driver conflict. `AttemptNumber` is diagnostic only. |
| V1-06 | Parallel join | `WhenAll` is success-only merge; `WhenAllOutcomes` merges ordered typed success/failure outcomes. Both normally wait for all and never auto-cancel siblings; ancestor cancellation/termination/deadline suppresses join and merge. |
| V1-07 | Dynamic fan-out | Root finite `ForEach` ships in both modes. Empty input is valid and merges once with an empty ordered list. Durable mode commits the item snapshot before admission. Nested dynamic fan-out is deferred. |
| V1-08 | Concurrency ownership | Host owns one countable `MaxConcurrentExecutionPathsPerInstance` token pool; authors have no global cap. A parent releases before fan-out and reacquires for merge. `ForEach` may declare a smaller admitted-item cap; effective limit is the lower value and parked items still count. Parallel admission is authored-order deterministic. Step throttles are keyed solely by exact step type; one ephemeral step may select one transient pool for the immediately preceding business step. Saturation parks rather than fails. V1 has no independent advancement/general-body ceiling, fail-fast/capacity-wait-timeout policy, or custom transient-governance SPI; driver segment budgets remain separate fairness mechanics. |
| V1-09 | Timeouts | `CompleteWithin` bounds the workflow; `WithStepTimeout` bounds one attempt. Orchestration uses persisted runtime deadlines/`TimeProvider`, not Polly. |
| V1-10 | Lease shape | Only lexical `AcquireResources(request, body)`. Non-empty immutable atomic request; no point/fiber-lifetime overload. |
| V1-11 | Lease recovery | No author TTL, holder renewal, or time-only reclaim. Ambiguous protected work quarantines capacity until trusted idempotent stop/terminal/fence proof. |
| V1-12 | DAG packaging | `OrcaCore.Dag` is a separate package/project with typed run input, typed workflow refs, direct-dependency output mapping, validation, and visualization; `OrcaCore.Dag.Hosting` is its sole durable bridge. |
| V1-13 | DAG execution | Each node occurrence is one durable child workflow instance. `OrcaCore.Dag.Hosting` alone consumes the versioned internal start/join seam in `OrcaCore.Durable.Hosting`; no public `RunChildren`. |
| V1-14 | Kubernetes boundary | Kubernetes Job API, manifests, watchers/reconcilers, optional EKS/AWS composition, cron, tenant policy, and job DTOs live in a separate companion project. OrcaCore never depends on it. |
| V1-15 | Generic external work | Public `RunExternalJob` is deferred. A named durable step performs short bounded create-or-observe using `StepOperationId`; normal `Wait` receives terminal outcome. |
| V1-16 | Removed control hints | One `Wait`; no `WaitLong`. Runtime-owned checkpoint quantum; no author `Yield`. |
| V1-17 | Review history | Existing dated independent reviews and the 2026-07-16 amendment remain immutable. New decisions are recorded only in new dated artifacts. |
| V1-18 | State codec | Each attempt receives a detached copy of committed state; only its winning copy commits. V1 fixes certified `System.Text.Json` format `orcacore-json-v1` and rejects unsupported/cyclic/polymorphic shapes at registration. |
| V1-19 | Event routing | Dedup is per target instance by `EventId`; identical normalized content is duplicate and changed content conflicts. Correlation routing permits exactly one active wait by `(DefinitionId, EventName, CorrelationId)`; definition fanout is deferred. |
| V1-20 | Hosting roles | Exact entry points are `AddOrcaCoreEphemeralEngine`, `AddOrcaCoreDurableEngine`, callback-only `AddOrcaCoreDurableEventIngress`, development/test `AddOrcaCoreInMemoryDurableProvider`, and `AddOrcaCoreDag`. Registration includes loops; no catch-all/toggle/implicit mode/codec hook. |
| V1-21 | Provider governance | One serialized aggregate per provider partition owns every durable pool/request/ticket/mark/resize/confirmation/tombstone through `Runtime.Protocol` records and the atomic-append `Provider.Abstractions` SPI. No force release/time reclaim. |

## 3. Typed workflow and execution identity

Workflow construction is staged so an author cannot build before declaring input and terminal
completion. Typed output plus optional fixed outcome metadata commits atomically. Definition
references carry only input/output types needed by callers and `OrcaCore.Dag`; child workflow
state remains private.

`StepContext<TState>` exposes the codec-detached current `State`, `ReplaceState(TState)`, and:

- `WorkflowInstanceId`;
- `StepOperationId OperationId`;
- `int AttemptNumber`.

The same operation ID survives retry, step-timeout reconciliation, replay, process
replacement, expected-version conflict, and competing drivers. Loop re-entry, another branch,
another `ForEach` item, or continue-as-new generation receives a new ID. External adapters use
the operation ID for create-or-observe; OrcaCore promises stable identity plus at-least-once
invocation, not exactly-once side effects.

Changing request construction or another behavior-affecting selector under the same definition
identity/version is a fingerprint conflict. This prevents a retried operation ID from silently
describing different external intent after deployment.

Failed, timed-out, cancelled, or late attempt copies are discarded. Retry starts from the same
last committed state. A token-ignoring timed-out body may continue physically, but it has no commit
authority; it retains any physical step-throttle/transient slot until it returns.

## 4. Join and state semantics

`Parallel` and `ForEach` isolate branch/item state. Results are ordered by authored branch or
item index, never completion time. One merge produces the complete replacement parent state and
runs at most once after the join commit boundary.

- `WhenAll`: wait for every terminal path; invoke merge only when all succeeded; otherwise fail
  the scope after all terminal paths finish.
- `WhenAllOutcomes`: wait for every terminal path; merge typed success/failure
  outcomes and complete the scope successfully. A following `If` reads the summary in parent
  state and decides business acceptance.

Neither join automatically cancels siblings. Ancestor instance cancellation, termination, or
`CompleteWithin` suppresses both joins and their merge. `WhenFirst` remains deferred until winner/tie,
loser cancellation, residual external work, and lease semantics can be approved together.

Durable `ForEach` is bounded: the finite selector result and node options commit before any
item admission, replay reuses them, item identity includes scope occurrence plus index, and
item concurrency is the lower of node cap and host path ceiling. An empty snapshot is valid and
invokes merge once with an empty ordered list. Runnable roots/branches/items share one countable
host path-token pool; a parent releases before fan-out and reacquires only for merge. The node cap
separately counts admitted nonterminal items, including parked ones.

## 5. Scoped leasing, waits, and deadlines

The approved durable authoring is only:

```csharp
AcquireResources(ResourceLeaseRequest request, body);
AcquireResources(state => ResourceLeaseRequest, body);
```

`ResourceLeaseRequest.Create(first, additional...)` makes an empty request unrepresentable.
The full request is immutable, unique by `ResourcePoolName`, positive-unit, and granted
atomically. Dedicated leased builders omit another acquisition and `ContinueAsNew`.

The lease is lexical:

- release commits before the parent builder resumes;
- sequential root-loop iteration scopes are legal because each scope exits before re-entry;
- independent sibling scopes may acquire independently;
- a descendant cannot acquire while an ancestor scope is pending or held;
- resources needed concurrently belong in one atomic request.

A `Wait` inside the lease body keeps the lease held. This is correct for a Kubernetes scheduler
slot representing an actually running Job. A database connection or submission-only resource
that is not needed while parked belongs in a smaller scope that exits before `Wait`; later steps
without that resource run outside the scope.

Timeouts do not prove external work stopped. Normal completion and definite pre-effect failure
can release exactly once. Cancellation, deadline, ambiguous submit, process loss, or forced
termination while protected work may exist moves the obligation to quarantine. Capacity stays
reserved until a trusted reconciler confirms all work labeled by the exact opaque protection
token stopped, became terminal, or was end-to-end fenced. Elapsed time, delete acknowledgement,
or terminal workflow status alone is insufficient.

`WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)` and `WithStepTimeout(TimeSpan)` attach
after a step; `maxAttempts` includes the initial attempt. `CompleteWithin` records one original
absolute deadline at instance start and preserves it across continue-as-new generations. When it
wins, the workflow becomes `TimedOut`, active work is cancelled/fenced, and joins/merges are
suppressed without waiting for a token-ignoring body.

## 6. DAG and companion scheduler boundary

`OrcaCore.Dag` depends on public OrcaCore application contracts; OrcaCore does not depend on
it. `OrcaCore.Dag.Hosting` alone bridges the DAG plan to the named/versioned internal child
start/join seam in `OrcaCore.Durable.Hosting`. A typed node references
`DurableWorkflowRef<TNodeInput,TNodeOutput>`. Its input mapper may
read immutable DAG run input and only the successful committed outputs of declared direct
dependencies. The mapped node input commits once before child start. Large payloads use
external references.

The runtime executes each node as one durable child workflow instance through an internal
protocol, preserving lineage, retry isolation, restart recovery, and cancellation. DAG node
admission uses separate host option `MaxConcurrentNodes`.

The companion scheduler/integration project owns Kubernetes `batch/v1 Job`, clients,
namespaces, manifests, labels, watcher/reconciler, UID-safe stop, optional AWS/EKS support,
cron, and operator policy. It may remain in `OrcaCore.slnx`, but no OrcaCore project or package
references it, Kubernetes, AWS, or a job-system SDK.

Execution hosts call `AddOrcaCoreDurableEngine(DurableEngineHostOptions)` and
`OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)`. Definition-less watcher/callback hosts use
`AddOrcaCoreDurableEventIngress()` and do not run definition/execution/timer/reconciliation/DAG
roles. `AddOrcaCoreInMemoryDurableProvider()` is development/test only. V1 has no catch-all
`AddOrcaCore` or separate hosted-service toggle.

## 7. Phase 0 guard consequences

The earlier Phase 0 guard package predates this amendment. Historical evidence remains useful as
provenance, but all 15 section-3 tasks are reopened:

| Task | Required retarget |
|---|---|
| 3.1 | Exact document-17/companion signatures and diagnostics (`WorkflowDiagnostic`, `Validation<T>`, `WorkflowDefinitionException`); application, DAG/DAG hosting, provider, protocol, companion, and internal tier classification; absence of unapproved overloads, placeholders, aliases, friend assemblies, and cross-tier leaks. |
| 3.2 | Clean packed consumers for minimal ephemeral, provider-backed durable, callback-only ingress, in-memory durable development/test provider, DAG/DAG hosting, the small meta-package, and the outward-only Kubernetes companion. |
| 3.3 | Provider-author/custom-host fixtures for the sole `Provider.Abstractions -> Runtime.Protocol` edge, governance-store load/expected-version append plus record validation, and forbidden application/engine/reverse edges. |
| 3.4 | Every exact staged factory/init/root/nested/branch/item/leased/scope/join/completion/definition/reference declaration; `TryBuild`/`Build` diagnostic parity; mode/root/lease restrictions; qualified `StepResult.*` and deferred/removed absence. |
| 3.5 | Strong values, fixed `orcacore-json-v1`, structural-only fingerprint plus opaque-code version bump, detached attempt state/`ReplaceState`, typed completion, projection opacity, and in-flight/private-state exclusion. |
| 3.6 | Success/failure-only ordered branch/item outcomes, deterministic join failure, ancestor merge suppression, no sibling cancellation, valid empty bounded `ForEach`, committed item replay, and exact parent-release/child-queue/merge-reacquire path-token behavior. |
| 3.7 | Exact typed registry/definition/instance/output/event facades and role-specific hosting/options; reduced management; exact path/step/transient-pool options with preceding-step/single-pool binding; absence of provisional advancement/general-body/fail-fast capacity policy, custom transient-governance SPI, bulk/query/statistics, deferred management, catch-all/toggle registration, and serializer hooks. |
| 3.8 | Application-only typed ephemeral/durable journeys for registration/start/reopen, ordinary waits, typed completion/output, all four event overloads/routes, dedup/conflict/non-consuming results, unique correlation, signal-stream reuse, and definition-less continuation handoff. |
| 3.9 | Exact retry eligibility/exclusions, workflow/wait/step deadlines and continue-as-new inheritance, detached attempt fencing/late overlap, stable `StepOperationId`, attempt increments, ambiguous create-or-observe, competing drivers, and occurrence-distinct identity. |
| 3.10 | Complete resultless/resultful DAG build/operation fixtures: direct typed `OutputOf`, mapping/input commit, ordinals/status/snapshots/start conflicts, output/cancellation/failure/reattachment, `MaxConcurrentNodes`, and sole `OrcaCore.Dag.Hosting` bridge. |
| 3.11a | Scoped static/selector lease request/admission, committed replay, atomic grant, exact fiber parking, legal loop/siblings, ancestry and continue-as-new runtime defense, and qualified `StepResult.*` absence. |
| 3.11b | Release before parent resume on normal/definite pre-effect exit; cancellation/workflow-or-step-timeout/ambiguous-submit/process-loss/forced-termination quarantine before merge or progression. |
| 3.11c | Exhaustive stop-confirmation result matrix, normal-release race, token/confirmation retention, stale/mismatched proof rejection, per-ticket review marks, causal release-gap recovery, missing-ticket `LeaseLost`, and no time/renewal reclaim. |
| 3.11d | Startup pool agreement, one serialized provider-partition aggregate, FIFO atomic multi-pool grants, four-stage workflow/governance handoff recovery, expected-version whole-batch append behavior, ownership equality, isolated/contended conservation, idempotent resize debt, tombstones, and no ghost/double grant. |
| 3.12 | Run every task-3 lane, record actual passing/expected-red counts and blockers, refresh status/review materials, and obtain independent approval of all 15 tasks before 4.0. |

Task 3.12 must refresh expected-red counts from actual execution after retargeting and obtain
the whole-section independent review. No existing count is evidence for this amended contract. Task
4.0 and all product implementation remain blocked until that guard packet is approved.

Current planning progress after the task splits is:

- `reshape-developer-facing-interfaces`: **15 done / 97 pending / 112 total**;
- Phase 0 section 3 within reshape: **0 done / 15 pending / 15 total**;
- `add-runtime-concurrency-limits`: **7 done / 9 pending / 16 total**.

## 8. Documentation disposition

This planning pass reconciles the normative matrix and authoring-contract companion, coordinated
OpenSpec changes, canonical requirements, phased plan, architecture/stack decisions, end-to-end
plan, historical Phase 4b index overlay, and companion scheduler handoff. It creates a current
Phase 0 status and a copy-ready review prompt.

It deliberately does not:

- modify product source, guard source, fixtures, samples, or completed historical task bodies;
- rewrite dated independent review findings;
- claim that a test/build/guard was rerun;
- check implementation tasks or advance Phase 0;
- decide a future deferred API without a new reviewed amendment.
