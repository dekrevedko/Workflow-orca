# T4B-04: Add RunExternalJob composite

> **Superseded historical task (2026-07-18):** do not execute this task or implement its
> signatures/deliverables. Current v1 authority is
> [document 17](../../../specs/17-selected-mode-capability-matrix.md) and the active
> `reshape-developer-facing-interfaces` change. Public `RunExternalJob` is deferred and
> `WaitLong` is removed; v1 uses an ordinary named durable create-or-observe step plus `Wait`.

**Difficulty**: Sonnet        **Depends on**: T4B-03
**Spec**: JS-002, JS-007, EV-041, EV-051, DU-031        **AC**: JS-AC-004, JS-AC-005, JS-AC-006, JS-AC-010, JS-AC-011, JS-AC-012, JS-AC-013

## Goal
Provide a durable submit-and-await external work composite. It acquires required durable
pool tickets before dispatch, emits a start outbox command, waits cold for correlated
completion/failure, and emits a stop command on timeout or cancellation.

## Read first
- `src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs`
- `src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `tests/OrcaCore.TestSupport/Providers/FakeMessageDispatcher.cs`
- Spec: `docs/specs/14-driving-scenario-eks-job-scheduler.md` sections 14.3 and 14.4
- Spec: `docs/specs/09-requirements-management-operations.md` section 9.7

## Deliverables
- Public durable command contracts for starting, completing, failing, timing out, and
  cancelling external jobs
- Outbox records for external job start and stop commands
- Durable wait matching for correlated job completion/failure
- `tests/OrcaCore.Engine.Durable.Tests/ExternalJobs/RunExternalJobTests.cs`
- Acceptance coverage in `tests/OrcaCore.Acceptance.Tests/ExternalJobAcceptanceTests.cs`

## Tests to write FIRST
In `RunExternalJobTests.cs`:
1. `StartExternalJob_WhenTicketsGranted_DispatchesStartCommandAndRegistersColdWait` -
   start happens after tickets are held.
2. `CompleteExternalJob_WhenCompletionRedelivered_ResumesExactlyOnce` - duplicate watcher
   completion is deduplicated. Trait JS-AC-004.
3. `TimeoutExternalJob_WhenTimerWins_DispatchesStopCommandAndReleasesTickets` - timeout
   emits stop and releases. Traits JS-AC-006 and JS-AC-011.
4. `StartExternalJob_WhenTicketQueued_DoesNotDispatchStartCommand` - queued jobs consume no
   external resources. Trait JS-AC-013.

## Implementation notes
The composite is reducible to outbox plus `WaitLong` plus timer race. Keep Kubernetes names
out of the contract; use generic external job ids, payloads, and command kinds. Use the
existing fake dispatcher for acceptance tests.

## Out of scope
Real EKS adapters, scheduled starts, and DAG syntax.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter "AC=JS-AC-004|AC=JS-AC-006|AC=JS-AC-010|AC=JS-AC-011|AC=JS-AC-012|AC=JS-AC-013"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] No Kubernetes-specific terms in public workflow contracts
- [ ] PROGRESS.md updated; committed as "T4B-04: run external job composite (JS-002)"
