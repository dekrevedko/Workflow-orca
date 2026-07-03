# T3-11: Track lifetimes and detect stuck work

**Difficulty**: Haiku        **Depends on**: T3-10
**Spec**: MG-032, MG-040        **AC**: AC-507, AC-508

## Goal
Track instance and step lifetime timestamps and detect apparently stuck steps/instances.
Detection emits lifecycle events and exposes queryable stuck flags.

## Read first
- `v3-gpt/src/OrcaCore.Abstractions/Instances/WorkflowInstanceSnapshot.cs`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Management/DurableManagementQuery.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Management/WorkflowInstanceQueryModel.cs`
- `v3-gpt/tests/OrcaCore.TestSupport/Clock.cs`
- Spec: `docs/specs/09-requirements-management-operations.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- Lifetime fields on snapshots/projections
- Stuck detection options and evaluation logic
- Lifecycle event publication for stuck signals
- Acceptance tests for AC-507 and AC-508

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Ephemeral.Tests/Management/StuckDetectionTests.cs`:
1. `StepBeyondThreshold_EmitsStuckStepEventAndSetsFlag`
2. `InstanceWithoutProgressBeyondThreshold_EmitsStuckInstanceEventAndSetsFlag`
In `v3-gpt/tests/OrcaCore.Acceptance.Tests/OperationsAcceptanceTests.cs`:
3. `[Trait("AC","AC-507")] StuckStep_IsSignalledAndQueryable`
4. `[Trait("AC","AC-508")] StuckInstance_IsSignalledAndQueryable`

## Implementation notes
Use `TimeProvider` only. Thresholds are definition-level or engine options, not hard-coded
wall-clock sleeps.

## Out of scope
Distributed stuck reconciliation after crash and durable ticket expiry.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "Stuck|AC=AC-507|AC=AC-508"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-11: lifetime tracking and stuck detection (MG-040, AC-507, AC-508)"
