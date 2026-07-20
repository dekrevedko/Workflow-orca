## Context

OrcaCore is greenfield and preparing its first release. The root implementation already contains a structured-fiber compiler and durable driver, but its provisional public surface is broader than the semantics that have been approved. Earlier review rounds found invented lease signatures, incomplete node signatures, unsafe lifetime assumptions, primitive string roles, and advanced protocol types leaking into normal application use.

The final v1 design therefore starts from supported user journeys rather than from currently public source. Existing source is evidence and reusable implementation substrate, not a compatibility contract. No aliases, obsolete tombstones, duplicate builder stacks, or placeholder members are required.

The primary initial workload is an advanced Kubernetes/EKS scheduler, while the ephemeral engine remains important for fast in-process workflows. OrcaCore itself must remain independent of Kubernetes, AWS, and job-scheduler SDKs. `OrcaCore.Dag` is a first-release package in the same solution; infrastructure-specific scheduler code is a separate outward-dependent companion project.

The exact workflow-authoring declaration baseline lives in `docs/specs/17-public-authoring-contract.cs`; its semantic and package baseline lives in `docs/specs/17-selected-mode-capability-matrix.md`. Both artifacts are normative and must agree before guards or source work proceeds. This change's delta specs define behavior and its task graph ensures guards are retargeted before source work.

## Goals / Non-Goals

**Goals:**

- Ship one complete, misuse-resistant v1 authoring surface for both engines.
- Make workflow input, output, definition identity, and mode visible in static types.
- Provide useful structured concurrency: fixed `Parallel`, bounded `ForEach`, and explicit all-results joins.
- Make durable deadlines, retries, external-call identity, and resource leasing honest under crash, replay, cancellation, and ambiguous ownership.
- Ship durable DAG execution as a separate package using child workflow instances and typed dependency mapping.
- Keep common authoring free of provider/runtime protocol, Kubernetes, AWS, and scheduler-specific concepts.
- Preserve deferred product direction in a reviewable future-capability registry without publishing placeholders.
- Mechanically prove the public surface, package direction, capability absence, and critical durable protocols before release.

**Non-Goals:**

- Preserve provisional API/source compatibility or old durable data.
- Ship public generic external-job orchestration, saga compensation, race joins, public child-workflow authoring, nested `Parallel`, nested `While`, or nested `ForEach` in v1.
- Put Kubernetes Job, EKS, AWS, scheduler, credential, cluster, or manifest types into OrcaCore.
- Promise exactly-once external effects.
- Use a timeout, lease TTL, or process liveness guess as proof that protected external work stopped.
- Expose executable compiler IR or runtime ownership identities as normal application metadata.

## Decisions

### 1. Use explicit package tiers and one-way dependencies

The supported dependency direction is:

```text
OrcaCore (primary application contracts/authoring package)
    <- OrcaCore.Core / OrcaCore.Engine.Ephemeral / OrcaCore.Engine.Durable
    <- OrcaCore.Durable.Hosting / OrcaCore.Dag / OrcaCore.Dag.Hosting

OrcaCore.Runtime.Protocol <- OrcaCore.Provider.Abstractions <- provider adapters
OrcaCore.Durable.Hosting <- OrcaCore.Dag.Hosting <- companion scheduler/application
```

`OrcaCore` is the primary application contract package, not a dependency-only meta-package, and owns the `OrcaCore` application namespace. Every other package has one assembly with the same identity as its PackageId and a fixed public/internal tier in the package manifest. Document 17 fixes the CLR namespace and assembly owner for every host-option, DAG, management, recovery, diagnostics, protocol, and provider family; unqualified sketches do not grant placement freedom. Application contracts never reference advanced seams. Runtime protocol never references provider abstractions. Engines may consume both advanced seams internally. `OrcaCore.Dag.Hosting` is the only DAG-to-durable-runtime product bridge and uses a named, versioned internal child-start/join contract exposed by `OrcaCore.Durable.Hosting` through `InternalsVisibleTo("OrcaCore.Dag.Hosting")`. The only other friend is test-only `OrcaCore.Engine.Durable -> InternalsVisibleTo("OrcaCore.ProviderCertification")` for exactly the four resource-governance post-commit barrier types. `OrcaCore.Dag` does not pull provider or infrastructure SDKs into the core graph. A companion scheduler may be in `OrcaCore.slnx`, but no OrcaCore product project references it.

Hosting extension classes are owned by their role assemblies rather than sharing one catch-all owner type. The first complete production durable provider is `OrcaCore.Providers.PostgreSql`; its one options object contains required nonblank `ConnectionString` and `Schema` values that registration copies and validates immediately, and its registration supplies the complete certified durable provider role set. `OrcaCore.Providers.InMemory` remains development/test only. Package fixtures validate only declared package references from a local Phase 0 feed; no project-reference transitivity or undeclared host package may make a consumer compile accidentally.

"Provider/runtime SPI" means the explicitly advanced service-provider interface for a storage/transport adapter or custom runtime host. It is not the ordinary workflow-author API and is consumed only by implementers who intentionally reference the advanced package.

Alternative considered: one broad abstractions package separated only by namespaces. Rejected because package restoration, IntelliSense, and public signatures would still present every advanced type as a normal application concept.

### 2. Make workflow input and output part of the type contract

A reusable durable workflow reference names its external contract without exposing internal business state:

```csharp
DurableWorkflowRef<TInput, TOutput>
DurableWorkflowRef<TInput>       // resultless
```

Mode-first staged builders produce immutable definitions carrying the matching reference. `Init` consumes the input once to create private workflow state. Each mode exposes exactly four completion overloads: resultless `End()`, resultless `End(WorkflowOutcomeName)`, resultful `End<TOutput>(Func<ReadOnlyStateSnapshot<TState>,TOutput>)`, and resultful `End<TOutput>(Func<ReadOnlyStateSnapshot<TState>,TOutput>, WorkflowOutcomeName)`. There are no nullable or optional outcome/selector parameters; an explicitly null selector or outcome throws `ArgumentNullException` immediately. `End` commits output, terminal status, and optional fixed outcome atomically. Resultless workflows use the one-arity reference rather than `Unit` in the public signature.

Named outcome metadata is a small fixed declaration-time classification useful for queries and metrics. Dynamic business classifications belong in `TOutput`; v1 does not create arbitrary runtime outcome names. A definition's private `TState` never appears in the reusable workflow reference or DAG dependency contract.

Alternative considered: use workflow state as both input and output. Rejected because it leaks internal evolution, makes DAG composition depend on child internals, and prevents a stable consumer contract.

### 3. Publish one complete v1 node set

The v1 authoring allowlist is:

| Capability | Ephemeral root | Durable root | Nested v1 placement |
|---|---:|---:|---|
| `Init`, typed business step, `End` | yes | yes | business steps in supported bodies |
| `Delay`, event `Wait` | yes | yes | where the matrix permits |
| `If` | yes | yes | nested `If` is allowed |
| `While` | yes | yes | root only |
| fixed `Parallel` | yes | yes | root only; branch body may contain nested `If` |
| bounded `ForEach` | yes | yes | root only; item body may contain nested `If` |
| `WhenAll`, `WhenAllOutcomes` | yes | yes | join owned by its fan-out scope |
| ephemeral lambda step body | yes | no | wherever an ephemeral business step is valid |
| `ContinueAsNew` | no | yes | quiescent durable root only; terminal for that generation |
| scoped `AcquireResources` | no | yes | durable root or branch/item body subject to ancestry analysis |

`WaitLong` and `Yield` are removed rather than deferred. One `Wait` node covers event suspension; hot/cold residency is a runtime/hosting decision. Fair turn scheduling and cooperative yielding remain implementation concerns.

Public `RunExternalJob`, Saga, `WhenFirst`, public `RunChildren`, nested `Parallel`, nested `While`, and nested `ForEach` are deferred and absent from assemblies and builders. `Parallel`, `While`, and `ForEach` are root-sequence-only in v1; nested, branch, item, and leased builders expose no fan-out member. The future registry in Decision 13 records deferred direction and re-entry gates.

### 4. Separate fan-out from join policy

`Parallel` defines a fixed non-empty authored set of independent branches. `ForEach` defines branches from a finite item set. Neither name implies a join policy. Each fan-out is completed with one of:

- `WhenAll`: merge receives successful branch/item results only. A failure prevents the success merge and fails the scope after every branch/item reaches terminal. One failure is preserved directly; multiple failures become `SFE-JOIN-FAILED` with causes ordered by authored branch or item index.
- `WhenAllOutcomes`: merge receives an ordered immutable success-or-failure outcome for every branch/item and returns the complete replacement parent state. The join scope then completes successfully; a following `If` inspects the stored summary and routes rejection through a named step that returns or throws a catalogued failure. No fluent `Fail` member is implied.

Expected domain rejection should normally be represented in a branch's typed result and handled with ordinary `If`; throwing/failing a branch is reserved for execution failure. `If` cannot catch an already terminal failed branch. `WhenAllOutcomes` exists for workflows that intentionally aggregate execution failures.

Branches receive isolated state/input. They cannot mutate parent state concurrently. The join merge is the only operation that creates the next parent state from the pre-fan-out parent snapshot plus immutable branch/item results. Results retain `AuthoredBranchId` or durable item identity and authored order. A merge is committed once and may be retried only before that commit from the same committed inputs.

`ReadOnlyStateSnapshot<TState>` is a runtime-created, non-positional, sealed read-only wrapper with no public constructor or deconstructor. It exposes only its detached `Value`; callers cannot forge a snapshot or infer that future metadata is part of positional equality.

Instance cancellation, operator termination, and the workflow deadline are ancestor terminal transitions. They signal/fence active work and suppress both joins; they are never converted into merge-visible cancellation variants. A scope-local wait or step timeout is an ordinary `Failed` outcome and remains visible to `WhenAllOutcomes`. Neither join automatically cancels a sibling.

Root fixed-`Parallel` branches are admitted in authored order under the host-owned `MaxConcurrentExecutionPathsPerInstance` ceiling. There is no author-level global concurrency option.

An empty root `Parallel` is a graph error discovered by `Build`/`TryBuild`, not an argument exception from the first fluent call. It reports `SFE-AUTH-BRANCH-004` (`EmptyParallelScope`) at the `Parallel` node. This does not broaden placement: `Parallel` remains root-only, and nested, branch, item, and leased builders expose no `Parallel` member.

### 5. Ship bounded durable `ForEach`

Durable dynamic fan-out is important for the first scheduler release, but its v1 form is deliberately bounded:

- a selector produces a finite deterministic `IReadOnlyList<TItem>`;
- the runtime synchronously validates `Count`, copies the list, round-trips it through the fixed codec, and commits one detached snapshot plus stable item identities before child fibers start;
- an explicit positive `maxItems` rejects an oversized collection before fan-out;
- every item has persisted pending/running/terminal state and deterministic result ordering;
- restart reuses the committed item set and never re-enumerates an external source;
- an empty snapshot is valid, admits no item, and invokes the selected merge once with an empty ordered list;
- the node joins with `WhenAll` or `WhenAllOutcomes`;
- item bodies may use nested `If`, but expose no nested `Parallel`, `ForEach`, or `While` in v1.

The item-state projector receives only the detached item and zero-based index. A workflow that needs parent/run data in each item carries that data explicitly in `TItem` when selecting the finite item list; no hidden parent-state capture is promised.

Both modes may specify a positive node-local `maxConcurrency`. Its effective concurrency is the lower of the node limit and the host `MaxConcurrentExecutionPathsPerInstance` ceiling.

### 6. Permit lambdas only for ephemeral business steps

Ephemeral authoring accepts only asynchronous lambda bodies over typed step context: `Func<StepContext<TState>, ValueTask>` and `Func<StepContext<TState>, CancellationToken, ValueTask>`. A caller can express synchronous work by returning `ValueTask.CompletedTask`; public `Action<StepContext<TState>>` overloads are absent so exceptions and completion have one observable path. Lambda bodies mutate or replace state and cannot return `StepResult`; exact-type transient throttling and durable step identity remain features of named typed steps. Durable authoring accepts registered typed step implementations only. This keeps durable dependency construction and required version evolution explicit.

Structural selectors and pure merge/projector expressions remain allowed where the matrix specifies them. They must be deterministic and side-effect-free, may be reinvoked before their result wins a commit, and are never claimed to be fingerprinted by delegate IL. C# cannot reliably prove that an allowed delegate is noncapturing or side-effect-free; documentation and certification therefore treat externally meaningful captures and effects as an explicit author trust boundary rather than promising a runtime check that does not exist.

Alternative considered: durable lambdas with serialized delegates or source hashes. Rejected for v1 because closure capture, dependency injection, code evolution, and stable fingerprinting are not honest across hosts.

### 7. Distinguish workflow deadlines from step attempt timeouts

V1 uses one non-replaceable certified `System.Text.Json` codec with persisted format ID `orcacore-json-v1`. Author selectors, workflow/DAG input, state, item snapshots, results, output, idempotency bytes, and returned query values are codec-detached. Unsupported cyclic or unapproved polymorphic shapes fail before commit. Authors normalize unordered collections when semantic order matters; OrcaCore guarantees deterministic bytes for its supported value graph, not canonicalization of arbitrary collections.

Each business-step attempt receives a codec-detached attempt-local copy of the last committed root/branch/item state. Mutable state changes through `StepContext<TState>.State`; immutable/value state changes through `ReplaceState`. Only a successful winning `Completed` or validated dynamic `WaitForEvent` transition commits the attempt copy. Failure or timeout discards it. A token-ignoring late body may physically overlap a policy retry, but its copy is fenced and it retains no logical commit authority; its physical step-throttle/transient slot remains occupied until return. A leased attempt is stricter: while its prior in-process body is still running, no overlapping policy retry may start and the same lease remains held. After host loss, the same attempt coordinate may be redispatched under the same durable obligation because the old process can no longer execute locally.

`WithRetry(maxAttempts, fixedDelay)` decorates only the immediately preceding business step, may appear once, and counts durable retry-policy ordinals starting at one. Authored `StepResult.Failed`, a normalized unhandled exception, and `StepAttemptTimeoutException` may commit a retry transition when budget remains. Cancellation, workflow deadline, termination, definition conflict, and runtime invariant failure never retry. Only a committed retry transition increments `AttemptNumber`; physical redispatch after crash, replay, optimistic-concurrency loss, or a lost response reuses the same operation ID, attempt ordinal, and absolute attempt deadline and does not consume retry budget. `maxAttempts = 1` therefore permits crash replay of ordinal one but no policy retry.

Decorators are legal only while an immediately preceding undecorated business step is available. Misplacement, repetition, or use after a structural node is rejected eagerly by the builder with a `WorkflowDefinitionException` containing the one catalogued diagnostic; it is not described as a C# compile-time error.

`CompleteWithin(duration)` creates one positive finite deadline from instance start. It includes path admission, retries, delays, event waits, lease queueing, and every continue-as-new generation; replay, wait, retry, host restart, and continue-as-new never reset it. A second fluent call eagerly throws `WorkflowDefinitionException` with `SFE-AUTH-DEADLINE-001` (`DuplicateWorkflowDeadline`), locating the second call as primary and the first as related while preserving the first deadline. When the deadline wins, the instance atomically becomes terminal `TimedOut` with `WorkflowDeadlineExceededException`, blocks admission, cancels wait/timer obligations, signals active attempt tokens, suppresses branch/item merges, and performs definite cleanup or lease quarantine without waiting for token-ignoring bodies.

`WithStepTimeout(duration)` decorates only the immediately preceding business step and bounds one attempt from invocation until return. The runtime supplies a cancellation token, fences/discards the timed-out copy, and records `StepAttemptTimeoutException`. A configured retry receives a higher attempt number and a new attempt deadline, but the workflow deadline remains the outer bound. Structural waits and external workloads are not secretly bounded by a step attempt timeout.

`Wait(eventName, correlation, timeout)` races its event against one runtime-owned timer. The committed winner cancels the losing obligation. If timeout wins, the current root, branch, or item fails with `WorkflowWaitTimeoutException`; there is no timeout-callback builder. A branch/item wait timeout can therefore appear as failure data in its enclosing `WhenAllOutcomes` join.

The orchestration implementation uses its own persisted timer/deadline protocol, not Polly. Polly may be used inside an application adapter for short transport retries, but it does not own workflow state, durable retry schedules, or timeout transitions.

No timeout proves an ambiguous external request did not commit. Cleanup and lease release follow the same ownership/stop-proof rules as cancellation.

All workflow, wait, step, and delay durations are positive and finite; retry delay may be zero but not negative. `CompleteWithin` appears at most once. A valid `ContinueAsNew` is durable-root-only, returns `DurableWorkflowCompletionBuilder<TInput>`, and is terminal for the authored generation: no `End`, step, decorator, or structural node may follow it. At runtime it requires the root to be the sole nonterminal fiber with no descendant scope or owned obligation, increments generation atomically, and inherits the original absolute workflow deadline. Runtime violation fails with `SFE-RUN-001` without changing generation or state. Conditional or finite rollover is deferred rather than implied by this perpetual terminal form.

### 8. Expose stable logical step-occurrence identity

Step execution context includes:

```csharp
context.Execution.WorkflowInstanceId
context.Execution.OperationId       // StepOperationId
context.Execution.AttemptNumber
```

`StepOperationId` is a runtime-created opaque strong value identifying one logical visit to one authored step. It is stable across retry, crash, replay, optimistic-concurrency loss, and host replacement. A new loop visit, `ForEach` item, parallel branch occurrence, or `ContinueAsNew` generation receives a new value. `AttemptNumber` is the positive durable retry-policy ordinal, not the physical CLR invocation count: crash/replay redispatch reuses it, and only a committed failure/timeout retry transition increments it.

The durable runtime derives or persists occurrence identity from committed execution coordinates so a retry after an ambiguous call receives the same value. It does not use reusable authored path text as the unique occurrence identity. Ephemeral execution exposes the same context shape but promises stability only within that in-memory run.

One durable business step should issue at most one logical external effect in v1. An adapter uses `StepOperationId` as a create-or-observe idempotency identity and stores a request fingerprint. OrcaCore promises stable identity plus at-least-once invocation, not exactly-once external effects.

`AttemptNumber` remains public diagnostic metadata, never an external idempotency key or permission to create a second effect. Before first dispatch the durable runtime commits the operation ID, attempt ordinal, optional absolute attempt deadline, and in-flight marker. Redispatch after host loss reuses that coordinate. If recovery observes that the persisted attempt deadline has already elapsed, it commits the timeout transition without redispatch and either advances to the next policy ordinal when budget remains or terminalizes the step. The one-effect convention, side-effect-free structural delegates, create-or-observe adapter behavior, and truthful stop confirmation form an explicit certified trust boundary. Provider/integration certification exercises duplicate invocation and ambiguous recovery; v1 does not pretend that the CLR can prove arbitrary author code pure or introduce a larger general effect framework.

### 9. Use scoped-only durable resource acquisition

The public authoring shape is conceptually:

```csharp
var request = ResourceLeaseRequest.Create(
    ResourceLeaseRequirement.Require(poolName, units),
    additionalRequirements);

builder.AcquireResources(request, body);
builder.AcquireResources(state => request, body);
```

Both values are immutable, factory-created, non-default-validating shapes. A request is copied, non-empty, duplicate-free by exact `ResourcePoolName`, and contains positive units. There is no point/fiber-lifetime form beside the scoped form; no author duration/TTL, renewal, holder ID, or acquisition-timeout overload exists.

The selector is deterministic and side-effect-free. It may be re-invoked before a winning commit, but exactly one normalized request commits before provider mutation and replay reuses it. Static and selector-produced normalized requests are checked against configured pools before queue or provider mutation; any missing names fail together through `ResourcePoolNotConfiguredException` (`WF-RESOURCE-POOL-NOT-CONFIGURED`). The runtime atomically grants the entire valid request or parks only the requesting fiber. Sibling fibers may continue. Lease ownership spans pending and held phases under one runtime-generated obligation and exact instance/generation/fiber/scope occurrence.

At most one lexically owned capacity-reserving durable acquisition may exist along a fiber's ancestry. A descendant cannot acquire while an ancestor acquisition is queued, pending-commit, held, review-marked, or ambiguous-held. Dedicated leased-body builders omit every fan-out member, nested acquisition, and `ContinueAsNew`. Sequential scopes and one fully contained scope per root `While` iteration are valid because the prior lexical scope releases before re-entry. A lease may also appear in a root-level nested `If` or `While` body, or in an independent durable root-`Parallel` branch or root-`ForEach` item body, whenever no live ancestor lease exists. Mutually exclusive `If` arms and independent siblings are valid. Resources required concurrently must be requested atomically. The compiler performs path-sensitive validation and the runtime repeats the inclusive ancestry check before queue/pool mutation.

The lease is a logical capacity reservation, not a live database connection object. A `Wait` inside its body keeps the logical reservation by design. Authors needing a database connection only for one step use adapter-local connection lifetime or a per-step transient throttle; authors keep an `AcquireResources` body no wider than the protected external obligation.

The exact obligation lifecycle is `Queued -> PendingCommit -> Held -> ReviewMarked -> AmbiguousHeld -> Quarantined -> Released`; transitions may skip states when their trigger does not occur, but never reverse or allocate a successor ticket under the same obligation. A retryable leased-step timeout, ambiguous submit, or recovered in-flight attempt moves ownership to `AmbiguousHeld`, preserves the same `StepOperationId`, protection token, ticket identities, pool units, provider generation, fiber/scope occurrence, and capacity, and continues retry under that obligation. A successful retry does not erase the recorded ambiguity. No overlapping retry starts while a token-ignoring prior in-process leased body is still running; host-loss recovery may resume because the old local execution is gone.

Normal body completion, definite pre-effect failure, and cancellation/failure after causally proven cleanup release exact tickets before parent resume. If ambiguity remains when the lexical scope exits, retries exhaust, or cancellation, deadline, termination, or abandonment wins, ownership transfers atomically from `AmbiguousHeld` to capacity-reserving `Quarantined` before any parent progression or failure becomes join-visible. Only trusted causal stop confirmation or an end-to-end resource fence releases quarantine. A `WhenAllOutcomes` branch/item may expose the failure only after that quarantine transfer commits; its merge cannot reclaim the ticket.

Each granted ticket retains exact pool, units, provider generation, and a pool-owned review timestamp; one atomic multi-pool request may therefore reach different per-ticket review deadlines. A deadline only triggers audited exact-owner reconciliation. Time never releases a live or ambiguous holder. Reconciliation may release only when causal owner/provider evidence proves never-committed, already-released, or terminal-and-cleaned-up plus protected-work stop/fence. A missing expected ticket yields `LeaseLost`, never silent reacquisition. Pending-commit, held, marked, ambiguous, and quarantine states reserve exact units. Downward resize may create `max(0, reserved - configured)` debt; it revokes nothing and permits no new grant until debt is zero and the whole next request fits.

Stop confirmation is serialized with normal release and uses one total precedence order: (1) a confirmation ID already bound to another token returns `ConfirmationConflict`; (2) the same ID already accepted for this token, or a token released by accepted confirmation, returns `AlreadyConfirmed`; (3) a live lexical `PendingCommit`/`Held`/`ReviewMarked`/`AmbiguousHeld` token returns `NotConfirmable`; (4) a quarantined token with an unused ID binds the ID and returns `Released` exactly once; and (5) a normally released, unknown, or purged token returns `TokenNotFound`. If normal release wins, an unused confirmation sees `TokenNotFound`; if confirmation wins, later lexical cleanup is an idempotent no-op. Confirmation bindings and token tombstones are retained for the workflow/provider dedup window so stale proof cannot release successor capacity.

V1 durable pools use one serialized resource-governance aggregate per configured provider partition. That aggregate owns pool definitions, FIFO queued atomic multi-pool requests, reservations/tickets, review marks, idempotent resize operations, confirmation bindings, and tombstones. `DurableResourcePoolDefinition.Capacity` is immutable creation capacity; `DurableResourcePoolSnapshot.ConfiguredCapacity` is current capacity after replaying resize records. Startup creates definitions atomically, and later hosts must present the identical creation name/capacity/review definition even when current capacity has been resized; startup never resets current capacity or debt. `ReviewAfter` marks for reconciliation and never expires a lease. `ResizeAsync` rejects zero or negative capacity with `ArgumentOutOfRangeException` before operation-ID binding or mutation, throws `ResourcePoolNotConfiguredException` for an unknown pool, and otherwise is idempotent by caller `ResourcePoolOperationId`: exact replay returns the original `Applied`, while reuse with changed pool or capacity returns `Conflict` without mutation. `GetAsync` uses the same unknown-pool exception. There is no force-release API.

Grant/cancellation is one serialized state machine. Cancellation that wins while an obligation is still `Queued` commits `CancelledBeforeGrant` with zero tickets and zero capacity. Once reservation commit wins, cancellation cannot erase its tickets: cancellation before workflow activation performs one exact compensating release because author code was never admitted, while cancellation after activation uses normal proven release or ambiguity-preserving quarantine. Delayed duplicate commands are idempotent stale no-ops.

The workflow and governance aggregates use an idempotent four-stage reservation protocol: workflow pending obligation, governance all-or-none ticket reservation, workflow activation, governance active-owner confirmation. Crashes leave capacity reserved for exact reconciliation. Friend-only provider-certification barriers named `WorkflowPendingObligationCommitted`, `GovernanceReservationCommitted`, `WorkflowActivationCommitted`, and `GovernanceOwnershipConfirmed` block after their durable commit and before the next protocol command. Each immutable barrier fact identifies the partition, obligation, instance, generation, fiber/scope occurrence, protection token, workflow/governance versions, and exact ticket/pool/unit/provider-generation facts. Deterministic crash tests prove ownership equality, no duplicate or ghost ticket, isolated exact restoration, contended conservation/direct transfer, distinct successor identity, and resize debt as the only permitted over-capacity state.

`OrcaCore.Runtime.Protocol` owns the protocol records, while `OrcaCore.Provider.Abstractions.ResourceGovernance.IDurableResourceGovernanceStore` provides `LoadAsync` plus one expected-version atomic ordered-batch `AppendAsync`. The governance event stream is created through a copying stream-level factory that rejects nulls, version-zero/nonempty mismatches, gaps, duplicates, reordering, and any sequence other than complete versions `1..Version`; append batches are copied, nonempty, consecutively numbered from `expectedVersion + 1`, and commit wholly or conflict. Providers that persist only per-workflow streams cannot satisfy v1 leasing. Certification covers every crash/conflict boundary, contention, FIFO admission, multi-pool atomicity, quarantine, resize debt, confirmations, and tombstones.

Trusted host management exposes `IDurableResourceLeaseDiagnostics.EnumerateOutstandingAsync` and `GetAsync(LeaseProtectionToken, ...)`. Detached immutable snapshots correlate partition, definition/instance/generation, fiber/scope occurrence, obligation, protection token, exact tickets/pools/units/provider generations, review and confirmation state, quarantine time, and accepted confirmation identity. Enumeration covers all outstanding capacity-affecting obligations rather than released history; exact lookup may return retained confirmation tombstones. These diagnostics are advanced operations, not ordinary workflow-instance enumeration.

### 10. Keep infrastructure jobs outside OrcaCore

The first scheduler may submit an ordinary Kubernetes `batch/v1` Job through the Kubernetes API, run on EKS, or use another application adapter. That code belongs to a separate companion project.

A durable scheduler child workflow can:

1. enter a scoped durable capacity acquisition;
2. execute a typed submit step whose adapter uses `StepOperationId` and request fingerprint to create or observe external work;
3. store the returned application-owned external reference in workflow state;
4. use ordinary durable `Wait(EventName, correlation)` for a watcher report;
5. validate the normalized terminal payload, Kubernetes Job UID, `StepOperationId`, `LeaseProtectionToken`, and terminality, then map it to typed workflow output while still inside the lease; and
6. exit the lease scope only after that exact terminal proof. An invalid or unproven report cannot release capacity and follows the ambiguity/quarantine rules.

The companion integration owns credentials, cluster/namespace/region selection, manifests, SDK clients, watcher/relist behavior, external identifiers, and result interpretation. On workflow cancellation/deadline it owns idempotent query/stop reconciliation. It supplies trusted proof through advanced generic `IDurableResourceLeaseRecovery.ConfirmProtectedWorkStoppedAsync(LeaseProtectionToken, StopConfirmationId, ...)` only after the protected work is terminal/absent or end-to-end fenced. Until then, capacity remains quarantined.

This design deliberately avoids a provisional generic `RunExternalJob`. A later generic lifecycle node must specify submit/query/stop, ambiguous outcomes, reports, cancellation, retry, and resource-bracket semantics together.

### 11. Ship `OrcaCore.Dag` as a separate v1 package

`OrcaCore.Dag` is a compiler/planner and durable orchestration facade, not another workflow node family. Each node declares:

- a stable typed node identity;
- a `DurableWorkflowRef<TNodeInput,TNodeOutput>`;
- direct dependency node references; and
- a pure typed input projector.

The public plan supports both resultless `DurableWorkflowRef<TNodeInput>` nodes and resultful `DurableWorkflowRef<TNodeInput,TNodeOutput>` nodes. `DagNodeRef` represents dependency-only nodes; only `DagNodeRef<TOutput>` is valid for `OutputOf`. Every node calls `MapInput` exactly once. Build rejects missing/duplicate mappings, duplicate nodes/dependencies, self-edges, cycles, foreign-plan references, and statically malformed dependency declarations with stable aggregate diagnostic ordering. Independent nodes are valid and are not rejected merely because no other node consumes their output.

The projector receives immutable DAG-run input plus typed outputs of direct successful dependencies only. It cannot inspect child workflow state, provider records, unrelated nodes, or runtime fibers. Because the mapping is opaque code, use of `OutputOf` for an undeclared, resultless, foreign, or otherwise unavailable dependency is validated when the runtime evaluates the mapping, before mapped input is committed or any child starts. Any invalid access or projector failure fails that node with stable code `DAG_INPUT_MAPPING_INVALID`, starts no child for it, and dependency-blocks its dependants; it is not falsely claimed as a build-time fingerprint conflict or static diagnostic. The runtime commits a valid projected input once when all required dependencies succeed, then starts or reattaches one durable child workflow instance for that node. A failed or cancelled node dependency-blocks its dependants; independent ready/running nodes continue. The run terminalizes only when no node can progress and reports every node in stable order as succeeded, failed, cancelled, or dependency-blocked.

Node outputs should be small immutable DTOs. Large data remains in external storage and flows through typed references. Node-child identity and start idempotency are deterministic from DAG run plus node occurrence, so restart or duplicate drive reattaches rather than duplicates work.

The ordinary DAG facade is explicit: `IDagDefinitionRegistry` returns registered/conflict with a cast-free `GetHandleOrThrow()` success projection; `StartOrGetAsync` returns accepted/conflict with `WasExisting` and the same helper; `GetRunAsync` reopens only a matching definition. Conflict helpers throw the corresponding typed DAG registration or start-conflict exception carrying the closed conflict value; callers that need to inspect conflicts retain the result union. `DagRunHandle` exposes detached snapshot, typed node output query, cancellation request, and notification-driven race-free `WaitForTerminalAsync(CancellationToken)`. The wait completes with the detached terminal snapshot, never polls, and caller cancellation cancels only that local wait. `DagRunStatus` is running/cancellation-requested/succeeded/failed/cancelled. Node status is pending/ready/running/cancellation-requested/succeeded/failed/cancelled/dependency-blocked, and every snapshot reports nodes by zero-based authored ordinal. Child failed/timed-out/terminated maps to stable node failure codes; child cancellation without a DAG-run cancellation request maps to `CHILD_CANCELLED` failure.

DAG cancellation is cooperative and idempotent: it prevents new admission, cancels pending/ready nodes, marks running nodes cancellation-requested, preserves already dependency-blocked nodes, propagates to children, and reaches run `Cancelled` only after running children are terminal. Without a DAG cancellation request, any non-success child makes the run `Failed`; all-success makes it `Succeeded`.

`RunChildren` remains an internal DAG runtime mechanism, not a public general workflow member. `OrcaCore.Dag.Hosting` is the sole adapter from plans to durable child execution and consumes a named, versioned internal child-start/join bridge exposed by `OrcaCore.Durable.Hosting` only through `InternalsVisibleTo("OrcaCore.Dag.Hosting")`. DAG scheduling has separate `MaxConcurrentNodes`; it is not the workflow host's per-instance path limit. Saga compensation is not the difference between DAG and Saga: a DAG models dependency readiness and dataflow, while Saga models compensating actions and recovery policy. Saga remains deferred even though a future saga may use DAG-like dependencies.

### 12. Bind durable execution to immutable definition versions and fingerprints

A registered definition is identified by `(DefinitionId, DefinitionVersion)` and an immutable structural fingerprint. `DefinitionId` is a non-defaultable immutable reference created through `New`/`Parse`/`TryParse`; `DefinitionVersion` is a non-defaultable immutable reference with a positive constructor and `Initial`. Existing instances resume only against the exact registered version/fingerprint. Registration rejects null identity/version and two different fingerprints for the same identity/version.

Caller-created string-backed strong values use one uniform factory-owned construction shape. `EventName`, `WorkflowOutcomeName`, `AuthoredBranchId`, `DagNodeId`, `ResourcePoolName`, `TransientPoolName`, `StartIdempotencyKey`, `CorrelationId`, `EventId`, `StopConfirmationId`, `ResourcePoolOperationId`, and `ResourceGovernancePartitionId` have private constructors and one public `Create(string)` factory. They expose neither a public constructor nor `Parse`/`TryParse`, `New`, implicit conversion, or a parallel raw-string overload. Runtime-created identities retain private construction plus canonical `Parse`/`TryParse`; `DefinitionId` retains `New`/`Parse`/`TryParse`, and positive numeric `DefinitionVersion` retains its validating constructor and `Initial`. `DefinitionId.Parse(Guid.Empty)` rejects with `ArgumentException`, and `TryParse` returns `false`; the same nonempty GUID rule applies to runtime-created `InstanceId`, `WaitId`, and `DagRunId`. `Create` validates but never trims or normalizes, so it does not weaken exact ordinal equality.

The fingerprint covers inspectable authored structure only: node/member kinds, ordering, strong values, referenced step/workflow types, static requests, and fixed codec format. It does not hash delegate IL, DI configuration, step constructor/configuration behavior, external adapter behavior, or arbitrary bytes supplied by an author. Changing selector/projector/merge/output code, step construction/configuration, DAG mapping, or external-request construction therefore requires a new `DefinitionVersion`; the version bump is the sole v1 contract for opaque code changes.

DAG definitions have equivalent immutable identity/version/fingerprint binding over inspectable node references, edges, ordering, and codec format. Projector behavior itself is versioned, not fingerprinted.

### 13. Keep an explicit future-capability registry

Deferred items stay visible in planning but have no v1 public symbols:

| Capability | Why deferred | Re-entry gate |
|---|---|---|
| Generic `RunExternalJob` | Submit/query/stop, ambiguous completion, reports, cancellation, and resource ownership must form one lifecycle contract | Two concrete integrations plus reviewed lifecycle/fencing semantics |
| Saga | Compensation order, failure of compensation, manual intervention, version evolution, and audit surface are not v1-critical | Separate capability proposal and runtime-owned durable acceptance suite |
| `WhenFirst` | Loser cancellation and cleanup are unsafe around leases and external work without a complete policy | Reviewed winner/loser/result/cleanup semantics and race tests |
| Public `RunChildren` | General child input/output/failure/version semantics add a second composition model | Separate workflow-composition proposal; DAG keeps an internal child primitive |
| Nested `While` | Multiplies path-sensitive lifetime and replay analysis | Compiler/runtime evidence and an explicit matrix amendment |
| Nested `ForEach` | Multilevel dynamic expansion multiplies bounds, payload, identity, and admission complexity | Combined item bounds, identity, admission, payload, merge, and durable restart semantics |
| Conditional/finite `ContinueAsNew` | V1's root form is an unconditional/perpetual generation terminal | Root-only conditional terminal shape, finite path validation, output/lineage, and deadline semantics |
| Durable lambda steps | Delegate identity, capture, and versioning are unsafe for persisted definitions | Stable code identity and replay/version contract |
| Definition-wide retry | Reset point, state/output retention, and occurrence identity are unresolved | Reset, retained input/state, attempt identity, and terminal policy |
| Failed-instance/step management retry | Terminal history is immutable in v1 | New-generation lineage, retained state/input, output invalidation, and authorization |
| Public pause/resume/archive/purge | These operations enlarge lifecycle, retention, and authorization semantics | Exact lifecycle transitions, reference safety, authorization, and provider certification |
| Workflow-authored `Publish` | No portable outbox event contract is needed for the first scheduler journey | Typed payload/destination, event identity, commit boundary, dispatch, and deduplication |
| Workflow-authored `Cancel` | Self-cancellation adds no value over typed failure/output in v1 | Target, terminal result, descendant/lease cleanup, and authorization |
| Definition-targeted event fanout | Target snapshot and per-target deduplication would enlarge delivery | Committed target set, per-target `EventId` deduplication, retry, and late-registration rules |

`WaitLong` and `Yield` are not in this registry because they are intentionally removed concepts, not promised future features.

### 14. Keep runtime and management facades application-oriented

`IWorkflowDefinitionRegistry` explicitly registers the four ephemeral/durable resultless/resultful definition families and returns a closed `Registered`/`HostIncompatible`/`Conflict` result with a typed definition handle. `DefinitionHostCompatibilityFailure` is a closed union of `EngineModeMismatch`, `MissingTransientPools`, and `MissingDurableResourcePools`; missing-name collections are copied, distinct, and ordinal-sorted. Validation performs mode mismatch first, then all statically inspectable pool references, then fingerprint conflict, and mutates nothing on failure. Ephemeral registration checks all authored transient pools; durable registration checks static lease requests, while selector-created pool names are checked at runtime before queue/provider mutation. `WorkflowRegistrationResult<TDefinitionHandle>.GetHandleOrThrow()` returns the registered handle, throws `WorkflowDefinitionHostCompatibilityException` with fixed code `WF-DEFINITION-HOST-INCOMPATIBLE` carrying the closed compatibility failure, or throws `WorkflowDefinitionRegistrationConflictException` carrying the conflict. A definition handle owns `StartOrGetAsync(input, StartIdempotencyKey)` and `GetInstanceAsync(InstanceId)`. Start binds identity, version, structural fingerprint, and fixed-codec input bytes; compatible key reuse returns the same handle with `WasExisting = true`, while incompatible reuse returns `StartIdempotencyConflict` and starts nothing. `WorkflowStartResult<TInstanceHandle>.GetHandleOrThrow()` returns the accepted handle or throws `WorkflowStartIdempotencyConflictException` carrying that conflict. The closed result unions remain the inspection surface for consumers that need failure details or `WasExisting`; the helpers remove casts from the ordinary success path.

The ordinary instance management surface is intentionally small. `WorkflowInstanceHandle` exposes only `GetSnapshotAsync`, fixed-codec-detached `GetStateAsync<TState>`, cooperative idempotent `RequestCancellationAsync`, and immediately fenced `TerminateAsync`. `WorkflowInstanceHandle<TOutput>` additionally exposes nonblocking typed pending/available/unavailable `GetOutputAsync` and notification-driven race-free `WaitForOutputAsync(CancellationToken)`. The latter returns the detached output, throws typed `WorkflowOutputUnavailableException` carrying terminal status/failure when the instance terminalizes without output, never polls, and treats caller cancellation as cancellation of only the local wait. `WorkflowStartResult<WorkflowInstanceHandle<TOutput>>` has a same-named convenience extension so the common path is `await start.WaitForOutputAsync(token)`; it projects through `GetHandleOrThrow()`. Durable waiting uses subscribe/recheck around the committed notification boundary so completion cannot be lost. The exact application snapshot remains unchanged: it carries authored/business facts and active wait deadlines, and represents terminal workflow timeout through `Status = TimedOut` plus `Failure`. Running absolute workflow deadline, active attempt ordinal/deadline/outcome, executable plans, branch/item or in-flight attempt copies, `FiberId`, `ScopeId`, stream versions, and raw obligations remain runtime/BCL telemetry or advanced diagnostics rather than v1 application snapshot/history. Lease quarantine is projected separately by lease diagnostics. Resultless handles have no output member. Pause/resume, failed-instance retry, archive, purge, workflow-instance enumeration, bulk selection/list/count/statistics, and durable history are deferred rather than mode-specific v1 methods.

`IWorkflowEventClient` has exactly two route names, `DeliverToInstanceAsync` and `DeliverByCorrelationAsync(DefinitionId, event)`, with one payloadless `WorkflowEvent` and one generic `WorkflowEvent<TPayload>` overload for each route (four overloads total). Both event factories validate strong values and UTC time; the generic form additionally fixed-codec detaches its payload. Accepted delivery atomically deduplicates per target instance by `EventId` and records an at-least-once continuation handoff. Same ID/same envelope is `Duplicate`; same ID/different envelope is `EventConflict`; `NoActiveWait` and `InstanceTerminal` do not consume the ID. Correlation routing resolves `(DefinitionId, EventName, CorrelationId)`; registration of a second active wait for that pair, including within one instance, fails with `AmbiguousWaitRegistrationException` before parking. There is no ambiguous-match result or definition-wide fanout.

Event/correlation pairs are signal streams. After one wait consumes an event, a later loop occurrence may register the same pair and consume a later event. Authors encode occurrence-specific matching in `CorrelationId`; public delivery never accepts wait sequence, fiber, scope, provider generation, checkpoint, or raw command identity.

Delivery before a matching wait returns `NoActiveWait` and does not consume `EventId`; redelivery of the same envelope after wait registration can therefore be accepted. The currently registered engine role owns the one `IWorkflowEventClient` implementation and its routing store. Ephemeral and durable engine roles are mutually exclusive in one service provider for v1, so routing ownership is never implicit or last-registration-wins.

Microsoft hosting entry points are exact and role-specific: `OrcaCoreEphemeralEngineServiceCollectionExtensions.AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`, `OrcaCoreDurableEngineServiceCollectionExtensions.AddOrcaCoreDurableEngine(DurableEngineHostOptions)`, callback-only `AddOrcaCoreDurableEventIngress()`, development/test `AddOrcaCoreInMemoryDurableProvider()`, production `OrcaCorePostgreSqlProviderServiceCollectionExtensions.AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)`, and `OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)`. The PostgreSQL options require nonblank `ConnectionString` and `Schema`; all options are programmatic immutable configuration objects, are copied and validated immediately, and are not claimed to be configuration-binder DTOs. Identical repeated registration is idempotent and conflicting role/options fail startup. Registering ephemeral and durable engine roles together fails startup. The callback-only role exposes durable event ingress and continuation handoff but no registry, worker, timer/reconciler, or DAG coordinator. `AddOrcaCoreDag` requires the durable-engine role and adds only DAG coordination/registry. There is no catch-all `AddOrcaCore`, separate hosted-service toggle, implicit mode selection, serializer replacement hook, or host-owned bulk facade.

Advanced resource administration remains separate: `IDurableResourcePoolManagement.ListAsync`, `GetAsync`, and `ResizeAsync` expose exact aggregate snapshots and the closed resize result; `IDurableResourceLeaseRecovery` accepts trusted stop confirmation; and `IDurableResourceLeaseDiagnostics` exposes trusted outstanding-obligation discovery. Forced termination and workflow deadline follow Decision 9 for protected leased work and never imply that external work stopped.

All public runtime failures derive from `OrcaCoreException` and expose a nonblank stable `Code`. Built-in failure classes bind one fixed code; normalized author/integration failures use the documented generic code without treating raw exception text or CLR type names as protocol identity. Workflow, DAG, management, and build result-to-exception helpers preserve the existing closed conflict/failure value as structured data.

### 15. Use one exact execution-path token model

`StructuredExecutionHostOptions.MaxConcurrentExecutionPathsPerInstance` is the host-owned logical path ceiling shared by root, fixed branches, and `ForEach` items. A runnable root/branch/item owns one token. Parking on an event, delay, resource request, or join releases it; progression reacquires it. A parent releases its token before fan-out and reacquires one only for merge/continuation, so a ceiling of one cannot deadlock solely because the parent waits for its children.

Root fixed-`Parallel` branches queue by authored ordinal and root-`ForEach` items by index. `ForEachOptions.MaxConcurrency` separately counts admitted nonterminal item scopes, including items parked in waits, delays, or resource admission, and only tightens the host path ceiling. `DagHostOptions.MaxConcurrentNodes` counts admitted nonterminal DAG nodes, including children parked in waits, delays, or lease queues, until their child instance is terminal. It is not merely a count of currently executing CLR work. Per-step throttles, transient pools, and durable resource leases remain independent limits with different lifetimes.

A timed-out token-ignoring attempt loses its logical path token and commit authority, allowing retry or sibling progression, but continues holding its physical step-throttle/transient slot until it actually returns. Cooperative execution therefore does not imply that external work or late bodies are physically single-threaded.

### 16. Retarget guards before implementation

Phase 0 guards currently encode rejected names and semantics, including `AcquireLease`, author duration/expiry reclaim, durable nested acquisition absence, raw strings, `WaitLong`, and external-job facade assumptions. They cannot be treated as completed evidence.

Tasks 3.1 through 3.12, including separate required slices 3.11a through 3.11d, are one 15-task expected-red Phase 0 packet. They use the exact companion declarations, semantic matrix, provider/facade contracts, project graph, and deferred/removed absence list. Source implementation does not proceed to section 4 until every section-3 task is complete and the entire packet receives independent approval. Public-signature absence scans use qualified names where structural nodes and result variants could otherwise collide. The deterministic lane is limited to friend-only test seams and fixed-codec immutable facts; no test hook becomes a public authoring/runtime API. The diagnostic catalog fixes every `WF-*`, `SFE-*`, `DAG-*`, and public exception code used by guards plus one canonical `AuthoredLocation` grammar, and guards reject undocumented or duplicate codes.

## Risks / Trade-offs

- **[Bounded durable `ForEach` still adds persistence and scheduler work]** -> Commit the finite item set once, reuse existing structured fibers, prohibit nesting, and require a positive item bound.
- **[Root-only fan-out cannot express conditional, item-local, or leased nested concurrency in v1]** -> Accept the smaller first-release surface, compose sequential root `Parallel`/`ForEach` scopes or use durable DAG concurrency, and require a future amendment to define recursive identity, admission, merge, and lease rules before nested fan-out is introduced.
- **[All-outcomes merge encourages swallowing infrastructure failures]** -> Make acceptance an explicit merge result, preserve complete failure diagnostics, and document domain rejection as typed data.
- **[A workflow deadline terminalizes before external cleanup]** -> Fence resume, quarantine capacity, and require trusted stop/fence proof before reuse.
- **[Stable operation identity is mistaken for exactly once]** -> Document at-least-once invocation and require create-or-observe adapters with request fingerprints.
- **[Holding a lease across `Wait` appears wasteful]** -> Define it as logical external capacity, keep scopes narrow, and use step-local connection lifetime for ordinary database access.
- **[Typed DAG mapping becomes verbose]** -> Restrict inputs to run input plus direct dependency outputs and let C# type checking replace runtime dictionaries and casts.
- **[Durable typed steps are less concise than lambdas]** -> Preserve concise ephemeral lambdas; favor restart/version honesty for durable workflows.
- **[Deferred features disappear from product direction]** -> Maintain the explicit registry and re-entry criteria while keeping binaries free of placeholders.
- **[Package split creates ceremony]** -> Keep `OrcaCore` as the primary application package, require explicit engine-role packages, and validate every supported clean-consumer package set from the local Phase 0 feed; advanced packages remain opt-in.
- **[Fingerprint cannot detect arbitrary code changes]** -> Make it structural-only and require a version bump for every opaque behavior change; do not accept author-supplied contributors that imply stronger detection.
- **[One fixed codec rejects some CLR graphs]** -> Fail unsupported/cyclic/unapproved polymorphic shapes before commit and version the persisted format as `orcacore-json-v1` rather than claiming arbitrary serializer portability.
- **[One serialized resource-governance aggregate limits throughput]** -> Accept the v1 correctness trade-off for mandatory EKS scheduling, certify conflict/restart behavior, and require an explicit partitioning amendment before sharding.

## Migration Plan

1. Approve this amended plan and final selected-mode signature matrix.
2. Replace all 15 required Phase 0 guard tasks 3.1 through 3.12, including 3.11a through 3.11d, and obtain independent approval of the whole packet.
3. Consolidate typed mode-first workflow authoring and delete provisional aliases/results/nodes.
4. Implement joins and bounded durable `ForEach` on the existing structured-fiber substrate.
5. Implement persisted workflow/step deadlines, `StepOperationId`, and scoped durable leases with quarantine/stop proof.
6. Establish exact tier/package boundaries, role-specific hosting entry points, reduced application facades/event delivery, and serialized resource-governance provider contract.
7. Implement `OrcaCore.Dag` and `OrcaCore.Dag.Hosting` with complete resultless/resultful planning, typed operation results, and the isolated friend child bridge; keep scheduler integrations outward-only.
8. Rewrite samples and documentation, run strict OpenSpec validation and all public/package/runtime/provider guards, and prepare independent review.

Rollback is source-level: revert the change and recreate development fixtures/stores. No released package or durable-data compatibility contract exists.

## Open Questions

No release-blocking design question remains. Deferred capabilities require their own future proposals and cannot be introduced through implementation convenience or placeholder APIs.
