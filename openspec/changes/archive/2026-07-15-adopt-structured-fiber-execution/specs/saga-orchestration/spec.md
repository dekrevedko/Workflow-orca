## ADDED Requirements

### Requirement: Saga records are owned by fibers and scopes
Every compensable forward action SHALL record its owning fiber, owning scope, stable instruction identity, committed completion position, and compensation state. Cancelling or failing a scope SHALL preserve the records required to finish or diagnose compensation.

#### Scenario: Host restarts during scoped compensation
- **WHEN** a host restarts after one nested-scope compensation commits and before the next begins
- **THEN** rehydration resumes from the persisted compensation position without rerunning completed forward actions or completed compensations

## MODIFIED Requirements

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
