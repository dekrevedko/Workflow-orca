## ADDED Requirements

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
