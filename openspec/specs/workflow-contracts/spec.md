## Purpose

Define the shared contracts, models, messaging abstractions, and functional primitives that every OrcaCore runtime path depends on.
## Requirements
### Requirement: Steps execute against typed business state
The shared contract layer SHALL expose an asynchronous step contract built around `IStep`, `StepContext`, and `StepResult` so workflow steps operate on typed business state and report orchestration intent through explicit outcomes.

#### Scenario: Business step is authored
- **WHEN** a workflow author implements a new step
- **THEN** the step receives typed execution context and returns an explicit result instead of mutating runtime orchestration state directly

### Requirement: Runtime metadata is separate from business data
The contract layer SHALL model workflow runtime metadata separately from workflow business state so lifecycle state, waits, errors, and snapshots remain inspectable without relying on opaque domain payloads alone.

#### Scenario: Instance state is inspected
- **WHEN** tooling or tests inspect a workflow instance
- **THEN** runtime-owned status, wait metadata, and error details are available independently from the workflow's business-state payload

### Requirement: Events and dispatch use normalized transport models
The contract layer SHALL define normalized models for inbound events and outbound dispatch through types such as `EventEnvelope`, `DispatchMessage`, `DispatchPayload`, and `DispatchOutcome`.

#### Scenario: Runtime hands work to transport infrastructure
- **WHEN** a runtime publishes or routes an event through a provider boundary
- **THEN** it uses shared transport models rather than provider-specific message shapes

### Requirement: Serialization and validation seams are explicit
The contract layer SHALL expose payload serialization, schema resolution, and validation primitives so runtimes can persist and restore payloads without embedding provider-specific serialization rules in step code.

#### Scenario: Durable payload crosses a persistence boundary
- **WHEN** workflow or event payload data is serialized for storage or dispatch
- **THEN** serialization and validation occur through explicit shared abstractions rather than ad hoc runtime-specific logic

### Requirement: Host-facing execution hints remain optional and declarative
The contract layer SHALL allow workflow definitions or individual steps to carry optional, serializable metadata for host policies such as named resource pools (see `runtime-resource-governance`) without forcing step implementations to acquire semaphores or other synchronization primitives directly.

#### Scenario: Pool key is attached at authoring time
- **WHEN** an author marks a step or node with a named pool identifier intended for host enforcement
- **THEN** step code can remain focused on business outcomes while compliant hosts apply shared limits consistently

### Requirement: Branch data contracts are separate from StepResult
The contract layer SHALL define serializable branch input, fiber-local business data, branch result, and merge contracts separately from ordinary `StepResult`. `StepResult` SHALL remain limited to orchestration control intent and SHALL NOT become a generic business-result transport.

#### Scenario: Branch returns business data
- **WHEN** a local branch completes with data required by its parent
- **THEN** the data is emitted through the branch result contract rather than encoded as an ordinary step control result

### Requirement: Branch and merge contracts are type checked
Every scope SHALL declare one result type shared by its branches, and its merge SHALL accept that result type and the parent state type. Contract validation SHALL require serializers for persisted branch input, fiber-local data, results, and merged parent state.

#### Scenario: Merge result types do not match
- **WHEN** one branch return or the declared merge uses an incompatible result type
- **THEN** definition compilation fails before runtime registration

### Requirement: Dynamic item outcomes are ordered contracts
An ephemeral `ForEach` scope SHALL expose each admitted item's result through a runtime-owned `ForEachItemOutcome<TResult>` contract containing item index, terminal status, optional typed result, and failure metadata. The outcome collection SHALL be ordered by item index and SHALL NOT expose item-completion timing as merge order.

#### Scenario: Dynamic items complete out of order
- **WHEN** admitted `ForEach` item fibers reach terminal states in an order different from their item indices
- **THEN** the parent observes `ForEachItemOutcome<TResult>` values in item-index order

### Requirement: Executable plan identity is explicit
Shared contracts SHALL represent definition identity, authored definition version, compiler format version, execution-envelope version, and executable-plan fingerprint as distinct values used during registration and resume.

#### Scenario: Durable instance resumes
- **WHEN** a durable checkpoint is loaded
- **THEN** the runtime can compare every required identity and format value before interpreting persisted execution position
