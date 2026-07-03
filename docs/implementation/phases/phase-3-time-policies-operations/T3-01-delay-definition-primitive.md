# T3-01: Add Delay definition primitive

**Difficulty**: Haiku        **Depends on**: T3-00
**Spec**: EV-050, CR-002, CR-032        **AC**: none

## Goal
Add a first-class `Delay` definition primitive to the authoring and immutable definition
model. This task is structural only: it records delay intent and validation, but does not
schedule or fire timers.

## Read first
- `v3-gpt/src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- `v3-gpt/src/OrcaCore.Core/Definitions/Nodes.cs`
- `v3-gpt/tests/OrcaCore.Core.Tests/Definitions/DefinitionModelTests.cs`
- `v3-gpt/tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs`
- Spec: `docs/specs/05-requirements-events-waits-timers.md`
- Spec: `docs/specs/04-requirements-core-runtime.md`

## Deliverables
- `v3-gpt/src/OrcaCore.Core/Definitions/Nodes.cs`
- `v3-gpt/src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- Any small supporting definition type needed under `v3-gpt/src/OrcaCore.Core/Definitions/`
- Unit tests in `v3-gpt/tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs`:
1. `Delay_WithPositiveDuration_AddsTimerNode` - built definition contains one delay node with the requested duration.
2. `Delay_WithNonPositiveDuration_ReportsValidationError` - build validation reports a stable error code.
3. `Build_DelayBeforeEnd_DoesNotCountAsEnd` - delay remains runtime work and does not satisfy completion.

## Implementation notes
Keep delay as definition metadata, not a business step. Do not use sleeping, `Task.Delay`,
thread timers, or provider ports in this task.

## Out of scope
Ephemeral scheduling, durable scheduling, timeout policy, timer/event races, and lifecycle
events.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter Delay` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-01: delay definition primitive (EV-050)"
