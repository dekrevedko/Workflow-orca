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
- Make accepted third-party events durable before acknowledgement, retain events that precede waits, and reactivate cold durable instances without requiring broker handlers to know workflow definitions or runtime state.
- Separate immutable workflow declaration, application-composition registration, and typed runtime handle lookup.
- Commit outbound workflow events with workflow state and expose a transport-neutral dispatcher seam suitable for MassTransit, Rebus, SNS, SQS, RabbitMQ, and equivalent adapters.
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
- Couple OrcaCore to a message-broker SDK, provide built-in broker publishing, or promise exactly-once broker delivery.
- Discover workflows or event contracts by runtime assembly scanning or make Temporal-style attributes a second source of workflow truth.

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

`OrcaCore` is the primary application contract package, not a dependency-only meta-package, and owns the `OrcaCore` application namespace. Every other package has one assembly with the same identity as its PackageId and a fixed public/internal tier in the package manifest. Document 17 fixes the CLR namespace and assembly owner for every host-option, DAG, management, recovery, diagnostics, protocol, and provider family; unqualified sketches do not grant placement freedom. Application contracts never reference advanced seams. Runtime protocol never references provider abstractions. Engines may consume both advanced seams internally. `OrcaCore.Dag.Hosting` is the only DAG-to-durable-runtime product bridge and uses a named, versioned internal child-start/join contract exposed by `OrcaCore.Durable.Hosting` through `InternalsVisibleTo("OrcaCore.Dag.Hosting")`. The complete product and owning-test friend graph is closed by Decision 22; its one cross-package test edge remains `OrcaCore.Engine.Durable -> OrcaCore.ProviderCertification` for deterministic resource-governance barrier certification. `OrcaCore.Dag` does not pull provider or infrastructure SDKs into the core graph. A companion scheduler may be in `OrcaCore.slnx`, but no OrcaCore product project references it.

Hosting extension classes are owned by their role assemblies rather than sharing one catch-all owner type. The first complete production durable provider is `OrcaCore.Providers.PostgreSql`; its one options object contains required nonblank `ConnectionString` and `Schema` values that registration copies and validates immediately, and its registration supplies the complete certified durable provider role set. `OrcaCore.Providers.InMemory` remains development/test only. Package fixtures validate only declared package references from a local Phase 0 feed; no project-reference transitivity or undeclared host package may make a consumer compile accidentally.

`OrcaCore` owns immutable workflow-event contract descriptors, self-routing inbound envelopes, typed acceptance results, resumed/outbound application event projections, and durable authoring members. `OrcaCore.Durable.Hosting` owns `IWorkflowEventIngress`, the application-shaped `IWorkflowEventDispatcher`, its dispatch result/failure values, the durable engine/ingress composition builders, and hosted pumps. Serialized route/inbox/outbox records remain in `OrcaCore.Runtime.Protocol`; provider stores and raw commit records remain in `OrcaCore.Provider.Abstractions`; no application dispatcher receives an `OutboxWrite`, internal continuation kind, stream version, or provider state.

"Provider/runtime SPI" means the explicitly advanced service-provider interface for a storage/transport adapter or custom runtime host. It is not the ordinary workflow-author API and is consumed only by implementers who intentionally reference the advanced package.

Alternative considered: one broad abstractions package separated only by namespaces. Rejected because package restoration, IntelliSense, and public signatures would still present every advanced type as a normal application concept.

### 2. Make workflow input and output part of the type contract

A reusable durable workflow reference names its external contract without exposing internal business state:

```csharp
DurableWorkflowRef<TInput, TOutput>
DurableWorkflowRef<TInput>       // resultless
```

Ephemeral definitions expose symmetric `EphemeralWorkflowRef<TInput,TOutput>` and `EphemeralWorkflowRef<TInput>` values for application-configuration registration and exact handle lookup; only durable references may become DAG child contracts. Mode-first staged builders produce immutable definitions carrying the matching reference. `Init` consumes the input once to create private workflow state. Each mode exposes exactly four completion overloads: resultless `End()`, resultless `End(WorkflowOutcomeName)`, resultful `End<TOutput>(Func<ReadOnlyStateSnapshot<TState>,TOutput>)`, and resultful `End<TOutput>(Func<ReadOnlyStateSnapshot<TState>,TOutput>, WorkflowOutcomeName)`. There are no nullable or optional outcome/selector parameters; an explicitly null selector or outcome throws `ArgumentNullException` immediately. `End` commits output, terminal status, and optional fixed outcome atomically. Resultless workflows use the one-arity reference rather than `Unit` in the public signature.

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

Every fixed root-`Parallel` branch fiber exists when its scope starts. Runnable branches receive
host-owned `MaxConcurrentExecutionPathsPerInstance` tokens fairly in authored order; there is no
branch/live-fiber admission resource and no author-level global concurrency option.

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

V1 uses one non-replaceable certified `System.Text.Json` codec with persisted format ID `orcacore-json-v1`. Author selectors, workflow/DAG input, state, item snapshots, results, output, idempotency bytes, and returned query values are codec-detached. Unsupported cyclic or unapproved polymorphic shapes fail before commit. The closed collection allowlist has one JSON-array sequence representation (`T[]`, `List<T>`, `IList<T>`, or `IReadOnlyList<T>` declarations, materialized only as a one-dimensional array or exact `List<T>`) and one JSON-object map representation (`Dictionary<string,T>`, `IDictionary<string,T>`, or `IReadOnlyDictionary<string,T>` declarations, materialized only as exact `Dictionary<string,T>`). Sequence enumeration order and dictionary insertion/enumeration order are part of the encoded value. Dictionaries therefore remain usable only with string keys and a normalized insertion order when order is not itself business data. Sets, queues, linked lists, multidimensional arrays, sorted/custom dictionaries, custom collection subclasses, and every other declared or runtime collection shape reject before commit. OrcaCore guarantees deterministic bytes for this supported value graph and order; it does not canonicalize arbitrary collections.

Each business-step attempt receives a codec-detached attempt-local copy of the last committed root/branch/item state. Mutable state changes through `StepContext<TState>.State`; immutable/value state changes through `ReplaceState`. Only a successful winning `Completed` or validated dynamic `WaitForEvent` transition commits the attempt copy. Failure or timeout discards it. A token-ignoring late body may physically overlap a policy retry, but its copy is fenced and it retains no logical commit authority; its physical step-throttle/transient slot remains occupied until return. A leased attempt is stricter: while its prior in-process body is still running, no overlapping policy retry may start and the same lease remains held. After host loss, the same attempt coordinate may be redispatched under the same durable obligation because the old process can no longer execute locally.

`WithRetry(maxAttempts, fixedDelay)` decorates only the immediately preceding business step, may appear once, and counts durable retry-policy ordinals starting at one. Authored `StepResult.Failed`, a normalized unhandled exception, and `StepAttemptTimeoutException` may commit a retry transition when budget remains. Cancellation, workflow deadline, termination, definition conflict, and runtime invariant failure never retry. Only a committed retry transition increments `AttemptNumber`; physical redispatch after crash, replay, optimistic-concurrency loss, or a lost response reuses the same operation ID, attempt ordinal, and absolute attempt deadline and does not consume retry budget. `maxAttempts = 1` therefore permits crash replay of ordinal one but no policy retry.

Decorators are legal only while an immediately preceding undecorated business step is available. Misplacement, repetition, or use after a structural node is rejected eagerly by the builder with a `WorkflowDefinitionException` containing the one catalogued diagnostic; it is not described as a C# compile-time error.

`CompleteWithin(duration)` creates one positive finite deadline from instance start. It includes path admission, retries, delays, event waits, lease queueing, and every continue-as-new generation; replay, wait, retry, host restart, and continue-as-new never reset it. A second fluent call eagerly throws `WorkflowDefinitionException` with `SFE-AUTH-DEADLINE-001` (`DuplicateWorkflowDeadline`), locating the second call as primary and the first as related while preserving the first deadline. When the deadline wins, the instance atomically becomes terminal `TimedOut` with `WorkflowDeadlineExceededException`, blocks admission, cancels wait/timer obligations, signals active attempt tokens, suppresses branch/item merges, and performs definite cleanup or lease quarantine without waiting for token-ignoring bodies.

`WithStepTimeout(duration)` decorates only the immediately preceding business step and bounds one attempt from invocation until return. The runtime supplies a cancellation token, fences/discards the timed-out copy, and records `StepAttemptTimeoutException`. A configured retry receives a higher attempt number and a new attempt deadline, but the workflow deadline remains the outer bound. Structural waits and external workloads are not secretly bounded by a step attempt timeout.

`Wait(eventContract, correlation, timeout)` races one explicit payloadless or typed `WorkflowEventContract` descriptor against one runtime-owned timer. The descriptor's stable `EventName` plus positive `EventContractVersion`, not a CLR type name or attribute lookup, is the durable match identity. The committed event/timer winner cancels the losing obligation. If timeout wins, the current root, branch, or item fails with `WorkflowWaitTimeoutException`; there is no timeout-callback builder. A branch/item wait timeout can therefore appear as failure data in its enclosing `WhenAllOutcomes` join.

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
4. use ordinary durable `Wait(WorkflowEventContract<TReport>, correlation)` for a watcher report;
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

The fingerprint covers inspectable authored structure only: node/member kinds, ordering, strong values, referenced step/workflow types, static requests, and fixed codec format. It excludes compiler format, workflow mode, definition identity/version, and every compiler option because those are either separate binding values or acceptance policy rather than authored structure. It does not hash delegate IL, DI configuration, step constructor/configuration behavior, external adapter behavior, or arbitrary bytes supplied by an author. Changing selector/projector/merge/output code, step construction/configuration, DAG mapping, or external-request construction therefore requires a new `DefinitionVersion`; the version bump is the sole v1 contract for opaque code changes.

Compiler format remains a distinct registration/resume binding. A durable host retains support for
every compiler format referenced by a nonterminal instance and retires a format only after those
instances terminalize or are explicitly migrated. Before the first released compatibility
contract, a format bump may use a hard cutover when no supported persisted instance exists.

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
| Workflow-authored `Cancel` | Self-cancellation adds no value over typed failure/output in v1 | Target, terminal result, descendant/lease cleanup, and authorization |

`WaitLong` and `Yield` are not in this registry because they are intentionally removed concepts, not promised future features.

### 14. Keep runtime and management facades application-oriented

Every built ephemeral and durable resultless/resultful definition exposes a matching state-opaque typed reference. `IWorkflowDefinitionRegistry` explicitly registers all four families and returns a closed `Registered`/`HostIncompatible`/`Conflict` result with a typed definition handle. It also exposes four `GetRequiredHandle(reference)` overloads that resolve an already registered exact mode/identity/version/fingerprint without registering; an absent or stale exact reference throws `WorkflowDefinitionNotRegisteredException` with code `WF-DEFINITION-NOT-REGISTERED` and structured `DefinitionId`, `DefinitionVersion`, and `DefinitionFingerprint` properties copied from the requested reference. `DefinitionHostCompatibilityFailure` is a closed union of `EngineModeMismatch`, `MissingTransientPools`, `MissingDurableResourcePools`, and `MissingWorkflowEventDispatcher`; missing-name collections are copied, distinct, and ordinal-sorted. Validation performs mode mismatch first, all statically inspectable host capabilities second, then fingerprint conflict, and mutates nothing on failure. Ephemeral registration checks all authored transient pools; durable registration checks static lease requests and whether a definition containing `Publish` has an application dispatcher, while selector-created pool names are checked at runtime before queue/provider mutation.

`AddOrcaCoreEphemeralEngine` and `AddOrcaCoreDurableEngine` return `OrcaCoreEphemeralEngineBuilder` and `OrcaCoreDurableEngineBuilder`, respectively. Each exposes only the two resultless/resultful `AddWorkflow` overloads for its own mode, returns itself for composition, and stages the already built immutable definition; application modules may group explicit calls in ordinary extension methods. Declaration therefore stays independent from host composition:

```csharp
services.AddOrcaCoreDurableEngine(options)
    .AddWorkflow(OrderFulfillment.Definition);

var orders = registry.GetRequiredHandle(OrderFulfillment.Reference);
```

`OrderFulfillment.Definition` and its reference are application-owned static declarations, not DI-created graphs. A logical durable definition uses the same explicitly fixed `DefinitionId` across process restarts and deployments; production catalog reconstruction never calls `DefinitionId.New()` to mint a new identity at startup. Environment-specific graphs or opaque behavior require a deliberately distinct identity/version rather than conditional construction under the same durable contract.

Before readiness or any worker, timer, continuation, inbox, outbox, or DAG pump starts, the selected engine preflights the complete staged batch and applies it atomically. Exact duplicate mode/identity/version/fingerprint entries are idempotent; a missing capability or conflicting fingerprint starts no loop and leaves the registry unchanged. The staged startup batch is frozen, not the entire low-level registry, so explicit dynamic/test registration remains possible without adding a fourth registry result. `AddOrcaCoreDurableEventIngress` exposes no definition builder, registry, reference lookup, worker, or execution loop; combining it with an engine role remains invalid.

`WorkflowRegistrationResult<TDefinitionHandle>.GetHandleOrThrow()` returns the registered handle, throws `WorkflowDefinitionHostCompatibilityException` with fixed code `WF-DEFINITION-HOST-INCOMPATIBLE` carrying the closed compatibility failure, or throws `WorkflowDefinitionRegistrationConflictException` carrying the conflict. A definition handle remains the sole owner of `StartOrGetAsync(input, StartIdempotencyKey)` and `GetInstanceAsync(InstanceId)`. Start and exact-reference lookup never register as a side effect. Start binds identity, version, structural fingerprint, and fixed-codec input bytes; compatible key reuse returns the same handle with `WasExisting = true`, while incompatible reuse returns `StartIdempotencyConflict` and starts nothing. `WorkflowStartResult<TInstanceHandle>.GetHandleOrThrow()` returns the accepted handle or throws `WorkflowStartIdempotencyConflictException` carrying that conflict. The closed result unions remain the inspection surface for consumers that need failure details or `WasExisting`; the helpers remove casts from the ordinary success path.

The ordinary instance management surface is intentionally small. `WorkflowInstanceHandle` exposes only `GetSnapshotAsync`, fixed-codec-detached `GetStateAsync<TState>`, cooperative idempotent `RequestCancellationAsync`, and immediately fenced `TerminateAsync`. `WorkflowInstanceHandle<TOutput>` additionally exposes nonblocking typed pending/available/unavailable `GetOutputAsync` and notification-driven race-free `WaitForOutputAsync(CancellationToken)`. The latter returns the detached output, throws typed `WorkflowOutputUnavailableException` carrying terminal status/failure when the instance terminalizes without output, never polls, and treats caller cancellation as cancellation of only the local wait. `WorkflowStartResult<WorkflowInstanceHandle<TOutput>>` has a same-named convenience extension so the common path is `await start.WaitForOutputAsync(token)`; it projects through `GetHandleOrThrow()`. Durable waiting uses subscribe/recheck around the committed notification boundary so completion cannot be lost. The exact application snapshot remains unchanged: it carries authored/business facts and active wait deadlines, and represents terminal workflow timeout through `Status = TimedOut` plus `Failure`. Running absolute workflow deadline, active attempt ordinal/deadline/outcome, executable plans, branch/item or in-flight attempt copies, `FiberId`, `ScopeId`, stream versions, and raw obligations remain runtime/BCL telemetry or advanced diagnostics rather than v1 application snapshot/history. Lease quarantine is projected separately by lease diagnostics. Resultless handles have no output member. Pause/resume, failed-instance retry, archive, purge, workflow-instance enumeration, bulk selection/list/count/statistics, and durable history are deferred rather than mode-specific v1 methods.

Durable event ingress and outbound dispatch follow Decisions 24 and 25. The ordinary facade exposes one self-routing acceptance operation rather than caller-selected delivery methods, never exposes wait sequence/fiber/scope/provider/checkpoint identity, and never returns `NoActiveWait`. Ephemeral hosting makes no no-loss event-ingress or durable-publish promise.

Microsoft hosting entry points remain exact and role-specific: `OrcaCoreEphemeralEngineServiceCollectionExtensions.AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`, `OrcaCoreDurableEngineServiceCollectionExtensions.AddOrcaCoreDurableEngine(DurableEngineHostOptions)`, callback-only `AddOrcaCoreDurableEventIngress()`, development/test `AddOrcaCoreInMemoryDurableProvider()`, production `OrcaCorePostgreSqlProviderServiceCollectionExtensions.AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)`, and `OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)`. The first two return their mode-specific composition builder rather than `IServiceCollection`; `AddWorkflow` is a builder operation, not another hosting role entry point. The PostgreSQL options require nonblank `ConnectionString` and `Schema`; all options are programmatic immutable configuration objects, are copied and validated immediately, and are not claimed to be configuration-binder DTOs. Identical repeated role registration is idempotent and conflicting role/options fail startup. Registering ephemeral and durable engine roles together fails startup. The callback-only role exposes durable event acceptance and continuation handoff but no registry, workflow catalog, worker, timer/reconciler, outbox dispatcher, or DAG coordinator. `AddOrcaCoreDag` requires the durable-engine role and adds only DAG coordination/registry. There is no catch-all `AddOrcaCore`, separate hosted-service toggle, implicit mode selection, serializer replacement hook, reflection discovery, or host-owned bulk facade.

Advanced resource administration remains separate: `IDurableResourcePoolManagement.ListAsync`, `GetAsync`, and `ResizeAsync` expose exact aggregate snapshots and the closed resize result; `IDurableResourceLeaseRecovery` accepts trusted stop confirmation; and `IDurableResourceLeaseDiagnostics` exposes trusted outstanding-obligation discovery. Forced termination and workflow deadline follow Decision 9 for protected leased work and never imply that external work stopped.

All public runtime failures derive from `OrcaCoreException` and expose a nonblank stable `Code`. Built-in failure classes bind one fixed code; normalized author/integration failures use the documented generic code without treating raw exception text or CLR type names as protocol identity. Workflow, DAG, management, and build result-to-exception helpers preserve the existing closed conflict/failure value as structured data.

### 15. Use one exact execution-path token model

`StructuredExecutionHostOptions.MaxConcurrentExecutionPathsPerInstance` is the host-owned logical path ceiling shared by root, fixed branches, and `ForEach` items. A runnable root/branch/item owns one token. Parking on an event, delay, resource request, or join releases it; progression reacquires it. A parent releases its token before fan-out and reacquires one only for merge/continuation, so a ceiling of one cannot deadlock solely because the parent waits for its children.

Every fixed root-`Parallel` branch fiber exists when the scope starts; runnable branches queue for
path tokens by authored ordinal. There is no separate branch or live-fiber admission resource.
Root-`ForEach` items queue by index, while `ForEachOptions.MaxConcurrency` separately counts admitted
nonterminal item scopes, including items parked in waits, delays, or resource admission, and only
tightens the host path ceiling. An admitted item can therefore block later admission if it waits
on work assigned to a pending item; v1 documents that authored dependency caveat rather than
claiming unconditional progress. `DagHostOptions.MaxConcurrentNodes` counts admitted nonterminal
DAG nodes, including children parked in waits, delays, or lease queues, until their child instance
is terminal. It is not merely a count of currently executing CLR work. Per-step throttles,
transient pools, and durable resource leases remain independent limits with different lifetimes.

The implementation-only `MaxActiveFibers` quantity has no normative owner and is removed from
compiler acceptance, `ForEach` admission, runtime terminal failure, and fingerprinting together
with its diagnostic codes. No replacement branch-width limit is added without evidence for a
concrete v1 safety requirement. Allocation exhaustion is an infrastructure fault and never a
different authored workflow outcome.

A timed-out token-ignoring attempt loses its logical path token and commit authority, allowing retry or sibling progression, but continues holding its physical step-throttle/transient slot until it actually returns. Cooperative execution therefore does not imply that external work or late bodies are physically single-threaded.

### 16. Retarget guards before implementation

Phase 0 guards currently encode rejected names and semantics, including `AcquireLease`, author duration/expiry reclaim, durable nested acquisition absence, raw strings, `WaitLong`, and external-job facade assumptions. They cannot be treated as completed evidence.

Tasks 3.1 through 3.12, including separate required slices 3.11a through 3.11d, are one 15-task expected-red Phase 0 packet. They use the exact companion declarations, semantic matrix, provider/facade contracts, project graph, and deferred/removed absence list. Source implementation does not proceed to section 4 until every section-3 task is complete and the entire packet receives independent approval. Public-signature absence scans use qualified names where structural nodes and result variants could otherwise collide. The deterministic lane is limited to friend-only test seams and fixed-codec immutable facts; no test hook becomes a public authoring/runtime API. The diagnostic catalog fixes every `WF-*`, `SFE-*`, `DAG-*`, and public exception code used by guards plus one canonical `AuthoredLocation` grammar, and guards reject undocumented or duplicate codes.

### 17. Approve deltas, then synchronize canonical requirements before each source section

New normative decisions are authored first in the active change's proposal, design, amendment, and
delta specs. Independent approval reviews that proposal package against the unchanged accepted
canonical baseline. Only an approval without a gate-blocking finding authorizes a dedicated
canonical-synchronization task; the synchronized canonical diff is then reviewed before the
dependent section's first product-source edit.

| Source section | Canonical areas synchronized after approval |
|---|---|
| 4 | Workflow authoring, workflow contracts, quality verification |
| 5 | Structured authoring, composition, durable runtime |
| 6 | Deadlines, durable runtime, management, leasing, quality |
| 7 | Repository tiers, developer surface, management, providers |
| 8 | DAG, durable runtime, repository boundaries, developer surface, quality |

The map is the mandatory synchronization scope for tasks 4.0 through 8.0. Historical section gates
that already completed remain historical facts. For the 2026-07-28 amendment, task `4.15` approves
Revision 8's proposal package, task `10.14` synchronizes the approved text, and only then may `4.16`
or the dependent remediation source slice begin. Later documentation work verifies traceability,
examples, and evidence; it does not substitute for approval or pre-apply unapproved canonical text.

### 18. Freeze one phase- and scope-bound authoring session

The existing fluent signatures remain mutable façade APIs, but every handle belongs to one
authoring session, epoch, and lexical scope. The session moves through `Open`, `JoinPending`, and
`Frozen`. Starting root fan-out makes the current root handle superseded; selecting its single join
returns a new façade for the successor epoch. A nested, branch, item, or leased handle expires when
its authoring callback returns.

Root `End` or terminal `ContinueAsNew` atomically freezes the authored graph. A completion builder
captures that frozen snapshot rather than a callback over live mutable state. Repeated `Build` and
`TryBuild` calls are structurally stable with identical ordered diagnostics and fingerprints.
Superseded-handle use, duplicate join selection, post-terminal mutation, escaped callback-handle
use, and losing concurrent authoring operations raise catalogued lifecycle diagnostics and leave
the authored graph unchanged. This obtains safe lifecycle behavior without redesigning every
`Action<TBuilder>` body into a persistent functional API.

### 19. Carry authored and occurrence failure provenance separately

`WorkflowFailure` carries a non-null derived `AuthoredLocation` naming the authored instruction and
a non-null runtime-created `FailureOccurrence` with exactly `Root`, `Branch(AuthoredBranchId)`, and
`Item(index)` variants. The base is externally non-derivable and variant constructors are internal.
The runtime attaches both coordinates when the failure is created. A synthesized
`SFE-JOIN-FAILED` uses the owning scope fiber's occurrence while each ordered cause retains its own.

`FailureOccurrence` variants use value equality. `WorkflowFailure` retains its existing reference
equality; detachment copies both coordinates and causes. `orcacore-json-v1` uses the closed
versioned occurrence discriminator allowlist `root`, `branch`, and `item`. A generalized ancestry
path is deferred with nested fan-out.

### 20. Treat root fan-out as bulk-synchronous composition

Every root `Parallel` or `ForEach` join returns the root builder and replaces parent state, so
fan-out stages compose sequentially without bound. Common grouped heterogeneous work may be
flattened into tagged items, and data-dependent phases may be staged through a barrier. These are
conditional observational simulations, not equivalences: they may lose per-group concurrency,
sequential dependency, grouped failure attribution, or pipelining and may exceed authored item or
fixed encoded-value budgets.

Nested fixed `Parallel` is declined for v1 because it removes barriers for cases that satisfy those
preconditions but does not solve unbounded data-dependent repetition, which needs nested
`ForEach` or a separately specified continuation mechanism. Re-entry requires concrete latency or
throughput evidence plus recursive identity, token/admission, merge, failure-provenance, and lease
semantics.

### 21. Treat the complete packaged API and active-test attribution as reviewed artifacts

The package graph is not a public API baseline. Each of the exact 11 packaged assemblies has one
checked-in, deterministic baseline covering every externally visible type and declared member,
including constructors, properties, methods, fields, events, generic arity and constraints,
parameter modifiers/defaults, and return signatures. Verification compares both the current
build and freshly packed assemblies, refuses missing inventory, and reports exact additions and
removals. Updating a baseline is an explicit reviewed change; the verifier never self-accepts the
current product surface.

Internal placeholders for removed concepts require qualified metadata and source absence guards
because an exported-API baseline cannot observe them. In particular, authored `Yield` and the
four transitional engine result bridges are removed; runtime fairness remains a scheduler-owned
quantum decision with no author-returned result, alias, reflection adapter, or tombstone.

Active acceptance accounting distinguishes current-v1 public-facade evidence from preserved but
excluded legacy, deferred, provider-internal, or future-capability regressions. Historical test
sources stay recoverable until a method-level crosswalk proves each retired-replacement
declaration has executable successor evidence. Saga remains future/non-v1 rather than Section 8;
Section 8 owns only DAG, companion hosting, and its single versioned internal child bridge.

### 22. Use exact internal friends for implementation package boundaries

The greenfield package split SHALL NOT force compiler, execution-kernel, concrete engine, provider,
or hosted-loop implementation types into exported metadata. Type-safe internal access is preferred
to reflection bridges. Product friends are exact: `OrcaCore.Core` grants
`OrcaCore.Engine.Ephemeral` and `OrcaCore.Engine.Durable`; `OrcaCore.Engine.Durable` grants
`OrcaCore.Durable.Hosting`; and `OrcaCore.Durable.Hosting` grants `OrcaCore.Dag.Hosting`. These edges
grant CLR access only and do not add a package dependency in the reverse direction.

Owning white-box test assemblies may be exact friends of their implementation assembly:
`OrcaCore.Core.Tests`, `OrcaCore.Engine.Ephemeral.Tests`, `OrcaCore.Engine.Durable.Tests`,
`OrcaCore.Hosting.Tests`, and `OrcaCore.Providers.PostgreSql.Tests`. The existing
`OrcaCore.Engine.Durable` grant to `OrcaCore.ProviderCertification` remains the one cross-package
test friend for deterministic post-commit barriers. Acceptance, behavior-scenario, compile-fixture,
and integration assemblies exercise public or advanced provider contracts and receive no friend
access. Every other friend remains forbidden and the exact compiled metadata is guarded.

`OrcaCore.Core` therefore exports no implementation types; application authoring contracts remain
owned by `OrcaCore`. Engine packages export only their documented role options and registration
extensions, hosting exports only its documented options/management contracts/extension, and
provider packages export only documented provider-authoring ports, role options, and registration
extensions. A test convenience or cross-assembly call is not evidence for a public API.

### 23. Use explicit versioned event contracts rather than attribute discovery

`WorkflowEventContract` and `WorkflowEventContract<TPayload>` are immutable application values with a stable `EventName` and positive `EventContractVersion`. They are the common identity used by structural/dynamic waits, inbound delivery, resumed-event materialization, durable publish, outbox reconstruction, and external dispatch. CLR type name, assembly-qualified name, serializer metadata, or destination name never becomes wire identity. The generic descriptor fixes the expected payload type and the fixed codec detaches every accepted or published value.

Temporal-style `[Workflow]`, `[Signal]`, and event-discovery attributes are not part of v1. Orca workflows are explicit immutable builder results rather than annotated method containers, and event descriptors must also work for third-party or unmodifiable broker message types. Application modules may expose named static descriptor values and ordinary registration extensions, but the runtime performs no assembly scanning, reflection discovery, or hidden attribute registration. A future source-generated attribute may emit the same descriptor only after a separate package/AOT/analyzer amendment; it cannot become an alternative identity or routing mechanism.

Alternative considered: use attributes as the primary workflow and event declaration model. Rejected because it creates a second source of truth beside the staged builder, hides application composition, makes third-party message contracts awkward, and adds reflection/source-generator/package obligations without improving durable semantics.

### 24. Treat inbound events as durably accepted self-routing work

`IWorkflowEventIngress` exposes payloadless and typed `AcceptAsync` overloads over one immutable `WorkflowInboundEvent` shape. The envelope contains the explicit event contract, globally unique `EventId`, `CorrelationId`, optional causation event identity, UTC occurrence time, fixed-codec payload, and exactly one closed route: direct `InstanceId`; correlation within a `DefinitionId`; committed-snapshot fanout within a `DefinitionId`; or exact `DefinitionId`/`DefinitionVersion` start-or-deliver with `StartIdempotencyKey` and distinct fixed-codec workflow input. Every value needed for routing therefore comes from the upstream message; a MassTransit, Rebus, SNS/SQS, RabbitMQ, or other handler forwards one envelope and never resolves a workflow definition, handle, wait, or in-memory engine object.

`AcceptAsync` returns the closed `WorkflowEventAcceptanceResult` union: `Accepted`, `Duplicate`, or `Rejected(WorkflowEventAcceptanceRejection)`. The rejection union is exactly `EventConflict`, `DirectInstanceNotFound`, `DirectInstanceTerminal`, `StartConflict(StartIdempotencyConflict)`, or `FanoutLimitExceeded`. `Accepted` means the complete normalized envelope and route intent are committed before return, so the broker may acknowledge. The same `EventId` and identical normalized bytes returns `Duplicate` and is also safe to acknowledge; this identity comparison precedes current target-state checks so redelivery remains `Duplicate` after the target progresses or terminalizes. The same identity with different normalized bytes returns `Rejected(EventConflict)` and never overwrites the accepted envelope. A new event aimed directly at an absent or terminal instance, a conflicting start binding, or a fanout snapshot that cannot fit the provider's atomic acceptance limit returns the matching rejection before any ownership or partial target commit. Only `Accepted` and `Duplicate` assert durable ownership; infrastructure, serialization, or cancellation failures propagate without an acknowledgement-safe result. Semantic failures discoverable only by a definition-owning worker after acceptance become durable observable poison records. `NoActiveWait` is not a durable ingress outcome.

A direct event is stored in the target instance inbox even when no wait is active. A correlation event without a current unique wait is stored in a route-level inbox keyed by definition, contract, and correlation. Wait registration and pending-event acceptance/claim form one atomic race: either the oldest eligible event by durable acceptance order is claimed and the owner becomes runnable, or the event remains pending and the wait parks. One wait consumes one event; later loop occurrences consume later events. Timeout, cancellation, host loss, or a failed consumption commit cannot silently delete an unmatched accepted event, and v1 applies no automatic pending-event TTL. Terminal/unresolvable records remain observable poison/dead-letter state for explicit operational handling.

An accepted event matching a persisted wait commits a continuation even when the instance is not in memory. A definition-owning pump loads the instance's exact bound definition/version/fingerprint, rehydrates its checkpoint, applies the inbox event once, and continues execution. Callback-only ingress commits the same event, any required pending start intent, and continuation handoff without loading a definition. Ephemeral hosting does not register this durable ingress and makes no no-loss acknowledgement promise.

Correlation retains registration-time uniqueness for `(DefinitionId, event contract, CorrelationId)`; a second simultaneously active candidate is rejected before parking. Fanout is a distinct explicit route: first acceptance derives membership from provider-visible committed instance state for the `DefinitionId` across all versions, independent of the accepting host's catalog or hot instances; it commits that complete current nonterminal target set, creates independently deduplicated buffered target deliveries, treats an empty snapshot as accepted, and excludes instances created later. Redelivery reuses the committed set.

Start-or-deliver acceptance atomically creates or reuses one durable pending start intent keyed by `StartIdempotencyKey`, bound to exact definition identity/version and normalized workflow-input bytes, and retains the event with that intent. A known incompatible binding returns `Rejected(StartConflict)` before acceptance. A definition-owning host may materialize a compatible intent in the acceptance transaction; callback-only ingress leaves it pending for a definition-owning pump. Materialization resolves the exact registered definition, binds its structural fingerprint, and atomically creates or reattaches the compatible instance, target inbox delivery, and continuation. A definition/version missing from the owning catalog or another semantic incompatibility discovered only after acceptance records observable poison against the owned intent/event; it does not retroactively require broker redelivery. Workflow input is never inferred from event payload.

### 25. Commit outbound workflow events and delegate only transport dispatch

Durable sequential builders expose `Publish` over an explicit payloadless or typed event contract plus side-effect-free correlation/payload selectors. Ephemeral builders expose no `Publish`. The authored contract and node position contribute to the structural fingerprint; selector behavior remains opaque and requires a definition-version bump when changed. Authors do not construct provider records, dispatch attempts, timestamps, or event identity.

When a publish node wins commit authority, the runtime creates one replay-stable outbound `EventId`, inherits the current inbound `EventId` as optional causation, and records the correlation, origin `InstanceId`, `DefinitionId`, `DefinitionVersion`, deterministic occurrence time, contract descriptor, and fixed-codec payload. That `WorkflowOutboundEvent` and workflow progression commit atomically. A crash before commit publishes nothing; a crash after broker send but before outbox acknowledgement may resend the same event identity. The promise is durable at-least-once dispatch, never exactly once.

`OrcaCore.Durable.Hosting.IWorkflowEventDispatcher` exposes exactly `ValueTask<WorkflowEventDispatchResult> DispatchAsync(WorkflowOutboundEvent outboundEvent, CancellationToken cancellationToken = default)`. `WorkflowEventDispatchResult` is the closed `Succeeded`/`RetryableFailure(WorkflowEventDispatchFailure)`/`PermanentFailure(WorkflowEventDispatchFailure)` union; `WorkflowEventDispatchFailure` is an immutable application value with a nonblank stable `Code` and optional diagnostic `Detail`. Success marks the external outbox record dispatched; retryable failure or an exception retains it for retry; cancellation releases its claim; permanent failure records observable poison state carrying the supplied failure. Internal continuation records are claimed by their own pump and never reach this dispatcher. The application maps stable event contract identity to its MassTransit, Rebus, SNS/SQS, RabbitMQ, or other destination. OrcaCore provides the transactional outbox, claim/retry/poison lifecycle, and envelope reconstruction but ships no broker SDK adapter.

A durable definition containing `Publish` is host-incompatible when no application dispatcher is registered, preventing a definition-owning host from silently accumulating undispatchable external records. A future separately deployed dispatcher-only role would require its own explicit hosting contract; callback-only event ingress is not that role.

### 26. Treat code and test removal as a first-class reviewed change

Removing an obsolete application API does not authorize removing the runtime, provider, host,
operator, persistence, observability, or retention behavior that previously supported it. Every
physically deleted, project-orphaned, or compile-excluded production family receives exactly one
reviewed disposition before Section 7 exit:

- **Remove** cites the exact normative absence requirement and proves source, metadata, and packed
  consumer absence without deleting a separately retained capability.
- **Replace or relocate** names the new owning package/type/port and supplies executable equivalence
  at every applicable public, provider, restart, and operational boundary.
- **Defer** cites the exact future task or registry entry and re-entry criteria, keeps recovery
  source available, and receives no completed or passing evidence credit.
- **Dead or duplicate** proves production unreachability and identifies the surviving behaviorally
  equivalent path.

Compile failure after an API reshape, `<Compile Remove>`, absence from the solution, a smaller green
suite, aggregate declaration counts, or the existence of a purported successor test are not proof
of any disposition. A method-level test crosswalk compares setup, invoked boundary, failure or
crash schedule, persistence/restart point, and assertions; one broad scenario cannot absorb
unrelated declarations merely because it still compiles.

The recovery worktree and checkpoint remain evidence anchors, not sources to restore wholesale.
Forbidden public statistics, archive/purge, catch-all hosting, serializer, child, job, Saga, and
compiler shapes remain absent. Still-required host/operator statistics and pressure projections,
provider retention and cleanup safety, BCL diagnostics, and deferred DAG/companion behavior are
reimplemented or preserved behind their correct owners. Provider projects and migrations receive
explicit ship, defer, replace, or remove dispositions; greenfield schema creation contains the
complete current schema and retains no compatibility DDL or provisional upgrade path.

An exported-API baseline and a deletion ledger are reviewed together. A source slice cannot claim
completion while the baseline is missing, the local package feed is stale, a reflection bridge
substitutes for the approved typed friend boundary, or the ledger contains an unresolved family.

## Risks / Trade-offs

- **[Bounded durable `ForEach` still adds persistence and scheduler work]** -> Commit the finite item set once, reuse existing structured fibers, prohibit nesting, and require a positive item bound.
- **[Root-only fan-out can add barriers or exceed a flat representation budget]** -> Treat v1 as
  bulk-synchronous fork-join. Use tagged-item flattening or sequential staging only when authored
  item limits, fixed encoded-value budgets, dependency order, failure attribution, per-group
  concurrency, and barrier placement remain acceptable. Require measured latency/throughput
  evidence plus recursive identity, admission, merge, provenance, and lease rules before nested
  fan-out is introduced.
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
- **[Buffered unmatched events and their serialization keys can grow without bound]** -> Make pending/poison state observable, require provider operational metrics, forbid silent TTL expiry, and defer deletion to an explicit reviewed retention/dead-letter policy. Route-revision retention must remain monotonic: physical pruning may retain a tombstone/generation but must never recreate a deleted key at revision zero while a delayed snapshot could still validate.
- **[Fanout can create a large write set]** -> Make fanout explicit, snapshot targets once, enforce provider transaction/batch limits, and reject acceptance before partial target ownership when the snapshot cannot be committed atomically.
- **[At-least-once outbox dispatch duplicates broker publication]** -> Reuse the stable outbound `EventId` on every attempt and require adapters/consumers to deduplicate rather than claiming exactly once.
- **[A host starts with an incomplete workflow catalog or missing dispatcher]** -> Preflight the entire staged definition batch before readiness and start no progression loop on any incompatibility or conflict.

## Migration Plan

1. Approve this amended plan and final selected-mode signature matrix.
2. Replace all 15 required Phase 0 guard tasks 3.1 through 3.12, including 3.11a through 3.11d, and obtain independent approval of the whole packet.
3. Consolidate typed mode-first workflow authoring and delete provisional aliases/results/nodes.
4. Implement joins and bounded durable `ForEach` on the existing structured-fiber substrate.
5. Implement persisted workflow/step deadlines, `StepOperationId`, and scoped durable leases with quarantine/stop proof.
6. Establish exact tier/package boundaries, role-specific hosting entry points, application-configuration workflow catalogs, reduced application facades, and serialized resource-governance provider contract.
7. Replace provisional event delivery with versioned event descriptors, durable self-routing ingress, pending inbox matching, cold activation, start-or-deliver, committed-snapshot fanout, durable publish, and the application-shaped outbox dispatcher.
8. Implement `OrcaCore.Dag` and `OrcaCore.Dag.Hosting` with complete resultless/resultful planning, typed operation results, and the isolated friend child bridge; keep scheduler integrations outward-only.
9. Rewrite samples and documentation, run strict OpenSpec validation and all public/package/runtime/provider guards, and prepare independent review.

Rollback is source-level: revert the change and recreate development fixtures/stores. No released package or durable-data compatibility contract exists.

## Open Questions

No release-blocking design question remains. Deferred capabilities require their own future proposals and cannot be introduced through implementation convenience or placeholder APIs.
