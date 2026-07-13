# Missed Negative Tests & Edge-Case Scenarios

Companion to [R9-test-coverage-gaps.md](../findings/R9-test-coverage-gaps.md). Each file lists
**negative** scenarios (invalid input, rejection, failure paths, safety guards) and **edge-case**
scenarios (boundary values, races, ordering, restart mid-state) that the `tests` suite does
not yet cover or covers only shallowly.

> **Re-baseline 2026-07-04.** Statuses were re-checked against the current suite: every scenario
> whose ID a test references via `[Trait("Scenario", …)]` is now marked `Covered` (was stale at
> `Missing`/`Partial`). Current totals across the nine files: **~275 scenarios — 187 Missing,
> 56 Partial, 32 Covered.** `Covered` here means a behavioral test cites the scenario ID; a few
> `Partial` entries with thin assertions were not individually re-deepened. This backlog (chiefly
> the 187 Missing) is the largest outstanding pre-ship item; work it by area file, highest
> `Priority` first.

## How to read each file

| Column | Meaning |
|--------|---------|
| **ID** | Stable scenario ID for tracking (`NEG-*` negative, `EDGE-*` edge) |
| **Priority** | P0 = correctness/safety; P1 = spec gap; P2 = hardening |
| **AC / REQ** | Linked acceptance criterion or requirement |
| **Status** | `Missing`, `Partial` (shallow or wrong assertion), `Covered` (reference only) |

Suggested test naming: `{Area}Tests.{ScenarioId}_{ShortDescription}`.

**Integration testing** (cross-boundary, Testcontainers, multi-host): see
[`docs/review/integration-tests/`](../integration-tests/README.md).

## Files

| File | Scope |
|------|-------|
| [01-core-runtime-and-authoring.md](01-core-runtime-and-authoring.md) | Builder validation, step failure, lifecycle rejection, state isolation |
| [02-events-waits-timers.md](02-events-waits-timers.md) | Routing errors, mailbox, dedup, timer/event races |
| [03-durable-execution.md](03-durable-execution.md) | OCC conflicts, inbox poison/buffer, restart, versioning |
| [04-composition-and-children.md](04-composition-and-children.md) | Parallel/ForEach/RunChildren failure and boundary paths |
| [05-saga.md](05-saga.md) | Compensation failure, partial completion, idempotency |
| [06-management-and-operations.md](06-management-and-operations.md) | Destructive safety, pause/resume, eviction, pools |
| [07-providers.md](07-providers.md) | Store conflicts, dispatch failures, retention rejects |
| [08-hosting-and-integration.md](08-hosting-and-integration.md) | Hosted services, DI misconfiguration, cross-layer |
| [09-job-scheduler-dag-external-jobs.md](09-job-scheduler-dag-external-jobs.md) | DAG failures, external job timeouts, quota edges |

## Conventions

- Prefer **public API** entry points (`EphemeralWorkflowEngine`, `DurableCommandProcessor`, management fluent).
- Negative tests should assert **exception type + stable error code/message**, not only `Throws`.
- Edge cases involving time use `FakeTimeProvider`; concurrency uses `RaceCoordinator`.
- Tag with `[Trait("AC", "AC-xxx")]` when mapping to doc 12/14.
