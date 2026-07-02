# T1-01: Core contracts in Abstractions

**Difficulty**: Haiku        **Depends on**: T0-03
**Spec**: CR-011, CR-020, CR-021, CR-022, EV-001, EV-002        **AC**: none directly

## Goal
Define the shared contracts every runtime path depends on: the step contract, control-intent
results, the event envelope, statuses, strongly-typed IDs, and the metadata-only snapshot.

## Read first
- Spec: [specs/03-domain-model-and-glossary.md](../../../specs/03-domain-model-and-glossary.md) §3.2
- Spec: [specs/04-requirements-core-runtime.md](../../../specs/04-requirements-core-runtime.md) §4.2–4.3
- `src/OrcaCore.Abstractions/Primitives/` (from T0-03)

## Deliverables
In `src/OrcaCore.Abstractions/` (folders: `Steps/`, `Events/`, `Instances/`, `Ids/`):
- IDs: `InstanceId`, `EventId`, `WaitId`, `DefinitionId`, `DefinitionVersion`,
  `CorrelationId` — readonly record structs, `New()` factories using `Guid.CreateVersion7()`
  where GUID-based; `CorrelationId` wraps `string`.
- `IStep<TState>` — `ValueTask<StepResult> ExecuteAsync(StepContext<TState> context,
  CancellationToken ct)`.
- `StepContext<TState>` — `State` (mutable business state, the ONLY mutation channel),
  `ResumedEvent` (`EventEnvelope?`, set only on first step after a wait resume — EV-022),
  `TimeProvider`. NO runtime surface (CR-012 is enforced by this shape).
- `StepResult` — abstract record; sealed variants `Completed`, `Failed(OrcaCoreException
  Error)`, `WaitForEvent(string EventName, CorrelationId CorrelationId)`, `Yield`.
- `EventEnvelope` — `EventId`, `EventName`, `CorrelationId`, `Payload (object?)`,
  `OccurredAt (DateTimeOffset)`; immutable record.
- `WorkflowStatus` enum — `Running`, `Waiting`, `Completed`, `Failed`, `Cancelled`,
  `Terminated`, `Paused` (shared set per CR-030; `Paused` exists in the enum, is simply
  never produced by the ephemeral engine).
- `WorkflowInstanceSnapshot` — metadata-only immutable record: instance id, definition
  id+version, status, created/updated timestamps, error summary (`string?`), end outcome
  name (`string?`).
- Exception taxonomy per conventions §5 (base exists from T0-03; add the derived types).

## Tests to write FIRST
In `tests/OrcaCore.Core.Tests/Contracts/`:
1. `Ids_New_AreUniqueAndVersion7Ordered` — two `New()` ids differ; creation order is
   reflected in byte order (V7 property)
2. `StepResult_Variants_HaveValueEquality`
3. `StepResult_SwitchOverVariants_IsExhaustive` — a helper switch compiling over all four
   variants (guards against accidental new public variant without handling)
4. `Envelope_WithSameData_AreEqual`
5. `Snapshot_IsMetadataOnly` — snapshot type exposes no `TState`, no live references
   (assert via public property types)

## Implementation notes
- These records are the most-referenced types in the codebase: XML-doc every member with
  its contract per conventions §6. Do not add members "for later" — later tasks add them
  with their tests.

## Out of scope
- Wait records (T1-08), definition/node model (T1-03), any engine logic, dispatch models
  (Phase 2), serialization seams (Phase 2).

## Definition of done
- [ ] All listed tests green; zero warnings; Abstractions still has zero dependencies
- [ ] Every public member XML-documented
- [ ] PROGRESS.md updated; committed as "T1-01: core contracts (CR-011/020/022, EV-001)"
