## MODIFIED Requirements

### Requirement: Ephemeral mode has explicit limitations
When the runtime is used without durable persistence, it SHALL support short-lived orchestration and in-memory waits while providing no restart-safe rehydration. Wait residency SHALL remain a runtime and hosting policy rather than an authored node distinction, so ephemeral `Wait` SHALL NOT survive process loss and SHALL NOT be distinguished by a separate authored member. The ephemeral builder SHALL NOT expose durable-only capabilities such as root `ContinueAsNew` or scoped `AcquireResources`.

#### Scenario: Host restarts in ephemeral mode
- **WHEN** an in-memory workflow instance is waiting and the process restarts
- **THEN** the prior instance state is not recoverable and no cold wait residency is claimed

#### Scenario: Ephemeral author looks for durable-only capabilities
- **WHEN** an ephemeral author looks for root `ContinueAsNew` or scoped `AcquireResources`
- **THEN** the members are absent from the statically selected ephemeral builder
