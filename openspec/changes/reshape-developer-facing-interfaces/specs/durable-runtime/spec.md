## MODIFIED Requirements

### Requirement: Wait and WaitLong have distinct residency behavior
The durable runtime SHALL publicly author and execute both resident waits and cold waits, with `Wait` remaining available for hot instances and `WaitLong` explicitly representing cold durable residency. Ephemeral authoring SHALL NOT expose `WaitLong`.

#### Scenario: Long-running wait is authored
- **WHEN** a workflow uses `WaitLong` through the public durable builder
- **THEN** registration accepts the definition and the runtime checkpoints the instance for durable resume rather than requiring it to remain hot in memory

## ADDED Requirements

### Requirement: Durable runtime is the complete application facade
The durable runtime SHALL own explicit definition registration, typed idempotent start, event delivery, external-job reporting, guaranteed continuation, lifecycle progression, and management composition without requiring application callers to construct `DurableCommandProcessor` or raw command records.

#### Scenario: Normal durable workflow runs
- **WHEN** an application starts and interacts with a supported durable definition entirely through the facade and its focused submodules
- **THEN** the runtime commits each accepted transition and either progresses it locally to a stable point or guarantees an at-least-once continuation handoff

#### Scenario: Raw protocol access is required
- **WHEN** a certified custom host needs command-level integration
- **THEN** it uses an explicitly referenced runtime-protocol seam and the normal application facade remains unchanged

### Requirement: Durable application operations own protocol mechanics
Durable application operations SHALL own clock timestamps, generated command/event identifiers, payload serialization and content type, deduplication, conflict translation, provider-capability checks, and follow-up driving. Callers SHALL supply only stable business identifiers or explicit idempotency identities that have application meaning.

#### Scenario: External-job completion is retried
- **WHEN** a worker reports the same logical completion more than once with the same completion identity
- **THEN** the application Interface returns a stable duplicate/already-applied result without requiring the worker to understand inbox records or stream versions

#### Scenario: External-job failure is reported
- **WHEN** a worker reports failure with a stable report identity, failure reason, and optional payload before the job times out
- **THEN** the runtime commits a worker-failure fact, applies the authored failure policy, and returns progression or pending-continuation status

#### Scenario: Runtime is manually constructed
- **WHEN** a non-DI caller uses a supported runtime factory
- **THEN** the factory accepts documented application options and Adapters rather than a command processor, mutable registry, checkpoint mapper, or driver observer

### Requirement: Application results use application vocabulary
Durable facade and management operations SHALL return operation-specific application results or typed failures and SHALL NOT expose raw command outcomes such as poisoned commit state as their normal result contract.

#### Scenario: Operation targets a terminal instance
- **WHEN** an application operation cannot apply because the instance is terminal
- **THEN** the caller receives a stable application-level terminal/already-complete result or exception with actionable context

### Requirement: Definition registration is explicit, host-scoped, and mode-typed
The durable application facade SHALL require `DurableWorkflowDefinition<TState>` definitions (or an equivalently explicit durable definition type) to be registered explicitly on each definition-owning host and SHALL return a typed `DurableDefinitionHandle<TState>` whose start methods infer input types without a phantom state generic. Starting SHALL NOT register a definition as a side effect. The normal durable registry SHALL NOT accept an ephemeral definition, inspect `RequiresDurableEngine`, or require a public compiled plan.

#### Scenario: Registered definition is started
- **WHEN** a host registers a durable definition and uses the returned typed handle to call `StartOrGetAsync(key, input, ct)`
- **THEN** the call compiles without explicit state generic arguments and uses the host's existing registration

#### Scenario: Ephemeral definition is passed to durable registration
- **WHEN** application code attempts to register an `EphemeralWorkflowDefinition<TState>` with the durable runtime
- **THEN** the normal call does not compile

#### Scenario: Unregistered definition is started
- **WHEN** a caller attempts to start a definition identity that is not registered on the host
- **THEN** the facade returns a stable `DefinitionNotRegistered` result or diagnostic without mutating registration state

#### Scenario: Payloadless event is raised
- **WHEN** an application raises an event that has no payload
- **THEN** a payloadless overload is available without an explicit `object?` generic argument or `null` payload

### Requirement: Accepted outcomes guarantee continuation across hosts
Every accepted facade operation SHALL atomically commit its protocol outcome with an at-least-once continuation-outbox handoff. The facade SHALL drive inline only when the bound definition is registered locally and SHALL distinguish `AppliedAndProgressed` from `AppliedPendingContinuation`.

#### Scenario: Definition-owning host reports completion
- **WHEN** a host with the registered definition accepts an external-job completion and reaches a stable point inline
- **THEN** the result is `AppliedAndProgressed`

#### Scenario: Definition-less callback host reports completion
- **WHEN** a callback host without the registered definition accepts an external-job completion
- **THEN** the result is `AppliedPendingContinuation` and a continuation pump on a definition-owning host later progresses the instance

### Requirement: External-job outcomes are complete and idempotent
The durable application facade SHALL expose typed completion, worker-failure, and timeout operations. Completion and worker failure SHALL accept the same form of caller-stable report identity, and duplicate reports SHALL return stable duplicate outcomes.

#### Scenario: Worker failure wins before timeout
- **WHEN** a worker reports failure before the timeout command commits
- **THEN** the failure path is committed and the authored failure policy progresses without waiting for timeout

#### Scenario: Worker failure is duplicated
- **WHEN** the same logical failure is reported again with the same report identity
- **THEN** the facade returns a stable duplicate result and does not apply the failure twice

### Requirement: Event routing outcomes are typed
Instance, definition, and correlation event-delivery operations SHALL represent no match, ambiguous match, unmatched event on a live instance, and paused target as expected typed results. Only invalid programming inputs such as empty identifiers, blank event names, or invalid payload arguments SHALL throw.

#### Scenario: Correlation route is ambiguous
- **WHEN** a correlation/definition event route resolves to more than one eligible instance
- **THEN** delivery returns `AmbiguousMatch` without selecting an arbitrary instance or throwing

#### Scenario: Live instance has no matching wait
- **WHEN** an event targets a live instance that has no matching active wait
- **THEN** delivery returns a typed unmatched result rather than an exception

#### Scenario: Event targets a paused instance
- **WHEN** an event resolves to a paused target
- **THEN** delivery returns `TargetPaused` and does not silently discard or apply the event

### Requirement: Durable remediation is application-safe
Application poison remediation SHALL be exposed on a durable instance handle using an opaque diagnostic ticket or equivalent stable compare-and-act token and SHALL NOT expose protocol `StreamVersion`. Raw stream-version rearm SHALL remain in the runtime-protocol seam.

#### Scenario: Remediation ticket is stale
- **WHEN** an operator submits a remediation ticket after the instance has advanced or changed poison state
- **THEN** the handle returns a stable conflict without mutating the instance

#### Scenario: Current poison is acknowledged
- **WHEN** an operator submits the current remediation ticket for an acknowledged poison
- **THEN** the runtime rearms the instance and guarantees continuation using the same local-or-handoff contract

### Requirement: Durable leases are scope-owned obligations
A durable resource lease SHALL be owned by its structured fiber/scope and SHALL release deterministically on normal scope exit, branch cancellation, scope failure, or workflow terminal transition. Lease expiry SHALL be a crash-recovery backstop.

#### Scenario: Owning branch is canceled
- **WHEN** a structured branch holding a durable lease is canceled
- **THEN** the driver commits deterministic release before completing cancellation handling

#### Scenario: Host crashes while a lease is held
- **WHEN** a host fails before deterministic release can commit
- **THEN** restart reconstruction and expiry recovery eventually make capacity available without treating expiry as normal release

### Requirement: Durable DAG progression is runtime-owned
The durable runtime SHALL reconstruct DAG progress from committed state, schedule newly ready child nodes, and continue after child outcomes without requiring application callers to supply completed, failed, or already-scheduled node sets.

#### Scenario: Host restarts during a DAG run
- **WHEN** a durable DAG root is resumed after child work was scheduled before host failure
- **THEN** the runtime reconstructs scheduled and terminal child state and does not duplicate in-flight nodes
