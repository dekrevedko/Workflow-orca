# OT1-05: Concurrency and duplicate-activation defense

**Difficulty**: Sonnet        **Depends on**: OT1-04
**Spec**: OE-013, OE-021        **AC**: OE-AC-011, OE-AC-012

## Goal
Prove the two-layer concurrency story: grain turns serialize normal traffic, and
expected-version append remains the correctness backstop when serialization is bypassed
(the duplicate-activation scenario).

## Read first
- `src/OrcaCore.Core/Concurrency/InstanceLane.cs` (context only — what currently serializes)
- The `Engine.Durable.Tests` expected-version conflict test (search `AppendOutcome`/version
  conflict; read 1 file) — the conflict result shape to assert
- [01-architecture.md](../01-architecture.md) §3 duplicate-activation note

## Deliverables
Tests only (no production code expected). If a version-conflict result surfaces to the
grain caller in an unclear shape, add minimal mapping in `WorkflowCommandCodec`
result encoding — nothing else.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Orleans.Tests/Grains/ConcurrencyDefenseTests.cs`:
1. `ConcurrentDeliveries_SingleSerialOrder` — `[Trait("AC","OE-AC-011")]` — N parallel
   `RaiseEventAsync`/grain calls against one waiting instance (mixed matching + duplicate
   envelopes) → committed stream shows one serial order; exactly one resume; no partial facts.
2. `BypassedSerialization_OneWinner` — `[Trait("AC","OE-AC-012")]` — simulate duplicate
   activation by invoking a second `DurableCommandProcessor` directly against the same
   store concurrently with a grain call racing the same expected version → exactly one
   winner; loser observes the documented conflict outcome.
3. `LoserConflict_SurfacesCleanly` — the losing path returns/throws the documented result
   through the grain (no silent retry inside the grain, per OE-021).

## Implementation notes
- Test 2 is the Orleans analog of OR-002/DU-022: it must NOT rely on grain single-threading
  — that is exactly the layer being bypassed.
- Race windows: use `TaskCompletionSource` gates in a test observer (the
  `IWorkflowRuntimeObserver` seam) rather than sleeps, if timing control is needed.

## Out of scope
Silo-kill chaos variants (OT5-05); retries/backoff policy.

## Definition of done
- [ ] All listed tests green and non-flaky (run 20× locally: `dotnet test ... --filter` loop)
- [ ] No production logic added outside result mapping
- [ ] PROGRESS.md updated; committed as "OT1-05: concurrency defense (OE-021, OE-AC-011, OE-AC-012)"
