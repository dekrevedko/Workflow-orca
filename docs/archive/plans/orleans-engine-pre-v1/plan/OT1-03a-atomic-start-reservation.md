# OT1-03a: Atomic start reservation in the commit boundary

**Difficulty**: Sonnet        **Depends on**: OT1-00 (approved SEAMS.md proposal) — pure
`Engine.Durable`/provider work; may run in parallel with OT1-01/01a/02
**Spec**: OE-042 (durability layer)        **AC**: enables OE-AC-013

## Goal
Close the crash window in idempotent start: today `IWorkflowStartIdempotencyStore` is
lookup-only and the reservation is recorded outside the start commit, so a crash between
"start committed" and "reservation recorded" can mint a second instance on retry. After
this task, the reservation is persisted **atomically with the started/version-binding
facts** inside the start commit boundary, and the port expresses reserve-or-return.

## Read first
- `docs/orleans-engine/plan/SEAMS.md` — the approved reservation port shape
- `src/OrcaCore.Abstractions/Providers/ProviderPorts.cs` —
  `IWorkflowStartIdempotencyStore` (lookup-only today, ~line 50)
- `src/OrcaCore.Engine.Durable/Execution/DurableCommitMaterializer.cs` — where
  commit effects are materialized (~line 51); the reservation joins this boundary
- `src/OrcaCore.Engine.Durable/Execution/DurableStartService.cs` — current
  lookup/record flow being replaced

## Deliverables
- Port extension per SEAMS.md (e.g. the start command carries the idempotency key and the
  commit pipeline materializes the reservation; plus an atomic
  `ReserveOrGetAsync(key, instanceId)`-style member if the gate approved one). Exact shape
  comes from SEAMS.md — do not improvise beyond it.
- `Providers.InMemory` implementation of the new semantics.
- Certification suite (`OrcaCore.ProviderCertification`): a new invariant test —
  concurrent reserve attempts for one key yield one winner; a crash-shaped sequence
  (start committed, "separate" reservation write skipped) cannot double-start, because
  there is no separate write.
- `DurableStartService`/commit path updated so no lookup-then-record sequence remains.

## Tests to write FIRST
1. Certification: `StartReservation_ConcurrentReserves_OneWinner` (in the abstract
   certification class; runs against in-memory now, PostgreSQL when its test project
   inherits it).
2. Certification: `StartReservation_IsAtomicWithStartCommit` — after a successful start
   commit, the reservation is queryable; a failed/conflicted commit leaves no reservation.
3. `Engine.Durable.Tests`: retried start with the same key after a simulated lost result →
   same instance, one stream (the durable-engine-level version of OE-AC-013).

## Implementation notes
- This is a **port change**: review gate + certification-suite impact are mandatory.
  Scope here is port + commit materialization + in-memory provider + certification tests
  ONLY; the PostgreSQL side is pre-split into OT1-03b (it inherits the certification
  member automatically — its work is the SQL/transaction shape).
- The reservation record must carry enough to return the winner (`InstanceId`, key,
  timestamps) — reuse `StartedWorkflowIdempotencyRecord`.

## Out of scope
The Orleans idempotency grain and facade (OT1-03); PostgreSQL implementation (OT1-03b);
ephemeral-engine start semantics.

## Definition of done
- [ ] Certification + engine tests green against in-memory
- [ ] No lookup-then-record start path remains anywhere in `Engine.Durable`
- [ ] Gate sign-off referenced in PROGRESS.md; committed as "OT1-03a: atomic start reservation (OE-042)"
