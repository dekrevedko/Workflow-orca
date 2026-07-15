## MODIFIED Requirements

### Requirement: Advanced saga support is durable and auditable
Advanced saga behavior SHALL execute through an explicitly registered durable saga definition and the structured runtime-owned progression loop from `adopt-structured-fiber-execution`, which records forward actions, timeouts, compensation decisions, compensation outcomes, manual intervention, scope-owned obligations, and version-aware evolution. Application callers SHALL NOT construct saga protocol commands or use an interim command adapter to advance normal saga work.

#### Scenario: Operator reviews a long-running saga incident
- **WHEN** a durable saga requires investigation or intervention
- **THEN** the application management Interface exposes committed saga audit and supported remediation without requiring action keys, scope IDs, command IDs, or timestamps to be reconstructed by the caller

#### Scenario: Saga step fails
- **WHEN** a registered durable saga forward action fails after earlier actions committed
- **THEN** the runtime selects and progresses compensation from definition metadata and committed history

## ADDED Requirements

### Requirement: Saga authoring selects execution mode explicitly
Saga authoring SHALL select ephemeral or durable execution mode before mode-specific capabilities become available and SHALL produce distinct immutable `EphemeralSagaDefinition<TState>` and `DurableSagaDefinition<TState>` application types. The two types MAY share one internal representation, but normal registration SHALL accept only its matching definition family. Ephemeral saga definitions SHALL expose only in-process compensation semantics, while durable saga definitions SHALL expose only capabilities supported by runtime-owned durable progression.

#### Scenario: Ephemeral saga is authored
- **WHEN** a developer selects ephemeral saga authoring
- **THEN** durable audit, restart recovery, and manual durable remediation methods are absent and `Build()` returns an `EphemeralSagaDefinition<TState>`

#### Scenario: Durable saga is authored before progression exists
- **WHEN** runtime-owned durable saga progression is not implemented
- **THEN** the application Interface does not expose a provisional durable saga builder or interim command adapter as a complete feature
