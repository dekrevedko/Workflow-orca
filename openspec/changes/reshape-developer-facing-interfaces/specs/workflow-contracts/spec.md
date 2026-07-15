## MODIFIED Requirements

### Requirement: Steps execute against typed business state
The shared application contract layer SHALL expose asynchronous portable step execution built around `IStep`, `StepContext`, and a common result set containing only outcomes with intentionally shared semantics. Durable-only orchestration effects SHALL be represented by durable authoring nodes or a separate durable step contract and SHALL NOT be returnable through the portable `IStep` Interface.

#### Scenario: Portable business step is authored
- **WHEN** a workflow author implements a step for use in either engine
- **THEN** the step receives typed state, resumed-event context, deterministic time, and cancellation through the documented execution contract and can return only portable outcomes

#### Scenario: Event name is selected dynamically
- **WHEN** portable business code can determine its event name only while executing
- **THEN** it may return portable `WaitForEvent`, while a statically known event uses the preferred structural `Wait` node

#### Scenario: Durable external work is authored
- **WHEN** an author needs external-job dispatch, durable resource acquisition, or continue-as-new
- **THEN** they select a durable authoring capability that can validate payload, placement, and persistence requirements before execution

### Requirement: Host-facing execution hints remain optional and declarative
The contract layer SHALL allow host policy to apply per-step execution throttles to all steps or stable authored categories without forcing step implementations to acquire synchronization primitives directly. Named cross-instance transient-pool metadata SHALL be authorable only in a mode where every supported host enforces it, and persisted durable resource leases SHALL remain a separate contract. Host composition SHALL NOT change the methods available on a previously selected static builder type.

#### Scenario: Execution throttle targets authored work
- **WHEN** a host policy targets a stable authored step category
- **THEN** step code remains focused on business outcomes while a compliant selected-mode host enforces the transient limit

#### Scenario: Durable lease is requested
- **WHEN** a durable definition requests persisted capacity shared across instances
- **THEN** the request uses the durable resource-lease contract rather than the execution-throttle hint

#### Scenario: Cross-instance transient pool is requested
- **WHEN** ephemeral authoring requests named capacity shared across workflow instances without persistence
- **THEN** the request uses the transient-pool contract and documentation explicitly disclaims restart durability

## ADDED Requirements

### Requirement: Application contracts do not depend on advanced contracts
Application contract assemblies SHALL NOT reference provider-authoring or runtime-protocol assemblies, and no application-facing public signature SHALL contain a type from either advanced seam.

#### Scenario: Public signature guard runs
- **WHEN** the application assemblies are inspected during verification
- **THEN** no public return type, parameter, property, base type, or generic constraint leaks a provider commit DTO, durable command/event protocol, checkpoint, or driver type

### Requirement: Strong identifiers reject default and empty values
Every public application and advanced operation SHALL reject empty/default strong identifiers and non-positive definition versions at the nearest seam with a stable diagnostic.

#### Scenario: Default identifier is supplied
- **WHEN** a caller passes `default(DefinitionId)`, `default(InstanceId)`, a `Guid.Empty`-backed identifier, or definition version zero
- **THEN** authoring, registration, or operation invocation fails before persistence or workflow execution begins
