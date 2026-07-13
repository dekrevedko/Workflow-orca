# OT1-03: OrleansWorkflowEngine facade with cluster-safe StartOrGet

**Difficulty**: Sonnet        **Depends on**: OT1-01, OT1-03a (atomic reservation landed)
**Spec**: OE-015, OE-042, OE-070, OE-071        **AC**: OE-AC-013, OE-AC-043

## Goal
The public entry point users program against: `OrleansWorkflowEngine.StartOrGetAsync`
mirrors `DurableWorkflowRuntime.StartOrGetAsync` (same result type, same semantics) but is
idempotent **atomically across silos and clients**. The existing `DurableStartService`
process-local lock/cache is explicitly NOT sufficient here (OE-042); cluster-wide
serialization comes from a grain keyed by the idempotency key.

## Read first
- `docs/orleans-engine/plan/SEAMS.md` (from OT1-00) — the start-reservation seam decision
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableStartService.cs` — the process-local
  pattern being replaced, and the `IWorkflowStartIdempotencyStore` interaction to preserve
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs` — the
  StartOrGet contract to mirror (result type, parameter shape)
- `v3-gpt/src/OrcaCore.Engine.Orleans/Grains/IWorkflowInstanceGrain.cs`

## Deliverables
In `v3-gpt/src/OrcaCore.Engine.Orleans/`:
- `Grains/IStartIdempotencyGrain : IGrainWithStringKey` (key = idempotency key) —
  `Task<WorkflowCommandResultEnvelope> StartOrGetAsync(StartRequestEnvelope, CancellationToken)`.
  Turn logic: query the reservation → if a winner exists, return its `InstanceId` → else
  create `InstanceId` and issue the start command **carrying the idempotency key**; the
  OT1-03a commit boundary materializes the reservation atomically with the started facts —
  the grain performs **no separate reservation write** at any point. The grain's
  single-threaded turn is a fast-path serializer for racing callers; the atomic commit is
  the correctness layer, so a duplicate activation, crash, or retry cannot mint a second
  instance. On a conflict/duplicate outcome, re-read the reservation and return the winner.
- `StartRequestEnvelope` transport record (same `[GenerateSerializer]`/`[Id(n)]`/schema-version
  discipline as OT0-03).
- `OrleansWorkflowEngine.StartOrGetAsync(...)` — durable runtime's parameter shape and
  `DurableWorkflowStartResult` return; delegates to the idempotency grain; registered in
  `UseOrcaCoreOrleans()`.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Orleans.Tests/Facade/StartOrGetTests.cs`:
1. `StartOrGet_NewKey_StartsInstance` — fresh instance id; facts committed.
2. `StartOrGet_SameKey_ReturnsSameInstance` — second call → same `InstanceId`; one stream.
3. `StartOrGet_ConcurrentCallers_Converge` — `[Trait("AC","OE-AC-013")]` — N parallel
   calls with one key (through separate client handles where the fixture allows) → all
   receive the same `InstanceId`; exactly one instance, one stream, one reservation.
4. `StartOrGet_RetryAfterLostResult` — call once; retry same key simulating a caller that
   never saw the result (at-most-once, OE-015) → same instance; no duplicate start facts.
5. `BackstopHoldsWhenGrainBypassed` — a second start command with the same key issued
   directly through the durable path (simulating a duplicate idempotency-grain activation
   that lost the race) converges on the winner's `InstanceId`; no second instance exists.
6. `SurfaceParity` — `[Trait("AC","OE-AC-043")]` — reflection assertion that the facade signature matches the durable
   runtime's, so drift is caught.

## Implementation notes
- OT1-03a must be fully landed (port + commit materialization + certification tests) —
  this task builds only on the atomic reservation, never on check-then-act.
- The idempotency grain is non-reentrant and stateless between turns, same rules as the
  instance grain (OE-013).

## Out of scope
RaiseEvent/fanout (OT1-04), management operations (OT3-03).

## Definition of done
- [ ] All listed tests green (test 3 run 20× for flake check); solution builds zero-warning
- [ ] Facade returns `DurableWorkflowStartResult` (no new parallel result type)
- [ ] No process-local locking added anywhere in the Orleans path
- [ ] PROGRESS.md updated; committed as "OT1-03: cluster-safe StartOrGet (OE-042, OE-AC-013)"
