# T3-06: Add policy decorator model

**Difficulty**: Sonnet        **Depends on**: T3-05
**Spec**: CR-006, CR-002        **AC**: none

## Goal
Add declarative policy metadata attachable to steps, scopes, and definitions. This task
introduces the model and builder surface only; enforcement is split into later tasks.

## Read first
- `src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- `src/OrcaCore.Core/Definitions/Nodes.cs`
- `tests/OrcaCore.Core.Tests/Definitions/DefinitionModelTests.cs`
- `tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs`
- `docs/implementation/02-engineering-conventions.md`
- Spec: `docs/specs/04-requirements-core-runtime.md`
- Spec: `docs/specs/13-phasing-and-open-questions.md`

## Deliverables
- Policy metadata records under `src/OrcaCore.Core/Definitions/`
- Fluent builder methods for timeout, retry, cancellation, and pool-key hints
- Validation errors for invalid policy combinations
- Unit tests for policy metadata and validation

## Tests to write FIRST
In `tests/OrcaCore.Core.Tests/Building/WorkflowPolicyBuilderTests.cs`:
1. `StepPolicy_WithRetryAndTimeout_AttachesMetadataToNextStep`
2. `DefinitionPolicy_AppliesToRootMetadata`
3. `InvalidRetryPolicy_ReportsValidationError`

## Implementation notes
Resolve spec open question 6 here by choosing fluent builder calls plus explicit metadata
objects. Do not use attributes or reflection.

## Out of scope
Runtime enforcement, saga compensation policies, durable pools, and serialized DSL support.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] Spec open question 6 resolution logged if the task needs to close it
- [ ] `dotnet test OrcaCore.slnx --filter WorkflowPolicyBuilder` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-06: policy decorator model (CR-006)"
