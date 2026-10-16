# OT1-02: Wait suspends the turn; event delivery resumes

**Difficulty**: Haiku        **Depends on**: OT1-01, OT1-01a (public delivery seam implemented)
**Spec**: OE-011, OE-030        **AC**: OE-AC-002 (direct-delivery core; routing lands in OT1-04)

## Goal
Prove the turn model: a workflow reaching `Wait` returns its grain call with status Waiting
(no held call, no in-memory await), and a later event command delivered to the same grain
resumes and completes it.

## Read first
- `tests/OrcaCore.Engine.Orleans.Tests/Grains/InstanceGrainStartTests.cs` (fixture pattern)
- The `Engine.Durable.Tests` test that exercises wait-then-resume through
  `DurableCommandProcessor` (locate by searching that test project for the deliver/raise
  command type name; read 1 file) — mirror its definition and assertions
- [02-requirements.md](../02-requirements.md) §2.2, §2.4

## Deliverables
- Test-only: a two-step durable definition (step → `Wait` on correlated event → final step)
  registered in the Orleans test fixture. Production code changes only if the codec is
  missing the deliver-event command kind (then extend codec + its tests, same style).
- All delivery goes through the **public seam from OT1-01a** — if any call site would need
  an `internal` `Engine.Durable` member, this task is blocked, not creative.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Orleans.Tests/Grains/WaitResumeTests.cs`:
1. `Wait_ReturnsTurn_StatusWaiting` — `[Trait("AC","OE-AC-002")]` — start via grain; call
   completes (bounded time, e.g. seconds) with Waiting; active-wait projection row exists.
2. `MatchingEvent_Resumes_Completes` — deliver the correlated event command to the same
   grain; instance completes; final facts committed in order.
3. `TurnHoldsNoAwait` — after step 1 the grain accepts an unrelated read/command
   immediately (no queued multi-second turn) — guards against accidental in-turn waiting.

## Implementation notes
- Deliver the event by building the deliver/raise command directly and calling
  `ExecuteAsync` — correlation routing is deliberately NOT here (OT1-04).
- Use `TimeProvider`/fixture time discipline; no `Task.Delay` sleeps in assertions — poll
  projections with the test-support helpers if waiting is needed.

## Out of scope
Correlation resolution, buffering of unmatched events, timers/timeouts.

## Definition of done
- [ ] All listed tests green; solution builds zero-warning
- [ ] PROGRESS.md updated; committed as "OT1-02: wait/resume turn model (OE-030, OE-AC-002)"
