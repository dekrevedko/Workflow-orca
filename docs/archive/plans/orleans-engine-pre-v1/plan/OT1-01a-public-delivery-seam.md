# OT1-01a: Public durable delivery dispatch seam

**Difficulty**: Sonnet        **Depends on**: OT1-00 (approved SEAMS.md proposal)
**Spec**: OE-002, OE-040 (enabler)        **AC**: none directly (unblocks OE-AC-002 and OE-AC-010)

## Goal
Make delivery/resume command dispatch publicly callable from an external engine assembly.
Today `DurableCommandProcessor.ProcessAsync(DeliverEventCommand, …)` and the
resume/complete/fail overloads are `internal`, so `OrcaCore.Engine.Orleans` cannot legally
drive wait/resume. This task implements exactly the seam shape approved at the OT1-00
review gate — no more.

## Read first
- `docs/orleans-engine/plan/SEAMS.md` — the approved seam shape (promote overloads to
  `public` vs a new public dispatch facade type; the gate decided which)
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs` — the
  `internal` overloads at ~lines 83–94, 150–161, 368–425 (resume, deliver, complete, fail)
- The `Engine.Durable.Tests` file(s) exercising `DeliverEventCommand` via internals
  (locate by searching the test project for `DeliverEventCommand`; read 1 file)

## Deliverables
Per the SEAMS.md decision, one of:
- the deliver/resume `ProcessAsync` overloads promoted to `public` (with XML-doc contracts
  stating turn semantics and idempotency expectations), **or**
- a new public dispatch type in `Engine.Durable` wrapping those overloads 1:1.

Plus: XML-doc on every newly public member; no behavior change; no new overloads.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Durable.Tests/` (extend the existing delivery test file):
1. `PublicSeam_DeliverEvent_MatchesInternalBehavior` — the same deliver scenario driven
   through the new public surface produces identical facts/inbox state as the existing
   internal-path test (parity pin).
2. `PublicSeam_IsCallableFromExternalAssembly` — a compile-level guard: call the seam from
   `OrcaCore.Engine.Orleans.Tests` (a trivial smoke — resolves + invokes against the
   in-memory store). This is the test that fails today.

## Implementation notes
- This is a review-gated `Engine.Durable` public-surface change: the PROGRESS.md entry
  must reference the gate sign-off recorded by OT1-00.
- Certification-suite impact: none expected (no port changes) — state this explicitly in
  the PROGRESS.md line if it holds; stop and escalate if it doesn't.
- Do NOT use `InternalsVisibleTo` toward `Engine.Orleans` — the dependency-rules section
  of [01-architecture.md §5](../01-architecture.md) treats that as a violation, and OQ-11
  discipline (public-API-first) applies.

## Out of scope
Grain code (OT1-02 consumes the seam), start reservation (OT1-03a), management-command
surfaces (Phase O3 decides what they need).

## Definition of done
- [ ] Both tests green; full `Engine.Durable.Tests` suite green; zero warnings
- [ ] Newly public members XML-documented; no behavior deltas in existing tests
- [ ] Gate sign-off referenced in PROGRESS.md; committed as "OT1-01a: public delivery seam"
