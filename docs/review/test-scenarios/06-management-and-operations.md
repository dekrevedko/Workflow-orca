# Management & Operations — Negative Tests & Edge Cases

Scope: fluent management queries/commands, statistics, stuck detection, pause/resume, eviction,
resource pools, destructive safety. Requirements: MG-*, AC-5xx.

## Existing negative coverage (reference)

| Area | Test(s) | What is proven |
|------|---------|----------------|
| Broad terminate without confirm | `TerminalAcceptanceTests`, `ManagementQueryTests` (AC-516) | Throws without safety |
| Broad purge without confirm | `DurableManagementTests` | `WorkflowLifecycleException` |
| Pause buffers events | `DurableManagementTests` (AC-513) | No match while paused |
| Resume replay order | `DurableManagementTests` (AC-514) | Buffered events replay |
| Resume discard | `DurableManagementTests` (AC-517) | Discards audited |
| Wrong state type | `ManagementQueryTests` | Clear failure |
| Pool expiry | `PoolOperationsTests` (AC-521) | Lifecycle + queryable |
| Multi-pool all-or-nothing | `ResourcePoolStoreCertificationTests` (AC-522) | No partial hold |

---

## Missed negative tests

### NEG-MG-001 — Query empty registry
- **Priority:** P2 | **AC:** MG-001 | **Status:** Missing
- **Given** no instances
- **When** `All().List()`
- **Then** empty list; not null

### NEG-MG-002 — Where predicate always false
- **Priority:** P2 | **AC:** AC-501 | **Status:** Missing
- **When** `Where(_ => false).List()`
- **Then** empty; count 0

### NEG-MG-003 — Command on empty selection
- **Priority:** P1 | **AC:** AC-502 | **Status:** Missing
- **Given** `Where(_ => false)`
- **When** `TerminateAsync` with confirm
- **Then** zero affected; no error

### NEG-MG-004 — Retry on non-failed instance
- **Priority:** P1 | **AC:** AC-510 | **Status:** Missing
- **Given** `Running` instance
- **When** `RetryAsync`
- **Then** rejected

### NEG-MG-005 — Pause on terminal instance
- **Priority:** P1 | **AC:** AC-512 | **Status:** Missing
- **Given** `Completed`
- **When** `PauseAsync`
- **Then** rejected

### NEG-MG-006 — Cancel on already cancelled
- **Priority:** P2 | **AC:** CR-031 | **Status:** Missing
- **When** double cancel
- **Then** idempotent or rejected

### NEG-MG-007 — Terminate with confirm but empty scope
- **Priority:** P2 | **AC:** AC-516 | **Status:** Missing
- **Given** confirmed terminate on zero matches
- **Then** success with count 0

### NEG-MG-008 — Purge terminal with active wait projection
- **Priority:** P1 | **AC:** AC-314 | **Status:** Missing
- **Given** terminal but wait index stale
- **When** purge
- **Then** wait index cleaned; no ghost routing

### NEG-MG-009 — Statistics on empty store
- **Priority:** P2 | **AC:** AC-503 | **Status:** Missing
- **When** `Statistics()` with zero instances
- **Then** empty groups; not throw

### NEG-MG-010 — Stuck query false positive guard
- **Priority:** P2 | **AC:** AC-508 | **Status:** Missing
- **Given** instance progressing slowly but under threshold
- **When** stuck query
- **Then** not stuck

### NEG-MG-011 — Pool acquire on unknown pool name
- **Priority:** P1 | **AC:** MG-062 | **Status:** Missing
- **When** acquire `pool-does-not-exist`
- **Then** clear error; no ticket

### NEG-MG-012 — Pool acquire capacity zero
- **Priority:** P1 | **AC:** AC-518 | **Status:** Missing
- **Given** pool capacity 0
- **When** acquire
- **Then** suspend or reject per policy; never grant

### NEG-MG-013 — Release ticket not held
- **Priority:** P1 | **AC:** AC-520 | **Status:** Missing
- **When** release unknown ticket id
- **Then** no-op or error; capacity unchanged

### NEG-MG-014 — Double release same ticket
- **Priority:** P0 | **AC:** AC-520 | **Status:** Missing
- **Given** ticket released on success
- **When** second release same ticket
- **Then** capacity not incremented twice

### NEG-MG-015 — Durable management: command API missing (R4)
- **Priority:** P1 | **AC:** MG-011 | **Status:** Missing
- **Given** public `DurableManagement` only
- **When** pause/cancel needed
- **Then** tests document required surface — negative: internal processor not reachable from host

### NEG-MG-016 — GetHistory on ephemeral
- **Priority:** P2 | **AC:** DU-071 | **Status:** Partial (`DurableManagementTests` asserts absent)
- **When** `GetHistory` called
- **Then** not supported error

### NEG-MG-017 — Evict while command enqueued
- **Priority:** P1 | **AC:** AC-506 | **Status:** Missing
- **Given** eviction races deliver
- **Then** command completes or fails clearly; no duplicate execution

### NEG-MG-018 — Concurrent broad terminate
- **Priority:** P1 | **AC:** AC-516 | **Status:** Missing
- **Given** two operators `All().Terminate(force)`
- **When** parallel
- **Then** all terminal once; no double lifecycle events

### NEG-MG-019 — Filter by definition version no matches
- **Priority:** P2 | **AC:** MG-001 | **Status:** Missing
- **When** `ForDefinition(id, version: 99)`
- **Then** empty selection

### NEG-MG-020 — Instance scope wrong id
- **Priority:** P1 | **AC:** MG-002 | **Status:** Missing
- **When** `Instance(randomId).CancelAsync()`
- **Then** not-found

---

## Edge-case scenarios

### EDGE-MG-001 — Query/command symmetry large selection
- **Priority:** P1 | **AC:** AC-502 | **Status:** Partial
- **Given** 100 instances matching filter
- **When** `Count()` vs `List()` vs `Terminate` affected count
- **Then** same cardinality

### EDGE-MG-002 — Pause at exact commit boundary
- **Priority:** P1 | **AC:** AC-512 | **Status:** Missing
- **Given** pause arrives during append
- **When** commit completes
- **Then** pause effective before next command

### EDGE-MG-003 — Resume with empty buffer
- **Priority:** P2 | **AC:** AC-514 | **Status:** Missing
- **Given** paused with no deliveries
- **When** resume
- **Then** continues from pause point only

### EDGE-MG-004 — Stuck threshold exactly equals elapsed
- **Priority:** P2 | **AC:** AC-507 | **Status:** Missing
- **Given** elapsed == threshold
- **When** evaluate
- **Then** boundary inclusive/exclusive per spec

### EDGE-MG-005 — Pool FIFO: two waiters, one slot frees
- **Priority:** P1 | **AC:** AC-519 | **Status:** Partial
- **Given** waiter A before B
- **When** release
- **Then** A granted first

### EDGE-MG-006 — Pool ticket expiry at clock boundary
- **Priority:** P1 | **AC:** AC-521 | **Status:** Partial
- **Given** expiry = T
- **When** clock advances T
- **Then** expiry sweep fires once

### EDGE-MG-007 — Multi-pool acquire: first pool fails second succeeds
- **Priority:** P0 | **AC:** AC-522 | **Status:** Missing
- **Given** needs pool A and B; A available; B exhausted
- **When** acquire scope
- **Then** hold nothing from A (rollback)

### EDGE-MG-008 — Idle eviction then immediate query
- **Priority:** P1 | **AC:** AC-504 | **Status:** Partial
- **Given** idle evicted from memory
- **When** durable query
- **Then** snapshot from projection; resume rehydrates

### EDGE-MG-009 — Terminal eviction then query
- **Priority:** P1 | **AC:** AC-505 | **Status:** Partial
- **After** terminal handling evicted
- **Then** still queryable from store

### EDGE-MG-010 — Statistics with 10k instances
- **Priority:** P2 | **AC:** AC-503 | **Status:** Missing
- **When** grouped count
- **Then** completes without loading full payloads

### EDGE-MG-011 — ActiveWaitsByEventName aggregation
- **Priority:** P1 | **AC:** MG-030 | **Status:** Missing (R3)
- **Given** many waits on same event name
- **When** statistics
- **Then** correct count without full list

### EDGE-MG-012 — Retry exactly at MaxAttempts boundary
- **Priority:** P1 | **AC:** AC-510 | **Status:** Partial
- **Edge:** last retry succeeds vs fails

### EDGE-MG-013 — Governance limit 1: two instances start
- **Priority:** P1 | **AC:** AC-511 | **Status:** Partial
- **Given** global concurrency 1
- **When** second start
- **Then** second blocks or queues per policy

### EDGE-MG-014 — Purge during active outbox dispatch
- **Priority:** P0 | **AC:** AC-314 | **Status:** Partial (certification rejects)
- **Edge:** race between pump and purge operator

### EDGE-MG-015 — Discard resume then event redelivery same correlation new id
- **Priority:** P1 | **AC:** AC-517 | **Status:** Missing
- **Given** discarded EventId-1
- **When** new EventId-2 same correlation
- **Then** may match if wait still active
