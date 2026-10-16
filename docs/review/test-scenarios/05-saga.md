# Saga — Negative Tests & Edge Cases

Scope: forward steps, compensation order, failure outcomes, durable vs ephemeral saga paths.
Requirements: SG-*, AC-4xx.

## Existing negative coverage (reference)

| Area | Test(s) | What is proven |
|------|---------|----------------|
| Success no compensation | `SagaAcceptanceTests`, `EphemeralSagaTests` (AC-401) | Forward only → `Completed` |
| Failure triggers compensation | `SagaAcceptanceTests` (AC-402) | Eligible steps compensated |
| Compensation order | `SagaAcceptanceTests` (AC-403) | Reverse completion order |
| Compensation failure terminal | `CompensationFailureTests` (AC-404) | `CompensationFailed` status |
| Timeout + compensation | `SagaPolicyInteractionTests` (AC-405) | Policy interaction |
| Repeat compensation idempotent | `CompensationFailureTests` (AC-409) | Second request no-op |
| Child compensation explicit | `ChildCompensationTests` (AC-616) | No implicit on cancel |
| Manual recovery | `SagaAuditTests` (AC-408) | Intervention recorded |
| Durable aggregate audit | `SagaAuditTests` (AC-407) | Forward/comp list on inspect |

---

## Missed negative tests

### NEG-SG-001 — Compensate with no forward completions
- **Priority:** P1 | **AC:** SG-011 | **Status:** Missing
- **Given** saga fails on first forward step
- **When** compensation evaluated
- **Then** zero compensations run

### NEG-SG-002 — Forward step fails during compensation
- **Priority:** P1 | **AC:** SG-013 | **Status:** Missing
- **Given** compensation in progress
- **When** second compensation action throws
- **Then** `CompensationFailed`; prior compensations not rolled back

### NEG-SG-003 — Complete compensation too early (multi-action)
- **Priority:** P0 | **AC:** SG-010 | **Status:** Missing (R4 P1)
- **Given** two `SagaCompensationStarted` events
- **When** first `CompleteSagaCompensation`
- **Then** must **not** terminal `Compensated` until second completes

### NEG-SG-004 — Request compensation twice concurrently
- **Priority:** P1 | **AC:** SG-012 | **Status:** Partial
- **Given** saga already compensating
- **When** parallel `RequestSagaCompensation`
- **Then** single plan; idempotent

### NEG-SG-005 — Compensate non-compensatable step
- **Priority:** P1 | **AC:** SG-011 | **Status:** Missing
- **Given** forward step marked non-compensatable
- **When** failure occurs later
- **Then** skipped in compensation plan

### NEG-SG-006 — Saga builder: empty forward chain
- **Priority:** P2 | **AC:** SG-001 | **Status:** Missing
- **When** build saga with no steps
- **Then** validation error

### NEG-SG-007 — Durable saga via public API absent
- **Priority:** P1 | **AC:** AC-406 | **Status:** Missing
- **Given** durable host
- **When** attempt `StartSagaAsync` equivalent
- **Then** clear not-supported OR full E2E if implemented

### NEG-SG-008 — Manual recovery on wrong status
- **Priority:** P1 | **AC:** AC-408 | **Status:** Missing
- **Given** saga `Completed`
- **When** operator recovery command
- **Then** rejected

### NEG-SG-009 — Manual recovery on CompensationFailed twice
- **Priority:** P1 | **AC:** AC-408 | **Status:** Missing
- **Given** already recovered to `Running` or `Cancelled`
- **When** second recovery
- **Then** idempotent or rejected

### NEG-SG-010 — Compensation override order invalid
- **Priority:** P2 | **AC:** SG-010 | **Status:** Missing
- **Given** explicit override references unknown action id
- **When** plan built
- **Then** validation error

### NEG-SG-011 — Saga scope isolation
- **Priority:** P1 | **AC:** SG-020 | **Status:** Missing
- **Given** two sagas same instance different scopes
- **When** one fails
- **Then** other scope unaffected

### NEG-SG-012 — Ephemeral saga labeled limited — durable feature rejected
- **Priority:** P2 | **AC:** SG-030 | **Status:** Partial (`EphemeralSagaTests` doc test)
- **When** cold-wait saga feature on ephemeral
- **Then** build/runtime rejection

---

## Edge-case scenarios

### EDGE-SG-001 — Fail on last forward step
- **Priority:** P1 | **AC:** AC-402 | **Status:** Covered
- **Edge:** only one compensation needed

### EDGE-SG-002 — Fail on first forward step after many compensatable skipped
- **Priority:** P1 | **Status:** Missing
- **Given** steps 1–3 non-compensatable, step 4 fails
- **Then** compensate 1–3 only if policy says eligible

### EDGE-SG-003 — Compensation order with partial forward completion
- **Priority:** P1 | **AC:** AC-403 | **Status:** Covered
- **Edge:** steps A,B,C complete; D fails → compensate C,B,A

### EDGE-SG-004 — Timeout on forward step triggers compensation mid-saga
- **Priority:** P1 | **AC:** AC-405 | **Status:** Partial
- **Given** timeout policy `Compensate`
- **When** timeout fires
- **Then** same order as failure path

### EDGE-SG-005 — Durable restart mid-compensation between actions
- **Priority:** P0 | **AC:** AC-406 | **Status:** Missing
- **Given** first compensation completed; crash before second
- **When** rehydrate
- **Then** resume second only; no repeat first

### EDGE-SG-006 — Durable restart mid-forward before commit
- **Priority:** P0 | **AC:** AC-406 | **Status:** Missing
- **Given** forward action executed but not committed
- **When** restart
- **Then** forward action not duplicated

### EDGE-SG-007 — Saga audit under Continue-as-new
- **Priority:** P2 | **AC:** AC-407 | **Status:** Missing
- **Given** saga spans rollover
- **When** audit query
- **Then** full history or segment per policy

### EDGE-SG-008 — Child saga compensation via `Compensate(group)`
- **Priority:** P1 | **AC:** AC-616 | **Status:** Partial
- **Given** 3 completed children
- **When** compensate group
- **Then** exactly 3 compensation children; order deterministic

### EDGE-SG-009 — Compensation failure then manual retry compensation
- **Priority:** P1 | **AC:** AC-408 | **Status:** Missing
- **Given** `CompensationFailed`
- **When** operator retry compensation
- **Then** idempotent resume from failed action

### EDGE-SG-010 — Concurrent forward steps (if ever allowed)
- **Priority:** P2 | **Status:** Missing
- **Given** parallel forward (non-standard)
- **Then** compensation order still defined

### EDGE-SG-011 — Saga terminal during active child workflows
- **Priority:** P1 | **AC:** CP-026 | **Status:** Missing
- **Given** saga step spawned children
- **When** saga fails
- **Then** children handled per child failure policy

### EDGE-SG-012 — Policy: compensation timeout on slow compensate step
- **Priority:** P2 | **AC:** SG-014 | **Status:** Missing
- **Given** compensating action exceeds timeout
- **Then** `CompensationFailed` with reason
