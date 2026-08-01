# T3-09: Add WhenFirst residual policies

**Difficulty**: Sonnet        **Depends on**: T3-08
**Spec**: CP-004, EV-051, EV-044        **AC**: AC-204, AC-205

## Goal
Add `WhenFirst` composition with deterministic winner selection and explicit residual
policies for losing branches. Losing branch outcomes are inspectable and their waits/timers
are resolved per policy.

## Read first
- `src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- `src/OrcaCore.Core/Definitions/Nodes.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/ParallelTests.cs`
- `tests/OrcaCore.Acceptance.Tests/ParallelAcceptanceTests.cs`
- Spec: `docs/specs/08-requirements-composition.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- `WhenFirst` builder and definition model
- Runtime support for winner selection and residual policy handling
- Tests in engine and acceptance projects

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/WhenFirstTests.cs`:
1. `WhenFirst_ConcurrentCompletions_SelectsDeterministicWinner`
2. `WhenFirst_CancelRemaining_CancelsLosingBranchWork`
3. `WhenFirst_LetRemainingComplete_RecordsLoserOutcome`
In `tests/OrcaCore.Acceptance.Tests/WhenFirstAcceptanceTests.cs`:
4. `[Trait("AC","AC-204")] WhenFirst_WinnerIsDeterministic`
5. `[Trait("AC","AC-205")] WhenFirst_LosingBranchPolicyIsObservable`

## Implementation notes
Use deterministic coordination helpers for races. Do not introduce durable child workflow
semantics; `WhenFirst` remains an in-instance composition primitive.

## Out of scope
`ForEach`, `RunChild`, durable children, and saga compensation.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter "WhenFirst|AC=AC-204|AC=AC-205"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-09: WhenFirst residual policies (CP-004, AC-204, AC-205)"
