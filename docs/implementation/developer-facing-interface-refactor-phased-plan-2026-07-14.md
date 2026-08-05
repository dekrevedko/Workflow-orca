# Developer-facing interface refactor: phased implementation and review plan

**Original date:** 2026-07-14

**First-release simplification revision:** 2026-07-18

**Root-only fanout revision:** 2026-07-19

**Root-only fanout and authoring-lifecycle amendment:** 2026-07-28

**Review-E remediation revision:** 2026-07-19

**Status:** Sections 4, 5, and 6, including revision-8 remediation and the greenfield ownership-DDL
scanner remediation, are independently approved. Section 7 was independently approved and
checkpointed as `50254d08175431896d580ecfcc93d8e49e1c2ec7`. Post-checkpoint Section 7A closes the
non-event public-surface/test-evidence gap; pending Section 7B proposes the replacement durable
messaging and application-catalog contract. They must be completed, refrozen together,
independently approved, and checkpointed. The separate `harmonize-downstream-capability-specs`
change remains pending and must first remove its conflicting event ownership before planning
approval and canonical synchronization. Task 8.0 and all Section 8 source work remain blocked until
the combined Section 7A/7B target and final non-conflicting harmonized canonical/docs target are
independently approved and checkpointed.

**Primary change:** [`reshape-developer-facing-interfaces`](../../openspec/changes/reshape-developer-facing-interfaces/)

**Coordinated change:** [`add-runtime-concurrency-limits`](../../openspec/changes/add-runtime-concurrency-limits/)

**Pending canonical harmonization:** [`harmonize-downstream-capability-specs`](../../openspec/changes/harmonize-downstream-capability-specs/)

**Normative surface:** [`17-selected-mode-capability-matrix.md`](../specs/17-selected-mode-capability-matrix.md)

**Exact authoring declaration companion:** [`17-public-authoring-contract.cs`](../specs/17-public-authoring-contract.cs)

**Historical revision input:** [`developer-facing-interface-v1-simplification-amendment-2026-07-18.md`](../review/developer-facing-interface-v1-simplification-amendment-2026-07-18.md)

**Historical construction input:** [`developer-facing-interface-v1-strong-value-construction-amendment-2026-07-19.md`](../review/developer-facing-interface-v1-strong-value-construction-amendment-2026-07-19.md)

**Current review state:**
[`developer-facing-interface-v1-simplification-review-e-remediation-and-phase-00-status-2026-07-19.md`](../review/developer-facing-interface-v1-simplification-review-e-remediation-and-phase-00-status-2026-07-19.md)
records the applied Review-E remediation. The
[independent planning re-review](../review/developer-facing-interface-review-e-remediation-independent-rereview-2026-07-19.md)
approved guard retargeting with no P0/P1/P2 findings. The root-only owner decision remains
incorporated, while its earlier readiness verdict is historical evidence. The current
[root-only fan-out and authoring-lifecycle amendment](../../openspec/changes/reshape-developer-facing-interfaces/AMENDMENT-2026-07-28-root-only-fanout-and-authoring-lifecycle.md)
confirms that boundary, removes the implementation-only live-fiber limit from v1 semantics, and
adds the pending lifecycle, provenance, and fingerprint remediation.

**Historical Phase 0 snapshot:** [`developer-facing-interface-phase-00-public-consumer-guards-implementation-status-2026-07-18.md`](../review/developer-facing-interface-phase-00-public-consumer-guards-implementation-status-2026-07-18.md)

## 1. Outcome and execution stance

Implement one coherent first-release OrcaCore API directly on the accepted structured-fiber
runtime. The repository is greenfield: no external client contract has shipped. Superseded
members are deleted rather than carried as aliases, obsolete tombstones, placeholders, or dual
execution paths.

The first release deliberately concentrates on:

- staged typed workflow input/state/output, structural fingerprints, cast-free success helpers,
  and notification-driven completion waits;
- named steps in both engines and lambda step bodies in the ephemeral engine only;
- root `If`/`While`/`Parallel(...).WhenAll*`/bounded `ForEach(...).WhenAll*`, nested `If`,
  `Wait`, `Delay`, `CompleteWithin`, and `WithStepTimeout`;
- stable runtime-created `StepOperationId` plus diagnostic `AttemptNumber`;
- caller-created string-backed strong values with private constructors and one public
  `Create(string)` factory, distinct from runtime-created parser-only identities;
- scoped-only durable `AcquireResources(request, body)` with no author TTL or renewal;
- separate `OrcaCore.Dag` typed planning over internal durable child instances;
- a separate outward-dependent companion scheduler project for Kubernetes/AWS/job concerns.

Public `WhenFirst`, Saga, `RunExternalJob`, `RunChild`/`RunChildren`, nested `Parallel`/`While`,
nested `ForEach`/fan-out, durable lambda steps, and definition-wide retry are deferred. `WaitLong` and
author `Yield` are removed. Deferred intent remains documented in the matrix and future registry
without reflection-visible members or fake implementation tasks.

Implementation is divided into eight dependency-ordered phases, 0 through 7. Every phase ends
at an independent review gate. The next phase does not begin until the verdict and required
remediation are resolved. OpenSpec task lists remain the traceability source of truth; this
document controls execution order, evidence, and review boundaries.

## 2. Rules that apply to every phase

1. Work only in the repository-root implementation: `src/`, `tests/`, `samples/`,
   `benchmarks/`, `OrcaCore.slnx`, and root documentation. Do not reopen
   `archive/legacy-poc`.
2. Author new decisions in the active proposal/amendment/deltas, obtain independent approval,
   synchronize the approved text into canonical artifacts through the named gate task, and review
   that sync before source work in the affected section.
3. Use bounded TDD packets: contract-relevant red test, minimum final implementation,
   refactor, then affected-suite reruns. Public compile fixtures and packed consumers are
   product tests.
4. Phase 0 guards and explicitly mapped post-amendment conformance gaps are the only expected-red
   exceptions. Each must identify its owning remediation task and must not be reported as passing
   implementation evidence. Product work remains blocked until the applicable guard or amendment
   packet is independently approved.
5. Delete provisional APIs as soon as their replacement/removal guard is green. Do not add
   aliases, obsolete tombstones, compatibility overloads, placeholder members, or parallel
   builder families.
6. Keep Kubernetes, AWS, EKS, Job manifests, and scheduler DTOs outside every OrcaCore library,
   provider, engine, hosting package, and primary `OrcaCore` application package. The separate companion may
   stay in the solution but depends only outward on documented public contracts.
7. Preserve unrelated working-tree changes and record exact phase diff/commit scope.
8. Report exact passed, failed, skipped, and environment-gated counts for every command.
9. Run strict validation for both coordinated changes, Markdown/link checks, and
   `git diff --check` whenever requirements or docs change.
10. Whenever an `OrcaCore.Engine.*` assembly changes, run the CI-equivalent coverage report and
    prove aggregate engine line coverage remains at least 80%.
11. Stop after publishing the phase status and copy-ready reviewer prompt. Self-review from the
    implementation session cannot advance the gate.
12. Definition identity is immutable. Changed inspectable authored structure under the same
    `(DefinitionId, DefinitionVersion)` is a typed structural-fingerprint conflict. Mapper,
    selector, merge/output code, step construction/configuration, and external-request
    construction are opaque to v1 fingerprinting; changing any of them requires a new
    `DefinitionVersion`, and the version bump is the only v1 drift contract for that code.

## 3. Phase map

| Phase | Outcome | OpenSpec coverage | Review proves |
|---|---|---|---|
| 0 | Retarget public/consumer guards | reshape section 3.1 through 3.12 | Every guard, fixture, consumer, architecture assertion, and evidence ledger encodes the reconciled contract, then runs and receives scoped independent review |
| 1 | Typed workflows and reduced builders | reshape section 4 | Staged input/output, structural fingerprints/opaque-code versioning, step identity context, timeouts, lambdas, and capability absences are exact |
| 2 | Joins and bounded dynamic fan-out | reshape section 5; concurrency path-cap portions | Root `Parallel` and finite root `ForEach` have deterministic outcomes, merges, bounds, and restart behavior |
| 3 | Scoped leasing and durable governance | reshape section 6; remaining concurrency 3.1-5.2 | Lexical leases, quarantine/stop proof, deadlines, operation identity, transient governance, and capacity accounting are safe |
| 4 | Package tiers, facade, and management | reshape section 7 | Ordinary applications avoid runtime/provider SPIs and management exposes honest mode capabilities |
| 5 | Typed DAG and companion boundary | reshape section 8 | `OrcaCore.Dag` maps typed dependencies to internal child instances; no OrcaCore Kubernetes/AWS/job dependency exists |
| 6 | Samples, docs, and future registry | reshape section 9 | Every supported journey is executable and every deferred/removed capability is explicit without a placeholder |
| 7 | Release certification and removal proof | reshape section 10 | Suites, packages, signatures, dependency edges, samples, absence scans, and specs agree |

## 4. Phase details

### Phase 0 - Retarget public and consumer guards

**Goal:** Replace yesterday's guard assumptions with executable constraints for the approved
first-release contract before any source implementation begins.

Historical evidence remains useful as provenance, but every section-3 task from 3.1 through
3.12 is reopened because the v1 simplification changes the tier model, public declarations,
state/output semantics, capability guards, consumer journeys, package graph, or evidence gate.
Task 4.0 must not start until the complete retargeted packet is independently approved.

**Required work:**

1. Retarget the tier/signature harness, clean application/provider/custom-host consumers, and
   exact public declaration baseline to `17-public-authoring-contract.cs` and the exhaustive
   package manifest: `OrcaCore`, `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`,
   `OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions`, `OrcaCore.Engine.Durable`,
   `OrcaCore.Durable.Hosting`, `OrcaCore.Providers.InMemory`,
   `OrcaCore.Providers.PostgreSql`, `OrcaCore.Dag`, and `OrcaCore.Dag.Hosting`. Treat
   `OrcaCore` as the primary application package, not a meta-package, and enforce the exhaustive
   direct-edge/assembly-owner table.
2. Retarget root/nested compile fixtures to staged typed workflow references, input/output,
   fixed outcome metadata, ephemeral-only lambdas, root-only `While`/`Parallel`/`ForEach`,
   nested `If`, and explicit absence of deferred/removed members and nested fanout.
3. Guard compiled-IR opacity, declaration uniqueness, structural-only fingerprints plus mandatory
   opaque-code version bumps, fixed codec/detached state and output, and the exact management
   authority boundary. Preserve inspectable closed workflow/DAG registration/start unions while
   proving `GetHandleOrThrow()` is the cast-free success projection and output/DAG terminal waits
   use notification-driven subscribe-then-recheck with no polling.
4. Guard root `Parallel(...).WhenAll` and `WhenAllOutcomes`, bounded durable root `ForEach` snapshot/
   ordering/restart, node-local/host concurrency composition, and no automatic sibling
   cancellation. Treat an empty item snapshot as valid and suppress joins/merges when an ancestor
   cancellation, termination, or workflow deadline wins.
5. Replace the external-job/split-host guard journey with stable `StepOperationId`, step and
   workflow deadlines, a typed create-or-observe step, normal `Wait`, event deduplication including
   non-consuming pre-wait `NoActiveWait`, role-owned `IWorkflowEventClient` routing, and
   definition-owner continuation.
6. Guard typed DAG run input, `DurableWorkflowRef<TInput,TOutput>`, direct-dependency `OutputOf`,
   and one internal durable child instance per node. Opaque mapping access is validated at runtime
   after dependencies succeed and before mapped-input commit or child start; invalid access or a
   projector failure is `DAG_INPUT_MAPPING_INVALID` and starts no child. Include cast-free start,
   notification-driven terminal waiting, no visualization promise, and
   `MaxConcurrentNodes` counting parked nonterminal children.
7. Replace point/fiber-lifetime lease assumptions with scoped-only
   `AcquireResources(request, body)` at the durable root, inside root `If`/`While` bodies, and in
   independent durable root-`Parallel` branch/root-`ForEach` item bodies when no live ancestor
   lease exists. Guard that leased bodies expose no `Parallel`, `ForEach`, or `While`, plus
   committed selectors, lexical release, ancestry defense, loop re-entry, wait-inside-scope
   behavior, and the
   exact retry lifecycle: retryable ambiguity remains capacity-reserving `AmbiguousHeld` under the
   same operation/token/tickets; quarantine commits only before progression when ambiguity survives
   scope exit, exhaustion, cancellation, deadline, termination, or abandonment.
8. Guard package direction so `OrcaCore.Dag.Hosting` is the sole internal child-execution bridge
   and only the companion may reference Kubernetes/AWS/job SDKs. Pack exact version
   `0.0.0-phase0` to `artifacts/phase0-packages` and restore clean consumers through
   `PackageReference` only. Guard split extension owners and the exact role methods, including
   `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)` as one complete
   certified production durable role. Options are programmatic immutable values copied/validated
   before registration, with no binder surface. Engine roles are mutually exclusive; each owns its
   registry/execution/event routing. Reject catch-all `AddOrcaCore`, hosted-service toggles,
   aliases, implicit mode selection, and superseded hosting scenarios.
9. Restrict deterministic crash testing to friend-only test seams and fixed-codec immutable facts.
   Use exactly `WorkflowPendingObligationCommitted`, `GovernanceReservationCommitted`,
   `WorkflowActivationCommitted`, and `GovernanceOwnershipConfirmed`; no guard lane may invent a
   future public authoring/runtime API.
10. Refresh actual expected-red counts and ledger entries; do not predict them from the old
   packet.
11. Complete task 3.12: run all retargeted section-3 lanes, refresh the Phase 0 status/review
    request, and obtain independent approval scoped to tasks 3.1-3.12.

**Exit criteria:** Every section-3 artifact from 3.1 through 3.12 reflects document 17 and the
authoring-contract companion; every failure is one approved product gap; task 3.12 records actual
lane counts and independent review of the whole packet; task 4.0 remains untouched until that
review approves the guard packet.

**Review focus:** Signature completeness, realistic consumer code, absence proof, guard
determinism, scoped lease semantics, typed DAG mapping, and whether any test preserves a
provisional API merely because source currently implements it.

### Phase 1 - Implement typed workflows and reduced builders

**Goal:** Establish the small, capability-safe authoring foundation used by both engines.

**Required work:**

1. Add staged mode-first construction: an init-only builder, typed root builder, and
   completion-only builder producing resultless or typed-output definitions/references.
2. Commit typed output and optional fixed `WorkflowOutcomeName` atomically at `End`; put
   dynamic business classification in `TOutput`.
3. Bind each definition identity/version to one deterministic structural fingerprint and reject
   changed inspectable structure at registration/rehydration. Require a new definition version for
   opaque code changes; do not claim that their IL or behavior changes the fingerprint.
4. Expose `StepContext<TState>` with codec-detached `State`, `ReplaceState`, runtime-created
   `StepOperationId`, and diagnostic `AttemptNumber`; preserve one operation ID across
   attempts/replay and create new identities for loop/item/branch/generation occurrences.
5. Fix the v1 workflow-state codec to certified `System.Text.Json` format
   `orcacore-json-v1`; reject unsupported/cyclic/polymorphic shapes at registration and fence all
   failed/timed-out copies from commit.
6. Implement named `Then<TStep>()` for both modes and sync/async inline lambda steps only for
   ephemeral mode.
7. Implement structural `Wait`, `Delay`, root/nested `If`, root-only `While`, step decorators,
   `WithStepTimeout`, and root `CompleteWithin`; use runtime `TimeProvider`, not Polly, for
   orchestration semantics.
8. Implement instance-targeted event dedup by `(InstanceId, EventId)` and unique correlation
   routing by `(DefinitionId, EventName, CorrelationId)`; reject an ambiguous active wait before
   parking, ensure pre-wait `NoActiveWait` does not consume `EventId`, and keep
   definition-targeted fanout absent.
9. Delete `WaitLong`, public/author `Yield`, mixed-mode/fallback builders, definition-wide
   retry, and every alias/tombstone/placeholder for deferred members.
10. Govern every mutable façade through one phase- and scope-bound authoring session; freeze the
    graph atomically at root terminal selection, build only the frozen snapshot, and reject stale,
    duplicate-join, post-terminal, escaped-callback, and losing concurrent operations without
    graph mutation.
11. Keep the structural fingerprint limited to inspectable authored structure plus codec format;
    exclude compiler format, mode, identity/version, and every compiler option, with
    compiler-format retention or migration for nonterminal durable instances.

**Verification:** Typed-definition/registration tests; structural-fingerprint conflicts and
opaque-code version-bump tests; execution-ID
retry/replay tests; timeout/late-result fencing; positive/negative compile fixtures; public
signature and removed-member scans; affected samples and benchmarks.

**Exit criteria:** Reshape section 4 is complete; workflows cannot build before `End`; durable
definitions cannot contain lambda steps; removed/deferred members are absent; Phase 0 workflow
foundation guards are green.

### Phase 2 - Implement joins and bounded dynamic fan-out

**Goal:** Ship the branching needed by the first scheduler and ephemeral journeys without the
larger cancellation state machine of `WhenFirst` or recursive/nested fanout.

**Required work:**

1. Implement fixed isolated root `Parallel<TResult>` with terminal join builders
   `.WhenAll(...)` and `.WhenAllOutcomes(...)` in both modes.
2. `WhenAll` waits for every branch, merges only all-success results, and otherwise fails the
   scope after all terminal outcomes. `WhenAllOutcomes` merges ordered typed success/failure
   outcomes and completes successfully so a following `If` can apply business policy. Neither
   form cancels siblings automatically. If ancestor cancellation, termination, or
   `CompleteWithin` wins, both joins and their merges are suppressed.
3. Implement root bounded `ForEach` in both modes using finite committed item
   snapshots, stable item-index identity, positive `MaxItems`, optional positive
   `MaxConcurrency`, ordered results/outcomes, and the same two join forms. An empty snapshot is
   valid and invokes merge once with an empty ordered list.
4. In durable mode commit the item snapshot before admission and reuse it after restart.
5. Make `MaxConcurrentExecutionPathsPerInstance` host-owned. A runnable root/branch/item owns
   one token and releases it on wait/delay/resource request/join; the parent releases before
   child scheduling and reacquires only for merge/continuation. Create every fixed root
   `Parallel` branch fiber at scope start and schedule runnable branches fairly in authored order
   with no live-fiber admission resource. Effective item concurrency is the lower of host and node
   ceilings and separately counts admitted nonterminal items including parked ones; document that
   admitted-item dependence on pending work has no global-progress guarantee.
6. Keep `WhenFirst`, nested `Parallel`, and nested `ForEach`/multilevel dynamic expansion absent
   and documented for a future amendment.
7. Carry `AuthoredLocation` plus runtime-created root/branch/item occurrence on every
   `WorkflowFailure`, attaching it at failure creation and preserving it through aggregation and
   fixed-codec round-trip.

**Verification:** Success/failure outcome matrices; authored-order versus completion-order tests;
ancestor cancellation/termination/deadline merge suppression; merge-at-most-once; selector-once
and restart replay; item-bound and host-limit diagnostics; cancellation/restart; root-only fanout
and nested-`If` capability compile fixtures.

**Exit criteria:** Reshape section 5 plus the execution-path portions of the concurrency change
pass; bounded durable `ForEach` is supported; no deferred join/fan-out surface exists.

### Phase 3 - Implement scoped leasing and durable governance

**Goal:** Make durable resource capacity and host-local transient admission safe under waits,
retries, deadlines, cancellation, process loss, and restart.

**Required work:**

1. Implement factory-only `ResourceLeaseRequirement` and non-empty immutable
   `ResourceLeaseRequest`; grant the whole request atomically.
2. Implement only lexical `AcquireResources(request, body)` static/selector forms at the durable
   root, within root `If`/`While` bodies, and in independent durable root-`Parallel` branch/
   root-`ForEach` item bodies when no live ancestor lease exists. Dedicated leased builders omit
   `Parallel`, `ForEach`, `While`, another acquisition, and `ContinueAsNew`; release commits
   before the parent resumes.
3. Preserve one exact lease obligation/protection identity through request, provider
   reservation, grant, body, release, mark, quarantine, reconciliation, and stop confirmation.
4. Permit a wait inside a lease scope when capacity is consumed by the external work; authors
   place a resource needed only for submission in a smaller scope that exits before `Wait`.
5. Add no author TTL or renewal. Pool review time only marks/reconciles. Retryable leased-step
   timeout, ambiguous submit, or recovered in-flight work remains `AmbiguousHeld`, keeps the same
   operation/protection/ticket identities and capacity, and retries under that obligation without
   overlapping a still-running local attempt. If ambiguity survives scope exit, retry exhaustion,
   cancellation, deadline, termination, or abandonment, atomically transfer it to `Quarantined`
   before parent/join progression. Only trusted idempotent stop/terminal/fence confirmation may
   release quarantine.
6. Complete durable host-owned advancement/path/step governance, restart re-admission, and
   metrics while keeping `WithTransientPool(TransientPoolName)` ephemeral-only. Durable shared
   capacity uses scoped `AcquireResources(ResourceLeaseRequest, body)`, not a transient-pool hint.
7. Prove reserved-unit conservation, grant-time admission, resize debt, exact isolated
   restoration, contended waiter transfer, and stale confirmation rejection.
8. Persist all durable pool state through one serialized resource-governance aggregate per
   configured provider partition. `OrcaCore.Runtime.Protocol` owns its records and
`OrcaCore.Provider.Abstractions.ResourceGovernance.IDurableResourceGovernanceStore` provides load plus one
   expected-version atomic append. There is no force-release operation.
9. Implement only the deterministic friend-test seams needed to prove the four-stage reservation
   protocol: `WorkflowPendingObligationCommitted`, `GovernanceReservationCommitted`,
   `WorkflowActivationCommitted`, and `GovernanceOwnershipConfirmed`. Each exposes immutable
   correlated facts after its durable commit and before the next command; none is public API.

**Verification:** Selector commit/replay; lexical loop and ancestry fixtures; wait-inside/
after-scope tests; normal/failure/quarantine release; ambiguous create and stop-proof cases;
grant/cancel races; provider certification; durable host-limit saturation/restart; durable
`WithTransientPool` compile absence; exact metrics and management projections.

**Exit criteria:** Reshape section 6 and all remaining `add-runtime-concurrency-limits` tasks
are complete; no time-only reclaim exists; capacity is never reused while protected work may
still run; Phase 0 deadline/identity/lease guards are green.

### Phase 4 - Establish package tiers, facade, and management

**Goal:** Give ordinary applications a typed surface while keeping provider/runtime SPIs and
protocol identities in advanced packages.

**Required work:**

1. Apply the approved exhaustive eleven-package graph and make `OrcaCore` the primary application
   contracts/authoring package, not a meta-package. Enforce every declared direct edge and reject
   every unlisted or reverse dependency.
2. Keep application contracts/authoring, engines, hosting, runtime protocol, provider
   abstractions, the in-memory and PostgreSQL provider adapters, and DAG roles explicit. Assign one
   package/assembly owner to every public extension class; no extension class is partial across
   assemblies.
3. Implement only role-specific hosting entry points:
   `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`,
   `AddOrcaCoreDurableEngine(DurableEngineHostOptions)`, callback-only
   `AddOrcaCoreDurableEventIngress()`, development/test
   `AddOrcaCoreInMemoryDurableProvider()`, production
   `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)`, and
   `OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)`. Engine registration includes its loops;
   delete catch-all `AddOrcaCore`, aliases, and any separate hosted-service toggle.
   PostgreSQL registration supplies one complete certified durable provider role and rejects a
   null or blank get-only `ConnectionString`/`Schema` before partial services are visible.
   `OrcaCore.Engine.Ephemeral` owns
   `OrcaCore.Hosting.OrcaCoreEphemeralEngineServiceCollectionExtensions`; separately,
   `OrcaCore.Durable.Hosting` owns
   `OrcaCore.Hosting.OrcaCoreDurableEngineServiceCollectionExtensions`.
4. Implement host-scoped definition registration, typed start/event delivery, provider-global
   `StartIdempotencyKey`, split-host continuation, inspectable closed result unions, cast-free
   `GetHandleOrThrow()` success helpers, and notification-driven output waits without exposing
   processors, fibers, scopes, checkpoints, or provider generations.
5. Align asynchronous ephemeral/durable management, detached committed root state, typed
   outputs, authored wait projections, lifecycle controls, and generic protected-work stop
   confirmation.
6. Make ephemeral and durable engine roles mutually exclusive in one service provider. Each
   selected engine owns its definition registry, execution services, and `IWorkflowEventClient`
   routing. Keep callback-only durable ingress limited to event persistence and continuation
   handoff. Copy/validate programmatically constructed immutable options immediately, reject
   conflicting duplicates, and expose no configuration-binder compatibility claim.
7. Assign and certify local package IDs, pack every documented tier, and run clean consumers
   from exact version `0.0.0-phase0` in `artifacts/phase0-packages` through `PackageReference`
   only. Include clean application, provider-author, custom-host, callback, and DAG fixtures plus
   exhaustive allowed/forbidden dependency tests. External registry publishing, signing,
   SourceLink release setup, and release automation remain deferred.

**Verification:** Package restoration/packing, public baselines, architecture edges, start
idempotency/structural-fingerprint conflicts plus opaque-code version bumps, two-host continuation,
management races, detached copies,
and advanced-seam certification.

**Exit criteria:** Reshape section 7 is complete; ordinary applications use no raw protocol;
provider/runtime SPIs are sufficient for their intended advanced audiences and absent from the
common workflow journey.

### Phase 5 - Implement typed DAG and the companion boundary

**Goal:** Ship the advanced scheduler's dynamic dependency graph without adding a second
workflow runtime or coupling OrcaCore to Kubernetes/AWS/jobs.

**Required work:**

1. Create separate `OrcaCore.Dag` with immutable typed run input, `DagNodeId`, typed
   `DurableWorkflowRef<TInput,TOutput>`, declared dependencies, direct-dependency `OutputOf`,
   `Build`, and `TryBuild`. V1 exposes no visualization renderer/package/projection.
2. Evaluate opaque node mapping after all direct dependencies succeed. Validate every `OutputOf`
   access at runtime before mapped-input commit or child start; undeclared/resultless/foreign/
   otherwise unavailable access or projector failure records `DAG_INPUT_MAPPING_INVALID` and
   starts no child for that node. Commit one fixed-codec node input before child start and reuse it
   after restart; keep child internal state private. Use small immutable DTO outputs and external
   references for large data/artifacts.
3. Create `OrcaCore.Dag.Hosting` as the only bridge to the named/versioned internal child
   start/join seam in `OrcaCore.Durable.Hosting`. Execute each node as one durable child workflow
   instance with lineage, retry isolation, failure blocking, cancellation, restart, and
   `MaxConcurrentNodes`, counting admitted nonterminal children even while they are parked in a
   wait, delay, or lease queue. Preserve inspectable registration/start unions, cast-free success
   helpers, and notification-driven terminal waits. Expose no public `RunChild`/`RunChildren` or
   provider SPI for the bridge.
4. Add/retarget a separate companion scheduler project in the solution. It owns Kubernetes
   `batch/v1 Job`, client/authentication, optional AWS/EKS composition, manifests, watchers,
   reconcilers, cron, tenant policy, and Job-specific DTOs.
5. Prove the primary Kubernetes journey with an ordinary bounded typed durable step using
   `StepOperationId`, scoped lease protection, normal `Wait`, stable `EventId` redelivery, and
   trusted generic stop confirmation. OrcaCore promises at-least-once invocation and stable
   identity, not exactly-once external calls.
6. Add architecture/package scans proving no OrcaCore/`OrcaCore.Dag` dependency on the
   companion, Kubernetes, AWS, or job-system types.

**Verification:** Typed DAG compile fixtures; graph/build validation; runtime invalid/missing/
non-direct dependency mapping before child start;
diamond/failure/restart/cancellation/lineage tests; duplicate child-start dedup; companion
create-or-observe and watcher redelivery; parked-node-limit enforcement; notification-driven
terminal waiting without polling; quarantine/stop reconciliation; dependency closure and
visualization-surface absence.

**Exit criteria:** Reshape section 8 is complete; `OrcaCore.Dag` is first-release ready; the
companion proves the Kubernetes scheduler use case while dependency direction remains outward
only.

### Phase 6 - Rewrite samples, docs, and the future registry

**Goal:** Make the supported first-release surface understandable from shipped documentation
and keep deferred intent discoverable without polluting IntelliSense.

**Required work:**

1. Rewrite golden ephemeral, durable, bounded fan-out, scoped-resource, typed-output, DAG, and
   companion Kubernetes scheduler journeys against final packages/signatures. Ordinary success
   paths use `GetHandleOrThrow()` or `await start.WaitForOutputAsync(token)` and DAG terminal waits;
   samples do not cast result unions or poll snapshots.
2. Explain when a lease scope intentionally encloses `Wait` and when a short submission-only
   scope must exit first.
3. Publish mode/capability, timeout, concurrency, operation-identity, definition-version, DAG
   mapping, package, management, and provider/runtime-SPI guidance.
4. Maintain a future-feature registry for `WhenFirst`, Saga, public external jobs, public child
   workflows, nested `Parallel`, nested `While`, nested dynamic fan-out, durable lambdas, and
   definition retry.
   State the design questions and amendment/evidence required to reopen each.
5. State that `WaitLong` and author `Yield` are removed, not future aliases.
6. Compile every snippet or source fixture and check all links.

**Exit criteria:** Reshape section 9 is complete; a new consumer can build each supported
journey from its guide; deferred features remain visible in documentation but absent from
public assemblies and samples.

### Phase 7 - Certify the release surface and prove removals

**Goal:** Produce release-quality evidence that signatures, behavior, packages, docs, and
absence claims all describe the same product.

**Required work:**

1. Run all unit, acceptance, hosting, management, provider, restart, DAG, companion,
   resource-governance, deadline, and concurrency suites in the required safe order.
2. Run the CI coverage gate and record the measured aggregate `OrcaCore.Engine.*` line rate.
3. Pack every documented package; build/run clean application, provider-author, custom-host,
   DAG, and companion consumers solely from packed artifacts.
4. Review every public signature and exact project/package dependency edge.
5. Run golden scenarios under assertions.
6. Scan for removed `WaitLong`/`Yield`, deferred members, aliases, obsolete tombstones,
   placeholders, mixed builders, public compiled IR, raw application protocol, duplicate
   declarations, and forbidden Kubernetes/AWS/job dependencies.
7. Strict-validate both changes; reconcile canonical/OpenSpec headings and executable evidence.
8. Refresh final status/reviewer materials with exact counts, skipped reasons, package results,
   coverage, signatures, and Phase 0 baseline comparison.

**Exit criteria:** Reshape section 10 is complete; both changes strict-validate; every required
test and packed consumer passes; absence/dependency scans are clean; every public type is
justified by a supported first-release journey. Archiving remains a separate explicit action
after independent approval.

## 5. Mandatory phase review protocol

At the end of every phase:

1. Stop implementation and leave the next phase untouched.
2. Check only tasks whose full implementation and verification are complete.
3. Create a dated phase status report and filled copy-ready reviewer prompt under
   `docs/review/`.
4. Include the developer outcome, API additions/removals, implementation files, regression
   origin, exact commands/counts, coverage when required, package/signature/dependency diffs,
   absence scans, OpenSpec/docs changes, deviations/risks, commit scope, and focused questions.
5. Request independent review and wait.
6. Apply actionable findings with regressions, refresh evidence, and request re-review when the
   contract, architecture, durable semantics, or verdict basis changed.

Verdicts:

- **Approve:** record the verdict and start the next phase.
- **Approve with changes:** remain in phase until required changes and evidence are complete.
- **Reject:** reopen the phase plan; do not advance.
- P0/P1 findings require remediation and re-review. P2 requires remediation before advancing
  unless the reviewer and owner accept a named later task that cannot affect the next phase.

## 6. Current progress accounting

The selected-mode matrix and canonical OpenSpec requirements are the accepted baseline. Revision 8
was independently approved under task 4.15 and synchronized under task 10.14; the active deltas
remain the implementation mapping for that canonical target, and OpenSpec checkboxes are the
machine-readable execution record. Counts must agree. All dated amendments, status reports,
reviews A-E, the earlier consolidated review, and the root-only decision are immutable historical
inputs and are not rewritten by this synchronization.

Current disposition:

- Review-E planning remediation is applied and independently approved for guard retargeting;
- revisions 4 and 6 of the 2026-07-28 documentation amendment are rejected; revision 8 supersedes
  revision 7's premature canonical sequencing and is independently approved under task 4.15;
- task 10.14 synchronized all 137 approved delta operations (130 reshape plus 7 coordinated
  runtime-governance operations) into canonical OpenSpec specs, applied the capability-matrix and
  guide wording, and published the non-normative semantic appendix; task 4.16 subsequently landed
  the authoring lifecycle and published L4's bounded build-agreement law;
- tasks 5.10, 9.10, 9.12, and 10.14 are complete; proposal-validation tasks 10.9 and 10.13 remain
  planning evidence rather than product-source conformance evidence;
- current reshape OpenSpec progress is 108 complete / 47 pending / 155 total with no duplicate
  task IDs; coordinated concurrency progress is 16 complete / 0 pending / 16 total; the separate
  downstream-capability harmonization change remains pending independent approval and unsynchronized;
- tasks 3.1 through 3.11d are implemented against the complete live planning packet and have
  clean infrastructure plus intentional-red execution evidence;
- task 3.12 reconciled the full packet and is closed after the final immutable independent
  approval recorded no P0-P3 gate findings;
- tasks 4.16-4.21, 5.11, and 5.13-5.15 now implement builder lifecycle, frozen completion
  snapshots, exact lifecycle diagnostics, portable role parity, failure provenance,
  live-fiber-limit removal, structural fingerprint conformance, compiler-profile binding, and
  tagged-item/budget regressions;
- the Section 4/5 remediation is independently approved. Section 6 implements workflow and wait
  deadlines, retry/attempt identity and fencing, scoped durable leasing and recovery, exact
  execution-path/step/transient governance, restart re-admission, metrics/debug counters, and the
  operator contract;
- the first Section 6 exit target was independently rejected twice because
  `ancestor-terminal-suppresses-merge` ended at task `6.2` but remained ExpectedRed with no
  executable driver. Both immutable verdicts are preserved;
- remediation derives each scenario's terminal section from `turnsGreenTask` instead of three
  parallel ID lists and executes the missing deadline race across both engines, both root fan-out
  shapes, and both joins, including durable replacement-host projection after deadline commit;
- final remediation validation is green at build 0 warnings/0 errors; Core 464, Ephemeral 173,
  Durable 332, Hosting 17, Acceptance 70, ProviderCertification 78, and guard infrastructure 104
  on three consecutive runs. All 32 Section-6 current-physical scenario drivers pass, including
  deadline merge suppression plus forged `SFE-RUN-002` ancestry and `SFE-RUN-001` non-quiescent
  rollover checkpoints;
- the ExpectedRed guard lane is now exactly 61 intentionally failing later-section cases with zero
  passes. Green compile fixtures succeed; the product-authoring ExpectedRed compile set has zero
  remaining gaps; and eight package/application fixtures remain intentionally red for Sections 7
  and 8;
- the checkpoint's OpenSpec validations, `git diff --check`, and live NuGet audit were green; the
  current Section 7A target and the pending harmonization change require fresh validation against
  the reworked normative-source workflow before their respective gates can close;
- the superseding Docker/provider, greenfield schema, compatibility-DDL, and scanner remediation
  reviews are preserved. The exact Section 6 target received immutable independent approval in
  `developer-facing-interface-section-06-ownership-ddl-scanner-remediation-independent-rereview-verdict-2026-07-30.md`;
- Section 7 implemented the exact 11-package graph, application facades, split host/provider roles,
  reduced management, durable governance, the persisted-collection allowlist, operational
  diagnostics, and 37 current-physical behavior drivers, then received independent approval and
  the checkpoint commit `50254d08175431896d580ecfcc93d8e49e1c2ec7`;
- Section 7A now closes the post-checkpoint exact-API-baseline and test-attribution findings, while
  the pending Section 7B amendment owns the proposed durable messaging and application-catalog
  contract. Neither is frozen or approved. The harmonization change must first discard the
  superseded event-contract ownership, then complete planning approval, canonical synchronization,
  final-target review, and checkpoint. Task 8.0 remains open, and no Section 8 implementation is
  authorized until the combined 7A/7B gate and the non-conflicting harmonization gate both close.
