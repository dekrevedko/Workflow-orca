# Saga Initial Acceptance Criteria

These criteria define the minimum acceptable behavior for the first saga slice.

## Acceptance set

### SGI-AT-001: Successful saga completes without compensation

Given a saga with successful forward steps
When all forward steps succeed
Then the saga reaches successful completion
And no compensation runs

### SGI-AT-002: Failure triggers compensation

Given a saga with completed forward steps followed by a failing step
When the failure occurs
Then compensation starts for eligible prior steps

### SGI-AT-003: Compensation order is deterministic

Given multiple successfully completed forward steps
When compensation starts
Then compensating actions run in the documented order

### SGI-AT-004: Compensation failure is observable

Given a failing compensating action
When compensation runs
Then the saga reaches a distinct compensation-failed outcome

### SGI-AT-005: Timeout policy interacts with compensation predictably

Given a forward step with timeout
When the timeout is exceeded
Then the saga applies the documented timeout outcome and compensation policy
