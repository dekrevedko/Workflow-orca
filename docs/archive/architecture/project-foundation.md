# Project Foundation

## Problem statement

The engine should allow an application to define workflows as reusable steps and execute them asynchronously. Some steps complete immediately, some branch or spawn parallel work, and some suspend execution until an external event is received. If a persistence provider is configured, suspended workflows should be resumable after restart, which implies durable runtime state and durable event correlation.

## Desired capabilities

### Workflow model

- Reusable step building blocks.
- Infrastructure steps for control flow and synchronization.
- User-defined business steps.
- Async-first execution contracts.
- A workflow instance must carry both runtime state and business state.

### Runtime behavior

- Start a workflow instance from a definition.
- Execute ready steps.
- Suspend on waits.
- Resume when a matching event arrives.
- Support parallel branches and synchronization points.
- Persist progress after meaningful state transitions when persistence is enabled.

### Infrastructure abstraction

- Optional storage provider abstraction for workflow state and runtime metadata.
- Event provider abstraction for publish/subscribe or queue-based delivery.
- No hard dependency on a specific database or broker.

## Runtime modes

### Ephemeral mode

When no persistence provider is configured, the engine runs entirely in memory.

Properties:

- workflow instances are in-memory only
- process restart loses workflow instances
- durable rehydration is unavailable
- `WaitLong` is unavailable or rejected
- short-lived execution and short waits are still supported

### Durable mode

When a persistence provider is configured, the engine can support durable suspension, rehydration, host-restart recovery, retention, and long-running workflows.

## Candidate architecture

### Layer 1: Abstractions

Contracts for definitions, steps, execution context, persistence, events, and time.

### Layer 2: Runtime

The orchestration engine that interprets definitions, schedules executable steps, tracks branch state, and decides when to persist or resume.

### Layer 3: Providers

Adapters for optional persistence and messaging implementations such as relational databases, document databases, SQS, RabbitMQ, or Kafka.

## Workflow instance model

A workflow instance should contain two different categories of state.

### 1. Runtime state

Runtime state is owned by the engine and describes orchestration progress.

Examples:

- workflow instance identifier
- definition identifier and version
- current execution point
- workflow lifecycle status
- branch state
- active waits / subscriptions
- correlation metadata
- timestamps
- failure details
- execution history or checkpoints

In durable mode this state must be queryable and durable. It should not be hidden inside a single opaque business payload blob.

### 2. Business state

Business state is owned by the workflow definition and step logic. It is the workflow's application data.

Examples:

- order data
- approval results
- accumulated outputs from prior steps
- local variables needed by later steps
- domain-specific correlation values

This state should be serializable and deserializable as part of a workflow instance when persistence exists. A typed model such as `TState` is a likely direction, although the exact API is still open.

## Persistence guidance

Persistence is optional, but its absence changes the runtime guarantees.

Better practice for OrcaCore is:

- support an explicit ephemeral mode when no persistence provider is configured
- support an explicit durable mode when a persistence provider is configured
- fail fast when a workflow requests durable-only behavior in ephemeral mode
- in durable mode, persist business state as serialized workflow data when appropriate
- in durable mode, persist runtime metadata in first-class queryable fields or related records
- in durable mode, keep waits, branch state, correlation keys, and lifecycle state queryable without deserializing business payload only

This conclusion is reinforced by competitor research, especially Workflow Core issue-pattern review, where opaque JSON persistence created operational and reporting problems.

## Execution model draft

Each step execution should produce a result object rather than directly mutating the engine. That result can express:

- `Completed`
- `Failed`
- `Branch`
- `SpawnParallel`
- `WaitForEvent`
- `PublishEvents`

This keeps step authors focused on intent while the runtime remains responsible for orchestration and, when enabled, persistence.

## Important design constraints

- All public execution paths should be async.
- Business state should be serializable and deserializable across persistence boundaries when persistence exists.
- Event correlation must work across process boundaries when resume from external signals is supported.
- Providers must be replaceable without changing workflow definitions.
- The engine should support deterministic reasoning about branch completion.
- Durable rehydration and post-restart resume require durable mode.

## Open design decisions

1. Whether definitions are code-first only, or later support a serialized format.
2. Whether long-running waits should use the same primitive as short waits with different provider behavior.
3. Whether business state should be strongly typed, loosely typed, or a hybrid model.
4. Whether retries, compensation, and saga-like behavior are first-class in the initial runtime.
5. How the feature surface should differ between ephemeral mode and durable mode.
