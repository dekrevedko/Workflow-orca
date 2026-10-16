# T3-08: Enforce retry policies

**Difficulty**: Haiku        **Depends on**: T3-07
**Spec**: CR-006, MG-041        **AC**: AC-510

## Goal
Enforce bounded retry policies for failed steps without producing duplicate committed
outcomes. Attempts, terminal retry exhaustion, and retry lifecycle events are observable.

## Read first
- `src/OrcaCore.Abstractions/Steps/StepResult.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `tests/OrcaCore.Acceptance.Tests/PolicyAcceptanceTests.cs`
- Spec: `docs/specs/04-requirements-core-runtime.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- Retry attempt tracking in runtime state/events
- Retry delay support using the timer primitive where a backoff is configured
- Unit and acceptance tests for bounded idempotent retry

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Policies/RetryPolicyTests.cs`:
1. `RetryPolicy_TransientFailures_RetriesUntilSuccess`
2. `RetryPolicy_ExhaustedAttempts_FailsOnceWithoutDuplicateCommit`
In `tests/OrcaCore.Acceptance.Tests/PolicyAcceptanceTests.cs`:
3. `[Trait("AC","AC-510")] RetryPolicy_IsBoundedAndIdempotent`

## Implementation notes
Retry policy is structured metadata, not a user step. Backoff uses `TimeProvider`; tests
advance fake time.

## Out of scope
Operator retry commands already implemented in Phase 2, saga compensation retry, and
distributed retry scheduling outside durable timers.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter "RetryPolicy|AC=AC-510"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-08: retry policy enforcement (CR-006, AC-510)"
