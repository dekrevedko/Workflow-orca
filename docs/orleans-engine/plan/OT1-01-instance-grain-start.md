# OT1-01: Instance grain + start path

**Difficulty**: Sonnet        **Depends on**: OT0-04
**Spec**: OE-010, OE-011, OE-013, OE-014, OE-020, OE-022        **AC**: OE-AC-001, OE-AC-044

## Goal
The first core grain slice: `WorkflowInstanceGrain` accepts a command envelope, runs the
degenerate durable advancement segment containing exactly one `DurableCommandProcessor`
cycle, and returns the result envelope. A start command through the grain commits real
facts to the event store. Later document-16 work reuses the same grain as the host for full
advancement segments driven by the durable interpreter.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Orleans/Transport/` (from OT0-03)
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs` — `ProcessAsync`
  overloads and `StartWorkflowCommand` handling
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs` — how a start
  command is built from a definition (registration, input state)
- [01-architecture.md](../01-architecture.md) §2–3

## Deliverables
In `v3-gpt/src/OrcaCore.Engine.Orleans/Grains/`:
- `IWorkflowInstanceGrain : IGrainWithGuidKey` —
  `Task<WorkflowCommandResultEnvelope> ExecuteAsync(WorkflowCommandEnvelope envelope,
  CancellationToken cancellationToken)` (OE-014 — Orleans flows caller cancellation to
  grain methods; the token passes through codec → processor → ports unswallowed).
- `WorkflowInstanceGrain : Grain, IWorkflowInstanceGrain` — decodes via `WorkflowCommandCodec`,
  guards `envelope.InstanceId == this.GetPrimaryKey()` (mismatch → fail fast), dispatches to
  the singleton `DurableCommandProcessor` with the token, encodes the result. No state
  fields beyond injected services. NOT `[Reentrant]`. `OnActivateAsync` does nothing beyond base.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Orleans.Tests/Grains/InstanceGrainStartTests.cs`
(TestingHost + in-memory event store; register a minimal one-step durable definition the way
`Engine.Durable.Tests` does):
1. `Start_ThroughGrain_CommitsStartedFacts` — `[Trait("AC","OE-AC-001")]` — ExecuteAsync
   with an encoded start command → result envelope reports success; event store stream for
   the instance contains start/version-binding facts; projection summary exists.
2. `InstanceIdMismatch_FailsFast` — envelope addressed to a different instance id than the
   grain key → clear error result, nothing committed.
3. `SecondStart_SameInstance_IsRejectedOrIdempotent` — assert whatever
   `DurableCommandProcessor` already does for a repeated start (read its tests to pin the
   expectation) — the grain must not change that semantic.
4. `PreCanceledToken_CommitsNothing` — `[Trait("AC","OE-AC-044")]` — `ExecuteAsync` with an already-canceled token →
   canceled/failed result; event store stream stays empty (OE-014: token observed before
   the commit boundary, never a partial mutation).

## Implementation notes
- The grain is a *transport adapter*: zero decision logic; all semantics stay in the
  processor (OE-002). If you feel the urge to add logic here, the design is drifting — stop.
- Per OOQ-2, calling `ProcessAsync` (which internally uses `InstanceLane`) is correct;
  the redundant lane hop is accepted for now.

## Out of scope
Facade (OT1-03), event routing (OT1-04), queries (OT1-06), timers (Phase O2).

## Definition of done
- [ ] All listed tests green; solution builds zero-warning
- [ ] `WorkflowInstanceGrain` has no mutable fields and no `[Reentrant]`/interleave attributes
- [ ] PROGRESS.md updated; committed as "OT1-01: instance grain command-segment start path (OE-010, OE-011, OE-AC-001)"
