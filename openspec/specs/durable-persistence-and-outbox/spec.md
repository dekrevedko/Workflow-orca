## Purpose

Define the persistence, outbox, serialization, and retention capabilities required by OrcaCore durable execution.

## Requirements

### Requirement: Workflow store persists first-class durable records
The durable system SHALL persist workflow instances, waits, inbox state, outbox state, history, and related queryable runtime records through an explicit `IWorkflowStore` boundary.

#### Scenario: Durable state is committed
- **WHEN** a workflow transition is durably recorded
- **THEN** the store contract preserves the instance snapshot and its related operational records as first-class durable data

### Requirement: Outbox delivery is adapter-driven and at-least-once
Outbound durable dispatch SHALL flow through `IMessageDispatcher` and an outbox pump so external delivery remains adapter-based and at-least-once rather than being coupled directly to workflow execution.

#### Scenario: Local commit succeeds before external delivery
- **WHEN** a workflow transition commits outbound work into the outbox
- **THEN** the dispatcher pump can retry delivery independently without losing the already committed workflow state

### Requirement: Durable payloads cross boundaries through explicit serializers
The durable system SHALL serialize workflow and event payloads through explicit payload-envelope and schema-resolution abstractions so persisted data can be restored deterministically.

#### Scenario: Persisted payload is read after restart
- **WHEN** the runtime rehydrates a durable instance or outbox record
- **THEN** payload materialization occurs through the configured serialization abstractions instead of provider-specific ad hoc logic

### Requirement: Retention and purge preserve runtime correctness
The durable system SHALL support retention and purge operations for inbox, outbox, and history artifacts without corrupting live runtime state or breaking terminal-instance inspection guarantees.

#### Scenario: Old operational artifacts are purged
- **WHEN** retention cleanup removes expired durable artifacts
- **THEN** active runtime correctness and documented inspection behavior remain intact
