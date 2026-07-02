# T1-05: Interpreter — straight-line execution + engine facade

**Difficulty**: Sonnet        **Depends on**: T1-02, T1-03, T1-04
**Spec**: CR-010…CR-014, CR-021        **AC**: AC-001, AC-004

## Goal
The first executing slice: an internal interpreter that walks `Init → business steps → End`
(happy path and failure path), and the minimal public ephemeral engine facade with
`StartAsync` returning a metadata-only snapshot.

## Read first
- Spec: [specs/04-requirements-core-runtime.md](../../../specs/04-requirements-core-runtime.md) §4.2–4.3
- `src/OrcaCore.Core/Definitions/` and `Lifecycle/` (T1-02/03)
- `src/OrcaCore.Abstractions/Steps/` (T1-01)

## Deliverables
- `src/OrcaCore.Engine.Ephemeral/Execution/` (internal): `WorkflowInstance<TState>`
  (runtime state: status, pointer, error details, timestamps, end outcome; plus `TState`),
  `Interpreter<TState>` — loop: current node → execute → apply `StepResult` via
  `LifecycleMachine` → advance pointer; exhaustive switch over `StepResult` variants
  (only `Completed`/`Failed` handled now; `WaitForEvent`/`Yield` throw
  `NotSupportedException` with the owning task id in the message).
- `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine` (public):
  `RegisterDefinition(definition)`, `Task<WorkflowInstanceSnapshot>
  StartAsync<TInput,TState>(definitionId, input, ct)`; in-memory instance registry
  (`ConcurrentDictionary`) behind an internal `IInstanceRegistry` seam.
- Failure semantics per CR-014: `Failed` result OR unhandled step exception → status
  `Failed`, error captured (type, message, step path, timestamp), later nodes untouched.

## Tests to write FIRST
Unit — `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/InterpreterTests.cs`:
1. `Run_InitStepEnd_CompletesAndMutatesState`
2. `Run_TwoSteps_ExecuteInOrder_SharedState`
3. `Run_StepReturnsFailed_InstanceFailed_LaterStepsSkipped`
4. `Run_StepThrows_InstanceFailed_ErrorDetailsCaptured`
5. `Run_EndWithOutcome_RecordsOutcomeName` (data only; AC-012 finishes in T1-14)
6. `Run_UnsupportedResult_ThrowsNamingOwnerTask`
Acceptance — `tests/OrcaCore.Acceptance.Tests/StraightLineAcceptanceTests.cs`:
7. `[Trait("AC","AC-001")] StraightLine_Completes_StateReflectsSteps` — via public engine +
   a temporary internal-free state read (full `GetState` arrives in T1-13; assert here via
   a step that records into a test-owned sink)
8. `[Trait("AC","AC-004")] FailingStep_FailsInstance_ErrorInspectable` (snapshot error
   summary)

## Implementation notes
- The interpreter owns ALL orchestration decisions (CR-010/012): steps get `StepContext`
  with `State` + `TimeProvider` only. Any temptation to hand the step more surface is a
  spec violation.
- Single-threaded execution is fine here; the execution lane (CR-040) is T1-06 — do not
  build synchronization now, but route all mutations through one internal `Advance` method
  so T1-06 can wrap it.
- `StartAsync` runs the workflow to its first suspension/terminal inline for now;
  scheduling refinements come with T1-06.

## Out of scope
- Waits, events, If/While/Parallel, management queries, cancellation, Yield.

## Definition of done
- [ ] All listed tests green incl. both AC traits; zero warnings
- [ ] Engine facade exposes snapshots only (CR-021) — no live instance type is public
- [ ] PROGRESS.md updated; committed as "T1-05: straight-line interpreter (AC-001, AC-004)"
