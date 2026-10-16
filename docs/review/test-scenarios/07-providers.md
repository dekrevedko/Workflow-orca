# Providers — Negative Tests & Edge Cases

Scope: `OrcaCore.Providers.*`, certification harness, event store, inbox/outbox, projections,
timers, resource pools, message dispatchers. Requirements: PR-*, DU-030…033, AC-3xx [provider].

## Existing negative coverage (reference)

| Area | Test(s) | What is proven |
|------|---------|----------------|
| Append version conflict | `EventStoreCertificationTests`, PostgreSQL tests | Conflict result |
| Commit fail no timer leak | `PostgreSqlProviderCertificationTests` | Timer not scheduled if commit fails |
| Retention on active | `RetentionCertificationTests` | Rejected |
| Purge with pending outbox | `RetentionCertificationTests` | Rejected |
| Dispatcher retryable | `RabbitMqMessageDispatcherTests`, ZeroMQ tests | `RetryableFailure` |
| Dispatcher permanent | RabbitMQ tests | `PermanentFailure` |
| PostgreSQL conflict actual version | `PostgreSqlProviderCertificationTests` | (fix path for wrong version reporting) |
| InMemory reference certification | `InMemoryProviderCertificationTests` | Full harness |

---

## Missed negative tests

### NEG-PR-001 — SqlServer store restart loses data (stub)
- **Priority:** P0 | **AC:** PR-024 | **Status:** Missing
- **Given** events appended via `SqlServerWorkflowStore`
- **When** new store instance same connection string
- **Then** events visible — **expect fail until real SQL**

### NEG-PR-002 — Redis projection second instance empty
- **Priority:** P0 | **AC:** PR-013 | **Status:** Missing
- **Given** snapshots written with Redis adapter
- **When** new projection store instance
- **Then** reads same data — **expect fail today**

### NEG-PR-003 — Append empty batch
- **Priority:** P1 | **AC:** PR-010 | **Status:** Covered
- **When** `AppendAsync` empty events and no side effects
- **Then** reject or no-op per contract

### NEG-PR-004 — LoadTail negative/large version
- **Priority:** P2 | **AC:** PR-010 | **Status:** Missing
- **When** `LoadTailAsync` from version > head
- **Then** empty tail or error

### NEG-PR-005 — Inbox get unknown EventId
- **Priority:** P2 | **AC:** DU-030 | **Status:** Missing
- **When** `GetAsync` missing id
- **Then** not found; not throw

### NEG-PR-006 — Outbox claim with zero due records
- **Priority:** P2 | **AC:** DU-032 | **Status:** Covered
- **When** `ClaimDueAsync`
- **Then** empty batch; no lock leak

### NEG-PR-007 — Outbox mark dispatched unknown id
- **Priority:** P1 | **AC:** DU-032 | **Status:** Covered
- **When** mark dispatched orphan id
- **Then** error or no-op

### NEG-PR-008 — Double mark dispatched same outbox row
- **Priority:** P1 | **AC:** DU-032 | **Status:** Covered
- **Then** idempotent second call

### NEG-PR-009 — RabbitMQ publish without confirm wait
- **Priority:** P0 | **AC:** DU-032 | **Status:** Missing (R5)
- **Given** channel closes before confirm
- **When** publish returns
- **Then** must not report `Confirmed`

### NEG-PR-010 — RabbitMQ unroutable mandatory message
- **Priority:** P1 | **AC:** PR-015 | **Status:** Partial
- **When** publish to missing exchange
- **Then** `PermanentFailure`

### NEG-PR-011 — ZeroMQ peer down mid-send
- **Priority:** P1 | **Status:** Partial
- **When** dispatch
- **Then** `RetryableFailure`; no partial state in outbox

### NEG-PR-012 — PostgreSQL unique violation wrong actual version
- **Priority:** P1 | **AC:** PR-021 | **Status:** Partial (R5 fix test)
- **When** concurrent append conflict
- **Then** `actualVersion` != `expectedVersion` in conflict object

### NEG-PR-013 — Serializable pool deadlock retry
- **Priority:** P1 | **AC:** AC-518 | **Status:** Missing
- **Given** concurrent acquires causing PG deadlock
- **When** one transaction aborted
- **Then** retry succeeds; capacity never exceeded

### NEG-PR-014 — Pool acquire after pool deleted
- **Priority:** P1 | **AC:** MG-062 | **Status:** Missing
- **When** acquire on dropped pool
- **Then** clear error

### NEG-PR-015 — Timer claim for purged instance
- **Priority:** P0 | **AC:** AC-314 | **Status:** Missing (R5)
- **Given** instance purged; timer row remains
- **When** claim due
- **Then** skip or delete orphan

### NEG-PR-016 — Projection apply null snapshot operation
- **Priority:** P2 | **AC:** PR-013 | **Status:** Missing
- **When** malformed apply batch
- **Then** reject entire batch

### NEG-PR-017 — Deserialize unknown payload type in store
- **Priority:** P0 | **AC:** NF-040 | **Status:** Missing
- **Given** corrupted type discriminator in DB
- **When** load
- **Then** safe failure

### NEG-PR-018 — SQL injection via pool name / correlation (parameterized)
- **Priority:** P0 | **AC:** NF-040 | **Status:** Covered
- **Given** malicious string in pool id field
- **When** query
- **Then** parameterized; no execution

### NEG-PR-019 — InitializeAsync fails (DB down)
- **Priority:** P1 | **AC:** PR-020 | **Status:** Missing
- **When** provider init
- **Then** host fails fast; no partial schema

### NEG-PR-020 — Certification: stub provider must not pass as full
- **Priority:** P0 | **AC:** PR-024 | **Status:** Missing
- **Given** SqlServer in-memory stub
- **When** run full certification
- **Then** skipped or failed category — not green

---

## Edge-case scenarios

### EDGE-PR-001 — Append batch at max event size
- **Priority:** P2 | **AC:** DU-052 | **Status:** Missing
- **Given** large payload near limit
- **When** append
- **Then** success or explicit size error

### EDGE-PR-002 — Concurrent append same stream exactly one wins
- **Priority:** P0 | **AC:** AC-309 | **Status:** Partial
- **Given** two writers version N
- **When** parallel append
- **Then** one success one conflict

### EDGE-PR-003 — Inbox dedup across connection pool threads
- **Priority:** P1 | **AC:** EV-031 | **Status:** Missing
- **Given** same EventId parallel deliver
- **Then** one Applied

### EDGE-PR-004 — Outbox claim lease expires redelivery
- **Priority:** P1 | **AC:** DU-032 | **Status:** Missing
- **Given** claimant crashes mid-batch
- **When** lease expires
- **Then** another host reclaims

### EDGE-PR-005 — Timer due same millisecond ordering
- **Priority:** P2 | **AC:** EV-050 | **Status:** Missing
- **Given** 100 timers same due time
- **When** claim
- **Then** deterministic order; all eventually claimed

### EDGE-PR-006 — Retention archive exactly at age boundary
- **Priority:** P1 | **AC:** AC-314 | **Status:** Missing
- **Given** policy age = 30 days; instance 30 days old
- **Then** inclusive/exclusive per policy

### EDGE-PR-007 — Purge cascades all instance tables
- **Priority:** P1 | **AC:** AC-314 | **Status:** Partial
- **After** purge
- **Then** zero rows events, outbox, inbox refs, timers, waits, projections

### EDGE-PR-008 — Continue-as-new stream linkage in store
- **Priority:** P1 | **AC:** AC-313 | **Status:** Missing
- **Given** rollover event
- **When** load by logical id
- **Then** correct stream chain

### EDGE-PR-009 — Resource pool capacity 1 FIFO wake order
- **Priority:** P1 | **AC:** AC-519 | **Status:** Partial
- **Edge:** 10 waiters; 10 sequential releases → FIFO grants

### EDGE-PR-010 — Message dispatcher oversized payload
- **Priority:** P2 | **AC:** PR-015 | **Status:** Missing
- **When** dispatch > broker limit
- **Then** `PermanentFailure`

### EDGE-PR-011 — PostgreSQL migration idempotent re-init
- **Priority:** P2 | **AC:** PR-020 | **Status:** Missing
- **When** `InitializeAsync` twice
- **Then** no error; schema unchanged

### EDGE-PR-012 — InMemory provider thread safety stress
- **Priority:** P1 | **AC:** NF-010 | **Status:** Missing
- **Given** parallel append/load 50 threads
- **Then** no corruption

### EDGE-PR-013 — ZeroMQ multipart message boundary
- **Priority:** P2 | **Status:** Missing
- **Given** headers + body frames
- **When** dispatch/receive
- **Then** round-trip exact bytes

### EDGE-PR-014 — Pressure metrics at zero
- **Priority:** P2 | **AC:** DU-052 | **Status:** Missing
- **Given** empty store
- **When** `GetPressureMetrics`
- **Then** zeros; not null

### EDGE-PR-015 — Cross-provider: InMemory vs PostgreSQL parity
- **Priority:** P1 | **AC:** PR-024 | **Status:** Partial
- **Given** same certification scenario
- **When** run on both
- **Then** same pass/fail semantics
