# T1-07: Execute If and While nodes

**Difficulty**: Haiku        **Depends on**: T1-05
**Spec**: CR-010        **AC**: AC-002, AC-003

## Goal
Extend the interpreter beyond straight-line sequences to handle conditional and loop nodes.
`If` executes exactly one branch and rejoins the following sequence. `While` reevaluates the
condition before each iteration and exits cleanly when false.

## Read first
- `src/OrcaCore.Core/Definitions/Nodes.cs`
- `src/OrcaCore.Core/Definitions/ExecutionPointer.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/InterpreterTests.cs`
- Spec: `docs/specs/04-requirements-core-runtime.md` section 4.2

## Deliverables
- Interpreter support for `IfNode<TState>` and `WhileNode<TState>`.
- Pointer/frame handling for branch entry, branch exit, loop body entry, and loop exit.
- Acceptance tests for AC-002 and AC-003.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/InterpreterControlFlowTests.cs`:
1. `Run_IfConditionTrue_ExecutesThenBranchOnly`
2. `Run_IfConditionFalse_ExecutesElseBranchOnly`
3. `Run_IfWithoutElse_ContinuesAfterSkippedBranch`
4. `Run_WhileConditionTrueThenFalse_ReevaluatesConditionEachIteration`
5. `Run_NestedIfInsideWhile_MaintainsCorrectPosition`

In `tests/OrcaCore.Acceptance.Tests/ControlFlowAcceptanceTests.cs`:
6. `[Trait("AC","AC-002")] If_ExecutesExactlyOneBranch_ThenContinues`
7. `[Trait("AC","AC-003")] While_RunsThreeIterations_CompletesAfterFourthCheck`

Use AwesomeAssertions for assertions.

## Implementation notes
The interpreter still owns sequencing decisions; steps and predicates must not drive their
own orchestration. Keep predicate failures on the existing failure path. Use the explicit
step authoring surface from T1-04; do not add reflection-based construction helpers.

## Out of scope
Waits inside loops, loop-scoped wait isolation, parallel branches, Yield fairness.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] AC-002 and AC-003 are green in `OrcaCore.Acceptance.Tests`
- [ ] PROGRESS.md updated; committed as "T1-07: If and While (AC-002, AC-003)"
