# Applied OrcaCore Documentation Fixes

Date: 2026-07-06

## Files updated

- `16-requirements-durable-driver.md`
- `01-architecture.md`
- `02-requirements.md`
- `03-acceptance-criteria.md`
- `04-traceability.md`
- `end-to-end-plan.md`
- `production-readiness.md`
- `eks-scheduler-handoff.md`
- `README.md`
- `OT1-00-durable-seams-review.md`
- `OT1-01-instance-grain-start.md`
- `OT2-00-expand-task-index.md`
- `OT5-00-expand-task-index.md`

## Key fixes

1. Reconciled the durable-driver and Orleans turn models:
   - `OE-011` now says one grain turn processes one durable advancement segment.
   - Individual kernel commands remain the atomic commit unit inside that segment.
   - Direct command-envelope grain calls are documented as the degenerate case: one segment containing one kernel command.

2. Corrected durable `ForEach` semantics:
   - `DR-010` no longer lists lightweight `ForEach` as a durable-supported shape.
   - Durable fanout is expressed through `RunChild` / `RunChildren` and DAG-owned child scheduling.
   - Added `DR-AC-016` to require durable registration to fail fast for ephemeral-only `ForEach`.

3. Tightened durable checkpoint semantics:
   - `DR-011` now requires post-step business state and execution position to be persisted for wait, yield, failure, child dispatch, and terminal commands, not only completed steps.
   - `DR-012` now requires deterministic checkpoint envelope migration or diagnostic parking; guessing execution position is forbidden.
   - Added `DR-AC-013` and `DR-AC-018`.

4. Clarified restart-safe continuation behavior:
   - Continuation records are internal outbox records.
   - External dispatchers must ignore them.
   - A dedicated continuation pump owns continuation records.
   - Stale continuations must become no-op processed records, not duplicate execution.
   - Added `DR-AC-014` and `DR-AC-015`.

5. Added hard segment budgets:
   - `DR-051` now requires `MaxCommandsPerSegment` and `MaxSegmentDuration`.
   - Over-budget runnable instances must leave a durable continuation and release the lane/turn.
   - `OE-012` and `OE-AC-046` now align with this behavior.
   - Added `DR-AC-017`.

6. Tightened external side-effect guidance:
   - `Yield` is safe only for idempotent or idempotency-key-guarded chunks.
   - Non-idempotent external work belongs in external jobs/outbox-backed dispatch.
   - EKS scheduler handoff now explicitly states Kubernetes jobs must be dispatched work, not inline workflow/grain work.

7. Updated execution planning and readiness:
   - `end-to-end-plan.md` now gates definition-driven durable e2e on DR-P1 and DR-P2.
   - `production-readiness.md` now says production durable-engine claims require document-16 driver gates and provider certification.
   - `OT5-00` now asks the parity subset to include relevant `DR-AC` criteria.

8. Fixed task-plan typo and timer gate:
   - `OT1-00` DoD now says all four seams, not three.
   - `OT2-00` now makes claimed-timer loss semantics a hard O2 entry gate.

## Consistency checks performed

Searched the updated markdown set for stale phrases:

- `Turn = one command`
- `one grain turn = one DU-011 command`
- `run-to-suspension is one command cycle`
- `all three seams`
- `leave unbounded with diagnostics`

No stale matches remain in the updated `/mnt/data/*.md` files.
