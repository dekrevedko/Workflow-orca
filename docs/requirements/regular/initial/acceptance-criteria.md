# Regular Workflow Initial Acceptance Criteria

These criteria define when the initial regular workflow slice is complete.

## Acceptance set

### RWI-AT-001: Straight-line completion

Given a workflow `Init -> Step -> End`
When the workflow starts
Then it reaches `Completed`
And business state reflects the step result

### RWI-AT-002: Conditional branch selection

Given a workflow with `If`
When the condition is true
Then only the `then` branch executes
When the condition is false
Then only the `else` branch executes

### RWI-AT-003: Wait enters waiting state

Given a workflow that executes `Wait`
When the wait is registered
Then the instance status becomes `Waiting`
And one active wait is inspectable

### RWI-AT-004: Matching event resumes exactly once

Given a waiting workflow
When a matching event is raised
Then the workflow resumes from the wait point
And the matched event payload is available to the next step
And the workflow does not resume twice

### RWI-AT-005: Non-matching event does not resume

Given a waiting workflow
When an event has wrong `EventName` or wrong `CorrelationId`
Then the workflow remains waiting

### RWI-AT-006: Out-of-order event is buffered and later consumed

Given a workflow that will later wait for an event
When that event arrives before the wait is registered
Then the event is buffered
And when the matching wait is later registered it is consumed without re-sending the event

### RWI-AT-007: Duplicate event is deduplicated

Given a waiting workflow
When the same `EventId` is delivered more than once
Then only one delivery is consumed
And only one continuation occurs

### RWI-AT-008: Correlation-targeted routing resumes exactly one instance

Given multiple waiting instances with distinct correlation IDs
When an engine-wide correlation-targeted event is raised
Then only the uniquely matching instance resumes

### RWI-AT-009: Ambiguous or missing correlation is rejected clearly

Given correlation-targeted routing
When no active wait matches
Then the call fails with a clear "no active wait" error
When more than one instance matches
Then the call fails with a clear ambiguity error

### RWI-AT-010: Definition fanout resumes only instances of that definition

Given multiple instances of one workflow definition and another definition
When a definition-scoped fanout event is raised
Then only instances of the targeted definition receive delivery

### RWI-AT-011: While loop repeats until false

Given a workflow with `While`
When the loop condition stays true for three iterations
Then the body executes three times
And the workflow completes after the fourth condition check returns false

### RWI-AT-012: Wait inside loop is isolated by iteration

Given a workflow with `Wait` inside `While`
When the workflow advances across iterations
Then each iteration creates a fresh wait
And an event from a previous iteration cannot resume a later iteration

### RWI-AT-013: Parallel branches join exactly once

Given a workflow with `Parallel` followed by `WhenAll`
When all branches complete
Then the continuation after `WhenAll` executes exactly once

### RWI-AT-014: Different waits in parallel branches remain isolated

Given parallel branches with different waits
When one matching event is raised
Then only the matching branch resumes

### RWI-AT-015: Branch completion order is deterministic

Given a parallel workflow
When branches complete in different orders across two runs
Then the final committed outcome is the same

### RWI-AT-016: Step failure moves instance to failed

Given a failing step or an unhandled exception in a step
When execution reaches that step
Then the workflow reaches `Failed`
And later steps do not execute
And error details are inspectable

### RWI-AT-017: Management query filters by runtime metadata

Given multiple instances in different states
When `Where(x => ...)` is used over snapshots
Then only matching instances are listed or counted

### RWI-AT-018: Concurrent resume attempts serialize to one valid outcome

Given a waiting workflow
When two callers try to resume it concurrently
Then the workflow produces one valid sequential outcome
And no double continuation occurs

### RWI-AT-019: WaitLong is unavailable

Given the initial regular ephemeral surface
When a workflow definition attempts to use `WaitLong`
Then the builder does not expose it
And runtime fallback rejects it clearly if forced through an internal path

## Exit criteria

The initial regular workflow slice is complete when:

- every acceptance criterion above is green
- the public API still matches the fluent management baseline
- no durable-only or saga-only semantics have leaked into the public initial surface
- design decisions remain traceable in the implementation
