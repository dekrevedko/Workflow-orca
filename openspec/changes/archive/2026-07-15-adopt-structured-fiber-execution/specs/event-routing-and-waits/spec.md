## ADDED Requirements

### Requirement: Wait matching resumes the owning fiber
A matched event SHALL transition only the fiber that owns the selected wait from blocked to runnable. It SHALL NOT reconstruct or advance unrelated parent or sibling positions. Every wait SHALL receive a persisted per-instance `WaitSequence` at registration. When one non-fanout event matches multiple active waits with identical event identity and correlation, the runtime SHALL select the lowest `WaitSequence`, using stable `FiberId` as a final tie breaker.

#### Scenario: Event matches one nested branch wait
- **WHEN** an event matches a wait owned by one nested child fiber
- **THEN** only that child fiber becomes runnable and the scheduler retains its persisted ordering rules

#### Scenario: Event matches identical sibling waits
- **WHEN** two owner-distinct sibling waits have the same event identity and correlation and one non-fanout event matches both
- **THEN** exactly the earliest committed `WaitSequence` consumes the event and only its owning fiber becomes runnable

### Requirement: Wait cleanup follows scope lifecycle
Cancellation, failure, `WhenFirst` loser selection, and workflow termination SHALL durably cancel or consume every wait, timer, and pending resume owned by the affected scope descendants before those descendants are removed.

#### Scenario: Losing branch owns a wait
- **WHEN** a `WhenFirst` winner is selected while another branch owns an active wait
- **THEN** wait-cancellation facts and updated scope state commit without leaving an addressable orphan wait

## MODIFIED Requirements

### Requirement: Wait isolation is preserved across loops and branches
The runtime SHALL persist explicit `FiberId`, `ScopeId`, and `WaitSequence` ownership for waits created inside loops and branch scopes. Event identity and correlation determine matching candidates; deterministic wait sequence selects among otherwise identical candidates, and owner identity determines the exact fiber resumed. Repeated local branch names or nested positions SHALL NOT be treated as global identity.

#### Scenario: Two branches wait on related event shapes
- **WHEN** parallel branches or loop iterations register separate waits
- **THEN** each wait only consumes the event intended for its exact fiber and scope owner

#### Scenario: Nested scopes reuse a branch name
- **WHEN** separate nested scopes contain branches with the same authored local name
- **THEN** hierarchical scope and fiber identity keeps their waits distinct without relying on the branch name as global identity
