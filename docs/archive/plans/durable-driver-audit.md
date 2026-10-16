# Durable driver implementation audit

The original scope check was performed against
`docs/specs/16-requirements-durable-driver.md` (DR) and
`docs/specs/06-requirements-durable-execution.md` (DU) on 2026-07-06, before driver
implementation began. The source observations in "The seam" and "Command-contract deltas"
describe that baseline unless a later date is stated. The normative requirements were
reconciled with the implementation review on 2026-07-12; this document records evidence and
remaining conformance work, not alternative requirements.

## The seam

`DurableCommandProcessor.ProcessAsync(...)` is the single decision/commit path: every
overload funnels into `RunInLaneAsync` → `DurableWorkflowAggregate.Decide*` →
`DurableCommitPipeline.CommitAsync` (expected-version append + checkpoint + inbox/outbox +
projections + timer schedules in one `ProviderCommitBatch`). The driver drives through
these overloads only (DR-002). The overloads for `DurableStepCompleted/StepFailed/
WaitRegistered/WaitMatched/DeliverEvent/Complete/Fail/Pause/Resume` are `internal`
(OT1-01a); DR-003 is satisfied by promoting those overloads plus their command records to
public as one reviewed seam — no new dispatch abstraction is needed, the processor *is*
the seam. The interpreter mirrors `Engine.Ephemeral/Execution/Interpreter.cs` node
dispatch, but replaces in-memory continuations with a persisted frame stack (CR-015
analogue of `ExecutionPointer`/`ExecutionFrame`).

## Command-contract deltas

- New public versioned envelope (Abstractions): execution position (frame stack: node
  path, sequence index, loop iteration, branch progress, child-group/join, saga cursor,
  yield cursor) + business state, serialized as the checkpoint `Payload` under a
  discriminating content type. Legacy payloads (other content types) trigger the DR-012
  park path, never a guessed position (DR-AC-018).
- Extend: `DurableStepCompletedCommand` (+position), `DurableStepFailedCommand`
  (+last committed state, position, and diagnostics after policy exhaustion),
  `DurableWaitRegisteredCommand` (+state+position),
  `ScheduleTimerCommand` (+position, +state when preceding node mutated),
  `DurableCompleteCommand` (+final state+position), `DurableYieldCommand` and
  `DurableRunChild(ren)Command` (+state+position). `WorkflowStatus` gains `Parked`.
- `OutboxClaimRequest` gains a kind selector (include/exclude); `InMemory` + `PostgreSql`
  providers honor it; external pump excludes `continue`, continuation pump includes only
  `continue` (DR-037). Certification tests added.
- **Start input durability (now DR-018).** The baseline `DurableStartService` accepted
  `Input` but dropped it; `StartWorkflowCommand`/`WorkflowStartedEvent` carried no input
  payload. The required additive fields are `InputContentType`/`InputPayload` on both
  records, with DR-AC-023 covering the crash between the start commit and `Init`.
- **Park facts and re-arm (DR-017).** Parking is a durable lifecycle change and must be a
  committed fact: `DurableParkCommand`/`WorkflowParkedEvent` carry reason, diagnostic,
  durable attempt count, position version, and timestamp. Re-arm is an explicit,
  expected-version management operation with reason-specific preconditions; definition
  registration or migration installation alone does not unpark an instance.
- Wait-timeout race (`WaitNode.Timeout`, DR-AC-020): kernel keeps wait and timer
  unlinked. Race arbitration is positional: the winning resume commits a position past
  the race; the loser must resolve under EV-044/051. A timeout win cancels and removes the
  wait in the same commit. A distinct late event is unmatched/rejected or handled under the
  documented mailbox policy — never mislabeled as an `EventId` duplicate — while a late
  timer firing on a consumed/cancelled timer is a no-op.

## Reconciliation status (2026-07-12 working tree)

- Start input fields now exist on `StartWorkflowCommand` and `WorkflowStartedEvent`;
  DR-AC-023 proves typed input recovery before `Init` and reject-before-commit serialization
  failure.
- Execution envelopes, driver/continuation components, `Parked`, and kind-selective outbox
  claims are present. In-memory, PostgreSQL, and SQL Server implementations and certification
  coverage exist; DR-AC-029 is green in the reconciled test tree.
- DR-019 durable policy state now lives on the execution cursor. Authored fixed retry backoff
  commits the next attempt, eligibility time, stable logical-operation key, rolled-back state,
  and a durable timer before releasing the host; DR-AC-024 resumes attempt 2 on a replacement
  host. Timeout admission commits an absolute deadline before user code starts, so a replacement
  host cannot reset elapsed time. `WithCancellation` registers the running step token with the
  host command runtime, allowing operator cancellation to stop it and commit `Cancelled` without
  entering retry; the two DR-AC-025 facts cover deadline expiry and cooperative cancellation.
- Continuation disposition paths now distinguish retryable failures from dispatched
  suspension/terminal/no-op outcomes. A drive failure retains its claimed signals as retryable
  even after recording the durable failure fact, because a `Waiting` parallel shape can have a
  runnable sibling without a checkpoint in that failure decision. Zero-progress budget exhaustion
  likewise keeps the existing claim retryable instead of consuming the instance's only signal.
  Cancellation, lease-loss, and transient-store variants still keep DR-AC-026 open as a full
  fault-matrix gate.
- Poison attempt count, unresolved position version, and next-eligible time are workflow facts
  and checkpoint state. Exponential backoff and threshold parking therefore survive host
  replacement. DR-AC-027 now alternates three newly constructed hosts and proves attempts
  1 and 2 remain durable before the third host parks once at threshold 3.
- `DurableWorkflowRuntime.RearmAsync` is an expected-version operation with reason-specific
  preconditions, and definition registration no longer unparks. DR-AC-009/028 now prove
  missing-definition rejection, registration-without-unpark, one-winner concurrent re-arm,
  generic-resume rejection, and poison acknowledgement.
- Segment defaults are resolved at 256 commands / 30 seconds. The duration is an admission
  deadline between operations, not forcible preemption of an already-running user step.
  Hosted worker, continuation, poison, and segment controls are exposed and validated;
  DR-AC-031 remains the startup and override proof.
- Correlation-targeted and definition-fanout event routes plus `DurableManagement` are exposed
  from the public durable facade. DR-AC-030 proves correlation routing, matching-active-wait
  fanout with distinct target event IDs, and management queries; instance inbox dedup remains
  covered by DR-AC-004.
- All seven DR-050 instruments exist. Continuation and external-outbox gauges use exact
  provider statistics split by pending/retryable/claimed state; in-memory, PostgreSQL, and
  SQL Server certification prevents batch-size approximations from masquerading as backlog.
- The interpreter now schedules `RunChild`/`RunChildren` with a suspended child cursor and
  atomically consumes the parent resume token, matched child resumes, and next position.
  Hosted child-start/completion routing, full DR-041 DAG-plan driving, and DR-040 saga driving
  remain open; the bridge alone does not satisfy the no-manual-pumping gate.
- Driver-owned continue-as-new now has a typed public step result. The rollover commit carries
  replacement state plus a fresh post-`Init` root cursor in the durable envelope, preserves
  logical identity/version lineage, and leaves a successor continuation; DR-AC-033 is green
  across host replacement.
- Durable `WhenFirst` now has a cross-host race proof. Optimistic stream concurrency admits
  exactly one matched wait, the committed cursor selects one winner and cancels the losing
  wait under `CancelRemaining`, one continuation completes the parent sequence, and later
  replacement-host pumps cannot select a second winner. Residual ownership is container-ancestor
  based rather than equal-frame-depth based, so nested losing-branch waits are cancelled and
  removed with the winner join. Merge passes scan every completed candidate, so an incomplete
  `Parallel` join cannot starve a ready `WhenFirst` elsewhere in the cursor tree; DR-AC-032 is
  green. Child-owning loser cleanup still needs the P3 residual-child contract described below.
- Opportunistic facade drives continue through committed policy and continue-as-new boundaries,
  so `StartOrGetAsync` completes a runnable successor without requiring hosted services. Required
  continuation-pump drives still release at those boundaries to preserve fair host turns and
  restart testability.

## Risks

1. **Parallel-inside-If resume fidelity (DR-AC-006)** is the hardest correctness work:
   branch progress must reconstruct from the envelope alone. Mitigation: design the
   position model + serialization first, with unit tests before wiring the lane host.
2. **At-least-once semantics with multi-commit segments (DR-013/014):** crash between
   commits re-runs only the uncommitted step; the interpreter must re-derive "next work"
   idempotently from committed position (also covers stale continuations, DR-AC-015).
3. **Public-surface creep vs DR-003:** limit to the processor overloads + command records
   + envelope types; everything else stays internal.
4. **Continuation pump vs lane host duplication:** both may schedule the same instance;
   correctness rests on reload-committed-position + no-op detection, not exclusion.
5. **Policy model breadth:** fixed backoff, persisted attempts/deadlines, rollback, timeout-fail,
   and cooperative cancellation are implemented. Rich retry predicates, terminal conditions,
   variable backoff strategies, and the remaining EV-052 timeout outcomes are still future
   shared-model work; they are not silently inferred by the durable driver.
6. **Child cancellation ownership:** a child-owning cursor that loses a `WhenFirst` race needs
   a committed residual intent and resume-token cleanup. Simple child joins are wired; this
   mixed composition remains a P3 correctness edge.

## Phase order

DR-P1 interpreter + envelope + parked/start-input/policy semantics → DR-P2 seam/lane host/
continuation (`continue` outbox kind, outcome disposition, durable poison accounting, and
kind-partitioned claims) → DR-P3 saga + DAG driving → DR-P4 facade/telemetry/samples. The
complete gates are authoritative in DR §16.8; a phase is not complete merely because its
happy-path driver tests pass.
