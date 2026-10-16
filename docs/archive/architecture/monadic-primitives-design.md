# Monadic Primitives Design

Reviewed on April 10, 2026.

## Purpose

This document proposes a narrow functional-design layer for OrcaCore based on only three concrete abstractions:

- `Result<T>`
- `Option<T>`
- `Validation<T>`

The goal is to gain the design benefits of monadic composition where it genuinely improves the engine, without turning OrcaCore into a heavily functional codebase.

## Short Position

Use monadic ideas selectively.

Do:

- make success, absence, and validation explicit
- reduce exception-driven control flow
- improve command, routing, builder, and persistence contracts

Do not:

- make the entire public API monadic-heavy
- build a full functional programming framework
- force category-theory abstractions into ordinary engine code where simple objects are clearer

## Why This Is Worth Doing

OrcaCore has several recurring kinds of outcomes:

1. an operation succeeded or failed
2. a value may or may not exist, and absence is not an error
3. a definition/configuration may have many validation errors that should be collected

These currently tend to show up as combinations of:

- exceptions
- `null`
- `TryGet...`
- `bool + out`
- early throw-on-first-error validation

That is workable, but it makes some parts of the engine noisier and less explicit than they should be.

The proposed abstractions map cleanly to those three cases:

- `Result<T>` for success/failure
- `Option<T>` for present/absent
- `Validation<T>` for accumulated build/authoring errors

## Proposed Primitives

### 1. `Result<T>`

Purpose:

- represent operation success or failure explicitly

Use when:

- failure is meaningful and expected at the contract level
- caller should handle failure explicitly
- throwing exceptions for control flow is undesirable

Typical shape:

```csharp
public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public WorkflowEngineException? Error { get; }
}
```

Recommended helpers:

- `Success(T value)`
- `Failure(WorkflowEngineException error)`
- `Map(...)`
- `Bind(...)`
- `Match(...)`

### 2. `Option<T>`

Purpose:

- represent “value exists” vs “value absent” without implying failure

Use when:

- absence is normal
- `null` would be ambiguous
- you want to separate “not found” from “failure”

Typical shape:

```csharp
public readonly record struct Option<T>
{
    public bool HasValue { get; }
    public T? Value { get; }
}
```

Recommended helpers:

- `Some(T value)`
- `None`
- `Map(...)`
- `Bind(...)`
- `Match(...)`
- `GetValueOrDefault(...)`

### 3. `Validation<T>`

Purpose:

- represent successful build/compilation/validation or a collected list of validation errors

Use when:

- multiple errors should be returned together
- authoring/build/definition validation is the main concern
- fail-fast is a worse user experience

Typical shape:

```csharp
public sealed record Validation<T>(
    T? Value,
    IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
```

Recommended helpers:

- `Valid(T value)`
- `Invalid(params ValidationError[] errors)`
- combine/merge operations
- projection helpers for building immutable definitions

## Where Each Primitive Should Be Used

## `Result<T>` Use Cases

### A. Event routing

Good fit:

- resolve correlation
- resolve instance target
- resolve child group join condition

Instead of:

- throwing for no-match and ambiguity in ordinary control flow

Use:

```csharp
Result<ResolvedInstanceTarget> ResolveCorrelation(...);
```

Benefits:

- explicit routing outcome
- easier composition in command handlers
- fewer exception-driven branches

### B. Command handling in the event-driven engine

Good fit:

- command acceptance
- aggregate decision
- durable commit outcome

Example:

```csharp
Result<WorkflowDecision> Decide(
    WorkflowCommand command,
    WorkflowAggregate aggregate);
```

Or:

```csharp
Result<CommitResult> Commit(WorkflowDecision decision);
```

Benefits:

- clearer write-model flow
- easier distinction between:
  - business rejection
  - routing error
  - store failure

### C. Durable store interactions

Good fit where failures are expected and should not always be exceptional at the composition boundary:

- expected-version append
- version mismatch check
- projection write result

Be careful:

- low-level store implementation may still throw
- `Result<T>` is more useful at engine/service boundaries than deep inside every provider call

### D. Child workflow orchestration

Good fit:

- child scheduling result
- child join evaluation
- child completion/failure application

Example:

```csharp
Result<ChildJoinOutcome> TryAdvanceChildGroup(...);
```

## `Option<T>` Use Cases

### A. Queries and lookups

Good fit:

- summary projection may or may not exist
- checkpoint may or may not exist
- buffered event may or may not exist
- active wait may or may not exist

Examples:

```csharp
Option<PrototypeCheckpointState> TryLoadCheckpoint(...);
Option<WaitRecord> TryFindMatchingWait(...);
Option<PendingEvent> TryFindBufferedEvent(...);
```

Benefits:

- avoids nullable ambiguity
- cleaner than `TryGet...` in many composition paths

### B. Optional runtime relationships

Good fit:

- active parallel group
- child workflow group
- current resumed event
- parent workflow link

### C. Optional projections in the event-driven design

Good fit:

- active wait projection
- child group projection
- instance summary projection

## `Validation<T>` Use Cases

### A. Workflow builder compilation

This is the strongest current use case.

Good fit:

- missing `Init`
- missing `End`
- duplicate branch ids
- invalid child workflow configuration
- unsupported policy combinations
- illegal durable/ephemeral feature mix

Instead of:

- throw on first error

Use:

```csharp
Validation<WorkflowDefinition<TState>> Build();
```

Or:

```csharp
Validation<CompiledWorkflowPlan<TState>> Compile();
```

Benefits:

- much better authoring diagnostics
- easier future DSL/build-time tooling

### B. Child workflow configuration

Good fit:

- invalid `JoinPolicy` / `FailurePolicy` combinations
- missing partitioner/body/child definition
- illegal quick-engine usage of durable-only child semantics

### C. Event-driven command/event catalog validation

Good fit:

- duplicate event ids in configuration
- incompatible projection registration
- invalid version binding rules

## Where These Primitives Should Not Be Used

Do not use them everywhere.

### Avoid on the public happy-path API if it harms ergonomics

For example:

- `Start(...)`
- `RaiseEvent(...)`
- `GetStateAsync<T>()`

These may still reasonably throw engine exceptions rather than force every consumer into chained `Result<T>` handling.

The internal architecture can use `Result<T>` even if the public facade chooses exception-based API boundaries.

### Avoid turning every internal helper into nested wrappers

Avoid designs like:

```csharp
Task<Result<Option<T>>>
```

unless it is truly the cleanest expression of the semantics.

Prefer:

- split methods
- helper adapters
- simpler service boundaries

### Avoid `Validation<T>` for runtime operational failures

`Validation<T>` is for:

- compile time
- build time
- configuration time

It is not for:

- store unavailable
- event duplicate at runtime
- child workflow timed out

Those belong to `Result<T>` or exceptions.

## Suggested Project Boundaries

### Public API

Keep public API pragmatic.

Allowed approach:

- public API may continue to throw domain-specific exceptions
- internal implementation may use `Result<T>` / `Option<T>` / `Validation<T>`

This gives:

- cleaner internals
- familiar external API

### Internal engine/services

This is the preferred main use area.

Examples:

- routing
- durable command handling
- projection application
- builder compilation
- child workflow orchestration

### Provider boundaries

Use carefully.

Good:

- return `Option<T>` from lookup-oriented provider methods
- return `Result<T>` from higher-level provider service wrappers

Avoid:

- making every lowest-level provider method monadic just for style

## Example Designs

## Example 1: Correlation Resolution

Instead of:

```csharp
string ResolveExactlyOne(...); // throws for no-match or ambiguous
```

Use internally:

```csharp
Result<ResolvedInstanceTarget> ResolveCorrelation(...);
```

Possible outcomes:

- success
- no active wait
- ambiguous correlation
- store unavailable

## Example 2: Checkpoint Lookup

Instead of:

```csharp
PrototypeCheckpointState? LoadCheckpoint(...);
```

Use:

```csharp
Option<PrototypeCheckpointState> TryLoadCheckpoint(...);
```

This keeps “not found” distinct from “failure to load.”

## Example 3: Builder Validation

Instead of:

```csharp
Build() // throws on first invalid condition
```

Use:

```csharp
Validation<WorkflowDefinition<TState>> BuildValidated();
```

Or:

```csharp
Build() // throws only after collecting and formatting all validation errors
```

This allows a friendlier external API while still using `Validation<T>` internally.

## Example 4: Event-Driven Command Handling

```csharp
Option<Checkpoint> checkpoint = store.TryLoadCheckpoint(instanceId);
Result<WorkflowAggregate> aggregate = AggregateLoader.Load(checkpoint, streamTail);
Result<WorkflowDecision> decision = aggregate.Bind(a => a.Decide(command));
Result<CommitResult> committed = decision.Bind(store.Commit);
```

This is one of the cleanest places for `Result<T>`.

## Interaction With Exceptions

This design does not eliminate exceptions.

Use exceptions for:

- truly exceptional/unrecoverable situations
- public API boundaries where exception-based usage is clearer
- programming errors / invariant violations

Use `Result<T>` for:

- expected operational outcomes
- explicit engine-level failure contracts

Use `Option<T>` for:

- absence without failure

Use `Validation<T>` for:

- collected authoring/configuration errors

## Recommended Adoption Order

### 1. `Validation<T>` in builders

Lowest risk and highest immediate UX value.

### 2. `Option<T>` in internal query and lookup paths

Simple and easy to reason about.

### 3. `Result<T>` in event-driven prototype command handling

Best strategic value with the least disruption to the current public API.

### 4. `Result<T>` in routing and child orchestration

Useful once the event-driven engine and child workflow orchestration evolve.

## Honest Assessment

Benefits:

- clearer internal contracts
- less hidden control flow
- better builder diagnostics
- cleaner event-driven command pipeline
- stronger distinction between absence and failure

Costs:

- more types to learn
- risk of overusing wrappers
- can become unreadable if nested carelessly
- may feel unusual to some .NET contributors

So the right strategy is:

- use only these three abstractions
- use them mostly internally
- keep the external API pragmatic

## Recommendation

Adopt:

- `Result<T>`
- `Option<T>`
- `Validation<T>`

But adopt them narrowly and intentionally:

- `Validation<T>` for authoring/build/compile paths
- `Option<T>` for lookups and optional runtime relationships
- `Result<T>` for command/routing/commit decision paths, especially in the event-driven engine

Do not go beyond this into a broader functional abstraction stack unless later evidence shows a real need.
