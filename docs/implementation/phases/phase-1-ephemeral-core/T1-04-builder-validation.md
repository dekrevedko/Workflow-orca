# T1-04: Fluent builder + accumulated validation

**Difficulty**: Sonnet        **Depends on**: T1-03
**Spec**: CR-001, CR-002, CR-005, CR-008        **AC**: AC-008

## Goal
The public authoring surface for Slice 1: a fluent `WorkflowBuilder<TState>` producing an
immutable, validated `WorkflowDefinition<TState>`, with build-time validation that reports
ALL errors together via `Validation<T>`.

## Read first
- Spec: [specs/04-requirements-core-runtime.md](../../../specs/04-requirements-core-runtime.md) §4.1
- `v3/src/OrcaCore.Core/Definitions/` (T1-03)
- `v3/src/OrcaCore.Abstractions/Primitives/Validation.cs` (T0-03)

## Deliverables
In `v3/src/OrcaCore.Core/Building/` (builder public; validators internal):
- `WorkflowBuilder<TState>` fluent surface for Slice 1 primitives:
  `Init(Func<TInput,TState>)` … `Then<TStep>()` / `Then(stepFactory)` …
  `If(condition, then, else?)` … `While(condition, body)` …
  `Parallel(branches...)` (each branch a nested sequence builder) …
  `Wait(eventName, correlationSelector)` … `End()` / `End(outcomeName)`.
  Definition identity supplied at build: `Build(definitionId, version)`.
- `BuildValidated(...)` returning `Validation<WorkflowDefinition<TState>>`; `Build(...)`
  as the throwing facade (aggregates all `ValidationError`s into one
  `WorkflowDefinitionException` message) — the internal/external split from spec PR-050.
- Validation rules (each a small internal validator, composed): missing `Init`, missing
  reachable `End`, empty `Parallel`/branch, duplicate branch names, `While` without body,
  null delegates. Each error: stable `Code`, human message, `Path` into the tree.

## Tests to write FIRST
In `v3/tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs`:
1. `Build_MinimalWorkflow_ProducesInitStepEndTree`
2. `Build_NestedStructures_ProduceExpectedTree` (If→Parallel→Wait shape)
3. `BuildValidated_MissingInit_ReportsError`
4. `BuildValidated_MultipleProblems_ReportsAllAtOnce` — missing Init + empty branch +
   null condition → 3 errors, stable codes, ordered [AC-008 basis]
5. `Build_WithErrors_ThrowsAggregatedDefinitionException` — message contains every code
6. `Build_Twice_ProducesEqualIndependentDefinitions` (builder not consumed/corrupted)
7. `End_WithOutcomeName_LandsOnEndNode`
In `v3/tests/OrcaCore.Acceptance.Tests/BuilderAcceptanceTests.cs`:
8. `[Trait("AC","AC-008")] Build_AccumulatesAllValidationErrors`

## Implementation notes
- Builder is sugar over the T1-03 tree (spec CR-001/CR-003: the tree is the stable
  contract): keep it a thin construction layer.
- No durable-only method may exist here (`WaitLong` etc.) — API absence is the enforcement
  (CR-020 discipline).

## Out of scope
- Policies/decorators (Phase 3), `WhenFirst`, timers, interpreter.

## Definition of done
- [ ] All listed tests green incl. the AC-008 trait; zero warnings
- [ ] Builder public surface has XML docs; validators internal
- [ ] PROGRESS.md updated; committed as "T1-04: builder + validation (CR-001/002, AC-008)"
