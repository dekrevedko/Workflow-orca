# T4B-06: Add run cancellation and DAG observability

**Difficulty**: Haiku        **Depends on**: T4B-05
**Spec**: JS-005, JS-006        **AC**: JS-AC-005, JS-AC-009

## Goal
Complete scheduler-scenario observability and cancellation behavior. Cancelling a DAG run
records stop intent for every in-flight external job before the run reaches `Cancelled`,
and operators can reconstruct DAG node status from lineage, projections, and history.

## Read first
- `src/OrcaCore.Engine.Durable/Management/DurableManagement.cs`
- `src/OrcaCore.Engine.Durable/Management/WorkflowInstanceQueryModel.cs`
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `tests/OrcaCore.Acceptance.Tests/ExternalJobAcceptanceTests.cs`
- Spec: `docs/specs/14-driving-scenario-eks-job-scheduler.md` sections 14.3 and 14.4
- Spec: `docs/specs/12-acceptance-criteria.md` durable and management criteria

## Deliverables
- Durable cancellation handling for active external-job waits
- Stop-command outbox records for all in-flight jobs before final cancellation
- DAG run reconstruction snapshot/query model
- `tests/OrcaCore.Engine.Durable.Tests/ExternalJobs/ExternalJobCancellationTests.cs`
- Acceptance coverage in `tests/OrcaCore.Acceptance.Tests/DagObservabilityAcceptanceTests.cs`

## Tests to write FIRST
In `ExternalJobCancellationTests.cs`:
1. `CancelRun_WithMultipleInFlightJobs_RecordsStopCommandsBeforeCancelled` - every active
   job has stop intent before terminal status. Trait JS-AC-009.
2. `ReconstructDagRun_AfterNodeCompletionAndFailure_ReturnsNodeStatusesAndTimings` - query
   uses lineage/projections only. Trait JS-AC-005.

## Implementation notes
Use existing lineage fields from Phase 4. Cancellation is cooperative and must be durable;
do not dispatch stop commands from volatile state only. DAG reconstruction should expose
snapshots, not live mutable instances.

## Out of scope
Scheduler UI, cron recurrence, real Kubernetes deletion, and multi-host lease work.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter "AC=JS-AC-005|AC=JS-AC-009"` passes
- [ ] `dotnet test OrcaCore.slnx --filter "AC=JS-AC-001|AC=JS-AC-002|AC=JS-AC-003|AC=JS-AC-004|AC=JS-AC-005|AC=JS-AC-006|AC=JS-AC-007|AC=JS-AC-009|AC=JS-AC-010|AC=JS-AC-011|AC=JS-AC-012|AC=JS-AC-013|AC=AC-518|AC=AC-519|AC=AC-520|AC=AC-521|AC=AC-522"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4B-06: run cancellation and dag observability (JS-005)"
