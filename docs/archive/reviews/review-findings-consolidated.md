# Consolidated Review Findings

Two independent reviews were performed against the design proposal, implementation plan, design decisions tracking, and minimal core scope documents. This document merges and deduplicates all findings into one actionable list.

Each finding has a severity, affected area, root documents, and a recommended action.

---

## Blocking (must resolve before implementation)

### R-01: Correlation index structure does not support fanout

**Severity**: High
**Area**: Architecture, Tests
**Sources**: F5, F13, F14, E2

The correlation index is defined as `Dictionary<(EventName, CorrelationId), InstanceId>` â€” a 1:1 mapping. But MC-AT-018 (fanout) has three instances registering the same `(EventName, CorrelationId)`. The dictionary can only hold one entry per key.

Additionally, DD-124 says uniqueness is enforced at **routing time**, but the `RegisterWait` pseudocode unconditionally adds to the index. MC-AT-017's uniqueness sub-test says "second registration fails" â€” which is **registration-time** enforcement, contradicting DD-124.

**What must be decided**:

1. Should the correlation index hold multiple entries per key? (e.g., `Dictionary<key, List<InstanceId>>`)
2. Is uniqueness enforced at registration time or routing time?
3. How does `RegisterWait` behave when the key already exists in the index â€” skip, overwrite, or error?

**Affected docs**:
- `design-proposal-minimal-core.md`: RegisterWait pseudocode (line ~502), section 5.2a uniqueness text
- `design-decisions-tracking.md`: DD-124
- `implementation-plan-minimal-core.md`: Step 7 uniqueness sub-test (line ~247)

---

### R-02: Cross-document contradictions on event routing scope

**Severity**: High
**Area**: Doc coherence
**Sources**: E2

The same documents simultaneously frame correlation routing as an open question and as in-scope:

- `minimal-core-scope.md:277` â€” section "Q3: How should event correlation work?" still frames general correlation as an open question and recommends "start instance-targeted first"
- `minimal-core-scope.md:459` â€” test order includes MC-AT-017 (correlation routing) and MC-AT-018 (fanout)
- `design-proposal-minimal-core.md:304` â€” matching rule text still leads with "Instance-targeted delivery" as the primary model, before the routing section clarifies three modes

**Action**: Reconcile Q3 in minimal-core-scope (mark as answered), update the matching rule text in the proposal to lead with the three routing modes as equals, remove any remaining "deferred" language about correlation routing.

---

### R-03: `abstract sealed` is not valid C# syntax

**Severity**: High
**Area**: .NET design
**Sources**: F8

`StepResult` is described as `abstract sealed / discriminated union` in `design-proposal-minimal-core.md:160`. C# does not allow `abstract sealed` on a class.

**Action**: Decide the implementation approach before Step 1:
- **Option A**: `abstract record StepResult` with `sealed record Completed : StepResult`, etc. (recommended â€” gives value equality, immutability, and pattern matching)
- **Option B**: `abstract class StepResult` with `sealed` nested subclasses (more traditional, no value equality)
- **Option C**: Wait for C# 14 discriminated unions if available in the .NET 10 preview

---

### R-04: No contract for event payload delivery to step after resume

**Severity**: High
**Area**: API, Tests
**Sources**: F15

`EventEnvelope` has `Payload: object?`. The `DeliverToInstance` pseudocode says "deliver event payload to step context." But `StepContext<TState>` has no field for the event payload. No acceptance test verifies that a step receives the payload after resume.

**What must be decided**:
- How does the step that follows a `Wait` access the event payload?
- Is it set on `StepContext` (e.g., `StepContext.EventPayload`)?
- Is it injected into business state by the runtime?
- Is it ignored in the minimal core and deferred?

**Affected docs**:
- `design-proposal-minimal-core.md`: StepContext type (line ~177), DeliverToInstance pseudocode (line ~474)
- `implementation-plan-minimal-core.md`: Step 3 (MC-AT-004)

---

## Should fix before implementation

### R-05: Step 1 creates ~15 types â€” too large for strict TDD

**Severity**: Medium-High
**Area**: TDD discipline
**Sources**: F18, E5

Step 1 (MC-AT-001) drives creation of ~15 types in a single red-green cycle: `WorkflowStatus`, `StepResult`, `StepContext`, `IStep`, `WorkflowInstanceSnapshot`, `WorkflowBuilder`, `WorkflowEngine`, `WorkflowEngine<T>`, `InstanceScope`, `WorkflowRuntime`, `WorkflowDefinition`, `WorkflowInstance`, `RuntimeState`, `InstanceLifecycle`, `InMemoryInstanceStore`.

Additionally, the plan introduces infrastructure before the test forces it: `PendingEvents` list in Step 3 (line 138) before buffering is tested in Step 5, and strong store-atomicity language in Step 1 (line 63) before any persistence boundary exists.

**Action**:
- Consider splitting MC-AT-001 into 2â€“3 micro-steps within the same acceptance test family (e.g., "builder compiles", "engine starts and returns snapshot", "business state is observable")
- Remove forward-looking infrastructure from early steps â€” add `PendingEvents` and atomicity in the step that tests them
- Keep the plan as a guide, not a binding class inventory; when a test passes with a simpler object, prefer the simpler object

---

### R-06: Event subsystem scope inflation

**Severity**: Medium-High
**Area**: Scope control
**Sources**: E1

The "minimal core" event subsystem now includes: instance-targeted routing, correlation-targeted routing, definition-targeted fanout, per-instance buffering, deduplication, and correlation indexing. This is 6 event-related capabilities â€” a coherent subsystem, but materially beyond what "minimal" originally meant.

**Action**: No rollback needed (the user explicitly requested fanout inclusion), but:
- Acknowledge this as a deliberate scope expansion in the scope document
- Consider whether the TDD order should prove the core interpreter (If, While, Parallel) **before** the routing subsystem (correlation, fanout), since routing is useless without a working interpreter
- Current order: Steps 1â€“8 are all event/routing, Steps 9â€“12 are interpreter. Alternative: prove interpreter basics (If, While) after dedup (Step 6), then routing (Steps 7â€“8)

---

### R-07: Access path from `WorkflowEngine` to `WorkflowEngine<T>` undefined

**Severity**: Medium
**Area**: API design
**Sources**: F1

Section 5.1 shows `WorkflowEngine<TDefinition>` and section 5.2a references `engine.ForDefinition<T>().RaiseEvent(...)`. But the design never shows how the caller obtains a `WorkflowEngine<T>`:
- Is it `engine.ForDefinition<T>()` (factory method)?
- Is it injected via DI as a separate registration?
- Is it constructed by the caller?

**Action**: Define the access path. Recommendation: `WorkflowEngine.ForDefinition<TState>(definitionId)` returns `WorkflowEngine<TState>`. The engine-wide instance acts as the factory.

---

### R-08: `GetState<TState>()` role needs explicit scoping

**Severity**: Medium
**Area**: API design
**Sources**: F2, E3

`GetState<TState>()` is pragmatic for TDD but weakens the separation between operational management and business data if it becomes a general-purpose production surface. The caller must guess the type; wrong type throws.

**Action**:
- Document `GetState<TState>()` as an **inspection/advanced** API, not a casual management primitive
- Ensure it returns a **copy** of the business state, not a reference to the live object
- Consider whether `InstanceScope` should surface it only via an explicit `.Inspect()` sub-scope to keep the primary management surface clean

---

### R-09: `IStep<TState>.ExecuteAsync` is dead code for infrastructure steps

**Severity**: Medium
**Area**: .NET design
**Sources**: F9

The interpreter recognizes infrastructure steps by type (`if step is IfStep<TState>`) and handles them specially. `IfStep.ExecuteAsync()` is never called. This means `ExecuteAsync` on infrastructure steps is misleading.

**Action**: TDD will surface this at Step 9 (first IfStep). Options:
- **A**: Keep `IStep<TState>` uniform, infrastructure steps throw `NotSupportedException` (pragmatic but ugly)
- **B**: Split: `IBusinessStep<TState>` with `ExecuteAsync`, `IControlFlowStep<TState>` without (cleaner but more types)
- **C**: Infrastructure steps are not `IStep` at all â€” they're internal interpreter nodes. Only business steps implement `IStep`. The definition graph uses a different node type.

Recommendation: defer to TDD discovery. Flag this as an expected design question at Step 9.

---

### R-10: `EventRouter` vs `EventMatcher` responsibility boundary unclear

**Severity**: Medium
**Area**: Architecture
**Sources**: F6

`EventMatcher` matches events to waits within an instance. `EventRouter` determines which instance(s) to deliver to. The `DeliverToInstance` pseudocode mixes both concerns.

**Action**: Clarify in the proposal:
- `EventRouter`: resolves target instance(s) from the routing mode (instance-targeted â†’ direct, correlation-targeted â†’ index lookup, fanout â†’ definition iteration)
- `EventMatcher`: matches an `EventEnvelope` against `WaitRecord` entries within a single instance
- `DeliverToInstance`: acquires lock, calls `EventMatcher`, handles result

---

### R-11: WaitLongStep in Abstractions is a layering concern

**Severity**: Medium
**Area**: Architecture, TDD
**Sources**: F20, E4

`WaitLongStep<TState>` is placed in Abstractions so the runtime can identify it. But rule 5 says "infrastructure steps are internal to Runtime," and `WaitLong` is a deferred durable-only feature. The minimal abstraction assembly now knows about an out-of-scope primitive.

**Action**: Options:
- **A**: Move `WaitLongStep` to Runtime (internal). The runtime-rejection test constructs it via reflection or an internal test helper. Keeps Abstractions clean.
- **B**: Keep in Abstractions but document the exception to rule 5 explicitly.
- **C**: Instead of a marker step type, use a builder guard only (builder doesn't expose `WaitLong`). The runtime test uses reflection to verify the method doesn't exist.

---

### R-12: Acceptance tests should pin exact behaviors, not use "or similar"

**Severity**: Medium
**Area**: Tests
**Sources**: E review (acceptance tests section), F14

Several test scenarios use soft language: "throws with clear error", "rejected with clear diagnostics", "or similar." Each test should specify exactly:
- What exception type is thrown (or what return value indicates failure)
- Whether a non-matching fanout event is buffered or silently dropped
- Whether a uniqueness violation throws at registration or returns an error

**Action**: Before implementation, tighten every test scenario to pin one exact observable behavior. No "or similar" in Given/When/Then.

---

### R-13: RaiseEvent routing modes share a method name but have different semantics

**Severity**: Medium
**Area**: API design
**Sources**: E review (API design section)

Three entry points all use `.RaiseEvent(envelope)`:
- `Instance(id).RaiseEvent(...)` â€” instance-targeted
- `WorkflowEngine.RaiseEvent(...)` â€” correlation-targeted
- `WorkflowEngine<T>.RaiseEvent(...)` â€” definition fanout

These are semantically different operations (direct delivery vs lookup vs broadcast), not overload variants. A developer reading `engine.RaiseEvent(envelope)` must know the engine type to understand the routing behavior.

**Action**: Consider whether the naming is clear enough or whether distinct method names would reduce confusion:
- Option A: Keep `.RaiseEvent()` everywhere â€” the entry point type disambiguates (current design)
- Option B: `.RaiseEvent()` for instance-targeted, `.RouteEvent()` for correlation, `.BroadcastEvent()` for fanout
- Option C: Keep current naming but document the three modes prominently in a single "event routing" section

---

## Nice to have / deferred

### R-14: .NET conventions not yet defined

**Severity**: Low
**Area**: .NET design
**Sources**: E6, F11

No conventions established for: nullable reference types, record vs class for contracts, primary constructors, value types for small records (`record struct`), exact .NET 10 SDK preview version.

**Action**: Define these in Step 0 (scaffolding):
- Enable `<Nullable>enable</Nullable>` from day one
- Enable `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- Prefer `record` for immutable contracts (StepResult, EventEnvelope, WaitRecord, etc.)
- Prefer `record struct` for small value-semantic types (ExecutionPointer?)
- Pin exact SDK version in `global.json`

---

### R-15: No `IAsyncDisposable` on `WorkflowEngine`

**Severity**: Low
**Area**: .NET design
**Sources**: F12

The engine holds `SemaphoreSlim` instances. For ephemeral mode GC handles cleanup, but durable mode will need graceful shutdown.

**Action**: Add `IAsyncDisposable` to `WorkflowEngine` in Step 0 or Step 1. Cheap to add now, expensive to retrofit.

---

### R-16: No `CancellationToken` on `RaiseEvent`

**Severity**: Low
**Area**: API design
**Sources**: F4

All three routing entry points return `Task` but accept no `CancellationToken`. Fanout to many instances could be long-running.

**Action**: Add `CancellationToken cancellationToken = default` parameter to all `RaiseEvent` methods.

---

### R-17: `Payload: object?` on EventEnvelope is weakly typed

**Severity**: Low
**Area**: API design
**Sources**: F3

The only `object` in the entire type model. Creates serialization pressure for durable mode.

**Action**: Acceptable for minimal core. Flag for review when durable mode enters scope. Consider `TPayload` generic or `ReadOnlyMemory<byte>` later.

---

### R-18: Component diagram naming drift

**Severity**: Low
**Area**: Doc coherence
**Sources**: F7

Section 2 component diagram says "Event Dispatcher" but the rest of the document uses "EventRouter" and "EventMatcher".

**Action**: Update the diagram to match: "EventRouter" + "EventMatcher".

---

### R-19: MC-AT-008 may not add coverage beyond MC-AT-006

**Severity**: Low
**Area**: Tests
**Sources**: F16

MC-AT-006 proves "join fires exactly once." MC-AT-008 proves "same outcome regardless of completion order." If MC-AT-006 already uses branches that could complete in any order, MC-AT-008 is redundant.

**Action**: Keep as a separate test â€” the cost is low and it explicitly documents the ordering invariant. But it could be merged with MC-AT-006 as additional assertions if step count needs trimming.

---

### R-20: MC-AT-012 concurrency test is timing-dependent

**Severity**: Low
**Area**: Tests
**Sources**: F17

Using `Task.Run` with 100 iterations hopes for thread interleaving but doesn't guarantee it. CI machines may not reproduce real contention.

**Action**: Use deterministic concurrency primitives (`ManualResetEventSlim` barriers, `SemaphoreSlim` gates) to force two threads to reach the critical section simultaneously. The loop-of-100 approach can supplement but should not be the primary mechanism.

---

### R-21: `Func<TState, bool>` on If/While not serializable

**Severity**: Low
**Area**: .NET design, future
**Sources**: F10

For ephemeral mode this is fine. For durable replay mode, lambdas can't be persisted. The coupling is tight â€” a future serializable definition format would need a parallel type hierarchy.

**Action**: Acknowledged as a deferred concern. No change for minimal core. The design-proposal replay-safety note already flags this.

---

### R-22: Step 7 will likely refactor Step 3's RaiseEvent path

**Severity**: Low
**Area**: TDD discipline
**Sources**: F19

Step 3 creates instance-targeted `RaiseEvent` directly. Step 7 introduces `EventRouter` and wraps the existing path. This refactoring is expected in TDD but not flagged in the plan.

**Action**: Add a note to Step 7: "This step may refactor the `RaiseEvent` path from Step 3 to introduce the routing layer."

---

## Summary

| ID | Severity | Area | Status | One-line summary |
|----|----------|------|--------|-----------------|
| R-01 | **Blocking** | Architecture | **Resolved** (DD-124) | Correlation index changed to multi-map `HashSet<InstanceId>`; uniqueness at routing time only |
| R-02 | **Blocking** | Docs | **Resolved** | Q3 marked answered; routing modes reconciled across all docs incl. DD-107 note and DD-123 index text |
| R-03 | **Blocking** | .NET | **Resolved** (DD-125) | StepResult uses `abstract record` with `sealed record` subclasses |
| R-04 | **Blocking** | API/Tests | **Resolved** (DD-126) | `StepContext.ResumedEvent: EventEnvelope?` added for payload delivery |
| R-05 | Medium-High | TDD | **Resolved** | Premature atomicity and PendingEvents removed from early steps |
| R-06 | Medium-High | Scope | **Resolved** | Scope expansion note added to minimal-core-scope.md |
| R-07 | Medium | API | **Resolved** | `WorkflowEngine.ForDefinition<TState>(definitionId)` access path defined |
| R-08 | Medium | API | **Resolved** (DD-121) | `GetState<TState>()` documented as inspection API returning snapshot copy |
| R-09 | Medium | .NET | **Deferred to TDD** | Flagged as design question at Step 9; three options documented |
| R-10 | Medium | Architecture | **Resolved** | EventRouter/EventMatcher responsibilities clarified in proposal Layer 2 |
| R-11 | Medium | Architecture | **Resolved** | WaitLongStep placed in Runtime, not Abstractions; TDD rule 8 updated |
| R-12 | Medium | Tests | **Resolved** | Test scenarios tightened with exact exception types and behaviors |
| R-13 | Medium | API | **Resolved** | Naming convention documented; entry point type disambiguates routing mode |
| R-14 | Low | .NET | **Resolved** | .NET conventions added to Step 0 in implementation plan |
| R-15 | Low | .NET | **Resolved** | `IAsyncDisposable` added to Step 0/Step 1 scope |
| R-16 | Low | API | **Resolved** | `CancellationToken` added to Step 0 conventions |
| R-17 | Low | API | **Deferred** | Acceptable for minimal core; flagged for durable mode review |
| R-18 | Low | Docs | **Resolved** | Component diagram updated: "Event Router + Matcher" |
| R-19 | Low | Tests | **Kept** | MC-AT-008 kept as separate test â€” low cost, documents ordering invariant |
| R-20 | Low | Tests | **Resolved** | MC-AT-012 updated with `ManualResetEventSlim` barriers |
| R-21 | Low | .NET | **Deferred** | Acknowledged; no change for minimal core |
| R-22 | Low | TDD | **Resolved** | Refactoring note added to Step 7 |

**Resolution summary**: All 4 blocking findings resolved. Both medium-high findings resolved. 5 of 7 medium findings resolved, 1 deferred to TDD discovery (R-09). 7 of 9 low findings resolved, 2 deferred (R-17, R-21). Total: 18 resolved, 1 deferred to TDD, 2 deferred to future scope.
