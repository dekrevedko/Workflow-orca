## MODIFIED Requirements

### Requirement: Management API is scope-oriented and fluent
The management Interface SHALL use shared asynchronous engine-wide, definition-scoped, selection-scoped, and instance-handle vocabulary across ephemeral and durable modes. `Instance(id)` SHALL return an instance handle with direct instance queries and commands rather than a list-style query whose single-item semantics depend on LINQ exceptions.

#### Scenario: Operator targets one workflow instance
- **WHEN** an operator selects a known instance in either execution mode
- **THEN** they can call consistently named `GetAsync`, `GetStateAsync<TState>`, active-wait, cancel, and terminate operations with stable not-found and wrong-state diagnostics

#### Scenario: Operator targets a selection
- **WHEN** an operator composes an engine-wide or definition-scoped selection
- **THEN** `Where`, `ListAsync`, `CountAsync`, `GetAsync`, active-wait, and statistics vocabulary is consistent across both engine Adapters

### Requirement: Command availability follows execution-mode capability
The management Interface SHALL expose shared instance commands such as event delivery, cancel, and terminate through common handles, while pause, durable resume, history, archive, purge, resource administration, and durable remediation remain explicitly durable. Application commands SHALL obtain time from the owning runtime rather than from caller-supplied timestamps.

#### Scenario: Operator invokes a durable-only command in ephemeral mode
- **WHEN** a caller uses an ephemeral instance handle
- **THEN** durable-only management methods are absent from the Interface rather than pretending the capability exists

#### Scenario: Durable command is invoked
- **WHEN** an operator pauses, resumes, cancels, or terminates a durable instance through management
- **THEN** the runtime supplies command identity and time and executes through the shared instance mutation lane

#### Scenario: Ephemeral memory is administered
- **WHEN** an operator uses an ephemeral management handle
- **THEN** `EvictAsync`, `EvictTerminalAsync`, `DetectStuckAsync`, and `GetLifecycleEventsAsync` are available as explicitly in-memory retention and diagnostic capabilities

## ADDED Requirements

### Requirement: Typed committed state is queryable in both modes
Both management Adapters SHALL provide typed detached root business-state inspection through `GetStateAsync<TState>`. Durable inspection SHALL deserialize the last committed root workflow state through the configured serializer without exposing checkpoint or provider DTOs and SHALL never return branch-private or item-private fiber payloads. Ephemeral inspection SHALL create its detached value through the same registered serializer/deep-copy contract used for branch-input isolation; when no compatible contract exists it SHALL return the same typed incompatible-state diagnostic rather than a live state reference.

#### Scenario: Durable business state is inspected
- **WHEN** a caller requests the registered state type for an active or terminal durable instance whose state is retained
- **THEN** management returns a detached typed value representing the last committed root workflow business state

#### Scenario: Structured branches contain private state
- **WHEN** active branch or item fibers hold payloads with the same or a different CLR type as the root workflow state
- **THEN** `GetStateAsync<TState>` ignores those private payloads and reads only the registered root state slot

#### Scenario: Durable state is unavailable
- **WHEN** state has been archived, purged, is incompatible, or the requested type does not match the registered definition
- **THEN** management returns a stable capability or lifecycle diagnostic rather than a raw serialization or LINQ exception

### Requirement: Management construction preserves one mutation lane
Management mutation handles SHALL be created by the owning runtime and SHALL NOT create fallback command processors, clocks, observers, or independent instance lanes.

#### Scenario: Management command races runtime work
- **WHEN** a management mutation and workflow advancement target the same durable instance concurrently
- **THEN** both operations pass through the configured shared serialization and conflict-handling path

### Requirement: Application wait snapshots expose authored facts
Application active-wait queries SHALL expose stable authored facts such as wait kind, immutable `AuthoredLocation`, event name, correlation, residency, relevant logical timing, and opaque `WaitId`. `AuthoredLocation` SHALL be the same structured location contract used by compiler diagnostics and SHALL remain stable for an unchanged authored graph. `WaitId` SHALL support application targeting and diagnostic correlation without encoding runtime ownership. Application snapshots SHALL NOT expose `FiberId`, `ScopeId`, `WaitSequence`, or raw obligation ownership; those remain advanced runtime diagnostics.

#### Scenario: Application lists active waits
- **WHEN** an operator inspects waits for a workflow with nested branches
- **THEN** the result identifies the authored branch/path and matching information without requiring runtime routing identities

### Requirement: Shared management models have one canonical declaration
The application tier SHALL define exactly one canonical `WorkflowInstanceQueryModel`, `WorkflowStatistics`, and `WorkflowStatisticsGroup`, and SHALL replace duplicate engine-local destructive-safety declarations with exactly one non-default application confirmation contract.

#### Scenario: Public management surface is inspected
- **WHEN** public type baselines and repository declaration scans run
- **THEN** each shared query/statistics model and the selected destructive confirmation type appears exactly once and no engine-local duplicate remains

### Requirement: Ephemeral retention and diagnostics remain available
Ephemeral management SHALL preserve `EvictAsync`, `EvictTerminalAsync`, `DetectStuckAsync`, and `GetLifecycleEventsAsync` without describing those operations as durable retention or restart-safe persistence.

#### Scenario: Terminal ephemeral instances are evicted
- **WHEN** an operator invokes terminal eviction through the ephemeral management Adapter
- **THEN** eligible in-memory terminal instances are removed and the result makes no restart-durability claim

#### Scenario: Stuck ephemeral instance is diagnosed
- **WHEN** an operator requests stuck-instance detection or lifecycle events
- **THEN** management returns the supported in-memory diagnostic view through asynchronous application vocabulary

### Requirement: Destructive confirmation is explicit and non-default
Single-instance termination SHALL use the explicit instance selection as its scope guard. Selection-wide termination and durable purge SHALL require a confirmation whose default value is unconfirmed, and the Interface SHALL NOT expose overloads that can only throw.

#### Scenario: Default confirmation is passed
- **WHEN** a caller passes `default` or `None` to a broad destructive operation
- **THEN** the operation rejects it before mutating any instance or retained data

#### Scenario: Single instance is terminated
- **WHEN** a caller invokes terminate on an explicit instance handle
- **THEN** no redundant broad-operation confirmation parameter is required
