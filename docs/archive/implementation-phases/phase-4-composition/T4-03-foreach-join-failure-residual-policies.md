# T4-03: Add ForEach join and failure policies

**Difficulty**: Haiku        **Depends on**: T4-02
**Spec**: CP-011, CP-012        **AC**: AC-604, AC-605

## Goal
Extend ephemeral `ForEach` with join, failure, and residual policies. `WhenAny` cancellation
intent must be recorded before the parent continues.

## Read first
- `src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- `src/OrcaCore.Core/Definitions/Nodes.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/ForEachTests.cs`
- Spec: `docs/specs/08-requirements-composition.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- `ForEach` join policy model: `WhenAll`, `WhenAny`
- Failure policies: `FailFast`, `WaitAllThenFail`, `ContinueWithPartialFailures`
- Residual policy for `WhenAny`: `CancelRemaining`, `LetRemainingComplete`
- Observable group outcomes

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/ForEachPolicyTests.cs`:
1. `[Trait("AC","AC-604")] ForEach_WaitAllThenFail_FailsAfterAllItemsFinish`
2. `[Trait("AC","AC-605")] ForEach_WhenAny_RecordsCancellationIntentBeforeParentContinuation`
In `tests/OrcaCore.Acceptance.Tests/ForEachAcceptanceTests.cs`:
3. `[Trait("AC","AC-604")] ForEach_WaitAllThenFailIsObservable`
4. `[Trait("AC","AC-605")] ForEach_WhenAnyCancellationIntentPrecedesContinuation`

## Implementation notes
Group-level retry remains out of scope. Cancellation is cooperative and in-process only.

## Out of scope
Durable children, child cancellation, and saga compensation.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter "ForEach|AC=AC-604|AC=AC-605"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4-03: ForEach join and failure policies (CP-011, AC-604, AC-605)"
