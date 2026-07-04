# Core Runtime & Authoring — Negative Tests & Edge Cases

Scope: `OrcaCore.Core` builders, primitives, lifecycle; ephemeral/durable step execution and
control flow. Requirements: CR-001…044, AC-0xx.

## Existing negative coverage (reference)

| Area | Test(s) | What is proven |
|------|---------|----------------|
| Builder validation | `WorkflowBuilderTests`, `BuilderAcceptanceTests` | Accumulated errors, empty branch, missing End |
| Step failure | `StraightLineAcceptanceTests`, `InterpreterTests` | Throwing step → `Failed`, later steps skipped |
| Illegal lifecycle | `TerminalAcceptanceTests` (AC-005) | Event/resume on terminal rejected |
| State type mismatch | `ManagementQueryTests.GetState_WrongType_ReturnsClearFailure` | Wrong `GetState<T>` throws clear error |
| DAG cycle | `DagBuilderTests` (JS-AC-002) | Cyclic DAG rejected at build |
| Heterogeneous DAG | `DagBuilderTests` | Mixed definition IDs rejected |

---

## Missed negative tests

### NEG-CR-001 — Start unknown definition
- **Priority:** P1 | **AC:** — | **Status:** Covered
- **Given** engine with no registered definition
- **When** `StartAsync(unknownDefinitionId, …)`
- **Then** clear `WorkflowDefinitionException` (not null ref / generic failure)

### NEG-CR-002 — Start with wrong input type
- **Priority:** P1 | **AC:** CR-011 | **Status:** Missing
- **Given** definition `Init<int>`
- **When** `StartAsync<string, …>(…)`
- **Then** rejected at API boundary with type mismatch error

### NEG-CR-003 — Resume/retry on ephemeral terminal instance
- **Priority:** P1 | **AC:** AC-005 | **Status:** Partial
- **Given** instance `Completed`, `Failed`, `Cancelled`, `Terminated` (each case)
- **When** `RaiseEventAsync`, management `RetryAsync` (if exposed), `StartAsync` same id
- **Then** each path rejected; snapshot unchanged

### NEG-CR-004 — Completion bridge on non-terminal workflow
- **Priority:** P1 | **AC:** AC-011 | **Status:** Covered
- **Given** workflow blocked on `Wait`
- **When** `AwaitCompletionAsync` with short timeout
- **Then** timeout/cancellation with clear outcome (not hang, not return `Waiting` as success)

### NEG-CR-005 — Completion blocked by unresolved runtime work
- **Priority:** P0 | **AC:** AC-010 | **Status:** Partial
- **Given** instance with active wait at implicit End path (policy-dependent)
- **When** interpreter reaches End with outstanding wait
- **Then** completion rejected or wait cancelled per policy — assert no `Completed` with active waits

### NEG-CR-006 — Double End / unreachable code after End
- **Priority:** P2 | **AC:** CR-008 | **Status:** Missing
- **Given** builder with two consecutive `End()` calls (if API allows) or dead branch after End
- **When** `Build()`
- **Then** validation error listing unreachable nodes

### NEG-CR-007 — While with non-bool condition
- **Priority:** P2 | **AC:** CR-010 | **Status:** Missing (compile-time only today)
- **Given** invalid condition delegate signature at runtime (dynamic/reflection path if any)
- **Then** clear failure at step execution

### NEG-CR-008 — Step returns invalid `StepResult`
- **Priority:** P1 | **AC:** CR-014 | **Status:** Missing
- **Given** step returning malformed/custom `StepResult` (if extensible)
- **When** interpreter processes result
- **Then** `Failed` with inspectable error, no undefined advancement

### NEG-CR-009 — Mutating returned snapshot does not affect engine
- **Priority:** P1 | **AC:** AC-009 | **Status:** Partial (`ManagementAcceptanceTests` one path)
- **Given** snapshot from `List()` / `ToSnapshot()`
- **When** caller mutates `ActiveWaits`, status, business state on snapshot DTO
- **Then** subsequent engine queries unchanged; durable projection unchanged

### NEG-CR-010 — `GetState` on missing instance
- **Priority:** P1 | **AC:** MG-002 | **Status:** Missing
- **Given** random `InstanceId` not in registry/store
- **When** `Management.Instance(id).GetState<T>()`
- **Then** not-found error (distinct from wrong type)

### NEG-CR-011 — Builder: RunChild on ephemeral builder
- **Priority:** P1 | **AC:** CP-021 | **Status:** Missing
- **Given** ephemeral `WorkflowBuilder`
- **When** `RunChild` / `RunChildren` added (if API exists on wrong builder)
- **Then** compile-time or build-time rejection

### NEG-CR-012 — WaitLong on ephemeral builder
- **Priority:** P1 | **AC:** AC-303 | **Status:** Missing
- **Given** ephemeral builder
- **When** `WaitLong` used
- **Then** build rejection with explicit code

### NEG-CR-013 — Nested definition depth exceeded
- **Priority:** P2 | **AC:** CP-040 | **Status:** Partial
- **Given** DAG/workflow nesting beyond configured max
- **When** `Build()`
- **Then** accumulated validation error with depth code

### NEG-CR-014 — Cancel does not propagate token to in-flight step
- **Priority:** P0 | **AC:** AC-014 | **Status:** Missing (R3 P1)
- **Given** step blocked on `TaskCompletionSource`, instance `Running`
- **When** `CancelAsync`
- **Then** step's `CancellationToken` cancelled; instance `Cancelled` — **today likely fails**

### NEG-CR-015 — Terminate during in-flight step
- **Priority:** P1 | **AC:** AC-015 | **Status:** Missing
- **Given** long-running step
- **When** `TerminateAsync` (force)
- **Then** no further commits after current boundary; no compensation/retry

### NEG-CR-016 — Yield after terminal transition attempted
- **Priority:** P1 | **AC:** AC-013 | **Status:** Missing
- **Given** step that yields then throws on second invocation after cancel
- **When** cancel arrives between yields
- **Then** no additional yield commits; terminal state consistent

### NEG-CR-017 — Durable-only nodes rejected before ephemeral execution
- **Priority:** P1 | **AC:** AC-001 / CR-002 | **Status:** Covered
- **Given** a definition containing `RunChild`/`RunChildren`
- **When** it is registered on the ephemeral engine
- **Then** registration throws `WorkflowDefinitionException`; no step side effects can run
  (`CoreRuntimeScenarioTests.NEG_CR_017_RegisterDefinitionWithDurableOnlyNodes_ThrowsDefinitionException`)

---

## Edge-case scenarios

### EDGE-CR-001 — While: zero iterations
- **Priority:** P1 | **Status:** Missing
- **Given** `While(false)` before body
- **When** start
- **Then** body never runs; completes immediately

### EDGE-CR-002 — While: maximum int iterations boundary
- **Priority:** P2 | **Status:** Missing
- **Given** policy max loop count (if exists) or practical bound
- **When** condition always true until counter
- **Then** policy enforced or completes without stack overflow

### EDGE-CR-003 — If: condition throws
- **Priority:** P1 | **Status:** Missing
- **Given** `If` condition delegate throws
- **When** evaluated
- **Then** instance `Failed`; neither branch runs

### EDGE-CR-004 — Empty workflow (Init → End only)
- **Priority:** P2 | **Status:** Covered implicitly
- **Given** no intermediate steps
- **When** start
- **Then** immediate `Completed`

### EDGE-CR-005 — Step failure mid-parallel branch
- **Priority:** P1 | **AC:** CP-002 | **Status:** Partial
- **Given** `Parallel` with one throwing branch
- **When** other branch succeeds
- **Then** join policy determines outcome (fail-fast vs wait-all); assert deterministic

### EDGE-CR-006 — Retry at exact policy limit
- **Priority:** P1 | **AC:** AC-510 | **Status:** Partial
- **Given** retry policy `MaxAttempts = 3`
- **When** step fails exactly 3 times
- **Then** fails on 3rd; 4th invocation never runs

### EDGE-CR-007 — Retry at limit minus one then success
- **Priority:** P1 | **Status:** Missing
- **Given** fails twice, succeeds third
- **When** complete
- **Then** single committed success outcome; no duplicate side effects

### EDGE-CR-008 — Named End with multiple named ends in builder
- **Priority:** P2 | **AC:** AC-012 | **Status:** Missing
- **Given** builder with `End("A")` and unreachable `End("B")`
- **When** build/run
- **Then** only reachable outcome recordable

### EDGE-CR-009 — Definition version mismatch at start
- **Priority:** P1 | **AC:** DU-040 | **Status:** Missing (ephemeral)
- **Given** two versions registered; start specifies version N while default is N+1
- **When** start with explicit version
- **Then** binds to requested version only

### EDGE-CR-010 — Concurrent start same definition (distinct instances)
- **Priority:** P1 | **AC:** CR-040 | **Status:** Missing
- **Given** N parallel `StartAsync` calls
- **When** all complete
- **Then** N distinct `InstanceId`s; no shared mutable state

### EDGE-CR-011 — Interpreter re-entry after yield (durable)
- **Priority:** P0 | **AC:** AC-013 | **Status:** Partial (ephemeral only)
- **Given** yielding step; crash after yield commit
- **When** rehydrate and continue
- **Then** resumes after last yield point; no duplicate pre-yield effects

### EDGE-CR-012 — State serialization round-trip edge types
- **Priority:** P2 | **AC:** CR-020 | **Status:** Missing
- **Given** state with null, empty collections, large strings, nested objects
- **When** durable commit + reload
- **Then** byte-identical or semantically equal state
