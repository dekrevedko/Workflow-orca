# Lifecycle, Resource Management, and Operational Signals

Reviewed on March 14, 2026.

## Purpose

This document defines runtime policies around:

- active in-memory instance lifetime
- eviction of inactive instances
- completed/terminated instance cleanup
- step and workflow lifetime tracking
- stuck detection and timeout handling
- lifecycle events
- operational statistics and queries

These concerns are part of product behavior, not just optimization.

## 1. Core position

OrcaCore should separate:

- durable workflow instance existence
- in-memory active execution presence

A workflow instance may exist durably for a long time while being absent from memory most of that time.

This is the correct model for long-running orchestration.

## 2. Active versus durable instance state

### Durable instance

A durable instance exists in storage and can be queried, resumed, retried, terminated, archived, or deleted according to policy.

### Active instance

An active instance is currently loaded in memory and eligible to execute, react to a short-term wait, or process a queued mutation.

### Design rule

Active presence is a cache-like optimization layer, not a correctness requirement.

## 3. Eviction policy

OrcaCore should support eviction of inactive in-memory instances.

### Candidates for eviction

- waiting instances with no immediate runnable work
- idle instances between commands/events
- completed instances after completion hooks are durably handled
- terminated or canceled instances after terminal hooks are durably handled
- faulted instances after failure state is durably recorded

### Instances that should not be evicted immediately

- instances currently executing a step
- instances in the middle of a durable commit
- instances with pending in-memory short-wait wake-up that has not been durably delegated according to policy

### Policy direction

A configurable active-instance cache is reasonable.

An LRU-like cache is a plausible implementation strategy, but the product requirement should be stated in semantic terms:

- idle instances may be evicted safely
- eviction must not lose correctness
- rehydration must restore execution when needed

So the requirement is safe eviction, not specifically LRU.

## 4. Completed and terminal instance lifecycle

Completed, terminated, canceled, and faulted instances should not stay active in memory longer than needed.

Suggested lifecycle:

1. Persist final terminal state.
2. Persist or publish required lifecycle events.
3. Make terminal metadata queryable.
4. Evict from active memory.
5. Retain durably according to retention policy.
6. Archive or delete later according to cleanup policy.

### Important distinction

Eviction from memory is not deletion from durable storage.

Users still need to inspect terminal instances, history, and reasons for failure or termination until retention rules remove them.

## 5. Active lifetime policy

OrcaCore should define how long an instance may remain active in memory while idle.

### Proposed policy dimensions

- active idle timeout
- short-wait eligibility window
- max active instance count
- memory-pressure eviction
- per-definition overrides if ever needed

### Practical interpretation

An instance may remain active in memory when:

- a step is currently executing
- the instance is in a short-term wait that benefits from remaining active
- the engine predicts near-term follow-up work

Otherwise, it should be eligible for eviction.

This aligns with Orleans activation collection thinking without copying Orleans directly.

## 6. Step lifetime tracking

Every executing step should have tracked lifetime metadata.

At minimum:

- step start time
- last heartbeat or progress timestamp if supported
- expected timeout if configured
- actual completion time
- terminal outcome

### Why this matters

Without step lifetime tracking, the engine cannot distinguish:

- healthy long-running work
- temporarily slow work
- stuck work
- timed-out work

## 7. Workflow instance lifetime tracking

Every workflow instance should also have tracked lifetime metadata.

At minimum:

- created time
- last state transition time
- last active execution time
- current status entered time
- total runtime age
- total wait age for current wait
- terminal time if completed/failed/terminated

This enables detection of stale or stuck instances.

## 8. Stuck detection

OrcaCore should support detection of apparently stuck steps and apparently stuck workflow instances.

### Step-level stuck conditions

Examples:

- step execution exceeds configured timeout
- step has no progress heartbeat beyond configured threshold
- step is still marked executing after host crash without successful lease recovery or reconciliation

### Workflow-level stuck conditions

Examples:

- instance remains in executing state beyond configured threshold
- instance remains in queued/runnable state without progress beyond configured threshold
- instance remains in wait state past expected expiration without documented outcome

### Important rule

Stuck detection should produce observable signals, not silent internal flags only.

## 9. Timeout policies

Steps should support an expected timeout or deadline policy.

When a step times out, the engine should allow configurable policy, such as:

- retry step
- fail workflow instance
- terminate workflow instance
- cancel branch and continue according to workflow definition
- invoke compensation or recovery path
- mark timed out and await operator action

This must be explicit and acceptance-tested.

## 10. Lifecycle events

OrcaCore should provide first-class lifecycle events for both workflow instances and steps.

### Workflow instance lifecycle events

At minimum consider:

- instance created
- instance activated
- instance evicted
- instance started executing
- instance suspended
- instance resumed
- instance completed
- instance failed
- instance timed out
- instance canceled
- instance terminated
- instance archived
- instance deleted
- instance detected as stuck

### Step lifecycle events

At minimum consider:

- step scheduled
- step started
- step completed
- step failed
- step retried
- step timed out
- step canceled
- step compensation started
- step compensation completed
- step detected as stuck

### Design rule

Lifecycle events should be part of the runtime contract and should be queryable or publishable through provider-backed mechanisms.

## 11. Statistics and query surface

Users need more than single-instance inspection. They need operational visibility.

OrcaCore should support queries such as:

- active instance count
- suspended instance count
- running instance count
- failed instance count
- timed-out instance count
- terminated instance count
- counts grouped by workflow definition and version
- counts grouped by status
- count of active waits by event type
- count of stuck steps / stuck instances
- oldest running instance age
- oldest suspended instance age
- oldest active step age

This can be exposed through a provider abstraction, but the requirement belongs to the engine.

## 12. Relationship to serialized execution

Resource management must not break the serialized execution guarantee.

That means:

- evicting an instance must release active ownership cleanly
- rehydrating an instance must reacquire serialized execution ownership cleanly
- active-cache policies must never allow two active mutators for the same instance

## 13. Relationship to short and long waits

### Short waits

Short waits may justify keeping an instance active in memory for a short configurable window.

### Long waits

Long waits should generally make the instance evictable after durable wait registration is complete.

This makes `WaitLong` a natural eviction boundary.

## 14. Retention and cleanup policy

OrcaCore should separate:

- active-memory eviction policy
- durable retention policy
- archival policy
- hard deletion policy

Users should be able to configure what happens to terminal instances after completion, failure, or timeout.

Examples:

- retain for inspection
- archive after N days
- delete after N days
- keep failed instances longer than completed ones

## 15. Acceptance criteria to add

### AT-032: Idle active instance is evicted safely

Given an idle workflow instance with no runnable work
When active-instance eviction occurs
Then the in-memory instance is removed
And later resume or inspection still works from durable state

### AT-033: Terminal instance is evicted after terminal handling

Given a completed or terminated workflow instance
When terminal persistence and lifecycle handling are complete
Then the instance is removed from active memory
And remains durably queryable according to retention policy

### AT-034: Step timeout policy is enforced

Given a step with a configured expected timeout
When the timeout is exceeded
Then the configured timeout policy is executed deterministically
And the step and workflow lifecycle events reflect the timeout

### AT-035: Stuck step detection emits observable signal

Given a step that remains executing beyond the configured stuck threshold
When the threshold is exceeded
Then a step-stuck lifecycle event is emitted
And operational queries reflect the stuck step

### AT-036: Stuck workflow detection emits observable signal

Given a workflow instance that remains non-terminal without progress beyond the configured stuck threshold
When the threshold is exceeded
Then an instance-stuck lifecycle event is emitted
And operational queries reflect the stuck instance

### AT-037: Statistics query by definition and status

Given workflow instances of multiple definitions and statuses
When operational statistics are queried
Then the engine returns grouped counts by definition and status correctly

### AT-038: Eviction never causes duplicate active mutators

Given an instance being evicted and reactivated under concurrent pressure
When activation ownership changes
Then at most one logical mutator exists for the instance at any time

## 16. Final position

OrcaCore should behave more like Orleans in resource lifecycle discipline:

- active objects are disposable
- idle objects should be evictable
- terminal objects should leave memory quickly
- correctness lives in durable state, not memory presence

But OrcaCore should go further than Orleans in workflow-specific observability:

- explicit step and instance lifecycle events
- stuck detection
- timeout policy handling
- operational statistics and status queries
