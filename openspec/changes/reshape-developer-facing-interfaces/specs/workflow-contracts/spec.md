## MODIFIED Requirements

### Requirement: Steps execute against typed business state
The shared application contract layer SHALL expose named asynchronous `IStep<TState>` execution through `StepContext<TState>` and a portable `StepResult` limited to `Completed`, `Failed`, and dynamic `WaitForEvent(WorkflowEventContract, CorrelationId)` using an explicit payloadless or typed descriptor. `StepContext<TState>` SHALL include typed attempt-local `State`, `ReplaceState(TState)`, execution identity, resumed event, deterministic time, optional item context, and optional lease protection context. The resumed event SHALL preserve contract/version, event/correlation/causation identity, occurrence time, and fixed-codec payload and SHALL provide contract-checked typed materialization. `StepContext<TState>` SHALL NOT expose `Yield`, continue-as-new, external-job dispatch, provider outbox writes, or resource-acquisition result variants.

#### Scenario: Business step is authored
- **WHEN** a workflow author implements a named step for either engine
- **THEN** the step receives typed business and execution context and returns only an approved portable orchestration result

#### Scenario: Event name is selected dynamically
- **WHEN** a business step can determine its event contract only while executing
- **THEN** it may return descriptor-based `WaitForEvent`, while a statically known contract uses the preferred structural `Wait`

#### Scenario: Timed attempt ignores cancellation
- **WHEN** a timed-out body continues mutating its detached attempt copy while a retry begins
- **THEN** only the winning attempt retains commit authority, the late copy is discarded, and immutable/value state can be replaced only through `ReplaceState`

### Requirement: Host-facing execution hints remain optional and declarative
The contract layer SHALL keep per-step execution throttles, ephemeral `TransientPoolName`, the host-owned per-instance path ceiling, optional `ForEach` node concurrency, DAG-node admission, and persisted `ResourcePoolName` leases as distinct contracts with distinct lifetimes. A `StepExecutionThrottle` SHALL match the exact named `TStep` type authored through `Then<TStep>()`; it SHALL NOT infer a category, match assignable/base types, or target an ephemeral lambda that has no named step type. Step implementations SHALL NOT acquire host synchronization primitives directly, and later host composition SHALL NOT alter a selected builder's public members.

#### Scenario: Exact-type step throttle is applied
- **WHEN** host options configure a throttle for one named `TStep`
- **THEN** it applies only to business-step occurrences authored as that exact type and not to derived, sibling, or lambda bodies

#### Scenario: Pool key is attached at authoring time
- **WHEN** ephemeral authoring guards work with `WithTransientPool(TransientPoolName)`
- **THEN** the host applies only the documented host-local transient lifetime and no durable recovery claim is implied

#### Scenario: Durable capacity is reserved
- **WHEN** a durable lexical scope requests cross-host logical capacity
- **THEN** it uses `ResourceLeaseRequest` and `ResourcePoolName`, not a step throttle or transient pool

### Requirement: Branch and merge contracts are type checked
Every root `Parallel` scope SHALL declare at least one fixed branch, one typed result shared by all branches, and exactly one parent-state merge. `Parallel` SHALL be absent from nested, branch, item, and leased builders. An empty root scope SHALL fail graph validation with `SFE-AUTH-BRANCH-004` at the `Parallel` node. `WhenAll` SHALL accept ordered successful `BranchResult<TResult>` values and run only when all branches succeed. One failure SHALL fail with that `WorkflowFailure`; multiple failures SHALL use `SFE-JOIN-FAILED` with ordered causes. `WhenAllOutcomes` SHALL accept ordered `BranchOutcome<TResult>` values covering success and failure only and SHALL run once after all branches become terminal unless an ancestor terminal transition suppresses the merge. Contract validation SHALL require fixed-codec round-trip support for persisted values.

#### Scenario: Merge result types do not match
- **WHEN** one branch return or the declared merge uses an incompatible result or parent-state type
- **THEN** definition compilation fails before runtime registration

#### Scenario: Branches finish out of order
- **WHEN** fixed branches become terminal in an order different from authoring order
- **THEN** the merge observes results or outcomes in stable authored-branch order

### Requirement: Dynamic item outcomes are ordered contracts
Both modes SHALL expose root-only bounded `ForEach` results through runtime-owned `ForEachItemResult<TResult>` and `ForEachItemOutcome<TResult>` contracts containing stable item index, typed result or success/failure outcome, and failure metadata where applicable. The item-state projector SHALL receive only `ForEachItemInput<TItem>` containing the detached item and zero-based index; parent/run data needed by item work SHALL be included explicitly in `TItem` before the snapshot is committed. Collections SHALL be ordered by item index, not completion time. Ancestor cancellation/termination/deadline SHALL suppress the merge rather than create an item cancellation variant. Durable mode SHALL use the once-committed item snapshot after replay, and an empty snapshot SHALL invoke the selected merge once with an empty ordered collection. Nested `ForEach` SHALL be absent from all nested, branch, item, and leased contracts.

#### Scenario: Dynamic items complete out of order
- **WHEN** admitted `ForEach` item fibers reach terminal states in an order different from their item indices
- **THEN** the parent observes results or outcomes in stable item-index order

#### Scenario: Durable host restarts during fan-out
- **WHEN** a durable `ForEach` resumes after some items became terminal
- **THEN** the runtime reuses the committed item snapshot and item identities without re-running the selector

#### Scenario: Item work needs parent data
- **WHEN** an item requires a value from the parent state
- **THEN** the root selector carries that value in `TItem`, and the item projector consumes only its `ForEachItemInput<TItem>`

### Requirement: Executable plan identity is explicit
Shared contracts SHALL represent definition identity, positive authored version, workflow mode, compiler format version, execution-envelope version, fixed codec format, and structural fingerprint as distinct values used during registration and resume. The fingerprint SHALL cover inspectable authored node/member kinds, ordering, strong values, referenced step/workflow types, static resource requests, and codec format only. It SHALL NOT include compiler format, workflow mode, definition identity/version, compiler acceptance or fairness limits, delegate IL, DI/step configuration, external adapter behavior, opaque mapping logic, or author-supplied contributors. A durable host SHALL retain every compiler format referenced by a nonterminal instance until the instance terminalizes or is explicitly migrated; before a released compatibility contract exists, a format bump MAY use a hard cutover when no supported persisted instance exists.

#### Scenario: Durable instance resumes
- **WHEN** a durable checkpoint is loaded
- **THEN** the runtime compares every required identity, version, format, and fingerprint before interpreting persisted execution position

#### Scenario: Opaque execution behavior changes
- **WHEN** durable step or external-request construction behavior changes in a way OrcaCore cannot hash automatically
- **THEN** the application supplies a new definition version because the version bump is the sole v1 contract for opaque code changes

#### Scenario: Compiler policy changes without authored structure changing
- **WHEN** scope-depth, internal-instruction, payload, envelope, or another compiler/runtime acceptance policy changes while the authored structure and codec format remain equal
- **THEN** the structural fingerprint remains equal and compatibility is decided through the policy's owning compiler-format, codec, envelope, host, or provider binding

## ADDED Requirements

### Requirement: Workflow references expose typed external contracts
Resultful ephemeral and durable definitions SHALL expose `EphemeralWorkflowRef<TInput,TOutput>` and `DurableWorkflowRef<TInput,TOutput>` respectively; resultless definitions SHALL expose the corresponding one-arity reference. A workflow reference SHALL contain immutable mode/identity/version/fingerprint and typed input/output contract but SHALL NOT expose private workflow state or executable plan data. DAG nodes SHALL continue to accept durable references only.

#### Scenario: DAG references a child workflow
- **WHEN** a DAG node is declared with one durable workflow reference
- **THEN** its input and output are statically known while the child workflow's private state remains inaccessible

#### Scenario: Resultless workflow is referenced
- **WHEN** a workflow has no public output
- **THEN** its one-arity reference avoids a synthetic `Unit` output in consumer signatures

#### Scenario: Ephemeral workflow is referenced
- **WHEN** application configuration stages an ephemeral definition and runtime code resolves its reference
- **THEN** the same state-opaque typed declaration/registration/lookup pattern is available without making it a durable DAG target

### Requirement: Event contracts have stable explicit wire identity
`WorkflowEventContract` and `WorkflowEventContract<TPayload>` SHALL be immutable non-positional application values containing validated `EventName` and positive `EventContractVersion`. The generic form SHALL bind the fixed-codec payload type. Contract identity SHALL be exact ordinal name/version and SHALL NOT derive from CLR type name, assembly name, serializer metadata, broker destination, or runtime attributes. V1 SHALL expose no workflow/signal/event discovery attribute, assembly scanner, or reflection registration path. Third-party and unmodifiable message types SHALL be usable through an explicit descriptor.

#### Scenario: Application declares an event contract
- **WHEN** application code creates a typed descriptor for a broker message type
- **THEN** the same descriptor is used by wait, ingress, resumed payload materialization, durable publish, and external dispatch without modifying or scanning the message type

#### Scenario: CLR payload type is renamed
- **WHEN** application code changes a CLR type name while retaining compatible payload shape and the same explicit event name/version
- **THEN** wire identity remains unchanged, while an incompatible payload/schema change requires a new event contract version

### Requirement: Inbound and outbound envelopes carry complete routing and causal metadata
`WorkflowInboundEvent` and `WorkflowInboundEvent<TPayload>` SHALL contain one event contract, globally unique caller-created `EventId`, `CorrelationId`, optional causation `EventId`, non-default UTC occurrence time, fixed-codec-detached payload, and exactly one closed `WorkflowEventRoute`. Routes SHALL be direct instance, definition/correlation, definition fanout, or exact-definition start-or-deliver carrying definition identity/version, start idempotency key, and fixed-codec typed start input distinct from event payload. `WorkflowOutboundEvent` SHALL contain contract, replay-stable runtime event identity, correlation, optional causation, origin instance/definition/version, deterministic occurrence time, and contract-checked payload materialization. Neither envelope SHALL expose wait/fiber/scope/provider/checkpoint/outbox-record identities.

#### Scenario: Broker handler forwards a self-routing event
- **WHEN** an upstream envelope already carries its route and identifiers
- **THEN** the handler can submit it without looking up workflow definitions, instances, waits, or engine state

#### Scenario: Published event is dispatched
- **WHEN** an application dispatcher receives a workflow outbound event
- **THEN** it can route and deduplicate using stable contract/event/correlation/causation/origin data without receiving a provider record

### Requirement: Event acceptance results are closed and transport-neutral
`IWorkflowEventIngress` SHALL expose payloadless and typed `AcceptAsync` overloads returning `ValueTask<WorkflowEventAcceptanceResult>`. That result SHALL be the closed union of `Accepted`, `Duplicate`, and `Rejected(WorkflowEventAcceptanceRejection)`. The rejection SHALL be the closed union of `EventConflict`, `DirectInstanceNotFound`, `DirectInstanceTerminal`, `StartConflict(StartIdempotencyConflict)`, and `FanoutLimitExceeded`; it SHALL expose no broker-specific acknowledgement, delivery-tag, queue, topic, or destination type. Only `Accepted` and `Duplicate` SHALL assert durable ownership. Infrastructure, serialization, or cancellation failure SHALL remain exceptional.

#### Scenario: Transport adapter maps an acceptance result
- **WHEN** an application adapter receives a `WorkflowEventAcceptanceResult`
- **THEN** it can acknowledge accepted/duplicate delivery and apply its own retry or dead-letter policy without OrcaCore referencing its broker SDK

### Requirement: Outbound dispatch results are closed and application-shaped
`IWorkflowEventDispatcher` SHALL expose exactly `ValueTask<WorkflowEventDispatchResult> DispatchAsync(WorkflowOutboundEvent outboundEvent, CancellationToken cancellationToken = default)`. `WorkflowEventDispatchResult` SHALL be the closed union of `Succeeded`, `RetryableFailure(WorkflowEventDispatchFailure)`, and `PermanentFailure(WorkflowEventDispatchFailure)`. `WorkflowEventDispatchFailure` SHALL be immutable, contain a nonblank stable `Code` plus optional diagnostic `Detail`, and expose no provider claim, outbox record, stream version, checkpoint, or broker SDK type.

#### Scenario: Application dispatcher reports failure
- **WHEN** the application's broker adapter cannot send one outbound event
- **THEN** it returns a typed retryable or permanent failure carrying application diagnostics while OrcaCore retains ownership of retry/poison state

### Requirement: Workflow success projections and output waits are typed
`WorkflowRegistrationResult<TDefinitionHandle>` SHALL be the closed union of `Registered`, `HostIncompatible(DefinitionHostCompatibilityFailure)`, and `Conflict(DefinitionRegistrationConflict)`. `DefinitionHostCompatibilityFailure` SHALL be the closed union of `EngineModeMismatch`, `MissingTransientPools`, `MissingDurableResourcePools`, and `MissingWorkflowEventDispatcher`; each missing-name collection SHALL be defensively copied, distinct, and ordinal-sorted. Validation SHALL perform mode mismatch first, then all statically inspectable host capabilities, then fingerprint conflict, with no registry mutation on failure. Ephemeral registration SHALL inspect all authored transient-pool names; durable registration SHALL inspect static lease requests and require an application dispatcher for authored `Publish`, while selector-created durable names remain runtime validation. `IWorkflowDefinitionRegistry` SHALL expose four typed `GetRequiredHandle(reference)` overloads that resolve an already registered exact mode/identity/version/fingerprint and never register; no match SHALL throw `WorkflowDefinitionNotRegisteredException` with code `WF-DEFINITION-NOT-REGISTERED` carrying the requested reference's `DefinitionId`, `DefinitionVersion`, and `DefinitionFingerprint` as immutable structured properties. `WorkflowStartResult<TInstanceHandle>` SHALL retain its closed accepted/conflict variants. Both results SHALL expose `GetHandleOrThrow()` returning the typed handle without a cast. Host incompatibility SHALL throw `WorkflowDefinitionHostCompatibilityException` with code `WF-DEFINITION-HOST-INCOMPATIBLE` carrying the failure, registration conflict SHALL throw `WorkflowDefinitionRegistrationConflictException`, and start conflict SHALL throw `WorkflowStartIdempotencyConflictException`; each exception SHALL carry the original closed value. `WorkflowInstanceHandle<TOutput>` SHALL expose `ValueTask<TOutput> WaitForOutputAsync(CancellationToken cancellationToken = default)`. A convenience extension with the callable shape `ValueTask<TOutput> WaitForOutputAsync<TOutput>(this WorkflowStartResult<WorkflowInstanceHandle<TOutput>> start, CancellationToken cancellationToken = default)` SHALL first project through `GetHandleOrThrow()`, enabling `await start.WaitForOutputAsync(token)`. Both wait forms SHALL be notification-driven and race-free, SHALL return the fixed-codec-detached output, SHALL never poll, and SHALL treat caller cancellation as cancellation of only the local wait rather than the workflow. If the instance terminalizes without output, they SHALL throw `WorkflowOutputUnavailableException` carrying its terminal status and optional `WorkflowFailure`. Durable waiting SHALL subscribe and recheck around the committed notification boundary so a completion cannot be lost. Resultless handles and resultless start results SHALL expose no output wait.

#### Scenario: Registration succeeds on the common path
- **WHEN** a consumer calls `GetHandleOrThrow()` on a registered workflow result
- **THEN** the exact typed definition handle is returned without a downcast or variant pattern match

#### Scenario: Configuration-registered definition is resolved
- **WHEN** a consumer calls `GetRequiredHandle` with an exact typed reference staged and installed at startup
- **THEN** the corresponding handle is returned without registration or keyed service resolution

#### Scenario: Definition does not fit the selected host
- **WHEN** registration observes an engine-mode mismatch or one or more missing statically inspectable pools
- **THEN** it returns `HostIncompatible` before mutation, and `GetHandleOrThrow()` throws `WorkflowDefinitionHostCompatibilityException` carrying the unchanged closed failure

#### Scenario: Start conflicts on the convenience path
- **WHEN** a consumer calls `await start.WaitForOutputAsync(token)` on a conflicting resultful start
- **THEN** `GetHandleOrThrow()` first throws `WorkflowStartIdempotencyConflictException` carrying the original `StartIdempotencyConflict`, and no output subscription is created

#### Scenario: Output becomes available after subscription
- **WHEN** a resultful workflow commits output while `WaitForOutputAsync` is pending
- **THEN** the notification-driven wait completes once with the detached output without polling or losing the completion race

#### Scenario: Local output wait is cancelled
- **WHEN** the caller cancels the token passed to `WaitForOutputAsync`
- **THEN** only that local wait is cancelled and the workflow receives no cancellation or termination request

#### Scenario: Workflow terminalizes without output
- **WHEN** a resultful instance becomes failed, timed out, cancelled, or terminated before committing output
- **THEN** `WaitForOutputAsync` throws `WorkflowOutputUnavailableException` carrying the terminal status and optional detached failure

### Requirement: Read-only state snapshots are runtime-created values
`ReadOnlyStateSnapshot<TState>` SHALL be a sealed non-positional reference type with an internal runtime constructor and one read-only detached `Value`. It SHALL expose no public constructor, positional equality contract, deconstructor, `init`, or record `with` copy surface. Author selectors, conditions, merges, and output projectors SHALL receive snapshots created by the runtime and SHALL NOT be able to forge one as input to another contract.

#### Scenario: Author inspects a state snapshot
- **WHEN** an approved selector receives `ReadOnlyStateSnapshot<TState>`
- **THEN** it can read the detached `Value` but cannot construct, deconstruct, or record-copy a snapshot

### Requirement: Typed completion is atomic
Each ephemeral and durable root builder SHALL expose exactly four completion overloads: resultless `End()`, resultless `End(WorkflowOutcomeName)`, resultful `End<TOutput>(Func<ReadOnlyStateSnapshot<TState>,TOutput>)`, and resultful `End<TOutput>(Func<ReadOnlyStateSnapshot<TState>,TOutput>, WorkflowOutcomeName)`. No overload SHALL use an optional or nullable selector/outcome parameter. An explicitly null selector or outcome SHALL throw `ArgumentNullException` immediately. Resultful `End<TOutput>` SHALL compute output from a read-only root-state snapshot and atomically commit the serialized output, successful terminal status, and optional fixed outcome. Resultless `End` SHALL atomically commit terminal status and optional fixed outcome. V1 SHALL NOT expose a dynamic outcome-name selector.

#### Scenario: Host fails during completion
- **WHEN** a host fails while applying typed `End`
- **THEN** recovery observes either no completion or one complete terminal output/outcome commit, never a terminal instance missing its output

#### Scenario: Business classification varies per run
- **WHEN** the author needs accepted, rejected, or manual-review detail that varies dynamically
- **THEN** the classification is represented in `TOutput` rather than an arbitrary runtime outcome name

### Requirement: Public failure exceptions carry stable codes
Every public workflow build, runtime, management, and success-projection failure exception SHALL derive from `OrcaCoreException` and expose a nonblank stable machine-readable `Code`. Each built-in failure class SHALL bind exactly one fixed catalogued code. `StepResult.Failed(OrcaCoreException)` and a thrown `OrcaCoreException` SHALL detach to `WorkflowFailure` using that code and a diagnostic message without persisting a live exception, CLR stack, or inner-exception graph. An arbitrary author or integration exception SHALL normalize to the catalogued generic unhandled-step code; its CLR type name and raw text SHALL NOT become protocol identity. Result-to-exception helpers SHALL preserve their original closed conflict or failure value as structured exception data.

#### Scenario: Authored failure becomes a branch outcome
- **WHEN** a named step returns `StepResult.Failed` with an `OrcaCoreException`
- **THEN** the committed `WorkflowFailure` retains the exception's stable code and detached message without retaining the live exception object

#### Scenario: Arbitrary exception is normalized on another host
- **WHEN** equivalent unhandled step failures are observed across retry or host replacement
- **THEN** both use the same catalogued generic failure code rather than a CLR type name, stack trace, or mutable message as identity

#### Scenario: Registration helper throws a conflict exception
- **WHEN** `GetHandleOrThrow()` projects a closed registration conflict
- **THEN** the typed exception exposes its fixed code and carries the unchanged `DefinitionRegistrationConflict` value

#### Scenario: Registration helper throws a compatibility exception
- **WHEN** `GetHandleOrThrow()` projects a closed `HostIncompatible` result
- **THEN** `WorkflowDefinitionHostCompatibilityException` exposes `WF-DEFINITION-HOST-INCOMPATIBLE` and carries the unchanged `DefinitionHostCompatibilityFailure`

#### Scenario: Durable pool lookup is not configured
- **WHEN** a normalized lease request or pool management call names an unconfigured durable pool
- **THEN** `ResourcePoolNotConfiguredException` exposes `WF-RESOURCE-POOL-NOT-CONFIGURED` and carries every copied distinct ordinal-sorted missing name

### Requirement: Workflow failures carry authored and occurrence provenance
Every runtime-created `WorkflowFailure` SHALL carry non-null `AuthoredLocation` and non-null
`FailureOccurrence`. `AuthoredLocation` SHALL use the existing authored-only grammar and identify
the instruction at which the failure is created. `FailureOccurrence` SHALL be an externally
non-derivable runtime-created abstract record with exactly `Root`, `Branch(AuthoredBranchId)`, and
`Item(int index)` variants; its base constructor SHALL be `private protected`, variant constructors
SHALL be internal, branch identity SHALL be non-null, and item index SHALL be nonnegative.

Provenance SHALL attach when the failure is created rather than at join. A single `WhenAll` failure
SHALL propagate unchanged. A synthesized `SFE-JOIN-FAILED` SHALL carry the owning scope fiber's
occurrence while each ordered cause retains its own provenance. Occurrence variants SHALL retain
record value equality; `WorkflowFailure` SHALL retain reference equality. Detachment SHALL copy
location, occurrence, and causes. `orcacore-json-v1` SHALL round-trip the closed versioned
occurrence discriminator allowlist `root`, `branch`, and `item`.

#### Scenario: One ForEach item fails
- **WHEN** root `ForEach.WhenAll` has exactly one failed item
- **THEN** the propagated failure retains the authored instruction location and that item's stable index occurrence without a synthetic join wrapper

#### Scenario: Several branches fail
- **WHEN** root `Parallel.WhenAll` synthesizes `SFE-JOIN-FAILED`
- **THEN** the aggregate records the owning root occurrence and every authored-order cause retains its own branch occurrence

#### Scenario: Consumer matches an occurrence
- **WHEN** application code switches over a `FailureOccurrence`
- **THEN** it can handle the three documented variants and uses a defensive default because C# v1 does not compiler-enforce exhaustiveness for the closed runtime-created hierarchy

### Requirement: Step operation identity is stable and opaque
`StepExecutionContext` SHALL expose `WorkflowInstanceId`, runtime-created `StepOperationId`, and positive `AttemptNumber`. One `StepOperationId` SHALL identify one logical visit to one business step and remain unchanged across policy retry, step-timeout reconciliation, replay, expected-version conflict, process replacement, and competing drivers. A new loop re-entry, item, branch occurrence, or continue-as-new generation SHALL receive a new ID. `AttemptNumber` SHALL be the durable retry-policy ordinal, not the physical CLR invocation count: before first dispatch the runtime SHALL persist the operation ID, ordinal, optional absolute deadline, and in-flight marker; crash/replay redispatch SHALL reuse all of them; only a committed eligible failure/timeout retry transition SHALL increment the ordinal. `maxAttempts = 1` SHALL allow crash replay of ordinal one but no policy retry. `AttemptNumber` SHALL remain public diagnostic metadata only and SHALL NOT be used as an external idempotency key, effect identity, or permission to issue another logical effect. External-effect adapters SHALL use the current occurrence's `StepOperationId`, retain a request fingerprint, and implement create-or-observe behavior. Arbitrary author code and external truth remain an explicit trust boundary; integration/provider certification SHALL exercise duplicate invocation and ambiguous recovery rather than claiming the CLR can prove correct use.

#### Scenario: External create response is lost
- **WHEN** a durable host redispatches after losing an ambiguous bounded create-or-observe response before a retry transition commits
- **THEN** the step receives the same `StepOperationId`, `AttemptNumber`, and absolute attempt deadline so its adapter can observe the prior logical request without consuming retry budget

#### Scenario: Committed failure schedules a policy retry
- **WHEN** an eligible failure or timeout transition commits and retry budget remains
- **THEN** the next attempt retains `StepOperationId` and uses the next `AttemptNumber` ordinal with its newly committed deadline

#### Scenario: Step executes in another loop iteration
- **WHEN** the authored instruction is visited again after its prior logical occurrence completed
- **THEN** the new occurrence receives a distinct `StepOperationId`

#### Scenario: Adapter is invoked on a retry attempt
- **WHEN** certification invokes one effect adapter repeatedly either at the same ordinal after crash redispatch or at a larger ordinal after a committed policy retry
- **THEN** the adapter reuses the current `StepOperationId` and request fingerprint instead of deriving a new external identity from the attempt number

### Requirement: Strong values reject invalid and interchangeable primitives
`DefinitionId` SHALL be an immutable non-defaultable reference value created through `New`, `Parse`, or `TryParse`; `New` SHALL never produce `Guid.Empty`, `Parse` SHALL reject its canonical text with `ArgumentException`, and `TryParse` SHALL return `false` with a null result. The same nonempty-Guid rules SHALL apply to runtime-created `InstanceId`, `WaitId`, and `DagRunId`. `DefinitionVersion` and `EventContractVersion` SHALL be distinct immutable non-defaultable reference values with positive validating constructors and `Initial`. Caller-created string-backed `EventName`, `WorkflowOutcomeName`, `AuthoredBranchId`, `DagNodeId`, `ResourcePoolName`, `TransientPoolName`, `StartIdempotencyKey`, `CorrelationId`, `EventId`, `StopConfirmationId`, `ResourcePoolOperationId`, and `ResourceGovernancePartitionId` SHALL be immutable validating non-positional reference values with a private constructor and one public `Create(string)` factory. That caller-created family SHALL expose no public constructor, `New`, `Parse`/`TryParse`, implicit primitive conversion, or parallel primitive overload. `StepOperationId` and other runtime-created identifiers SHALL retain private construction plus canonical nonempty `Parse`/`TryParse` or converter round-trip without allowing author-selected runtime identity. Every public boundary SHALL reject null again. Serialized definition and envelope limits SHALL bound aggregate payload size rather than inventing per-name length constants.

#### Scenario: Invalid scalar is supplied
- **WHEN** a caller supplies null, an empty identifier, a non-positive version, whitespace-only or leading/trailing whitespace text, or a sibling strong-value role
- **THEN** construction or the nearest public operation fails before persistence or execution

#### Scenario: Caller-created and runtime-created construction paths stay distinct
- **WHEN** a consumer constructs an event, correlation, authored name, pool name, idempotency key, confirmation ID, resource operation ID, or governance partition ID
- **THEN** the matching caller-created value is available only through `Create(string)`, while runtime-created identities expose no `Create` or public constructor and retain only their approved parser/converter round trip

#### Scenario: Empty Guid text is parsed
- **WHEN** a caller passes the canonical `Guid.Empty` text to `DefinitionId`, `InstanceId`, `WaitId`, or `DagRunId`
- **THEN** `Parse` throws `ArgumentException`, `TryParse` returns `false` with a null result, and no empty runtime identity reaches persistence

### Requirement: Durable lease requirements and requests enforce invariants
`ResourceLeaseRequirement` SHALL have a private constructor and public `Require(ResourcePoolName, int units = 1)` factory. `ResourceLeaseRequest` SHALL have a private constructor and `Create(first, params additional)` factory. Both SHALL be immutable; requests SHALL be non-empty, copied, duplicate-free by exact pool name, and contain only non-null requirements with positive units. No duration, holder ID, renewal token, mutable collection, public positional constructor, `init`, or `with` bypass SHALL exist.

#### Scenario: Request invariants are bypassed
- **WHEN** an author attempts a null/default pool, non-positive units, empty request, null element, duplicate pool, post-construction mutation, or record-copy replacement
- **THEN** the factory or fluent call rejects before any provider mutation

### Requirement: Lease protection confirmation is generic and trusted
`ResourceLeaseExecutionContext` SHALL expose an opaque runtime-created `LeaseProtectionToken` to code executing inside a durable lease scope. Advanced `IDurableResourceLeaseRecovery.ConfirmProtectedWorkStoppedAsync` SHALL accept that token plus idempotent `StopConfirmationId`, return typed `ProtectedWorkStopConfirmationStatus`, and SHALL contain no Kubernetes, AWS, or job-system type. This SHALL be an explicitly trusted, authorized, audited, and certification-tested integration boundary: OrcaCore SHALL reject stale or mismatched identities but SHALL NOT pretend it can independently verify arbitrary external stop truth.

#### Scenario: External protected work is confirmed stopped
- **WHEN** a trusted reconciler proves every external work item for the exact protection token is terminal, absent, or end-to-end fenced and submits a new confirmation identity
- **THEN** the runtime may release that exact quarantined obligation once

#### Scenario: Confirmation targets another occurrence
- **WHEN** a stale, mismatched, or duplicate confirmation is submitted
- **THEN** it cannot release capacity owned by a different lease occurrence

### Requirement: Application contracts do not depend on advanced contracts
Application contract assemblies SHALL NOT reference provider-authoring or runtime-protocol assemblies, and no application-facing public signature SHALL contain an advanced provider commit, durable command/event, checkpoint, stream version, fiber, scope, or driver type.

#### Scenario: Public signature guard runs
- **WHEN** application assemblies are inspected
- **THEN** no public return type, parameter, property, base type, or generic constraint leaks an advanced or implementation-only type

### Requirement: Durable values use one fixed detached codec
V1 SHALL use the non-replaceable certified `System.Text.Json` format `orcacore-json-v1` for supported workflow input/state/result/output, inbound event payload and start input, outbound event payload, DAG values, and idempotency bytes. Selector and query snapshots SHALL be codec-detached. Registration/acceptance SHALL reject unsupported cyclic or unapproved polymorphic graphs before commit. The only collection encodings SHALL be an ordered JSON-array sequence declared as a one-dimensional `T[]`, `List<T>`, `IList<T>`, or `IReadOnlyList<T>` and materialized as an array or exact `List<T>`, plus an ordered JSON-object map declared as `Dictionary<string,T>`, `IDictionary<string,T>`, or `IReadOnlyDictionary<string,T>` and materialized as exact `Dictionary<string,T>`. Sequence enumeration order and dictionary insertion/enumeration order SHALL be semantic codec input. Dictionaries remain usable only with string keys; authors SHALL normalize insertion order when map order is not business data. Every other declared or runtime collection shape SHALL reject before commit.

#### Scenario: Persisted collection shape crosses the fixed-codec boundary
- **WHEN** a value graph contains an allowlisted sequence or string-keyed dictionary
- **THEN** its enumeration order round-trips byte-for-byte, while sets, queues, linked lists, multidimensional arrays, sorted/custom dictionaries, non-string map keys, and custom collection subclasses reject before provider mutation

#### Scenario: Attempt starts from committed state
- **WHEN** an attempt begins after a prior failure, timeout, replay, or process replacement
- **THEN** it receives a fresh codec-detached copy of the last committed state rather than any discarded in-flight mutation

### Requirement: Execution-path tokens have one countable model
A runnable root, branch, or item SHALL own one per-instance path token; parking on wait, delay, resource request, or join SHALL release it; and progression SHALL reacquire it. A parent SHALL release its token before fan-out and reacquire only for merge/continuation. Every fixed root-`Parallel` branch fiber SHALL exist when its scope starts and runnable branches SHALL queue for tokens by authored ordinal; no separate branch or live-fiber admission resource SHALL affect workflow semantics. Root-`ForEach` items SHALL queue by index, and `ForEachOptions.MaxConcurrency` SHALL count admitted nonterminal item scopes and only tighten the host path ceiling. A parked item SHALL release its path token while retaining its admitted-item slot; eventual admission of every item is conditional on admitted items not depending on pending items.

#### Scenario: Path ceiling is one during fan-out
- **WHEN** a parent fans out while `MaxConcurrentExecutionPathsPerInstance` is one
- **THEN** the parent releases its token so a runnable child can receive it and later reacquires one for the join without a deadlock caused solely by the parent retaining path-token capacity

#### Scenario: Fixed branches exceed the path ceiling
- **WHEN** a root `Parallel` starts more fixed branches than the host path ceiling
- **THEN** every branch fiber exists, runnable branches receive tokens fairly in authored order, and no branch waits for a separate live-fiber admission slot

#### Scenario: Admitted item depends on a pending item
- **WHEN** every admitted `ForEach` item is parked awaiting an effect that only a pending item would produce
- **THEN** later item admission may remain blocked because parking releases the path token but not the admitted-item slot; v1 does not promise progress for that authored dependency

#### Scenario: Late attempt remains physical
- **WHEN** a token-ignoring timed-out body continues after losing logical commit authority
- **THEN** it releases the logical path token but retains its physical step-throttle/transient capacity until it returns
