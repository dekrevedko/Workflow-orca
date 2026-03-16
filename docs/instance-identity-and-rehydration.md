# Instance Identity, Rehydration, and Serialized Execution

Reviewed on March 14, 2026.

## Purpose

This document defines three tightly related ideas for OrcaCore:

- workflow instance identity
- workflow instance rehydration
- serialized execution guarantee per workflow instance

These concepts should be treated as core runtime semantics, not implementation details.

## Short answer

Yes, OrcaCore can and should aim to provide an Orleans-like guarantee that one workflow instance is logically single-threaded.

That does not mean the whole engine is single-threaded. It means:

- each workflow instance has one stable logical identity
- each workflow instance has at most one logical mutator at a time
- concurrent attempts to advance the same instance are serialized or rejected according to policy
- the instance can be rehydrated from durable state whenever execution resumes

## 1. Instance identity

A workflow instance must have a stable logical identity that is independent from:

- the current host process
- the current in-memory runtime object
- the current thread or task
- whether the instance is active or waiting

### Required identifiers

At minimum, a workflow instance should carry:

- `InstanceId`: globally unique stable identity for the workflow instance
- `DefinitionId`: workflow definition identity
- `DefinitionVersion`: definition version bound to the instance
- `ExecutionEpoch` or equivalent monotonic version for optimistic concurrency and resume safety

Optional but likely useful:

- `ClientReference` or `StartIdempotencyKey`
- `ParentInstanceId` for child workflows
- `RootInstanceId` for workflow trees

### Design rule

`InstanceId` identifies the logical workflow execution, not its current activation.

That means the in-memory executor is disposable. It can be reconstructed from durable state at any time.

## 2. Instance state shape

A workflow instance consists of two state categories.

### Runtime state

Engine-owned orchestration data, such as:

- lifecycle status
- current execution point
- branch states
- active waits / subscriptions
- correlation metadata
- timestamps
- failure details
- history / checkpoints
- current execution lease or version token

### Business state

Workflow-owned application data, such as:

- order / request / approval data
- results of completed business steps
- state accumulated for later decisions

### Design rule

Rehydration must reconstruct execution from both runtime state and business state.

Runtime state must remain queryable without deserializing business payload only.

## 3. Rehydration model

Rehydration is the process of reconstructing an executable workflow instance from durable state.

### Rehydration must happen for these reasons

- host restart
- process crash recovery
- durable wait resume
- in-memory eviction due to idleness or resource pressure
- execution transferred to another node in a future distributed mode

### Rehydration inputs

At minimum:

- persisted runtime state
- persisted business state
- definition identity and version
- any active wait or subscription records
- any required checkpoint or history data

### Rehydration outputs

- an in-memory execution object ready to resume from the current durable point
- no reliance on previous in-memory locals, thread state, or object identity

### Design rules

- rehydration must be deterministic for the same durable inputs
- rehydration must not require prior in-memory references
- resuming a waiting instance must be possible after complete host loss

## 4. Serialized execution guarantee

### Goal

OrcaCore should guarantee serialized execution per workflow instance by default.

In plain terms:

- one workflow instance behaves like one logically single-threaded state machine
- multiple instances may run concurrently
- one instance should not be mutated concurrently by multiple workers

This is one of the best properties to borrow from Orleans.

### Why this matters

This drastically reduces complexity around:

- branch updates
- wait registration and resume
- duplicate event races
- lifecycle transitions
- history/checkpoint ordering
- business-state consistency

### Guarantee statement

For a given `InstanceId`, observable state transitions must appear as a single serial order.

Even if multiple commands or events race, the outcome for that instance must be equivalent to some valid sequential ordering accepted by the runtime.

This is the right meaning of serialized execution here.

## 5. What this guarantee does and does not mean

### It means

- only one logical mutation is committed for an instance at a time
- conflicting concurrent resume attempts are detected and resolved
- branch completion updates are committed in a stable serial order
- handlers may be async without allowing concurrent mutation of the same instance

### It does not mean

- only one OS thread is ever involved
- only one task exists in the host process
- separate workflow instances cannot run concurrently
- user step code becomes magically thread-safe for shared external resources

This is an execution-serialization guarantee, not a physical-thread guarantee.

## 6. Ways to implement serialized execution

The guarantee is a semantic target. Different storage/hosting models can enforce it differently.

### Option A: In-memory per-instance mailbox or lock

Good for:

- single-node in-memory mode
- local development

Approach:

- route all work for the same instance through one in-memory queue/lock
- process messages/events/commands sequentially

Pros:

- simple mental model
- close to Orleans actor behavior on one node

Cons:

- not sufficient by itself for crash recovery or multi-node deployment

### Option B: Optimistic concurrency on persisted instance version

Good for:

- provider-agnostic durable runtime
- single-node and multi-node capable designs

Approach:

- every instance has a version/epoch
- load instance state
- compute next state
- write back only if version still matches
- on conflict, retry or reject according to policy

Pros:

- portable across many databases
- strong fit for durable state machines

Cons:

- conflict handling must be designed carefully
- higher retry churn under contention

### Option C: Durable execution lease per instance

Good for:

- future clustered hosting

Approach:

- one worker acquires a time-bound lease for an instance
- only lease owner may mutate until lease expires or is released

Pros:

- stronger ownership model across hosts

Cons:

- lease renewal and failure handling add complexity

## Recommended approach

Use a layered policy:

- local in-memory serialization inside one host when possible
- durable optimistic concurrency as the minimum cross-provider correctness boundary
- optional lease model later if multi-node execution becomes a goal

That gives an Orleans-like developer experience without requiring the whole Orleans runtime model.

## 7. Reentrancy policy

OrcaCore should default to non-reentrant instance execution.

That means:

- if an instance is already executing, a new event/command for the same instance is queued, retried, or rejected according to policy
- the runtime does not allow arbitrary interleaving of state mutation for the same instance by default

Future reentrancy should be treated as an advanced feature, if allowed at all.

## 8. Interaction with parallel branches

Serialized instance execution does not forbid parallel workflow branches conceptually.

It means branch progress is committed through one serialized instance state machine.

So:

- branches may represent independent logical paths
- branch work may wait on independent external events
- branch completions may arrive in any order
- the workflow instance still commits branch-state updates one at a time in serial order

This is the safest model.

## 9. Interaction with `Wait` and `WaitLong`

The serialized execution guarantee fits both wait types.

### `Wait`

- may use lighter local scheduling semantics
- resume still must re-enter the instance through the same serialized mutation path

### `WaitLong`

- uses durable wait/subscription records
- on resume, the instance is rehydrated if needed
- resume must still commit through the same serialized mutation path

So both waits share the same single-mutator rule even if their durability differs.

## 10. Failure handling under serialized execution

The runtime must define what happens when two resume attempts race or when a worker crashes mid-advance.

Minimum rules:

- state transitions are committed atomically per accepted mutation
- failed mutation attempts must not partially advance visible state
- concurrency conflicts must produce retry, no-op, or rejection according to explicit policy
- rehydration after crash must resume from the last durable checkpoint, not from partial in-memory progress

## 11. Acceptance criteria

The existing acceptance matrix already contains the most relevant tests:

- `AT-026` Rehydration after in-memory eviction
- `AT-027` One logical mutator per instance
- `AT-028` `Wait` versus `WaitLong` survivability

Additional acceptance tests should be added:

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

## 12. Final position

OrcaCore should explicitly guarantee serialized execution per workflow instance by default.

That is one of the strongest design decisions available because it makes the engine easier to reason about, easier to test, and far less vulnerable to the kinds of branch/wait race conditions seen in competing systems.

This should be documented as a runtime guarantee, enforced through provider-aware concurrency control, and protected by acceptance tests from the beginning.
