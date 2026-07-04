# Durable Execution — Negative Tests & Edge Cases

Scope: event sourcing, checkpoints, inbox/outbox, versioning, idempotency, recovery, Continue-as-new.
Requirements: DU-*, AC-3xx.

## Existing negative coverage (reference)

| Area | Test(s) | What is proven |
|------|---------|----------------|
| Version conflict | `DurableCommandPipelineTests`, `PostgreSqlProviderCertificationTests` | `Conflict` outcome on stale append |
| Duplicate inbox | `DurableInboxTests` (AC-305) | Second delivery `NoOp` |
| Crash before commit | `DurableInboxTests` (AC-114) | Wait stays active; retry succeeds |
| Concurrent resume | `DurableCommandPipelineTests` (AC-309) | One commit, one `NoOp` |
| Incompatible version | `DurableVersioningTests` (AC-307) | Explicit failure diagnostics |
| StartOrGet in-process | `DurableVersioningTests` (AC-311) | Duplicate key same instance |
| StartOrGet restart | `R4DurableEngineFindingsTests` | Durable key after restart |
| Early event buffer | `R4DurableEngineFindingsTests` | Buffer + match (fix path) |
| Pool acquire rollback | `R4DurableEngineFindingsTests` | Append failure rolls back ticket |
| Cross-host stale append | `R4DurableEngineFindingsTests` | OCC rejection |
| Retention on active | `RetentionCertificationTests` (AC-314) | Archive/purge rejected |
| Purge with in-flight outbox | `RetentionCertificationTests` | Rejected without breaking dispatch |

---

## Missed negative tests

### NEG-DU-001 — Append with wrong ExpectedVersion twice
- **Priority:** P1 | **AC:** DU-022 | **Status:** Missing
- **Given** stream at version 5
- **When** two commands both expect version 4
- **Then** first may succeed; second `Conflict` with **actual** version 6 (not stale reporting)

### NEG-DU-002 — Command to non-existent stream
- **Priority:** P1 | **AC:** DU-011 | **Status:** Missing
- **Given** no `WorkflowStartedEvent` for id
- **When** `DeliverEvent` / `FireTimer` without start
- **Then** clear rejection; no orphan events

### NEG-DU-003 — Duplicate WorkflowStartedEvent same id
- **Priority:** P0 | **AC:** DU-053 | **Status:** Missing
- **Given** instance already started
- **When** second start command same `InstanceId` (not StartOrGet)
- **Then** conflict / rejected

### NEG-DU-004 — StartOrGet different payload same key after complete
- **Priority:** P1 | **AC:** AC-311 | **Status:** Missing
- **Given** completed instance bound to idempotency key
- **When** `StartOrGet` same key again
- **Then** returns existing terminal instance; does not restart (document policy)

### NEG-DU-005 — Checkpoint write without events
- **Priority:** P2 | **AC:** DU-010 | **Status:** Missing
- **Given** empty decision
- **When** checkpoint-only batch
- **Then** rejected or no-op per contract

### NEG-DU-006 — Outbox dispatch before commit visible
- **Priority:** P0 | **AC:** AC-310 | **Status:** Partial
- **Given** failing commit after dispatcher invoked (hook test)
- **When** observer records dispatch attempts
- **Then** zero dispatch before commit visible — **needs failure injection**

### NEG-DU-007 — Inbox poison on terminal instance
- **Priority:** P1 | **AC:** EV-031 | **Status:** Missing
- **Given** `Completed` instance
- **When** inbound event delivered
- **Then** poisoned or rejected; not applied

### NEG-DU-008 — Continue-as-new on non-running instance
- **Priority:** P1 | **AC:** AC-313 | **Status:** Missing
- **Given** `Failed` / `Cancelled` instance
- **When** Continue-as-new command
- **Then** rejected

### NEG-DU-009 — Continue-as-new loses identity
- **Priority:** P0 | **AC:** AC-313 | **Status:** Partial
- **Given** continue-as-new commits
- **When** query lineage / logical id
- **Then** must preserve documented identity — negative: new random id rejected

### NEG-DU-010 — Rehydrate from empty stream
- **Priority:** P1 | **AC:** DU-013 | **Status:** Missing
- **Given** provider returns empty tail
- **When** load aggregate
- **Then** not-found / invalid state

### NEG-DU-011 — Rehydrate corrupted tail (unknown event type)
- **Priority:** P1 | **AC:** DU-020 | **Status:** Missing
- **Given** stream with unrecognized event discriminator
- **When** replay
- **Then** explicit failure; no partial apply

### NEG-DU-012 — WaitLong eviction then deliver without rehydrate
- **Priority:** P1 | **AC:** AC-304 | **Status:** Missing
- **Given** cold-evicted instance
- **When** event arrives without activation
- **Then** lazy rehydrate + resume or buffer — not lost

### NEG-DU-013 — Management command on wrong status
- **Priority:** P1 | **AC:** MG-013 | **Status:** Partial
- **Given** `Running` instance
- **When** `ResumeAsync`
- **Then** rejected (not paused)

### NEG-DU-014 — Purge active instance without force
- **Priority:** P1 | **AC:** AC-314 | **Status:** Partial
- **Given** `Running` / `Waiting` instance
- **When** purge without safety semantics
- **Then** rejected; data intact

### NEG-DU-015 — Archive then append
- **Priority:** P1 | **AC:** DU-051 | **Status:** Missing
- **Given** archived instance
- **When** new command append
- **Then** rejected

### NEG-DU-016 — Projection apply out of order
- **Priority:** P1 | **AC:** DU-070 | **Status:** Missing
- **Given** projection batch with version gap
- **When** `ApplyAsync`
- **Then** reject or reconcile per provider contract

### NEG-DU-017 — Deserialize payload with wrong type name
- **Priority:** P0 | **AC:** NF-040 | **Status:** Missing
- **Given** committed payload envelope with untrusted type id
- **When** provider deserializes
- **Then** failure without arbitrary type load

---

## Edge-case scenarios

### EDGE-DU-001 — Restart mid-yield sequence
- **Priority:** P0 | **AC:** AC-013 | **Status:** Missing
- **Given** step yielded 2 of 5 times; crash
- **When** rehydrate
- **Then** resumes at yield 3; business state reflects 2 commits

### EDGE-DU-002 — Restart mid-outbox pump batch
- **Priority:** P1 | **AC:** DU-032 | **Status:** Missing
- **Given** 10 outbox rows claimed; crash after 3 dispatched
- **When** restart pump
- **Then** at-least-once safe; 3 may redispatch; no lost undispatched

### EDGE-DU-003 — Checkpoint at exact wait registration event
- **Priority:** P1 | **AC:** DU-013 | **Status:** Missing
- **Given** checkpoint written at `WorkflowWaitRegisteredEvent`
- **When** load with empty tail slice
- **Then** active wait present in materialized state (R4 checkpoint gap)

### EDGE-DU-004 — Tail replay with 10k events
- **Priority:** P2 | **AC:** DU-052 | **Status:** Missing
- **Given** long stream
- **When** rehydrate
- **Then** completes within budget; pressure metric updated

### EDGE-DU-005 — Concurrent commands different instances same process
- **Priority:** P1 | **AC:** CR-040 | **Status:** Missing
- **Given** 100 instances, parallel deliver
- **When** all commit
- **Then** no cross-instance version confusion

### EDGE-DU-006 — Idempotency key collision different definitions
- **Priority:** P1 | **AC:** DU-053 | **Status:** Missing
- **Given** same key, different `DefinitionId` in request
- **When** second StartOrGet
- **Then** policy: reject or return first — document and test

### EDGE-DU-007 — Version N instance suspended; deploy N+1 compatible
- **Priority:** P1 | **AC:** AC-306 | **Status:** Partial
- **Given** waiting on version 1; compatible version 2 deployed
- **When** resume
- **Then** continues on version 1

### EDGE-DU-008 — Version N instance; incompatible N+1
- **Priority:** P1 | **AC:** AC-307 | **Status:** Covered (unit)
- **Edge:** failure message includes suspended step id

### EDGE-DU-009 — Continue-as-new at history size threshold
- **Priority:** P1 | **AC:** AC-313 | **Status:** Partial
- **Given** policy triggers rollover at N events
- **When** threshold hit mid-step
- **Then** rollover at safe boundary only

### EDGE-DU-010 — Inbox dedup across Continue-as-new boundary
- **Priority:** P1 | **AC:** EV-031 | **Status:** Missing
- **Given** EventId consumed pre-rollover
- **When** redelivered post-rollover
- **Then** still deduped globally per instance lineage

### EDGE-DU-011 — Double pause command
- **Priority:** P2 | **AC:** AC-512 | **Status:** Missing
- **Given** already `Paused`
- **When** pause again
- **Then** idempotent no-op

### EDGE-DU-012 — Resume during in-flight command
- **Priority:** P1 | **AC:** AC-514 | **Status:** Missing
- **Given** slow append in progress; resume arrives
- **Then** serialized outcome; buffered replay order preserved

### EDGE-DU-013 — Eviction during command processing
- **Priority:** P1 | **AC:** AC-506 | **Status:** Missing
- **Given** idle instance reactivated; eviction races deliver
- **When** concurrent
- **Then** at most one mutator; no lost command

### EDGE-DU-014 — StreamVersion.Empty first append races
- **Priority:** P1 | **AC:** DU-022 | **Status:** Partial
- **Given** two starters same new instance id (bug scenario)
- **When** parallel first append
- **Then** exactly one `WorkflowStartedEvent`

### EDGE-DU-015 — LoadTail from middle version
- **Priority:** P2 | **AC:** DU-071 | **Status:** Missing
- **Given** partial tail from version V
- **When** aggregate rebuild
- **Then** equivalent to full replay from start
