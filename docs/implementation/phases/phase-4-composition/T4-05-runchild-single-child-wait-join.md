# T4-05: Add RunChild single-child wait join

**Difficulty**: Sonnet        **Depends on**: T4-04
**Spec**: CP-021, CP-022, CP-026, DU-033        **AC**: AC-606, AC-615

## Goal
Add durable `RunChild` for a single child workflow. The parent persists a synthetic wait,
emits a child-start outbox record, and resumes when the child completion is delivered.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- Spec: `docs/specs/08-requirements-composition.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- Durable child-start command/event/outbox record kind
- Parent synthetic wait metadata
- Child completion/failure propagation decisions

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Composition/RunChildTests.cs`:
1. `[Trait("AC","AC-606")] RunChild_WaitJoin_ParentContinuesAfterChildCompletion`
2. `[Trait("AC","AC-615")] RunChild_ChildFailurePropagatesByPolicy`
In `v3-gpt/tests/OrcaCore.Acceptance.Tests/ChildWorkflowAcceptanceTests.cs`:
3. `[Trait("AC","AC-606")] RunChild_WaitJoinCompletesParent`
4. `[Trait("AC","AC-615")] RunChild_CompletionFailurePolicyIsDeterministic`

## Implementation notes
Child starts are resultless by default. The outbox record is a command to start a child, not
an immediate in-memory start.

## Out of scope
Dynamic fanout, child throttling, barrier tokens, and compensation.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "RunChild|AC=AC-606|AC=AC-615"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4-05: RunChild wait join (CP-021, AC-606, AC-615)"
