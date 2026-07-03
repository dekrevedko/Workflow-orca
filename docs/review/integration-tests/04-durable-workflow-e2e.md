# Durable Workflow E2E — Integration Scenarios

**Components:** Public durable surface (`DurableCommandProcessor`, `DurableManagement`,
`DurableStartService`) + **real** `PostgreSqlWorkflowStore` (or future SqlServer), optionally
`IHost` for timers/outbox. Maps acceptance tests that today use `InMemoryWorkflowProvider` to
provider-backed runs.

## Acceptance tests to port (InMemory → PostgreSQL)

| Acceptance test class | Key ACs | Port priority |
|----------------------|---------|---------------|
| `WaitAcceptanceTests` | AC-101…103 | P0 |
| `MailboxAcceptanceTests` | AC-104, AC-105 | P0 |
| `RetentionAcceptanceTests` | AC-314 | P0 |
| `ContinueAsNewAcceptanceTests` | AC-313 | P1 |
| `ChildWorkflowAcceptanceTests` | AC-606…614 | P1 |
| `ExternalJobAcceptanceTests` | JS-AC-004, 013 | P1 |
| `DagObservabilityAcceptanceTests` | JS-AC-005 | P1 |
| `PolicyAcceptanceTests` (durable path) | AC-113 | P2 |

**Approach:** Extract shared arrange/act helpers to `OrcaCore.TestSupport`; parameterized fixture
`StoreBackend.InMemory | PostgreSql` or duplicate thin integration classes with `[Trait("Category","Integration")]`.

---

## Missing E2E scenarios

### INT-E2E-001 — Straight-line durable start → complete on PostgreSQL
- **Priority:** P0 | **AC:** AC-001 | **Status:** Missing
- **Act:** Start → step completed events → terminal
- **Assert:** Projection `Completed`; stream tail consistent

### INT-E2E-002 — Durable wait + event delivery on PostgreSQL
- **Priority:** P0 | **AC:** AC-102, AC-301 | **Status:** Missing
- **Act:** Register wait (via commands) → deliver → match
- **Assert:** Single resume; payload available in subsequent step event

### INT-E2E-003 — Mailbox AC-104 on PostgreSQL
- **Priority:** P0 | **AC:** AC-104 | **Status:** Missing
- Mirror `MailboxAcceptanceTests` with processor + PG

### INT-E2E-004 — Dedup AC-105 across processor restart
- **Priority:** P0 | **AC:** AC-105 | **Status:** Missing
- **Act:** Duplicate `EventId` before/after new processor instance
- **Assert:** One match event in stream

### INT-E2E-005 — Child workflow WhenAll on PostgreSQL
- **Priority:** P1 | **AC:** AC-606 | **Status:** Missing
- **Act:** `RunChildren` → complete children → parent continues
- **Assert:** Lineage in projection; child streams exist in PG

### INT-E2E-006 — Child throttle continuation on PostgreSQL
- **Priority:** P0 | **AC:** AC-609 | **Status:** Missing
- **Act:** 4 children, maxConcurrency 2; complete one; inspect outbox
- **Assert:** Third `child-start` appears (regression for R6 P0)

### INT-E2E-007 — External job composite on PostgreSQL + fake dispatcher
- **Priority:** P1 | **AC:** JS-AC-004, JS-AC-010 | **Status:** Missing
- **Act:** `RunExternalJob` → outbox start → complete event
- **Assert:** Ticket + wait + single completion

### INT-E2E-008 — Saga durable E2E (when interpreter exists)
- **Priority:** P0 | **AC:** AC-406 | **Status:** Missing
- **Act:** Forward steps → crash → resume → complete without duplicate compensations

### INT-E2E-009 — Pause / resume buffered events on PostgreSQL
- **Priority:** P1 | **AC:** AC-512…514 | **Status:** Missing
- Port `DurableManagementTests` pause scenarios to PG fixture

### INT-E2E-010 — Archive / purge terminal instance on PostgreSQL
- **Priority:** P1 | **AC:** AC-314 | **Status:** Missing
- Port `RetentionAcceptanceTests` + timer row assertion

### INT-E2E-011 — Version binding under PG deploy simulation
- **Priority:** P1 | **AC:** AC-306, AC-307 | **Status:** Missing
- **Act:** Start v1; register v2 incompatible; resume suspended v1 instance
- **Assert:** Explicit failure with diagnostics

### INT-E2E-012 — Durable statistics and active wait query on PG
- **Priority:** P1 | **AC:** AC-308, AC-503 | **Status:** Missing
- **Act:** Multiple instances/waits; `StatisticsAsync` / `ListActiveWaitsAsync`
- **Assert:** Correct counts without full table scan timeout (NF-030 budget)

### INT-E2E-013 — DAG reconstruct on PostgreSQL stream
- **Priority:** P1 | **AC:** JS-AC-005 | **Status:** Missing
- Port `DagObservabilityAcceptanceTests` with real store tail load

### INT-E2E-014 — Resource pool FIFO wait on PostgreSQL
- **Priority:** P1 | **AC:** AC-519, JS-AC-013 | **Status:** Partial (certification)
- **Act:** Exhaust pool; two instances wait; release → FIFO grant through processor

### INT-E2E-015 — Yield durable crash recovery
- **Priority:** P0 | **AC:** AC-013 | **Status:** Missing
- **Act:** Multi-yield step; crash between yields; resume
- **Assert:** Progress preserved; single terminal completion

### INT-E2E-016 — Parallel waits different branches same correlation (durable)
- **Priority:** P0 | **AC:** AC-110 | **Status:** Missing
- **Act:** Two active waits; deliver with branch disambiguation
- **Assert:** Both branches resume; join completes

### INT-E2E-017 — Parent resume token barrier under PG
- **Priority:** P1 | **AC:** AC-610, AC-611 | **Status:** Missing
- **Act:** Concurrent child completions
- **Assert:** Parent continues once; token consume idempotent

### INT-E2E-018 — Unified outbox mixed kinds single commit
- **Priority:** P1 | **AC:** AC-613 | **Status:** Partial
- **Act:** Child-start + external-job-start in one batch
- **Assert:** Both rows in PG outbox table; pump dispatches both

---

## Implementation pattern

```csharp
[Collection(nameof(PostgreSqlCollection))]
[Trait("Category", "Integration")]
[Trait("Container", "PostgreSql")]
public sealed class DurableWaitPostgreSqlIntegrationTests(PostgreSqlEngineFixture fixture)
{
    [Fact]
    [Trait("AC", "AC-301")]
    public async Task WaitSurvivesProcessorRestart() { /* … */ }
}
```

Share one container per collection to amortize startup (~3–5s per collection vs per test).

## Priority order

1. INT-E2E-001…004 (core durable + mailbox)
2. INT-E2E-006, INT-E2E-015, INT-E2E-016 (known defect areas)
3. INT-E2E-005, INT-E2E-007, INT-E2E-010 (composition + retention)
4. Remaining ports from acceptance table
