# Acceptance Test Matrix

Derived from Workflow Core issue-pattern research on March 14, 2026.

## Purpose

These acceptance tests are intended to protect OrcaCore from repeating semantic, functional, and expectation failures observed in comparable engines.

They are written as future product-level tests, not unit tests. Each test should eventually be executable against at least:

- ephemeral in-memory mode without persistence
- one durable SQL-style provider
- one broker-backed event provider

## Start and identity

### AT-001: Idempotent start by client key

Given a workflow definition and a client-supplied start idempotency key
When the same start request is submitted twice because the first response is lost
Then only one workflow instance is created in durable mode
And both callers can resolve the same workflow instance identifier

Derived from:

- Workflow Core issue #828

### AT-002: Duplicate start without idempotency policy

Given duplicate start requests without an idempotency policy
When both requests are submitted
Then the engine behavior is explicit and documented
And either both are created intentionally or one is rejected deterministically

## Wait and resume

### AT-003: Immediate resume after durable wait registration

Given a workflow that enters a durable wait
When the matching external event arrives immediately after persistence of the wait
Then the workflow resumes exactly once without requiring an artificial delay

Motivation:

- protects against persistence visibility races common in workflow engines

### AT-004: Repeated wait inside a loop

Given a workflow with a loop that waits for the same event shape multiple times across iterations
When matching events arrive for each iteration
Then each wait consumes exactly one intended event
And later iterations do not accidentally match earlier subscriptions

Derived from:

- issue-pattern concern around cyclic waits and user expectations

### AT-005: Different waits in parallel branches

Given a workflow with two parallel branches waiting on different event types or correlation values
When one matching event for each branch arrives in any order
Then each branch resumes only from its own matching event
And no branch consumes the other branch's event

Derived from:

- Workflow Core issue #273 and competitor concern about branch-scoped waits

### AT-006: Duplicate event delivery

Given a waiting workflow instance
When the same correlated external event is delivered twice
Then the workflow resumes at most once
And the second delivery is treated according to documented deduplication rules

### AT-007: Out-of-order event arrival

Given a workflow expecting event B only after event A
When event B arrives before event A
Then the engine performs the documented behavior deterministically
And the event is either ignored, buffered, or rejected according to policy

### AT-039: Durable-only wait rejected in ephemeral mode

Given the engine is running without a persistence provider
When a workflow definition attempts to use `WaitLong` or any durable-only wait feature
Then the engine fails fast with clear configuration or execution diagnostics

### AT-043: Timer and event race resolves deterministically

Given a workflow waiting for an external event and a timeout timer simultaneously
When either the event or timer wins the race
Then the losing timer or wait is canceled or ignored according to explicit policy
And terminal behavior is deterministic

## Branching and synchronization

### AT-008: Parallel join executes once

Given a workflow with parallel branches joined by all-of semantics
When all branches complete
Then the post-join continuation executes exactly once
And structurally irrelevant no-op steps do not change that outcome

Derived from:

- Workflow Core issue #273

### AT-009: WhenFirst cancels losing branches deterministically

Given a workflow with `WhenFirst`
When one branch completes first
Then the winning branch result is committed once
And the losing branches are canceled, ignored, or allowed to finish according to explicit policy
And the chosen policy is observable in runtime state

### AT-010: Branch completion ordering is deterministic

Given a workflow with branches completing in different orders
When the same logical branch results are produced across repeated runs
Then the same continuation behavior is observed regardless of execution interleaving

Motivation:

- protects against ambiguous "what happens after branch completion" behavior

### AT-011: Wait inside branch with join and cancellation

Given parallel branches where one branch waits and another triggers cancellation or early completion
When the cancel or early-complete condition is met
Then join behavior remains deterministic
And canceled waits do not later resume orphaned continuations

## Failure and recovery

### AT-012: Retry from transient step failure

Given a workflow with a transiently failing step
When the step fails and the configured retry/recovery path is invoked
Then the workflow continues from the documented recovery point
And prior successful steps are not repeated unless policy requires replay

Derived from:

- Workflow Core issue #829

### AT-013: Resume versus retry semantics are distinct

Given one suspended workflow and one failed workflow
When a resume command is sent to both
Then only the suspended workflow resumes
And the failed workflow requires the documented retry or recovery command

### AT-014: Failure after publish-before-commit boundary

Given a step that persists state and requests outbound event publication
When a failure occurs between local commit and external delivery attempts
Then the engine enforces the documented outbox/inbox consistency behavior
And duplicate or lost side effects do not violate acceptance guarantees

### AT-032: Step timeout policy is enforced

Given a step with a configured expected timeout
When the timeout is exceeded
Then the configured timeout policy is executed deterministically
And the step and workflow lifecycle events reflect the timeout

## Lifecycle and terminal states

### AT-015: Completed workflow emits completion lifecycle event

Given a workflow that reaches successful completion including explicit end semantics
When the workflow finishes
Then the completion lifecycle event is emitted exactly once
And terminal state is durable before consumers observe it in durable mode

Motivation:

- derived from Workflow Core issue-list title #1353

### AT-016: Terminate is consistent and final

Given a running workflow
When termination is requested
Then the workflow reaches the documented terminal state exactly once
And no further work executes afterward

Motivation:

- derived from Workflow Core issue-list title #1398

### AT-017: Host shutdown is graceful

Given a host executing workflow steps
When the host is asked to stop gracefully
Then in-flight step behavior follows documented shutdown policy
And no step is silently abandoned without corresponding runtime state

Motivation:

- derived from Workflow Core issue-list title #1402

### AT-018: Completed-instance cleanup does not break consumers

Given a provider configured to delete completed workflows
When cleanup occurs while downstream consumers or queues are processing completion-related work
Then no null-reference or orphaned-consumer behavior occurs
And cleanup obeys documented timing guarantees

Motivation:

- derived from Workflow Core issue-list title #1376

### AT-033: Terminal instance is evicted after terminal handling

Given a completed or terminated workflow instance
When terminal persistence and lifecycle handling are complete
Then the instance is removed from active memory
And remains durably queryable according to retention policy in durable mode

### AT-044: Workflow does not complete with unresolved runtime-owned waits unless policy allows it

Given a workflow with outstanding runtime-owned wait or timer records
When the workflow reaches an apparent terminal path
Then completion is rejected or unresolved work is canceled according to explicit policy

## Inspection and persistence

### AT-019: Bulk instance retrieval

Given a set of known workflow instance IDs
When they are requested in bulk
Then the engine returns them efficiently through a first-class API
Without requiring external search infrastructure

Derived from:

- Workflow Core issue #261

### AT-020: Active wait inspection

Given a workflow waiting on one or more external events
When the instance is inspected
Then active waits are queryable with event type, correlation data, branch identity, and expiration metadata

### AT-021: Queryable runtime metadata

Given persisted workflow instances
When operators query runtime metadata for reporting or troubleshooting
Then state, timestamps, wait metadata, correlation metadata, and terminal reason are queryable without deserializing opaque business payload only

Derived from:

- Workflow Core issue #377

### AT-037: Statistics query by definition and status

Given workflow instances of multiple definitions and statuses
When operational statistics are queried
Then the engine returns grouped counts by definition and status correctly

### AT-040: Ephemeral mode exposes only in-memory inspection

Given the engine is running without a persistence provider
When the process restarts
Then previous workflow instances are no longer inspectable
And the documented ephemeral-mode limitations are observable and consistent

### AT-045: History or checkpoint pressure is observable

Given long-running workflows with growing history or checkpoint volume
When operational statistics are queried
Then the engine exposes enough information to detect history growth or runtime pressure

## Definition and DSL parity

### AT-022: Code-first and serialized definitions behave identically

Given equivalent workflow definitions in code-first and serialized form
When both are executed under the same scenarios
Then they exhibit identical branching, waiting, and lifecycle behavior

Motivation:

- protects against DSL parity regressions such as issue-list concerns #1354 and #1375

## Short-running bridge behavior

### AT-023: Await short-running workflow completion explicitly

Given a workflow that completes within the synchronous execution budget
When the caller uses the engine's short-running await path
Then the final result is returned directly without pretending the workflow is durable long-running work
And the behavior differs clearly from durable start-and-return semantics

Derived from:

- Workflow Core issue #162

## Provider invariants and rehydration

### AT-024: Provider invariant suite

Given each supported storage and event provider combination
When the standard durability, wait, resume, and cleanup scenarios are executed
Then each provider satisfies the same behavioral contract
And provider-specific deviations are surfaced as unsupported capabilities, not silent semantic drift

### AT-025: Runtime version compatibility smoke suite

Given a supported .NET target and supported provider matrix
When the acceptance suite is executed on a new framework/runtime version
Then semantic regressions such as stuck queues, null references, or missing lifecycle behavior are detected before release

Motivation:

- derived from open issue patterns such as #1352 and #1403

### AT-026: Rehydration after in-memory eviction

Given a suspended workflow instance persisted to storage
When its in-memory executor is discarded and the instance is later resumed
Then the workflow resumes correctly from durable state without relying on prior memory state

Derived from:

- Orleans activation and persistence patterns

### AT-027: One logical mutator per instance

Given concurrent attempts to advance the same workflow instance
When both attempts race
Then only one mutates the instance at a time according to the documented concurrency policy

Derived from:

- Orleans single-threaded grain execution model

### AT-028: Wait versus WaitLong survivability

Given one `Wait` and one `WaitLong`
When the host restarts before wake-up
Then the documented difference in survivability between the two is observed exactly

Derived from:

- Orleans timers versus reminders distinction

### AT-029: Racing resume commands serialize

Given two matching resume attempts for the same waiting workflow instance
When both arrive concurrently
Then only one state transition is committed at a time
And the final observable result is equivalent to a valid sequential order

### AT-030: Racing branch completions serialize

Given parallel branches of the same workflow instance
When branch completion updates arrive concurrently
Then branch-state updates are committed in a serialized order
And join semantics remain deterministic

### AT-031: Crash during execution does not leak partial mutation

Given a workflow instance being advanced
When the host crashes after computation but before durable commit completes
Then rehydration restores the last committed durable state only
And no partial transition is observed

### AT-034: Idle active instance is evicted safely

Given an idle workflow instance with no runnable work
When active-instance eviction occurs
Then the in-memory instance is removed
And later resume or inspection still works from durable state in durable mode

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

### AT-038: Eviction never causes duplicate active mutators

Given an instance being evicted and reactivated under concurrent pressure
When activation ownership changes
Then at most one logical mutator exists for the instance at any time

### AT-041: Instance is bound to definition version

Given a workflow instance started from definition version N
When newer versions of the definition are deployed
Then the instance remains bound to version N according to documented versioning rules

### AT-042: Incompatible definition change does not silently corrupt durable instance

Given a durable workflow instance paused under an older definition version
When an incompatible newer version is deployed
Then the engine either continues safely under version rules or fails with explicit versioning diagnostics
And it never silently resumes with corrupted semantics

## Priority order for first implementation

The first acceptance tests to implement should be:

1. AT-001 Idempotent start by client key
2. AT-003 Immediate resume after durable wait registration
3. AT-005 Different waits in parallel branches
4. AT-008 Parallel join executes once
5. AT-012 Retry from transient step failure
6. AT-015 Completed workflow emits completion lifecycle event
7. AT-020 Active wait inspection
8. AT-024 Provider invariant suite
9. AT-026 Rehydration after in-memory eviction
10. AT-027 One logical mutator per instance
11. AT-029 Racing resume commands serialize
12. AT-031 Crash during execution does not leak partial mutation
13. AT-032 Step timeout policy is enforced
14. AT-034 Idle active instance is evicted safely
15. AT-036 Stuck workflow detection emits observable signal
16. AT-039 Durable-only wait rejected in ephemeral mode
17. AT-041 Instance is bound to definition version
18. AT-043 Timer and event race resolves deterministically
