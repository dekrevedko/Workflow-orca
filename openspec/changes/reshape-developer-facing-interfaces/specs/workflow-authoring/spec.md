## MODIFIED Requirements

### Requirement: Regular workflows are authored fluently
The system SHALL provide explicit ephemeral and durable staged fluent entry points. Each workflow SHALL author exactly one typed `Init`, a sequence composed from the selected-mode v1 capability allowlist, and exactly one root generation terminal. An ephemeral workflow and an ordinarily completing durable workflow SHALL terminate with exactly one typed or resultless `End`; a perpetual durable rollover definition MAY instead terminate its authored generation with unconditional root `ContinueAsNew`. Builders SHALL expose only capabilities guaranteed by every supported host for their statically selected mode.

#### Scenario: Regular workflow is defined
- **WHEN** a contributor builds an ephemeral or durable workflow definition
- **THEN** they compose typed input, private state, control flow, business steps, and typed completion without constructing node graphs or protocol commands manually

#### Scenario: Perpetual durable rollover is defined
- **WHEN** a durable author selects root `ContinueAsNew` instead of `End`
- **THEN** that member is the sole generation terminal and returns a completion builder from which the resultless definition can be built

### Requirement: Durable workflows have a separate authoring surface
The system SHALL provide distinct ephemeral and durable init, body, completion, definition, nested, branch, item, and leased builder families. The common definition registry SHALL accept all four typed definition families but SHALL return a closed host-incompatibility failure before mutation when a definition mode does not match the registered engine role; it SHALL NOT infer mode from a flag or public compiled plan. Durable descriptor-based `Wait` SHALL be cold-capable without a separate `WaitLong` member, and durable sequential builders SHALL expose transactional `Publish` while every ephemeral builder omits it.

#### Scenario: Durable-only wait is authored
- **WHEN** a durable workflow may park long enough to be evicted and rehydrated
- **THEN** the author uses the same structural `Wait(WorkflowEventContract, ...)` contract and hosting/runtime residency policy remains outside workflow meaning

### Requirement: Built definitions are immutable
Workflow and DAG builders SHALL produce immutable, identity/version/structural-fingerprint-bound definitions whose public metadata cannot be downcast and mutated and whose executable factories, selectors, delegates, and compiled plans are not exposed as mutable application metadata. Every resultless/resultful ephemeral/durable definition SHALL expose its matching state-opaque typed reference and MAY be declared in a domain/application module independently of explicit application-configuration registration. The same logical durable definition SHALL reconstruct with the same explicitly fixed `DefinitionId` across process restarts and deployments; production catalog construction SHALL NOT invoke `DefinitionId.New()` on each startup. Conditional environment-specific graph or opaque behavior changes SHALL use a deliberately distinct definition identity/version rather than vary under the same durable contract. The fingerprint SHALL cover only inspectable authored structure, event contract descriptors, and fixed codec format; every opaque delegate, step-construction/configuration, mapping, event payload/correlation selector, or external-request behavior change SHALL require a new `DefinitionVersion` and SHALL NOT accept an author fingerprint contributor.

#### Scenario: Definition is registered
- **WHEN** a completed definition is built independently and later staged on its mode-specific engine builder
- **THEN** later caller mutation cannot change its graph, step registrations, selectors, policies, mapping logic, typed contract, or fingerprint

#### Scenario: Definition is grouped in application configuration
- **WHEN** an application-owned `AddOrderingWorkflows` extension stages several static built definitions
- **THEN** registration remains explicit and typed without requiring a workflow registrar class, environment-dependent graph construction, an attribute, or assembly scanning

#### Scenario: Durable application restarts
- **WHEN** the application reconstructs its configured durable workflow catalog after process replacement or deployment
- **THEN** each unchanged logical definition presents the same explicit identity/version/reference and can rehydrate existing instances rather than minting a new `DefinitionId`

#### Scenario: Same version has different behavior
- **WHEN** registration or resume observes a different compiled fingerprint for the same definition identity and version
- **THEN** it returns a typed conflict before executing changed semantics

### Requirement: Parallel authoring defines deterministic join structure
Parallel authoring SHALL be exposed only on the selected ephemeral and durable root workflow builders and SHALL declare at least one fixed isolated branch, one common serializable result type, and exactly one explicit `WhenAll` or `WhenAllOutcomes` merge. Nested, branch, item, and leased builders SHALL NOT expose `Parallel`; a hand-built graph that places `Parallel` below another structural body SHALL be rejected. Every join SHALL return the selected root builder and replace parent state, allowing root fan-out phases to compose sequentially while preserving a barrier between phases. An empty root scope SHALL remain constructible long enough for aggregate graph validation and SHALL report `SFE-AUTH-BRANCH-004` (`EmptyParallelScope`) through identical `Build`/`TryBuild` diagnostics at the root `Parallel` node. `WhenFirst` SHALL be absent in v1.

#### Scenario: Root Parallel workflow is composed
- **WHEN** a contributor adds fixed branches from a root workflow builder and chooses `WhenAll`
- **THEN** the definition captures branch inputs, one return per branch, stable authored ordering, and a deterministic merge that runs once only after every branch succeeds

#### Scenario: Parallel outcomes are inspected
- **WHEN** a contributor chooses `WhenAllOutcomes`
- **THEN** the merge receives every ordered success or failure outcome, replaces parent state once, and a following `If` can decide business acceptance

#### Scenario: Root fan-out phases are sequenced
- **WHEN** an author completes one root `Parallel` or `ForEach` join and starts another root fan-out scope
- **THEN** the first merge's replacement parent state is the second phase's input and no child from the second phase starts before the first barrier completes

#### Scenario: Ancestor terminal transition wins
- **WHEN** instance cancellation, operator termination, or `CompleteWithin` wins while branches are active
- **THEN** active work is signalled/fenced, both author merges are suppressed, and no cancellation outcome is fabricated

#### Scenario: Nested Parallel is requested
- **WHEN** a contributor looks for `Parallel` on a conditional, loop, branch, item, or leased builder
- **THEN** the member is absent and compiler defense rejects a hand-built nested graph with `SFE-AUTH-CAP-001`

#### Scenario: Root Parallel has no branches
- **WHEN** an author completes a root `Parallel` scope without declaring a branch
- **THEN** `Build` and `TryBuild` report `SFE-AUTH-BRANCH-004` at the `Parallel` node rather than throwing an argument exception from the fluent call

#### Scenario: Race join is requested
- **WHEN** a contributor looks for `WhenFirst`
- **THEN** no public member or residual-work policy is available until winner, loser cancellation, protected-work, and lease semantics are amended together

### Requirement: Mode-first builders share one compiled plan contract
Ephemeral and durable authoring SHALL share one internal authored graph and compiler while returning distinct staged typed definitions. `Build()` SHALL throw one `WorkflowDefinitionException` containing the same aggregate graph diagnostics returned by `TryBuild()`. Executable compiled plans SHALL remain implementation-only.

#### Scenario: Durable definition is built
- **WHEN** an author completes a durable workflow with typed `End` and calls `Build()`
- **THEN** the returned definition contains immutable application metadata and an implementation-only fingerprinted plan consumable by durable registration

#### Scenario: Definition has multiple graph errors
- **WHEN** an author calls `TryBuild()` on an invalid mode-specific completion builder
- **THEN** the result contains all discoverable graph diagnostics without publishing a definition

### Requirement: Durable ForEach has two-layer rejection
Durable public authoring SHALL expose the approved bounded root-only `ForEach(...).WhenAll*` contract. The shared compiler SHALL accept only a finite selector committed once, positive `ForEachOptions.MaxItems`, optional positive `MaxConcurrency`, stable item-index identity/order, supported item bodies, and one all-items join. It SHALL reject hand-built durable graphs that bypass those bounds or contain nested `ForEach`; nested `ForEach` remains deferred in both modes.

#### Scenario: Durable author uses normal discovery
- **WHEN** a developer inspects the durable root builder
- **THEN** bounded `ForEach` is available with `WhenAll` and `WhenAllOutcomes`, while nested item builders do not expose another `ForEach`

#### Scenario: Durable graph bypasses the builder
- **WHEN** an internal or stale graph contains unbounded, re-enumerated, or nested durable `ForEach`
- **THEN** compilation rejects it before registration with the selected-mode or limit diagnostic

### Requirement: ContinueAsNew is authored structurally
Continue-as-new SHALL be an unconditional durable root generation terminal with the exact result shape `DurableWorkflowCompletionBuilder<TInput>`. It SHALL be available only after every lexical resource scope has exited and when the root is the sole nonterminal fiber with no descendant scope or owned obligation. No `End`, business step, decorator, or structural node may follow it, and it SHALL NOT be returnable from portable `StepResult` or available in nested, branch, item, or leased builders. A valid rollover SHALL atomically increment generation, install replacement state, mint distinct occurrence identities, and inherit the original absolute `CompleteWithin` deadline without resetting it. Conditional or finite rollover SHALL be deferred and absent from v1.

#### Scenario: Portable step attempts rollover
- **WHEN** a portable step implementation is authored
- **THEN** its result contract contains no continue-as-new variant

#### Scenario: Author continues after rollover
- **WHEN** an author calls root `ContinueAsNew`
- **THEN** the returned `DurableWorkflowCompletionBuilder<TInput>` exposes only `Build` and `TryBuild`, so no `End`, step, decorator, or structural node can follow

#### Scenario: Conditional finite rollover is requested
- **WHEN** an author needs to roll over only when a runtime condition is true and otherwise complete
- **THEN** no conditional `ContinueAsNew` member or nested placement is available until a separate terminal-path contract is approved

#### Scenario: Lease remains active at rollover
- **WHEN** a hand-built graph reaches continue-as-new before a lexical lease scope exits
- **THEN** compilation reports `SFE-AUTH-LEASE-003` with the rollover and acquisition locations, and runtime defense uses `SFE-RUN-001` without rolling the generation

### Requirement: Successful workflow flow has one root entry and exit
An authored workflow SHALL contain exactly one staged root `Init` and exactly one root generation terminal. Every reachable ordinarily successful root path SHALL converge on exactly one `End`. A perpetual durable rollover definition SHALL instead converge on exactly one unconditional root `ContinueAsNew` and no `End`, which ends that authored generation without completing the workflow. Resultful `End<TOutput>` SHALL select typed output; resultless `End` and `ContinueAsNew` SHALL produce a one-arity definition/reference. Each mode SHALL expose exactly `End()`, `End(WorkflowOutcomeName)`, `End<TOutput>(Func<ReadOnlyStateSnapshot<TState>,TOutput>)`, and `End<TOutput>(Func<ReadOnlyStateSnapshot<TState>,TOutput>, WorkflowOutcomeName)`, with no optional/nullable selector or outcome parameters. Explicit null arguments SHALL throw `ArgumentNullException` immediately. `ContinueAsNew` SHALL carry no outcome and SHALL NOT satisfy an `End` requirement. Dynamic business classification SHALL be encoded in `TOutput`, not selected as a runtime outcome name.

#### Scenario: Workflow has multiple business outcomes
- **WHEN** alternative paths produce accepted, rejected, or manual-review domain classifications
- **THEN** they set typed state/output data, converge on one root `End`, and any fixed outcome metadata remains the same declared value

#### Scenario: Root terminal is missing or misplaced
- **WHEN** a definition has no root generation terminal, more than one root terminal, a nested `Init`, or executable nodes after root `End` or `ContinueAsNew`
- **THEN** validation reports every detected structural error and does not produce a definition

### Requirement: Every branch has one branch return
Each root fixed-parallel branch SHALL have one entry and one reachable `Return` of the scope's declared result type. Branches SHALL retain isolated private state and SHALL NOT contain workflow `Init`, workflow `End`, `ContinueAsNew`, `Parallel`, `While`, or `ForEach`. They MAY contain nested `If` and scoped durable acquisition when their selected-mode builder permits it.

#### Scenario: Branch finishes successfully
- **WHEN** branch execution reaches its return
- **THEN** it produces the declared serializable result tagged with `AuthoredBranchId` and becomes terminal without mutating parent state

#### Scenario: Branch contains workflow-global control or fan-out
- **WHEN** a branch contains `Init`, `End`, `ContinueAsNew`, `Parallel`, `While`, or `ForEach`
- **THEN** static builder absence or definition validation rejects it before registration

### Requirement: Validation covers complete structured reachability
Definition validation SHALL verify staged root structure, one reachable root `End` or perpetual root `ContinueAsNew` generation terminal, supported root-only fan-out and nesting, at least one root `Parallel` branch, branch and item identity, result/output/merge compatibility, `orcacore-json-v1` round-trip support, deterministic finite root-`ForEach` snapshots, configured item/depth/payload limits, workflow/wait/step/retry arguments, lease ancestry/quiescence, and absence of orphan or cross-scope references. Eager local decorator/deadline errors SHALL throw `WorkflowDefinitionException` with the one catalogued diagnostic at the fluent call; graph-wide errors, including an empty `Parallel`, SHALL accumulate through `TryBuild()`.

#### Scenario: Definition contains multiple invalid scope shapes
- **WHEN** a definition contains several structural, typing, ownership, timeout, mapping, or limit violations
- **THEN** `TryBuild()` accumulates actionable diagnostics ordered by authored location and code

### Requirement: Fluent blocks do not require authored closing nodes
The fluent authoring interface SHALL express nested `If`, root `While`, root fixed `Parallel`, bounded root-only `ForEach`, and scoped durable leasing through typed builders and SHALL NOT require manually balanced closing nodes. Root `End` or terminal `ContinueAsNew`, branch/item `Return`, and the selected join remain explicit because they have output, rollover, result, and state-replacement semantics.

#### Scenario: Author composes an If followed by another step
- **WHEN** an author completes the nested then and else builders
- **THEN** the next fluent operation continues after the conditional without an authored closing token

#### Scenario: Nested block is malformed
- **WHEN** nested builder content has unreachable flow, an invalid terminal, or an unsupported nested capability
- **THEN** static absence or structural validation reports the definition error without matching opening and closing tokens

### Requirement: ForEach authoring uses the structured scope contract
Root-only `ForEach` authoring in both modes SHALL declare a selector returning a finite item list, `ForEachOptions` with positive `MaxItems` and optional positive `MaxConcurrency`, an item-state projector receiving only `ForEachItemInput<TItem>` (the detached item plus its zero-based index), a body with one typed `Return`, and one `WhenAll` or `WhenAllOutcomes` merge. The projector SHALL NOT receive or capture a parent-state snapshot as part of the supported contract; shared parent/run data needed by an item SHALL be carried explicitly in `TItem` by the finite-list selector. Results SHALL be ordered by stable item index. Durable mode SHALL normalize and commit the item snapshot before admission and reuse it on replay. Nested `ForEach` SHALL remain deferred and absent from every nested, branch, item, and leased builder.

#### Scenario: Resultful ForEach is authored
- **WHEN** an author declares a common item result and `WhenAll`
- **THEN** the merge receives successful `ForEachItemResult<TResult>` values in stable item-index order only after every item succeeds

#### Scenario: Outcome-aware ForEach is authored
- **WHEN** an author selects `WhenAllOutcomes`
- **THEN** the merge receives each ordered success or failure outcome exactly once after all items become terminal

#### Scenario: Item bound is exceeded
- **WHEN** the selected finite snapshot exceeds its authored `MaxItems` or an applicable fixed-codec payload/envelope bound
- **THEN** the scope fails deterministically before admitting any item

#### Scenario: Item snapshot is empty
- **WHEN** the detached selected list has zero items
- **THEN** no item is admitted and the selected merge runs once with an empty ordered list

#### Scenario: Item needs shared parent data
- **WHEN** an item-state projector needs a value from parent or run state
- **THEN** the selector includes that detached value in `TItem`, and the projector reads it from `ForEachItemInput<TItem>` rather than receiving hidden parent state

#### Scenario: Nested ForEach is requested
- **WHEN** an item or other nested builder attempts to author another `ForEach`
- **THEN** the member is absent and compiler defense rejects a manually constructed nested graph

## ADDED Requirements

### Requirement: Authoring handles are phase-bound and definitions are frozen
Workflow authoring SHALL be governed by one session whose state is `Open`, `JoinPending`, or
`Frozen`. Every builder handle SHALL be valid only for the session epoch and lexical scope in which
it was produced. Starting root fan-out SHALL supersede the current root handle; selecting the
single join SHALL return a distinct façade bound to the successor epoch. A nested, branch, item,
leased, or callback-local scope handle SHALL expire when its authoring callback returns. Applying an
operator through a superseded or expired handle, selecting more than one join for one scope,
applying any operator
after a root terminal, or losing a concurrent authoring race SHALL throw one catalogued lifecycle
`WorkflowDefinitionException` and SHALL leave the authored graph unchanged.

A root `End` or terminal `ContinueAsNew` SHALL atomically move the session to `Frozen` and capture
one immutable authored-graph snapshot. The returned completion builder SHALL build only that
snapshot. Repeated `Build()` or `TryBuild()` on one completion builder SHALL produce structurally
equivalent definitions with identical ordered diagnostics and fingerprints. Workflow-wide
authoring configuration SHALL belong to the session rather than to an individual façade.

#### Scenario: Stale root handle is reused after a join
- **WHEN** a retained root handle is used after its fan-out join returned a successor root façade
- **THEN** the stale operation throws its lifecycle diagnostic and the graph visible to the successor façade is unchanged

#### Scenario: Callback-local handle escapes
- **WHEN** a nested, branch, item, leased, or callback-local scope handle is invoked after its authoring callback returned
- **THEN** the operation is rejected at that handle's authored location and cannot mutate the completed lexical body

#### Scenario: Terminal completion builder is reused
- **WHEN** one completion builder is built or validated repeatedly after unrelated stale aliases are invoked
- **THEN** every accepted build observes only the frozen terminal snapshot and produces the same structure, ordered diagnostics, and fingerprint

#### Scenario: Concurrent authoring operations race
- **WHEN** two operations target the same open session concurrently
- **THEN** at most one mutation wins atomically and every losing lifecycle operation leaves the graph unchanged

### Requirement: Selected mode is preserved through nested authoring
Every conditional, loop, root-parallel branch, root-`ForEach` item, and leased-scope builder SHALL retain selected mode and enclosing capability restrictions in its static type while sharing internal machinery. Nested, branch, item, and leased builders SHALL expose sequencing, nested `If`, waits, delays, and decorators where otherwise legal, but SHALL expose no `Parallel`, `ForEach`, or `While`. Durable non-leased builders MAY expose scoped `AcquireResources` at the placements approved by the matrix.

#### Scenario: Durable branch authoring is inspected
- **WHEN** a developer authors a durable branch or item with no active leased ancestor
- **THEN** nested `If` and scoped `AcquireResources` are available while ephemeral lambdas, transient pools, `Parallel`, `ForEach`, `While`, and public child/job members are absent

#### Scenario: Leased item authoring is inspected
- **WHEN** a developer inspects a leased item body
- **THEN** ordinary steps, nested `If`, `Wait`, `Delay`, decorators, and the item `Return` remain available as applicable while `Parallel`, `ForEach`, `While`, nested `AcquireResources`, and `ContinueAsNew` are absent

### Requirement: Event operations use explicit contracts and durable publish
Every existing structural `Wait` location SHALL accept a payloadless or typed `WorkflowEventContract` descriptor plus a side-effect-free correlation selector, with the optional positive finite timeout overload unchanged. Event contract name/version SHALL participate in matching and structural fingerprinting. Every permitted durable sequential root, nested, branch, item, and leased builder SHALL additionally expose payloadless/typed `Publish` using an explicit event contract and side-effect-free correlation/payload selectors; `Publish` SHALL return the same sequential builder family. The runtime SHALL create event/causation/origin/time metadata and the outbox record, so authors SHALL NOT supply `EventId`, provider record kind, broker destination, retry policy, or dispatch timestamp. Every ephemeral builder, completion builder, join object, and DAG authoring surface SHALL omit workflow-authored `Publish`.

#### Scenario: Durable workflow waits for a typed event
- **WHEN** an author passes `WorkflowEventContract<TPayload>` and a correlation selector to `Wait`
- **THEN** the registered wait persists exact contract name/version/correlation and the resumed step can materialize only through the same compatible descriptor

#### Scenario: Durable workflow publishes an event
- **WHEN** a durable sequential builder publishes one explicit contract with payload and correlation selectors
- **THEN** build records a structural publish node while runtime supplies replay-stable identity and atomically commits the outbound event with progression

#### Scenario: Ephemeral workflow looks for publish
- **WHEN** an ephemeral root, nested, branch, item, or completion builder is inspected
- **THEN** no `Publish` member exists because the in-memory engine cannot make the durable no-loss/outbox promise

#### Scenario: Workflow or event attributes are scanned
- **WHEN** a consumer looks for Temporal-style workflow/signal attributes or automatic event-contract discovery
- **THEN** no v1 attribute or scanner exists and explicit built definitions plus explicit event descriptors remain the only declarations

### Requirement: Lambda business steps are ephemeral only
Ephemeral builders SHALL expose exactly `Then(Func<StepContext<TState>, ValueTask>)` and `Then(Func<StepContext<TState>, CancellationToken, ValueTask>)` lambda business-step overloads wherever ephemeral business steps are allowed. They SHALL expose no `Action<StepContext<TState>>` overload; synchronous work SHALL return `ValueTask.CompletedTask`. Lambda bodies MAY mutate or replace state but SHALL NOT return `StepResult`. Durable builders SHALL require named `IStep<TState>` implementations and SHALL NOT expose lambda overloads. Host exact-type `StepThrottles` SHALL target named `Then<TStep>()` step types only; lambda bodies have no synthetic or inferred step type for that policy.

#### Scenario: Ephemeral workflow uses a lambda
- **WHEN** an ephemeral author supplies a one-parameter or cancellation-aware two-parameter lambda returning `ValueTask`
- **THEN** the runtime observes its completion and exception as one business step through the documented ephemeral `StepContext<TState>`

#### Scenario: Ephemeral lambda performs synchronous work
- **WHEN** an ephemeral lambda completes its state work synchronously
- **THEN** it returns `ValueTask.CompletedTask` and cannot bind to a void-return `Action` overload

#### Scenario: Lambda attempts portable orchestration return
- **WHEN** an ephemeral lambda attempts to return `StepResult.Completed`, `Failed`, or `WaitForEvent`
- **THEN** no lambda overload accepts that result type; portable `StepResult` remains the contract of named `IStep<TState>` implementations

#### Scenario: Durable author attempts a lambda
- **WHEN** a durable root or nested builder is inspected
- **THEN** no lambda `Then` overload is available because persisted code identity, closure capture, and versioning are not part of v1

### Requirement: Workflow, wait, and step timeouts have distinct authoring scopes
`CompleteWithin(TimeSpan)` SHALL be root-only, appear at most once, and bound the entire workflow from start across every continue-as-new generation. Its second fluent call SHALL eagerly throw `WorkflowDefinitionException` containing `SFE-AUTH-DEADLINE-001` (`DuplicateWorkflowDeadline`), with the second call as primary location, the first as related, and the first configured deadline preserved. Structural `Wait` SHALL offer its approved optional timeout overload with fixed failure semantics and no timeout-callback builder. `WithRetry`, `WithStepTimeout`, and ephemeral `WithTransientPool` SHALL decorate only the immediately preceding eligible business step; applicable decorators SHALL be order-independent and each MAY appear at most once for that step. Misplacement, repetition, or use after a structural node SHALL be rejected eagerly by the fluent call with `WorkflowDefinitionException` containing the one catalogued diagnostic, not described as a C# compile-time error. `WithStepTimeout` SHALL bound one attempt. Workflow/step/wait durations and `Delay` SHALL be positive and finite; retry delay MAY be zero but SHALL NOT be negative.

#### Scenario: Step is retried after timeout
- **WHEN** a timed business-step attempt reaches its deadline and a retry policy permits another attempt
- **THEN** the retry keeps the same `StepOperationId`, receives a higher attempt number and a new attempt deadline, and remains bounded by the original workflow deadline

#### Scenario: Timeout decorator follows a structural node
- **WHEN** an author places `WithStepTimeout` without an immediately preceding business step
- **THEN** that fluent call eagerly throws `WorkflowDefinitionException` containing the catalogued diagnostic before a definition can be built

#### Scenario: Decorator is repeated for one step
- **WHEN** an author applies the same retry, timeout, or applicable transient-pool decorator twice to one business step
- **THEN** the repeated fluent call eagerly throws `WorkflowDefinitionException` containing the corresponding catalogued diagnostic

#### Scenario: Workflow deadline is repeated
- **WHEN** an author calls `CompleteWithin` twice
- **THEN** the second call throws `SFE-AUTH-DEADLINE-001`, relates the first call, preserves the first deadline, and produces no partially changed definition

#### Scenario: Wait timeout is authored in an item
- **WHEN** an item body uses `Wait(EventName, correlation, timeout)` and the timer wins
- **THEN** the item fails with `WorkflowWaitTimeoutException`, no author timeout callback runs, and an enclosing `WhenAllOutcomes` can observe that failure

#### Scenario: Retry classification is evaluated
- **WHEN** an attempt returns `StepResult.Failed`, throws an exception normalized to failure, or reaches `StepAttemptTimeoutException` while budget remains
- **THEN** it retries from the last committed state with the same `StepOperationId`, a larger `AttemptNumber`, and the fixed delay, while cancellation/deadline/termination/definition/runtime-invariant failures never retry

### Requirement: Durable resource acquisition is lexical, atomic, and ancestry safe
Durable root, conditional, root-parallel-branch, and root-`ForEach` item builders with no active leased ancestor SHALL expose only scoped `AcquireResources(ResourceLeaseRequest, body)` and selector equivalents approved by the matrix. The non-empty duplicate-free request SHALL be granted atomically. Dedicated leased workflow, nested, branch, and item builders SHALL omit every fan-out member, nested acquisition, and `ContinueAsNew`. No ephemeral builder SHALL expose durable leasing.

#### Scenario: Mutually exclusive branches acquire
- **WHEN** separate `If` arms each contain one complete lexical lease scope
- **THEN** build succeeds because only one arm executes and each scope releases before its parent continues

#### Scenario: Descendant attempts acquisition under an ancestor
- **WHEN** a hand-built graph can acquire in a descendant while an ancestor scope is pending or held
- **THEN** build fails with `SFE-AUTH-LEASE-001` at the descendant and the ancestor acquisition as a related location

#### Scenario: Root loop reacquires sequentially
- **WHEN** one lexical acquisition scope is fully contained in a root `While` iteration
- **THEN** build succeeds because exact release occurs before the next iteration and the obsolete point-acquisition loop diagnostic is not retained

#### Scenario: Sibling branches acquire independently
- **WHEN** sibling branches each request resources with no leased ancestor
- **THEN** build succeeds and parking one requesting fiber does not prevent a runnable sibling from advancing

#### Scenario: Leased body attempts fan-out
- **WHEN** a contributor looks for `Parallel` or `ForEach` in a leased workflow, nested, branch, or item body
- **THEN** the member is absent and compiler defense rejects a hand-built fan-out below the lease with `SFE-AUTH-CAP-001`

### Requirement: Concurrency authoring uses distinct lifetimes
Authoring and hosting SHALL distinguish exact-named-step execution throttles, ephemeral named transient pools, the host-owned per-instance path ceiling, optional root-`ForEach` node concurrency, DAG-node admission, and persisted durable resource leases. A `StepExecutionThrottle` SHALL target only the exact named `TStep` authored through `Then<TStep>()`; it SHALL NOT target an ephemeral lambda or match an assignable/base type. A host option MAY tighten admission but SHALL NOT add methods to an already selected builder.

#### Scenario: ForEach concurrency is composed
- **WHEN** `ForEachOptions.MaxConcurrency` is lower or higher than the host per-instance path ceiling
- **THEN** effective item admission uses the lower value without changing durable lease or transient-pool semantics

#### Scenario: Durable host lacks transient-pool semantics
- **WHEN** a durable builder is selected
- **THEN** `WithTransientPool(TransientPoolName)` is absent from root and nested builders rather than ignored at runtime

#### Scenario: Lambda has no exact-type throttle identity
- **WHEN** an ephemeral lambda body is authored
- **THEN** host exact-type `StepThrottles` do not infer a synthetic step type for it; named `Then<TStep>()` remains the exact-type throttle boundary

### Requirement: Application definitions hide executable compiler IR
Application workflow definitions SHALL expose immutable mode, identity, version, fingerprint, typed input/output, and authored metadata without exposing compiled instructions, scopes, policies, executable delegates, or the executable plan in public application signatures.

#### Scenario: Definition surface is inspected
- **WHEN** the public application baseline examines both mode-specific definition families
- **THEN** neither family exposes compiled-plan, compiler identity, fiber, scope, or mutable executable metadata

### Requirement: Concrete workflow builder declarations are normative
Every public workflow factory, init/body/nested/branch/item/leased builder, branch scope, join, completion builder, definition, and durable reference SHALL match `docs/specs/17-public-authoring-contract.cs` exactly in name, receiver, generic arity, parameters, return type, and mode/location availability. Inline metavariables in the selected-mode matrix SHALL be semantic indexes only.

#### Scenario: Guard compiles the companion contract
- **WHEN** the authoring surface baseline is generated or compared
- **THEN** every companion declaration resolves to exactly one public product signature and no additional overload or provisional builder type is accepted
