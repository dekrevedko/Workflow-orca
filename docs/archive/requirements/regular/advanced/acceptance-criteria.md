# Regular Workflow Advanced Acceptance Criteria

These criteria define future acceptance coverage for advanced regular workflow features.

## Acceptance set

### RWA-AT-001: WhenFirst winner is deterministic

Given multiple branches racing toward `WhenFirst`
When more than one branch can complete close together
Then exactly one winner is chosen according to documented policy

### RWA-AT-002: Losing-branch behavior is explicit

Given `WhenFirst`
When one branch wins
Then losing branches follow the configured policy
And the policy outcome is observable

### RWA-AT-003: Timer completes after due time

Given a timer or delay step
When the due time elapses
Then the workflow continues exactly once

### RWA-AT-004: Child workflow completion propagates

Given a parent workflow with a child workflow
When the child completes
Then the parent observes completion and continues correctly

### RWA-AT-005: Child workflow failure policy is enforced

Given a child workflow that fails
When the parent awaits it
Then the configured failure policy is applied

### RWA-AT-006: Step timeout policy is enforced

Given a step with an expected timeout
When the timeout is exceeded
Then the configured timeout action occurs

### RWA-AT-007: Retry policy is idempotent and bounded

Given a retrying step
When transient failure occurs
Then retries follow the configured limit and do not produce duplicate committed outcomes

### RWA-AT-008: Lifecycle publication is observable

Given workflow execution
When significant state transitions occur
Then lifecycle events are published according to documented guarantees

### RWA-AT-009: Stuck workflow detection is queryable

Given a workflow that exceeds expected progress thresholds
When operational queries are executed
Then the workflow is visible as stuck
