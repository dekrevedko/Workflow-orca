# T3-11: Track lifetimes and detect stuck work

**Difficulty**: Haiku        **Depends on**: T3-10
**Spec**: MG-032, MG-040        **AC**: AC-507, AC-508

## Goal
Track instance and step lifetime timestamps and detect apparently stuck steps/instances.
Detection emits lifecycle events and exposes queryable stuck flags.

## Read first
- `src/OrcaCore.Abstractions/Instances/WorkflowInstanceSnapshot.cs`
- `src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs`
- `src/OrcaCore.Engine.Durable/Management/DurableManagementQuery.cs`
- `src/OrcaCore.Engine.Durable/Management/WorkflowInstanceQueryModel.cs`
- `tests/OrcaCore.TestSupport/Clock.cs`
- Spec: `docs/specs/09-requirements-management-operations.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- Lifetime fields on snapshots/projections
- Stuck detection options and evaluation logic
- Lifecycle event publication for stuck signals
- Acceptance tests for AC-507 and AC-508

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Management/StuckDetectionTests.cs`:
1. `StepBeyondThreshold_EmitsStuckStepEventAndSetsFlag`
2. `InstanceWithoutProgressBeyondThreshold_EmitsStuckInstanceEventAndSetsFlag`
In `tests/OrcaCore.Acceptance.Tests/OperationsAcceptanceTests.cs`:
3. `[Trait("AC","AC-507")] StuckStep_IsSignalledAndQueryable`
4. `[Trait("AC","AC-508")] StuckInstance_IsSignalledAndQueryable`

## Implementation notes
Use `TimeProvider` only. Thresholds are definition-level or engine options, not hard-coded
wall-clock sleeps.

## Out of scope
Distributed stuck reconciliation after crash and durable ticket expiry.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter "Stuck|AC=AC-507|AC=AC-508"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-11: lifetime tracking and stuck detection (MG-040, AC-507, AC-508)"
