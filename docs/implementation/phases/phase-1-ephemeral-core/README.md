# Phase 1 — Ephemeral Engine Core (spec Slice 1)

**Goal**: the quick engine, complete per spec Slice 1: contracts, lifecycle, builder,
interpreter, serialized execution, waits/events/routing, management baseline,
cancel/terminate, named End outcomes, Yield, completion bridge.

**Entry criteria**: Phase 0 exit green.
**Exit criteria**: AC-001…015, AC-101…110, AC-115, AC-201…203, AC-501…503, AC-516 all green
as `[Trait("AC",...)]` tests in `OrcaCore.Acceptance.Tests`; zero warnings; no durable-only
concept anywhere in the public ephemeral surface (spec CR-020 API discipline).

Tasks T1-01…T1-05 are fully detailed (exemplars). T1-06…T1-15 are indexed below and are
expanded by **T1-05a** (a mini `Tn-00`: Sonnet-level, expands the remaining index using the
template, reading the then-existing code).

## Task index

| Task | Title | Difficulty | Depends | Summary |
|------|-------|-----------|---------|---------|
| [T1-01](T1-01-core-contracts.md) | Core contracts in Abstractions | Haiku | T0-03 | `IStep`, `StepContext`, `StepResult`, `EventEnvelope`, statuses, IDs, snapshots |
| [T1-02](T1-02-lifecycle-state-machine.md) | Lifecycle state machine | Haiku | T1-01 | Transition table, named triggers, illegal-trigger rejection (CR-030) |
| [T1-03](T1-03-definition-model.md) | Definition graph model | Haiku | T1-01 | Immutable composite tree, node types for Slice 1, execution-pointer frames (CR-003/015) |
| [T1-04](T1-04-builder-validation.md) | Fluent builder + accumulated validation | Haiku | T1-03 | `WorkflowBuilder<TState>` → `Validation<...>`; AC-008 (CR-001/002/005) |
| [T1-05](T1-05-interpreter-straight-line.md) | Interpreter: straight-line execution | Sonnet | T1-02..04 | `Init→Step→End`, failure path, engine facade + Start; AC-001, AC-004 |
| T1-05a | Expand T1-06…T1-15 into task files | Sonnet | T1-05 | Per 04-task-protocol §4 |
| T1-06 | Per-instance execution lane | Sonnet | T1-05 | Channel/semaphore lane; concurrent advancement serializes (CR-040/042); AC-006 |
| T1-07 | `If` + `While` | Haiku | T1-05 | Branch selection, loop re-evaluation; AC-002, AC-003 |
| T1-08 | Wait records + matching | Haiku | T1-06 | `WaitForEvent` result → wait record; `EventName`+`CorrelationId` matching; `ResumedEvent` payload delivery (EV-020…023); AC-101…103 |
| T1-09 | Mailbox buffering + dedup + transactional consumption | Sonnet | T1-08 | Pending events, bidirectional matching, `EventId` dedup, commit-before-remove (EV-030…032); AC-104, AC-105, AC-010 |
| T1-10 | Correlation index + three routing modes | Sonnet | T1-08 | Multi-map index, instance/correlation/fanout entry points, uniqueness at routing time (EV-010…012); AC-106…108 |
| T1-11 | Wait-in-loop isolation | Haiku | T1-07, T1-09 | Fresh wait per iteration (EV-043); AC-109 |
| T1-12 | `Parallel` + `WhenAll` | Sonnet | T1-06 | Concurrent branches, serialized branch commits, atomic join, branch-scoped waits (CP-001…003, CR-044); AC-201…203, AC-110, AC-007 |
| T1-13 | Management surface baseline | Sonnet | T1-05 | Scopes, constrained `Where` (internal query model + expression facade per OQ-6), `List/Count/Get/GetState/GetActiveWaits`, snapshots-only (MG-001…005, MG-010); AC-501…503, AC-009, AC-115 |
| T1-14 | Cancel/Terminate + named End outcomes + completion bridge | Haiku | T1-13 | CR-031, CR-008, CR-016; AC-011, AC-012, AC-014, AC-015, AC-005, AC-516 |
| T1-15 | `Yield` | Haiku | T1-06 | Commit-and-reschedule same step (CR-017); AC-013 (ephemeral part) |

## Phase-wide guardrails

- Everything public lands in Abstractions or the ephemeral engine facade; `WaitLong`,
  pause/resume, retry, history, archive/purge MUST NOT appear anywhere (durable-only).
- The acceptance tests for this phase are written against the *spec text* of each AC in
  [specs/12-acceptance-criteria.md](../../../specs/12-acceptance-criteria.md) — the task
  files list unit tests; ACs are separate tests in `OrcaCore.Acceptance.Tests`.
