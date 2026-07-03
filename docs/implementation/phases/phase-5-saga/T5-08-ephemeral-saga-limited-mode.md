# T5-08: Add ephemeral saga limited mode

**Difficulty**: Haiku        **Depends on**: T5-02, T5-05
**Spec**: SG-030        **AC**: AC-401, AC-402, AC-403, AC-404, AC-409

## Goal
Add a clearly labeled ephemeral saga mode that supports compensation semantics within one
process lifetime only. Public XML documentation and implementation docs must state that
ephemeral saga provides no durable recovery, no durable compensation audit, and no
post-restart operator remediation guarantees.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- `v3-gpt/src/OrcaCore.Core/Building/SagaBuilder.cs`
- `v3-gpt/tests/OrcaCore.Engine.Ephemeral.Tests/Execution/InterpreterTests.cs`
- Spec: `docs/specs/07-requirements-saga.md` SG-030
- Spec: `docs/specs/13-phasing-and-open-questions.md` resolved question 13

## Deliverables
- Ephemeral saga execution support in `v3-gpt/src/OrcaCore.Engine.Ephemeral/`
- Public XML documentation on ephemeral saga APIs describing in-process-only limits
- Implementation docs update if a new public surface is introduced
- Tests in `v3-gpt/tests/OrcaCore.Engine.Ephemeral.Tests/Sagas/EphemeralSagaTests.cs`
- Acceptance coverage in `v3-gpt/tests/OrcaCore.Acceptance.Tests/SagaAcceptanceTests.cs`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Ephemeral.Tests/Sagas/EphemeralSagaTests.cs`:
1. `EphemeralSagaFailure_CompensatesCompletedActionsInReverseOrder` - in-process compensation follows durable semantics. Traits AC-402, AC-403.
2. `EphemeralSagaCompensationFailure_IsObservableInSnapshot` - failed compensation yields `CompensationFailed`. Trait AC-404.
3. `EphemeralSagaRepeatedCompensation_IsIdempotentWithinProcess` - duplicate requests do not duplicate effects. Trait AC-409.
4. `EphemeralSagaPublicDocs_StateInProcessOnlyLimits` - XML documentation text states the resolved limitation.

## Implementation notes
This mode is intentionally not durable. Do not add restart-survival tests or claims.
Any public API summary must include "in-process only" or equivalent wording.

## Out of scope
Durable audit, provider projections, operator recovery, and post-restart behavior.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "AC=AC-401|AC=AC-402|AC=AC-403|AC=AC-404|AC=AC-409"` passes for ephemeral saga tests
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] XML documentation and implementation docs clearly state in-process-only limits
- [ ] PROGRESS.md updated; committed as "T5-08: ephemeral saga limited mode (SG-030)"
