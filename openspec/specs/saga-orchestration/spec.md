## Purpose

Define the target saga-orchestration behavior for OrcaCore, including compensation and durable coordination, even though saga types are not yet implemented in source.
## Requirements
### Requirement: Saga workflows use a distinct definition kind
The system SHALL model saga workflows as a separate definition kind rather than treating compensation as an ad hoc extension of regular workflows.

#### Scenario: Saga is authored
- **WHEN** a contributor defines a process that requires compensating actions
- **THEN** the definition is represented as a saga-specific orchestration construct

### Requirement: Compensation has explicit scope and reverse ordering
Saga execution SHALL define which forward actions require compensation and SHALL apply compensation in deterministic reverse logical order for the covered scope. A committed forward action SHALL be immediately eligible inside its owning branch scope. A branch-result commit SHALL expose its eligible records to the containing scope. Failure or cancellation before merge SHALL compensate every committed descendant action covered by the failing scope. Successful merge SHALL transfer eligible records to the parent scope without changing their stable identities. Sequential actions SHALL use reverse committed sequence order. Actions completed by sibling fibers SHALL use reverse stable authored branch and instruction order rather than wall-clock completion order. A per-scope ordering override SHALL be accepted only when it is deterministic, bound into the compiled-plan fingerprint, and based on stable authored identities.

#### Scenario: Mid-saga failure occurs
- **WHEN** a saga fails after multiple compensable sequential actions have completed
- **THEN** compensation executes for the covered actions in reverse committed sequence order

#### Scenario: Parallel saga branches complete in different orders
- **WHEN** equivalent compensable actions in sibling fibers complete in different wall-clock orders
- **THEN** compensation uses the same reverse canonical scope order in every execution

#### Scenario: Scope fails before merge
- **WHEN** one branch has committed compensable actions and another branch causes the containing scope to fail before merge
- **THEN** scope compensation covers every committed descendant action, including actions from the successfully progressing branch

#### Scenario: Parent fails after successful merge
- **WHEN** a child scope merges successfully and a later parent-scope action fails
- **THEN** the transferred child compensation records remain eligible under the parent scope with their original stable ordering identities

### Requirement: Saga outcomes include saga-specific terminal semantics
Saga orchestration SHALL model terminal outcomes that distinguish successful completion, compensated completion, failed compensation, cancellation, and timeout-driven saga outcomes.

#### Scenario: Saga is cancelled after partial progress
- **WHEN** cancellation or timeout occurs after compensable work has already run
- **THEN** the resulting terminal state reflects saga-specific compensation semantics instead of a generic workflow completion code only

### Requirement: Advanced saga support is durable and auditable
Advanced saga behavior SHALL support durable coordination boundaries, compensation auditability, manual intervention, outbox or inbox consistency expectations, and version-aware evolution.

#### Scenario: Operator reviews a long-running saga incident
- **WHEN** a durable saga requires investigation or intervention
- **THEN** the system exposes enough durable state and audit information to understand completed actions, pending compensation, and operator-required next steps

### Requirement: Saga records are owned by fibers and scopes
Every compensable forward action SHALL record its owning fiber, owning scope, stable instruction identity, committed completion position, and compensation state. Cancelling or failing a scope SHALL preserve the records required to finish or diagnose compensation.

#### Scenario: Host restarts during scoped compensation
- **WHEN** a host restarts after one nested-scope compensation commits and before the next begins
- **THEN** rehydration resumes from the persisted compensation position without rerunning completed forward actions or completed compensations
