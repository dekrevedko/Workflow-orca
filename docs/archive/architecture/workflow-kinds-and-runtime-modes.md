# Workflow Kinds and Runtime Modes

Reviewed on March 14, 2026.

## Purpose

This document records the current design decision that OrcaCore has two orthogonal axes:

- definition semantics
- execution mode

It also defines:

- why this separation exists
- how the four combinations differ
- which infrastructure/management capabilities belong to each combination
- which step decorators or policy annotations are likely needed

## Design decisions

### DD-001: Separate workflow semantics from execution mode

Decision:

OrcaCore should model two independent axes:

- definition semantics:
  - regular workflow
  - saga workflow
- execution mode:
  - ephemeral
  - durable

Why:

- saga introduces distinct semantic meaning around compensation, failure, timeout, and partial success
- durability changes runtime guarantees, not business semantics
- combining them into one hierarchy would blur important distinctions
- two axes make the support matrix explicit and reduce contradictions

### DD-002: Saga is a separate semantic definition kind

Decision:

Saga should be represented as a separate definition kind, not merely a regular workflow with optional compensation flags.

Why:

- compensation is not a decoration on ordinary control flow; it changes failure semantics
- saga-specific timeout, retry, rollback, and completion rules are easier to reason about when modeled explicitly
- this keeps regular workflow APIs simpler

### DD-003: Durable and ephemeral should be separated at the API surface where possible

Decision:

Durable-only features should be unavailable from ephemeral-facing APIs whenever practical, not only rejected at runtime.

Why:

- reduces invalid states and runtime guard logic
- makes feature support clearer to users
- improves discoverability of what is actually safe in each mode
- still allows fail-fast runtime checks for dynamic or misconfigured cases

### DD-004: Ephemeral saga is allowed but limited

Decision:

Ephemeral saga is allowed as a valid combination, but it has reduced operational value and reduced guarantees.

Why:

- compensation and scoped rollback can still be useful inside one process lifetime
- some users may want saga semantics without durable storage
- but restart recovery, durable compensation tracking, and post-crash inspection are unavailable

## The four combinations

### 1. Ephemeral regular workflow

Meaning:

A normal in-memory workflow with no persistence provider.

Strengths:

- simplest runtime mode
- minimal infrastructure requirements
- fast local execution

Limits:

- no post-restart recovery
- no durable wait
- no durable history
- no durable inspection

### 2. Durable regular workflow

Meaning:

A normal workflow with persistence and durable runtime guarantees.

Strengths:

- durable wait/resume
- restart recovery
- durable inspection and history/checkpoints
- long-running coordination

Limits:

- higher infrastructure complexity
- stronger versioning and consistency requirements

### 3. Ephemeral saga

Meaning:

A saga-definition workflow running without persistence.

Strengths:

- supports compensation semantics inside one process lifetime
- useful for local or short-lived orchestrations that still need rollback behavior

Limits:

- no durable recovery after crash/restart
- no durable compensation tracking
- reduced operator visibility after process loss
- should be treated as limited-support mode, not a strong reliability story

### 4. Durable saga

Meaning:

A saga-definition workflow with persistence and durable runtime guarantees.

Strengths:

- full long-running compensation-aware coordination
- durable recovery and inspection
- most complete saga behavior

Limits:

- highest design and infrastructure complexity
- strongest requirements around consistency, idempotency, and versioning

## Shared control-flow/infrastructure steps

These steps are baseline infrastructure primitives for both regular workflow and saga definitions unless otherwise noted.

- `Init`
- `End`
- `If`
- `While`
- `Parallel`
- `WhenAll`
- `WhenFirst`
- `Wait`
- `WaitLong`

## Additional primitives by semantic kind

### Regular workflow additions

Likely useful but not saga-specific:

- `Delay` or `Timer`
- `ChildWorkflow`
- `Publish`
- `Cancel`

### Saga-specific additions

Strongly recommended:

- `CompensationScope`
- `Compensate`
- `Try`
- `Catch`
- `Finally`
- `ChildWorkflow`

Why these are saga-specific or saga-critical:

- saga needs explicit rollback boundaries
- saga needs clear forward vs compensating behavior
- saga failure semantics need scoped handling, not only global failure

## Management/infrastructure capabilities matrix

This matrix is about runtime/management capabilities, not only step availability.

### Ephemeral regular workflow

Supported:

- `Start`
- `GetInstance` while process is alive
- `ListInstances` while process is alive
- `RaiseEvent`
- `CancelInstance`
- `Terminate`
- `GetActiveWaits` while process is alive
- basic lifecycle events in-memory
- in-memory statistics

Not supported or limited:

- durable `WaitLong`
- post-restart `GetInstance`
- durable history retrieval
- purge/archive retention semantics
- durable pause/resume across restart

### Durable regular workflow

Supported:

- `Start`
- idempotent start or `StartOrGet`
- `GetInstance`
- `ListInstances`
- `GetHistory`
- `GetActiveWaits`
- `RaiseEvent`
- `Pause`
- `Resume`
- `CancelInstance`
- `Terminate`
- `RetryInstance`
- `Archive`
- `Purge`
- durable lifecycle events and operational statistics

### Ephemeral saga

Supported:

- all ephemeral regular workflow management operations
- compensation-aware failure handling during live process execution
- compensation status inspection while process is alive
- retry/terminate/cancel inside current process lifetime

Limited:

- no durable compensation recovery
- no post-restart saga inspection
- no durable operator remediation after process loss

### Durable saga

Supported:

- all durable regular workflow management operations
- durable compensation tracking
- compensation-aware retry / terminate / recovery actions
- operator inspection of forward and compensating progress
- long-running timeout and stuck-saga handling

## Step support matrix by combination

### `Wait`

- Ephemeral regular: supported
- Durable regular: supported
- Ephemeral saga: supported
- Durable saga: supported

### `WaitLong`

- Ephemeral regular: not supported by API
- Durable regular: supported
- Ephemeral saga: not supported by API
- Durable saga: supported

### `Parallel`

- all combinations: supported
- semantics remain serialized at parent-instance commit boundary

### `ChildWorkflow`

- Ephemeral regular: optional/future
- Durable regular: strongly recommended future feature
- Ephemeral saga: optional but useful
- Durable saga: strongly recommended

### `CompensationScope` / `Compensate`

- Ephemeral regular: not part of normal workflow API
- Durable regular: not part of normal workflow API
- Ephemeral saga: supported
- Durable saga: supported

### Durable timers

- Ephemeral regular: not supported as durable primitive
- Durable regular: supported
- Ephemeral saga: not supported as durable primitive
- Durable saga: supported

## API surface guidance

To reflect DD-003, the public API should likely separate mode-specific builders or runtime entry points.

Possible shape:

- definition kind axis:
  - `WorkflowDefinition`
  - `SagaDefinition`
- execution mode axis:
  - `IEphemeralWorkflowRuntime`
  - `IDurableWorkflowRuntime`
  - `IEphemeralSagaRuntime`
  - `IDurableSagaRuntime`

Or alternatively:

- common base runtime
- mode-specific feature interfaces layered on top

Example:

- common: start, cancel, terminate, inspect live state
- durable-only: pause, durable resume, purge, archive, durable history, `WaitLong`

The important rule is not the exact names. The important rule is that durable-only features should not be naturally discoverable on ephemeral-only APIs.

## Step decorators / policy annotations

These are not steps themselves. They are modifiers attached to steps, scopes, branches, or workflow definitions.

### Recommended decorators

- `RetryPolicy`
- `TimeoutPolicy`
- `CompensationPolicy`
- `CancellationPolicy`
- `IdempotencyPolicy`
- `ConcurrencyPolicy`
- `DurabilityRequirement`
- `VisibilityPolicy`
- `ErrorHandlingPolicy`

### Suggested targets

#### Step-level decorators

- retry
- timeout
- compensation handler binding
- idempotency
- visibility / telemetry tags

#### Scope-level decorators

- compensation scope behavior
- branch cancellation policy
- timeout for a whole parallel block or try block
- failure escalation policy

#### Workflow-definition-level decorators

- default retry behavior
- default timeout behavior
- default lifecycle event publication behavior
- retention policy in durable mode
- stuck-detection thresholds

## Recommended first policy set

To keep the first version manageable, the most valuable decorators are:

- `RetryPolicy`
- `TimeoutPolicy`
- `CancellationPolicy`
- `CompensationPolicy`
- `IdempotencyPolicy`

## Good and bad boundaries

### Good boundary

- steps express workflow structure or business action
- decorators express policy
- runtime commands express management
- saga type expresses semantic model
- durable/ephemeral runtime type expresses guarantee level

### Bad boundary

- turning every policy into a step
- hiding durable-only restrictions in late runtime failures only
- treating saga as merely a regular workflow with a boolean flag
- mixing operator commands with workflow graph steps

## Open questions

- Should child workflows be shared across regular workflow and saga from phase 1, or introduced later?
- Should `Delay` and timer semantics be represented as separate public primitives or as `Wait` variants?
- How many durable-only operations should be removed at compile time versus rejected at runtime?
- Should ephemeral saga be fully supported or explicitly labeled limited/advanced?
- Should decorators be attributes, fluent APIs, metadata objects, or a hybrid?

## Final position

OrcaCore should now be treated as having:

- two orthogonal axes
- separate workflow and saga semantics
- API-level separation between ephemeral and durable where possible
- policy decorators distinct from steps
- management commands distinct from workflow graph primitives

This gives the cleanest path to defining semantics without mixing unrelated concerns.
