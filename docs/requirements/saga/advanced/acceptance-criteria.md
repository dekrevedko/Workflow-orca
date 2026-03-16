# Saga Advanced Acceptance Criteria

## Acceptance set

### SGA-AT-001: Durable saga resumes after restart

Given a long-running saga suspended between messages
When the host restarts
Then the saga resumes from durable state without duplicate forward or compensating actions

### SGA-AT-002: Compensation audit trail is complete

Given a compensated saga
When operators inspect it
Then they can see forward actions, compensation actions, and final outcome

### SGA-AT-003: Manual compensation recovery is supported

Given a compensation failure
When an operator performs an allowed recovery action
Then the saga transitions according to policy and records the intervention

### SGA-AT-004: Outbox consistency prevents duplicate external effects

Given saga state changes with outbound messages
When failures occur around the commit boundary
Then duplicate or lost side effects do not violate documented guarantees
