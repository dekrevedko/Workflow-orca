# T3-10: Publish lifecycle events

**Difficulty**: Sonnet        **Depends on**: T3-09
**Spec**: MG-020, MG-021, DU-031        **AC**: AC-509

## Goal
Publish first-class instance and step lifecycle events with documented durability guarantees.
Durable lifecycle events that must survive restart flow through committed outbox records.

## Read first
- `src/OrcaCore.Core/Lifecycle/LifecycleMachine.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- `src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs`
- Spec: `docs/specs/09-requirements-management-operations.md`
- Spec: `docs/specs/13-phasing-and-open-questions.md`

## Deliverables
- Lifecycle event contracts/snapshots in `src/OrcaCore.Abstractions/`
- In-process lifecycle publication for ephemeral mode
- Durable outbox-backed lifecycle publication for durable terminal and significant events
- Query surface for lifecycle events if needed by AC-509

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Lifecycle/LifecycleEventTests.cs`:
1. `WorkflowCompletion_PublishesCompletionLifecycleEvent`
2. `StepFailure_PublishesStepFailedLifecycleEvent`
In `tests/OrcaCore.Engine.Durable.Tests/Lifecycle/DurableLifecycleEventTests.cs`:
3. `DurableTerminalTransition_CommitsLifecycleOutboxRecordWithState`
In `tests/OrcaCore.Acceptance.Tests/LifecycleAcceptanceTests.cs`:
4. `[Trait("AC","AC-509")] LifecycleEvents_FollowDocumentedDurabilityGuarantees`

## Implementation notes
Resolve spec open question 9 before coding: document which lifecycle events are durable vs
best-effort and log the crosswalk in `00-stack-decisions.md`. Use the IOQ-5 decision for
diagnostic names; lifecycle events are product events, not just telemetry spans.

## Out of scope
OpenTelemetry exporter wiring, saga compensation events beyond placeholders, and durable
pool ticket events.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] Spec open question 9 is resolved and documented
- [ ] `dotnet test OrcaCore.slnx --filter "LifecycleEvent|AC=AC-509"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-10: lifecycle event publication (MG-020, AC-509)"
