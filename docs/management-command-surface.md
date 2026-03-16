# Management Command Surface

Reviewed on March 15, 2026.

## Purpose

This document defines the current management command surface for OrcaCore.

It assesses each candidate command for:

- semantic usefulness
- applicability to regular workflow and saga
- applicability to ephemeral and durable modes
- whether it should be core, limited, renamed, or deferred

Management commands are runtime/operator commands. They are not workflow graph steps.

## Design decisions

### DD-005: Management commands are separate from workflow steps

Decision:

Commands such as pause, resume, purge, retry, query, and statistics belong to the runtime management surface, not the workflow graph.

Why:

- they are operator/runtime concerns
- they apply to instances, not to control-flow structure
- mixing them into workflow steps would confuse semantic design

### DD-006: Durable-only management commands should be hidden from ephemeral-facing APIs where practical

Decision:

If a command only makes sense with persistence, it should be absent from ephemeral-facing management APIs where possible.

Why:

- reduces invalid operations
- avoids fake support for durable semantics in ephemeral mode
- makes support matrices easier to understand

### DD-007: Management API should follow a fluent LINQ-like model

Decision:

Management APIs should be fluent and composable, with explicit separation between:

- scope selection
- filtering
- terminal query operations
- terminal command operations

Why:

- produces a consistent mental model across engine-wide, definition-scoped, and instance-scoped management
- avoids method-name explosion such as `PauseAll`, `PauseRunning`, `TerminateByDefinition`
- makes command semantics easier to compose and reason about
- keeps selection semantics explicit before destructive actions are invoked

### DD-008: `Where(...)` is the canonical filtering mechanism

Decision:

The primary filtering mechanism should be `Where(...)`, not a large family of named filter methods such as `WhereStatus(...)`.

Why:

- keeps the API surface smaller and more consistent
- aligns with LINQ-like expectations
- avoids endless special-purpose filter methods
- preserves one mental model for selection before terminal actions

Named helpers, if they exist later, should be treated as convenience sugar rather than the foundational model.

### DD-009: Management predicates must be constrained and translatable

Decision:

The public API may look LINQ-like, but management filters must be constrained to a safe, translatable subset.

Why:

- durable mode cannot safely promise arbitrary .NET predicate execution against persisted state
- provider-backed filtering requires queryable/translatable semantics
- unrestricted predicates would create ambiguous runtime behavior and provider inconsistency

Implication:

`Where(...)` should likely use expression-style predicates over a defined query model, not arbitrary delegates with unrestricted runtime logic.

## Fluent management model

### Core shape

Management operations should read like:

1. choose scope
2. optionally filter
3. perform a terminal query or terminal command

Examples:

- `WorkflowEngine.Where(x => x.Status == Running).Pause()`
- `WorkflowEngine<PriceUpdate>.Where(x => x.Status == Running).Pause()`
- `WorkflowEngine.Instance(id).RaiseEvent(evt)`
- `WorkflowEngine.All().Where(x => x.IsStuck).Statistics()`

### Scope kinds

#### Engine-wide scope

Examples:

- `WorkflowEngine.All()`
- `WorkflowEngine.Where(x => x.Status == Running)`

Meaning:

Selection across all visible workflow instances.

#### Definition-scoped scope

Examples:

- `WorkflowEngine<PriceUpdate>.All()`
- `WorkflowEngine<PriceUpdate>.Where(x => x.Status == Running)`

Meaning:

Selection across instances of one definition type.

#### Instance scope

Examples:

- `WorkflowEngine.Instance(id)`

Meaning:

Selection of one concrete workflow instance.

## Semantics of fluent operations

### Selection/filtering operations

Examples:

- `All()`
- `Where(...)`

These should not perform side effects.

### Terminal query operations

Examples:

- `List()`
- `Count()`
- `Statistics()`
- `GetHistory()`
- `GetActiveWaits()`
- `GetLifecycleEvents()`

These should observe, not mutate.

### Terminal command operations

Examples:

- `Pause()`
- `Resume()`
- `RaiseEvent(...)`
- `Cancel()`
- `Terminate()`
- `Retry()`
- `Archive()`
- `Purge()`

These should mutate runtime state or execute operator actions.

## Important semantic rules

### Rule 1: `All()` always means instances in the current scope

Examples:

- `WorkflowEngine.All()` = all instances across the engine
- `WorkflowEngine<T>.All()` = all instances of definition `T`

It must not mean "all definitions" in one place and "all instances" in another.

### Rule 2: instance scope should stay direct

Examples:

- `WorkflowEngine.Instance(id).Pause()`
- `WorkflowEngine.Instance(id).GetHistory()`

This avoids forcing one-instance operations through list-style query chains.

### Rule 3: query chains and command chains should share selection semantics

If a user can write:

- `WorkflowEngine<T>.Where(x => x.Status == Running).List()`

then they should be able to write:

- `WorkflowEngine<T>.Where(x => x.Status == Running).Pause()`

subject to mode/capability support.

### Rule 4: destructive broad commands need explicit safety semantics

Examples:

- `WorkflowEngine.All().Terminate()`
- `WorkflowEngine<T>.All().Purge()`

These commands are valid, but the API must define safety rules, confirmation requirements, or explicit force semantics where appropriate.

### Rule 5: `Where(...)` must stay semantically safe

Good examples:

- `Where(x => x.Status == Running)`
- `Where(x => x.DefinitionVersion == version)`
- `Where(x => x.CreatedAt < cutoff)`

Risky examples that should not define the public semantics:

- `Where(x => MyCustomMethod(x))`
- `Where(x => DateTime.Now > x.CreatedAt.AddDays(1))`
- `Where(x => ExternalService.IsEligible(x.Id))`

## Candidate assessment

### 1. `Start`

Status:

- core

Fluent shape:

- `WorkflowEngine<T>.Start(input)`
- `WorkflowEngine<T>.Start(options)`

Why:

- starting is definition-scoped rather than query-scoped
- it is still part of management, but not a list-selection terminal action

### 2. `StartOrGet`

Status:

- core in durable mode
- limited / optional in ephemeral mode

Fluent shape:

- `WorkflowEngine<T>.StartOrGet(key, input)`

Why:

- like `Start`, this is definition-scoped rather than selection-scoped
- strong semantics are durable-mode only

### 3. `GetInstance`

Status:

- core, but better expressed through instance scope

Preferred shape:

- `WorkflowEngine.Instance(id)`

Why:

- instance scope is cleaner than a separate retrieval verb
- once you have instance scope, operations become fluent and consistent

### 4. `ListInstances`

Status:

- core concept
- likely implemented as terminal query

Preferred shape:

- `WorkflowEngine.All().List()`
- `WorkflowEngine<T>.All().List()`
- `WorkflowEngine.Where(x => x.Status == Running).List()`

### 5. `GetActiveWaits`

Status:

- core

Preferred shape:

- `WorkflowEngine.Instance(id).GetActiveWaits()`
- `WorkflowEngine<T>.Where(x => x.Status == Waiting).GetActiveWaits()`

### 6. `RaiseEvent`

Status:

- core

Preferred shape:

- `WorkflowEngine.Instance(id).RaiseEvent(evt)`

Optional broader form only if semantics are explicit:

- `WorkflowEngine<T>.Where(...).RaiseEvent(evt)`

### 7. `Pause`

Status:

- durable-core
- limited or omitted in ephemeral-facing APIs

Preferred shape:

- `WorkflowEngine.Instance(id).Pause()`
- `WorkflowEngine<T>.All().Pause()`
- `WorkflowEngine.Where(x => x.Status == Running).Pause()`

### 8. `Resume`

Status:

- durable-core
- limited or omitted in ephemeral-facing APIs

Preferred shape:

- `WorkflowEngine.Instance(id).Resume()`
- `WorkflowEngine<T>.Where(x => x.Status == Paused).Resume()`

### 9. `CancelInstance`

Status:

- core

Preferred shape:

- `WorkflowEngine.Instance(id).Cancel()`
- `WorkflowEngine<T>.Where(...).Cancel()`

Recommendation:

Use `Cancel()` in fluent command form rather than `CancelInstance()`.

### 10. `Terminate`

Status:

- core

Preferred shape:

- `WorkflowEngine.Instance(id).Terminate()`
- `WorkflowEngine<T>.Where(...).Terminate()`

### 11. `RetryInstance`

Status:

- core, but semantics must be explicit

Preferred shape:

- `WorkflowEngine.Instance(id).Retry()`
- `WorkflowEngine<T>.Where(x => x.Status == Failed).Retry()`

### 12. `RetryStep`

Status:

- keep, but mark advanced

Preferred shape:

- `WorkflowEngine.Instance(id).Step(stepId).Retry()`

Why:

- step retry belongs to instance-plus-step scope, not to broad list scope

### 13. `Archive`

Status:

- durable-only

Preferred shape:

- `WorkflowEngine.Instance(id).Archive()`
- `WorkflowEngine<T>.Where(...).Archive()`

### 14. `Purge`

Status:

- durable-only

Preferred shape:

- `WorkflowEngine.Instance(id).Purge()`
- `WorkflowEngine<T>.Where(...).Purge()`

### 15. `GetStatistics`

Status:

- core

Preferred shape:

- `WorkflowEngine.All().Statistics()`
- `WorkflowEngine<T>.All().Statistics()`
- `WorkflowEngine.Where(x => x.IsStuck).Statistics()`

Recommendation:

Prefer `Statistics()` as a terminal fluent query rather than `GetStatistics()`.

### 16. `GetHistory`

Status:

- durable-core
- limited in ephemeral mode

Preferred shape:

- `WorkflowEngine.Instance(id).GetHistory()`

Optional broader analytics form:

- `WorkflowEngine<T>.Where(...).HistorySummary()`

### 17. `GetStuckInstances`

Status:

- core concept

Preferred shape:

- `WorkflowEngine.Where(x => x.IsStuck).List()`
- `WorkflowEngine.Where(x => x.IsStuck).Statistics()`

Recommendation:

This should mostly be expressed as a filter, not as a standalone command.

### 18. `GetTimedOutSteps`

Status:

- core concept

Preferred shape:

- `WorkflowEngine.Where(x => x.HasTimedOutSteps).List()`
- `WorkflowEngine<T>.Where(x => x.HasTimedOutSteps).List()`

Recommendation:

Also better expressed as filtered selection plus terminal query.

## Additional commands recommended

### `GetStep`

Preferred shape:

- `WorkflowEngine.Instance(id).Step(stepId)`

### `GetStepHistory`

Preferred shape:

- `WorkflowEngine.Instance(id).Step(stepId).GetHistory()`

### `GetLifecycleEvents`

Preferred shape:

- `WorkflowEngine.Instance(id).GetLifecycleEvents()`
- `WorkflowEngine<T>.Where(...).GetLifecycleEvents()`

### `GetCompensationState`

Preferred shape:

- `WorkflowEngine.Instance(id).Saga().GetCompensationState()`
- or direct instance command if the instance is saga-typed

### `QueryInstances`

Recommendation:

In a fluent model, explicit `QueryInstances` may no longer be needed as a public name. Filtering plus `List()` covers the same concept more cleanly.

## Commands that become filters instead of verbs

In a fluent LINQ-like API, these are better modeled as predicates/filters:

- stuck instances
- timed-out steps
- waiting instances
- paused instances
- failed instances
- terminal instances

Examples:

- `WorkflowEngine.Where(x => x.IsStuck).List()`
- `WorkflowEngine<T>.Where(x => x.HasTimedOutSteps).List()`
- `WorkflowEngine<T>.Where(x => x.Status == Failed).Retry()`

## API grouping recommendation

### Engine-wide root

Examples:

- `WorkflowEngine.All()`
- `WorkflowEngine.Where(x => x.Status == Running)`
- `WorkflowEngine.Where(x => x.IsStuck)`
- `WorkflowEngine.Instance(id)`

### Definition-scoped root

Examples:

- `WorkflowEngine<T>.Start(...)`
- `WorkflowEngine<T>.StartOrGet(...)`
- `WorkflowEngine<T>.All()`
- `WorkflowEngine<T>.Where(x => x.Status == Running)`

### Instance-scoped root

Examples:

- `WorkflowEngine.Instance(id).Pause()`
- `WorkflowEngine.Instance(id).RaiseEvent(evt)`
- `WorkflowEngine.Instance(id).Step(stepId).Retry()`

## Recommended first fluent command set

Definition-scoped creation:

- `WorkflowEngine<T>.Start(...)`
- `WorkflowEngine<T>.StartOrGet(...)`

Engine/definition selection + terminal query:

- `.All()`
- `.Where(...)`
- `.List()`
- `.Count()`
- `.Statistics()`

Instance and selection terminal commands:

- `.Pause()`
- `.Resume()`
- `.RaiseEvent(...)`
- `.Cancel()`
- `.Terminate()`
- `.Retry()`

Durable-only terminal commands:

- `.Archive()`
- `.Purge()`
- `.GetHistory()`

Advanced:

- `.Step(stepId).Retry()`
- `.GetLifecycleEvents()`
- saga compensation inspection

## Final position

The candidate command list remains valid, but in a fluent API many of those names should become:

- root entry points
- filters
- terminal query operators
- terminal command operators

The strongest direction is:

- root scope: engine-wide, definition-scoped, instance-scoped
- fluent filtering with canonical `Where(...)`
- terminal queries such as `List()` and `Statistics()`
- terminal commands such as `Pause()`, `Resume()`, `Cancel()`, `Terminate()`, and `Retry()`
- constrained, translatable predicate semantics rather than arbitrary runtime delegates

Examples that fit the intended style:

- `WorkflowEngine<T>.All().Pause()`
- `WorkflowEngine<T>.Where(x => x.Status == Running).Pause()`
- `WorkflowEngine.Instance(id).RaiseEvent(evt)`
- `WorkflowEngine.Where(x => x.IsStuck).Statistics()`
- `WorkflowEngine.Instance(id).Step(stepId).Retry()`

