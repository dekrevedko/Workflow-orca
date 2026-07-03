# T3-13: Add in-process resource governance

**Difficulty**: Sonnet        **Depends on**: T3-12
**Spec**: MG-060, MG-061, CR-006        **AC**: AC-511

## Goal
Enforce optional in-process advancement, step concurrency, and named-pool limits without
weakening per-instance serialization. Pool-key hints are declarative policy metadata; step
code never acquires locks directly.

## Read first
- `v3-gpt/src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- `v3-gpt/src/OrcaCore.Core/Definitions/Nodes.cs`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/InstanceExecutionLane.cs`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Instances/WorkflowInstanceSnapshot.cs`
- Spec: `docs/specs/09-requirements-management-operations.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- In-process governance options and named-pool coordinator
- Runtime enforcement around advancement and step execution
- Snapshot/query visibility for active pool pressure
- Acceptance test coverage for AC-511

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Ephemeral.Tests/Governance/ResourceGovernanceTests.cs`:
1. `StepConcurrencyLimit_AllowsOnlyConfiguredConcurrentSteps`
2. `NamedPoolLimit_SharedAcrossDefinitions_BoundsConcurrentExecution`
3. `Governance_DoesNotBreakPerInstanceSerialization`
In `v3-gpt/tests/OrcaCore.Acceptance.Tests/OperationsAcceptanceTests.cs`:
4. `[Trait("AC","AC-511")] ConcurrencyLimitsAndNamedPoolsAreHonored`

## Implementation notes
Transient named pools are in-process only and are lost on restart. Durable ticket pools
remain Phase 4b scope.

## Out of scope
Durable resource pools, all-or-nothing multi-pool tickets, ticket expiry, and external job
capacity governance.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "ResourceGovernance|AC=AC-511"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] Phase 3 exit AC list is green
- [ ] PROGRESS.md updated; committed as "T3-13: in-process resource governance (MG-060, AC-511)"
