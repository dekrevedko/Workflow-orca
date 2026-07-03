# Engine with Real Providers — Integration Scenarios

**Components:** `DurableCommandProcessor`, `DurableStartService`, `DurableManagement`,
`IWorkflowEventStore` / inbox / outbox / projection / timers on **real** PostgreSQL or SQL Server
(not `InMemoryWorkflowProvider`).

Certification and `PostgreSqlEventStoreTests` prove **store ports** in isolation. These scenarios
prove **engine + store** together across process and commit boundaries.

## Existing coverage

| Test | Scope | Limitation |
|------|-------|------------|
| `PostgreSqlEventStoreTests.PostgreSql_DurableWaitSurvivesRestart` | Store API only | No processor |
| `PostgreSqlProviderCertificationTests.*` | Certification harness | Not full workflow commands |
| `DurableRecoveryTests` | Processor + InMemory | No DB boundary |
| `R4DurableEngineFindingsTests` | Processor + InMemory/fakes | Regression, not PG |
| `PostgreSql_DuplicateEventsBeforeAndAfterRestartDedup` | Inbox on PG | No aggregate replay |

---

## Missing integration scenarios

### INT-EP-001 — Durable wait survive host restart (PostgreSQL)
- **Priority:** P0 | **AC:** AC-301 | **Status:** Partial (store-only test exists)
- **Components:** `DurableCommandProcessor` + `PostgreSqlWorkflowStore`
- **Act:** Start → wait registered → dispose store/processor → new processor same connection → deliver event
- **Assert:** `WorkflowWaitMatchedEvent`; projection `Waiting` → `Running`

### INT-EP-002 — Crash before commit leaves wait active (PostgreSQL)
- **Priority:** P0 | **AC:** AC-114 | **Status:** Missing
- **Components:** Processor + PG + inject append failure (transaction rollback)
- **Act:** Deliver event; fail commit; retry
- **Assert:** Inbox not `Applied`; wait still active; retry succeeds once

### INT-EP-003 — Concurrent resume serializes (PostgreSQL)
- **Priority:** P0 | **AC:** AC-309 | **Status:** Partial (InMemory pipeline test)
- **Components:** Two processors, shared PG, same instance
- **Act:** Parallel `DeliverEvent` same wait
- **Assert:** One `WorkflowWaitMatchedEvent`; other `Conflict` or `NoOp`

### INT-EP-004 — StartOrGet idempotency across process restart (PostgreSQL)
- **Priority:** P0 | **AC:** AC-311 | **Status:** Partial (InMemory R4 test)
- **Components:** `DurableStartService` + PG idempotency table
- **Act:** StartOrGet key → dispose → new starter same key
- **Assert:** Same `InstanceId`; single `WorkflowStartedEvent` in stream

### INT-EP-005 — Early event buffer on PostgreSQL (AC-104 durable)
- **Priority:** P0 | **AC:** AC-104 | **Status:** Missing
- **Act:** Deliver before wait → register wait
- **Assert:** `WorkflowDeliveryBufferedEvent` + match; inbox `Applied`

### INT-EP-006 — Outbox publish-after-commit (PostgreSQL + recording dispatcher)
- **Priority:** P0 | **AC:** AC-310 | **Status:** Missing
- **Components:** Processor + PG + `IMessageDispatcher` fake
- **Act:** Command with outbox row; fail DB commit on 2nd attempt
- **Assert:** Dispatcher never called until commit succeeds

### INT-EP-007 — Checkpoint + tail rehydrate from PostgreSQL
- **Priority:** P1 | **AC:** DU-013 | **Status:** Partial (InMemory)
- **Act:** Long stream with checkpoint; new processor loads tail only
- **Assert:** Active waits/timers match committed state

### INT-EP-008 — Continue-as-new with PostgreSQL retention of lineage
- **Priority:** P1 | **AC:** AC-313 | **Status:** Missing
- **Act:** Continue-as-new via processor; query management
- **Assert:** Same logical `InstanceId`; generation incremented in projection

### INT-EP-009 — Version conflict across two hosts (PostgreSQL OCC)
- **Priority:** P0 | **AC:** AC-309, AC-315 | **Status:** Partial (R4 in-memory)
- **Act:** Two processors append stale version
- **Assert:** One wins; loser gets conflict with **correct** actual version (R5 fix)

### INT-EP-010 — Pause / resume with buffered deliveries (PostgreSQL)
- **Priority:** P1 | **AC:** AC-513, AC-514 | **Status:** Partial (InMemory management tests)
- **Act:** Pause → deliver events → resume Replay on PG-backed processor
- **Assert:** Buffered rows in stream; matched in order

### INT-EP-011 — Resource pool acquire/release same transaction as events (PostgreSQL)
- **Priority:** P0 | **AC:** AC-518, AC-520 | **Status:** Missing
- **Components:** Processor + PG store + PG pool store
- **Act:** Acquire in command; fail append; success append + release
- **Assert:** No ghost tickets on failure; release on success

### INT-EP-012 — Purge instance removes all PG rows including timers
- **Priority:** P0 | **AC:** AC-314 | **Status:** Missing (R5)
- **Act:** Schedule timer → purge → `ClaimDueAsync`
- **Assert:** Empty; no ghost timer commands

### INT-EP-013 — SqlServer full engine path
- **Priority:** P0 | **AC:** PR-024 | **Status:** Partial
- **Note:** Container-backed `SqlServerWorkflowStore` now covers durable wait + processor restart, and SQL-backed resource-pool state is covered by provider certification plus INT-ST-013. Continue mirroring INT-EP-001…006 as SQL Server parity expands.

### INT-EP-014 — Inbox dedup across PG connection pool threads
- **Priority:** P1 | **AC:** AC-305 | **Status:** Partial
- **Act:** Parallel deliver same `EventId` through two processors
- **Assert:** Single applied outcome

### INT-EP-015 — Projection query matches committed wait (PostgreSQL)
- **Priority:** P1 | **AC:** AC-308, DU-070 | **Status:** Missing
- **Act:** Register wait; `DurableManagement.ListActiveWaitsAsync`
- **Assert:** Wait visible without loading payload from event stream

### INT-EP-016 — History pressure metrics after large append (PostgreSQL)
- **Priority:** P2 | **AC:** AC-312 | **Status:** Missing
- **Act:** Append N events; `GetPressureMetrics`
- **Assert:** Stream/checkpoint/outbox counts non-zero

### INT-EP-017 — Deserialize payload round-trip through PG serializer
- **Priority:** P1 | **AC:** NF-040 | **Status:** Missing
- **Act:** Commit business state blob; load via management
- **Assert:** Typed state equals original

### INT-EP-018 — WaitLong cold eviction query path (PostgreSQL)
- **Priority:** P1 | **AC:** AC-304 | **Status:** Missing
- **Act:** WaitLong → evict from memory (if activation layer exists) → event → rehydrate
- **Assert:** Resume from projection + stream only

---

## Test fixture sketch

```csharp
public sealed class PostgreSqlEngineFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; }
    public PostgreSqlWorkflowStore Store { get; private set; }

    public DurableCommandProcessor CreateProcessor() =>
        new(Store);

    public async Task<DurableCommandProcessor> CreateFreshProcessorAsync()
    {
        await using var _ = await CreateStoreAsync(); // new connection scope
        return new DurableCommandProcessor(Store);
    }
}
```

Place in `OrcaCore.Integration.Tests` referencing `OrcaCore.Providers.PostgreSql.Tests` patterns.

## Priority order

1. INT-EP-001, INT-EP-004, INT-EP-006 (durable correctness on real DB)
2. INT-EP-003, INT-EP-009 (concurrency)
3. INT-EP-005, INT-EP-011, INT-EP-012 (R4/R5 defect regression)
4. INT-EP-010, INT-EP-015 (management on real projections)
