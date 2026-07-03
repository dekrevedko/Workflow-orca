# T5-02: Add separate saga builder surface

**Difficulty**: Sonnet        **Depends on**: T5-01
**Spec**: SG-001, SG-002, SG-003        **AC**: AC-401, AC-402

## Goal
Add a saga-specific authoring surface that is distinct from regular workflow builders.
Forward steps can declare compensation handlers and compensation scopes without exposing
compensation APIs on `WorkflowBuilder<TState>`.

## Read first
- `v3-gpt/src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- `v3-gpt/src/OrcaCore.Core/Definitions/WorkflowDefinition.cs`
- `v3-gpt/src/OrcaCore.Core/Definitions/Nodes.cs`
- `v3-gpt/src/OrcaCore.Core/Building/BuilderValidationCodes.cs`
- `v3-gpt/tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs`
- Spec: `docs/specs/07-requirements-saga.md` section 7.1
- Spec: `docs/specs/12-acceptance-criteria.md` AC-401 and AC-402

## Deliverables
- `v3-gpt/src/OrcaCore.Core/Building/SagaBuilder.cs`
- Saga definition/node metadata under `v3-gpt/src/OrcaCore.Core/Definitions/`
- Validation codes in `v3-gpt/src/OrcaCore.Core/Building/BuilderValidationCodes.cs`
- Tests in `v3-gpt/tests/OrcaCore.Core.Tests/Building/SagaBuilderTests.cs`
- Contract absence tests in `v3-gpt/tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Core.Tests/Building/SagaBuilderTests.cs`:
1. `SagaBuilder_ForwardStepWithCompensation_BuildsSeparateSagaDefinition` - a forward step with `CompensateBy` creates saga metadata. Trait AC-401.
2. `SagaBuilder_CompensationScope_TracksEligibleForwardActions` - scope metadata records the compensatable forward actions. Trait AC-402.

In `v3-gpt/tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs`:
1. `WorkflowBuilder_DoesNotExposeCompensationMethods` - regular builder public methods do not include `CompensateBy`, `CompensationScope`, or saga-only verbs.

## Implementation notes
Follow the existing no-reflection builder rule: terse parameterless generic step syntax is
allowed, configured steps stay explicit through instances or factories. Saga definitions
are a separate kind, not a flag on regular workflow definitions.

## Out of scope
Runtime execution, durable compensation commands, child compensation, and ephemeral engine
support.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "AC=AC-401|AC=AC-402"` passes for tests introduced in this task
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] Compensation APIs are absent from `WorkflowBuilder<TState>`
- [ ] PROGRESS.md updated; committed as "T5-02: separate saga builder surface (SG-001)"
