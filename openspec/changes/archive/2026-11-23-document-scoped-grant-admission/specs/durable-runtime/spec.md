## ADDED Requirements

### Requirement: Scoped grant admission is conditional on the owning workflow checkpoint

Providers admitting scoped durable resource grants SHALL implement `ICheckpointGuardedResourceGovernanceStore.AppendIfCurrentAsync`. New grants, queue entries and FIFO promotions SHALL validate the checkpoint versions used to admit their owners in the same transaction as the expected-version governance append. Checkpoint validation and the governance append SHALL serialize with concurrent updates to those checkpoints. A provider decorator serving these operations SHALL preserve the same atomic check. A host whose provider lacks this capability SHALL fail before appending a governance batch that creates a scoped queue entry, grants a new scoped reservation, or promotes a scoped waiter.

#### Scenario: Cancellation commits after a worker reads eligibility

- **WHEN** an acquire or FIFO promotion reads a live owner checkpoint and the workflow commits cancellation before the governance write
- **THEN** the stale operation cannot grant a ticket to the cancelled occurrence or enter its protected work

#### Scenario: The guarded append conflicts

- **WHEN** the governance version differs or a required owner checkpoint is missing or has changed
- **THEN** the provider returns a conflict with the actual governance version and persists none of the batch

#### Scenario: No owner checkpoints are required

- **WHEN** the checkpoint condition set is empty and the governance version matches
- **THEN** the provider commits the ordered batch even before the first workflow checkpoint exists

#### Scenario: Two workers observe the same reservation

- **WHEN** both workers observe the same scoped ticket and one loses its workflow append
- **THEN** the losing worker leaves the original ticket and provider generation with the occurrence, before and after activation
- **AND** a replacement driving the still-pending occurrence can adopt that reservation

#### Scenario: Recovery replays closure before activation

- **WHEN** an occurrence closes before activation and governance has already recorded its receipt
- **THEN** recovery publishes one matching workflow release with the exact reserved tickets, or zero tickets when cancellation won before reservation
- **AND** replay preserves any successor ticket already transferred to a queued rival
