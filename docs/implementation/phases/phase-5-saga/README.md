# Phase 5 — Saga (spec Slice 5)

**Goal**: the saga semantic kind: separate definition surface, compensation scopes and
handlers, reverse-order compensation, saga terminal states, durable tracking/audit,
operator recovery, child compensation. Ephemeral saga as an explicitly labeled in-process
only limited mode.

**Entry criteria**: Phase 4b exit green. Spec open question 12 is resolved to
compensation-heavy saga first; spec open question 13 is resolved to in-process-only
ephemeral saga mode.

**Ephemeral saga limit**: ephemeral saga support is in-process only. Its public API and
tests must not claim durable recovery, durable compensation audit, post-restart inspection,
or operator remediation. Those guarantees belong to durable saga tasks and provider-backed
projections.
**Exit criteria**: AC-401…409, AC-616 green.

## Task index (expanded by T5-00)

| Task | Title | Difficulty | Summary |
|------|-------|-----------|---------|
| T5-00 | Expand index | Sonnet | Template expansion |
| T5-01 | Saga contracts + lifecycle rows | Haiku | `Compensated`/`CompensationFailed` statuses + transition-table rows; saga event/command catalog additions (SG-001/013, spec DU-012 saga facts) |
| T5-02 | Saga builder: separate definition kind | Sonnet | `SagaBuilder<TState>`: forward steps with `CompensateBy`, compensation scopes, `Try/Catch/Finally` shape (SG-001…003); compensation APIs invisible on workflow builders |
| T5-03 | Compensation decision layer | Sonnet | Compensation stack as durable facts; reverse-success order + override; trigger rules (SG-010/011, SG-020); AC-401…403 |
| T5-04 | Compensation failure + terminal outcomes | Haiku | `CompensationFailed` distinct outcome; idempotent re-request (SG-012/013); AC-404, AC-409 |
| T5-05 | Timeout/cancellation interaction | Haiku | Policy-driven compensation on timeout; cancellation never implies compensation (SG-011/014); AC-405 |
| T5-06 | Durable audit + operator recovery | Sonnet | Compensation projection (forward actions, compensations, order, outcome); operator retry/manual-resolution recorded (SG-021/022); AC-406…408 |
| T5-07 | Child compensation | Sonnet | Explicit `Compensate(group, spec)`: one compensation per completed child, idempotent, durable-only (CP-035); AC-616 |
| T5-08 | Ephemeral saga limited mode | Haiku | Same semantics inside one process lifetime only; XML docs and implementation docs must state no durable recovery, durable audit, or post-restart operator remediation claims (SG-030; spec open question 13 resolved) |

## Phase-wide guardrails

- Saga is a separate definition kind on the same runtime substrate — no compensation
  concept may leak into regular workflow builders or the ephemeral composition surface
  (SG-001, CP-035).
- All compensation state transitions go through the same command/event pipeline —
  no side-channel compensation bookkeeping.
