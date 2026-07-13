# T3-07: Enforce timeout policies

**Difficulty**: Haiku        **Depends on**: T3-06
**Spec**: EV-052, MG-041, CR-006        **AC**: AC-113

## Goal
Enforce configured step timeout policies through the timer primitive. A timed-out step takes
the configured outcome deterministically and records lifecycle/statistics-visible state.

## Read first
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `src/OrcaCore.Core/Definitions/Nodes.cs`
- `src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `tests/OrcaCore.Acceptance.Tests/TimerAcceptanceTests.cs`
- Spec: `docs/specs/05-requirements-events-waits-timers.md`
- Spec: `docs/specs/09-requirements-management-operations.md`

## Deliverables
- Timeout enforcement in ephemeral and durable execution paths
- Lifecycle outcome metadata for timed-out steps
- Acceptance test coverage for AC-113

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Policies/TimeoutPolicyTests.cs`:
1. `StepTimeout_FailInstance_MarksFailedWithTimeoutDetails`
2. `StepTimeout_CancelBranchAndContinue_ContinuesAfterBranch`
In `tests/OrcaCore.Acceptance.Tests/PolicyAcceptanceTests.cs`:
3. `[Trait("AC","AC-113")] StepTimeoutPolicy_TriggersConfiguredAction`

## Implementation notes
Baseline supported outcomes for this phase: retry, fail instance, cancel branch and
continue, and operator hold if already represented. Saga compensation hooks remain out of
scope until the saga phase.

## Out of scope
Retry attempt scheduling beyond handoff to T3-08, saga compensation, and durable pool ticket
timeouts.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter "TimeoutPolicy|AC=AC-113"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-07: timeout policy enforcement (EV-052, AC-113)"
