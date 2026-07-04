# Composition & Children — Negative Tests & Edge Cases

Scope: `Parallel`, `WhenAll`/`WhenFirst`, `ForEach`, `RunChild`/`RunChildren`, resume tokens,
lineage, throttling. Requirements: CP-*, AC-2xx, AC-6xx.

## Existing negative coverage (reference)

| Area | Test(s) | What is proven |
|------|---------|----------------|
| Child failure propagate | `ChildWorkflowAcceptanceTests` (AC-615) | `PropagateFailure` fails parent |
| Child failure continue | `ChildWorkflowAcceptanceTests` | `ContinueParent` parent survives |
| DAG node failure blocks deps | `DagAcceptanceTests` (JS-AC-003) | Dependents blocked |
| Empty parallel branch | `WorkflowBuilderTests` | Validation |
| Child compensation idempotent | `ChildCompensationTests` (AC-616) | Repeat no-op |
| Cancel does not compensate | `ChildCompensationTests` | No implicit compensation |
| Resume token second consume | `ParentResumeTokenTests` | `NoOp` on second consume |
| WhenFirst cancel remaining | `WhenFirstTests` | Losing branch cancelled |

---

## Missed negative tests

### NEG-CP-001 — Parallel with single branch
- **Priority:** P2 | **AC:** CP-001 | **Status:** Missing
- **Given** `Parallel` with one tuple
- **When** build
- **Then** validation error or degenerate pass-through documented

### NEG-CP-002 — Parallel with zero branches
- **Priority:** P1 | **AC:** CP-001 | **Status:** Missing
- **When** build empty parallel
- **Then** validation error

### NEG-CP-003 — WhenAll: one branch throws before join
- **Priority:** P1 | **AC:** AC-201 | **Status:** Partial
- **Given** branch A succeeds, B throws
- **When** join evaluates
- **Then** parent `Failed`; continuation runs zero or once per policy

### NEG-CP-004 — WhenFirst: all branches fail
- **Priority:** P1 | **AC:** AC-204 | **Status:** Missing
- **Given** both branches throw or timeout
- **When** when-first resolves
- **Then** `Failed` with aggregated errors

### NEG-CP-005 — ForEach empty collection
- **Priority:** P1 | **AC:** CP-011 | **Status:** Missing
- **Given** zero items
- **When** foreach runs
- **Then** immediate join success (0 work items)

### NEG-CP-006 — ForEach batch size zero / negative
- **Priority:** P1 | **AC:** AC-601 | **Status:** Missing
- **When** configure `batchSize = 0`
- **Then** build validation error

### NEG-CP-007 — ForEach maxConcurrency > item count
- **Priority:** P2 | **AC:** AC-603 | **Status:** Missing
- **Given** 3 items, maxConcurrency 10
- **When** run
- **Then** at most 3 active; completes normally

### NEG-CP-008 — RunChildren duplicate dispatch same group
- **Priority:** P1 | **AC:** CP-025 | **Status:** Partial
- **Given** same `RunChildren` command replayed
- **When** second dispatch
- **Then** no duplicate children (idempotent)

### NEG-CP-009 — RunChild unknown child definition
- **Priority:** P1 | **AC:** CP-021 | **Status:** Missing
- **Given** unregistered child definition id
- **When** run child
- **Then** clear failure at start or build

### NEG-CP-010 — Child completion after parent terminated
- **Priority:** P1 | **AC:** CP-026 | **Status:** Missing
- **Given** parent `Terminated`
- **When** child completes
- **Then** completion recorded but parent not advanced

### NEG-CP-011 — Parent resume token consumed twice concurrently
- **Priority:** P0 | **AC:** AC-610 | **Status:** Partial (sequential only)
- **Given** two children complete simultaneously
- **When** both trigger barrier
- **Then** parent continues exactly once

### NEG-CP-012 — WhenAny: parent continues before cancel recorded
- **Priority:** P0 | **AC:** AC-605, AC-612 | **Status:** Partial
- **Given** WhenAny policy
- **When** winner selected
- **Then** residual cancel intent durable **before** parent resume — negative test if reorder possible

### NEG-CP-013 — Throttle: dispatch while at capacity
- **Priority:** P0 | **AC:** AC-609 | **Status:** Missing
- **Given** maxConcurrency=2, 2 active children
- **When** third dispatch attempted
- **Then** rejected or queued — no third active

### NEG-CP-014 — Throttle: complete child does not dispatch when still at capacity
- **Priority:** P0 | **AC:** AC-609 | **Status:** Missing
- **Given** 4 children, 2 active, 2 complete waiting in queue
- **When** one active completes
- **Then** exactly one new start (R6 P0)

### NEG-CP-015 — Lineage query orphaned child
- **Priority:** P2 | **AC:** AC-614 | **Status:** Missing
- **Given** child stream exists; parent purged
- **When** lineage query from child
- **Then** parent link null or tombstone; no throw

### NEG-CP-016 — Compensate group with no completed children
- **Priority:** P1 | **AC:** AC-616 | **Status:** Partial
- **When** `Compensate(group)` with zero completed
- **Then** no-op; no compensation children spawned

### NEG-CP-017 — Compensate after explicit cancel
- **Priority:** P1 | **AC:** AC-616 | **Status:** Covered (cancel path)
- **Edge negative:** compensate invoked after cancel must not double-spawn

### NEG-CP-018 — Heterogeneous DAG build at runtime
- **Priority:** P1 | **AC:** JS-AC-001 | **Status:** Partial (build-time only)
- **Given** manual commands with mismatched child definitions
- **When** DAG wave runs
- **Then** failure or validation — per-node definition not enforced today

### NEG-CP-019 — ForEach WaitAllThenFail: zero failures
- **Priority:** P2 | **AC:** AC-604 | **Status:** Missing
- **Given** all items succeed
- **When** join
- **Then** parent still fails per policy (intentional semantics)

### NEG-CP-020 — Join with cancelled branch
- **Priority:** P1 | **AC:** CP-004 | **Status:** Missing
- **Given** WhenAll; one branch `Cancelled` via policy
- **When** join
- **Then** outcome per residual policy

---

## Edge-case scenarios

### EDGE-CP-001 — Parallel branch completion order reversed
- **Priority:** P1 | **AC:** AC-202 | **Status:** Covered
- **Edge:** three branches complete C, A, B order — same outcome

### EDGE-CP-002 — WhenFirst true simultaneous completion
- **Priority:** P0 | **AC:** AC-204 | **Status:** Missing (R3 P3)
- **Given** two branches waiting on events
- **When** `Task.WhenAll` on two `RaiseEventAsync`
- **Then** deterministic winner by documented rule

### EDGE-CP-003 — ForEach batch size does not divide item count
- **Priority:** P1 | **AC:** AC-601 | **Status:** Covered (23/10)
- **Edge:** batch size 1, 1 item; batch size = item count

### EDGE-CP-004 — ForEach item suspends on wait mid-batch
- **Priority:** P1 | **AC:** AC-603 | **Status:** Partial
- **Given** maxConcurrency=2; item waits
- **When** third item would start
- **Then** concurrency slot held until wait completes

### EDGE-CP-005 — RunChildren partition single item
- **Priority:** P2 | **AC:** CP-030 | **Status:** Missing
- **Given** partition returns 1 snapshot
- **When** run
- **Then** one child; join still waits for one

### EDGE-CP-006 — Child id determinism across duplicate command
- **Priority:** P0 | **AC:** AC-607 | **Status:** Partial
- **Given** replay same child index after restart
- **When** child ids computed
- **Then** byte-equal child instance ids

### EDGE-CP-007 — Item snapshot stability after parent state mutation
- **Priority:** P1 | **AC:** AC-608 | **Status:** Partial
- **Given** parent mutates state after scheduling children
- **When** child starts
- **Then** snapshot frozen at schedule time

### EDGE-CP-008 — Barrier with one child fails one succeeds
- **Priority:** P1 | **AC:** AC-610 | **Status:** Missing
- **Given** WhenAll children; one fails
- **When** barrier logic runs
- **Then** parent policy applied once at end

### EDGE-CP-009 — Nested RunChildren (child runs children)
- **Priority:** P1 | **AC:** CP-040 | **Status:** Missing
- **Given** 2-level nesting
- **When** inner completes
- **Then** lineage `RootInstanceId` consistent at all levels

### EDGE-CP-010 — Diamond DAG: B completes before A
- **Priority:** P1 | **AC:** JS-AC-001 | **Status:** Missing
- **Given** A→{B,C}→D
- **When** C and B complete before A (invalid schedule attempt)
- **Then** D not scheduled until A done

### EDGE-CP-011 — maxConcurrency restart mid-window
- **Priority:** P0 | **AC:** AC-609 | **Status:** Missing
- **Given** 2 of 4 dispatched; crash
- **When** rehydrate
- **Then** still 2 active max; correct continuation index

### EDGE-CP-012 — Unified outbox interleaved child-start and lifecycle
- **Priority:** P1 | **AC:** AC-613 | **Status:** Partial
- **Given** same commit batch
- **When** inspect outbox ordering
- **Then** relative order preserved for pump

### EDGE-CP-013 — ForEach cancellation mid-batch
- **Priority:** P1 | **AC:** AC-605 | **Status:** Missing
- **Given** parent cancelled while items running
- **When** cancel propagates
- **Then** in-flight items observe cancel; intent recorded

### EDGE-CP-014 — Empty partitioner result
- **Priority:** P2 | **AC:** CP-030 | **Status:** Missing
- **Given** partitioner returns empty
- **When** RunChildren
- **Then** immediate parent continue or validation error
