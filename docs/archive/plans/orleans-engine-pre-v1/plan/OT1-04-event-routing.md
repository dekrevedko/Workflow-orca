# OT1-04: Split delivery surfaces — correlation routing and inbox dedup

**Difficulty**: Sonnet        **Depends on**: OT1-02, OT1-03, OT1-00 (seam review sign-off)
**Spec**: OE-040, OE-041, OE-014        **AC**: OE-AC-010, OE-AC-043, OE-AC-044

## Goal
The facade's delivery surfaces, kept **separate** per OE-040: correlation-targeted
`RaiseEventAsync` resolves an inbound `EventEnvelope` to exactly the instances the durable
matching rules name; definition-targeted fanout (if the durable engine exposes one) is a
distinct operation mirrored 1:1. No combined method that mixes both semantics. Dedup and
buffering semantics stay byte-identical to the durable engine.

## Read first
- `docs/orleans-engine/plan/SEAMS.md` (from OT1-00) — the public dispatch/matching seam
  decision, incl. whether correlation-targeted and fanout are separate durable surfaces
- The durable routing/matching component named in SEAMS.md (read the files it lists, ≤2)
- `src/OrcaCore.Engine.Orleans/OrleansWorkflowEngine.cs`
- The `Engine.Durable.Tests` dedup test (duplicate `EventId` → `DuplicateIgnored`; read 1 file)

## Deliverables
- `OrleansWorkflowEngine.RaiseEventAsync(EventEnvelope, CancellationToken)` returning the
  durable engine's raise/deliver result type: query active-wait/routing projections via the
  seam from OT1-00 → build deliver commands → `ExecuteAsync` on each **matched** instance
  grain → aggregate results. Matching cardinality (single vs multiple instances per
  correlation) follows the durable matching rules exactly — the facade adds none of its own.
- Fanout surface: mirror the durable engine's definition-targeted operation as a separate
  facade method IF one exists (per SEAMS.md); otherwise record its absence in the task
  PROGRESS line and do not invent one.
- Reuse the durable routing/matching component; wrap, don't fork.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Orleans.Tests/Facade/EventRoutingTests.cs`:
1. `RaiseEvent_MatchingWait_ResumesInstance` — completes the OT1-02 scenario through the
   facade instead of direct grain delivery (upgrades OE-AC-002 to fully green).
2. `RaiseEvent_DuplicateEventId_Ignored` — `[Trait("AC","OE-AC-010")]` — same envelope
   twice → second reports duplicate; single committed transition; inbox row state per DU-030.
3. `RaiseEvent_NoMatch_FollowsBufferOrDiscardSemantics` — assert whatever the durable
   engine does for unmatched events (pin from its tests), unchanged.
4. `RaiseEvent_MatchingCardinality_MirrorsDurableRules` — `[Trait("AC","OE-AC-043")]` — same definition + same event
   raised on the durable engine and the Orleans engine → identical set of resumed
   instances (parity test for the routing seam; covers the exactly-one contract where the
   durable rules say exactly-one).
5. `RaiseEvent_CanceledToken_CommitsNothing` — `[Trait("AC","OE-AC-044")]` — cancellation
   during routing or grain dispatch is observed and leaves no partially committed delivery.

## Implementation notes
- The facade owns resolution and per-instance dispatch; the grain stays single-instance
  (OE-040 — grains never see unrouted events).
- Duplicate detection must remain in the processor/inbox, NOT in the facade — the facade
  may deliver duplicates; the commit path ignores them.
- Cancellation token flows through resolution queries and every grain call (OE-014).

## Out of scope
Cross-silo placement assertions (OT3-02), outbox dispatch (OT3-01).

## Definition of done
- [ ] All listed tests green (incl. re-tagged OE-AC-002); solution builds zero-warning
- [ ] No dedup/matching logic added outside `Engine.Durable`-owned components
- [ ] Correlation-targeted and fanout surfaces are separate methods (or fanout absence recorded)
- [ ] PROGRESS.md updated; committed as "OT1-04: split delivery surfaces (OE-040, OE-041, OE-AC-010)"
