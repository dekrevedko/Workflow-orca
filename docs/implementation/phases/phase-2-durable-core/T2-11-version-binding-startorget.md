# T2-11: Add version binding and StartOrGet

**Difficulty**: Haiku        **Depends on**: T2-10
**Spec**: DU-040, DU-041, DU-053        **AC**: AC-306, AC-307, AC-311

## Goal
Persist definition version binding as an early durable fact and add idempotent durable start.
Repeated starts with the same idempotency key return the existing instance.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/`
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/`
- `v3-gpt/src/OrcaCore.Core/Definitions/WorkflowDefinition.cs`
- Spec: `docs/specs/06-requirements-durable-execution.md` sections 6.5 and 6.7

## Deliverables
- Durable `StartOrGet` API and idempotency key contract
- Version-bound start events and projection fields
- Explicit incompatible-version diagnostics.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Versioning/DurableVersioningTests.cs`:
1. `[Trait("AC","AC-306")] StartedInstance_RemainsBoundToOriginalDefinitionVersion`
2. `[Trait("AC","AC-307")] IncompatibleDefinitionChange_FailsWithExplicitDiagnostic`
3. `[Trait("AC","AC-311")] StartOrGet_SameKey_ReturnsExistingInstance`
4. `StartOrGet_SameKeyDifferentInput_ReturnsExistingWithoutDuplicate`

## Implementation notes
Define compatibility narrowly for this slice and document diagnostics in test names/messages.

## Out of scope
Continue-as-new, history pressure, schema migration tooling.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] AC-306, AC-307, and AC-311 are green
- [ ] PROGRESS.md updated; committed as "T2-11: version binding and StartOrGet (AC-306, AC-307, AC-311)"
