# T4B-05: Add DAG builder validation

**Difficulty**: Sonnet        **Depends on**: T4B-04
**Spec**: JS-001, CP-002, CP-003, CP-021        **AC**: JS-AC-001, JS-AC-002, JS-AC-003

## Goal
Add a DAG authoring front-end that accepts nodes and dependency edges, validates graph
shape, and compiles nodes to durable child workflow instances per resolved spec open
question 15. Diamond joins and node failure policy use the existing child workflow and
join semantics.

## Read first
- `src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- `src/OrcaCore.Core/Building/BuilderValidationCodes.cs`
- `src/OrcaCore.Engine.Durable/Building/DurableWorkflowBuilder.cs`
- `src/OrcaCore.Abstractions/Durable/RunChildrenPolicies.cs`
- `tests/OrcaCore.Acceptance.Tests/ChildWorkflowAcceptanceTests.cs`
- Spec: `docs/specs/14-driving-scenario-eks-job-scheduler.md` sections 14.3 and 14.4
- Spec: `docs/specs/13-phasing-and-open-questions.md` section 13.2

## Deliverables
- DAG builder contracts in Core or Durable builder surface, matching existing authoring
  conventions
- Validation diagnostics for missing nodes, duplicate ids, and cycles
- Compile output that schedules runnable root nodes and dependency joins through durable
  child orchestration
- `tests/OrcaCore.Core.Tests/Building/DagBuilderTests.cs`
- `tests/OrcaCore.Acceptance.Tests/DagAcceptanceTests.cs`

## Tests to write FIRST
In `DagBuilderTests.cs`:
1. `Build_WhenDagHasCycle_ReturnsAccumulatedCycleDiagnostic` - cyclic graph fails with
   named cycle. Trait JS-AC-002.
2. `Build_WhenDagHasDiamondShape_ProducesDependencyJoinPlan` - D depends on B and C.

In `DagAcceptanceTests.cs`:
1. `DiamondDag_StartsJoinNodeAfterBothParentsCompleteInAnyOrder` - D starts once after B
   and C. Trait JS-AC-001.
2. `FailingDagNode_PreventsDependentNodesAndAppliesFailurePolicy` - dependents do not
   start and independent branches follow policy. Trait JS-AC-003.

## Implementation notes
This is a compile front-end, not a second runtime. Use child-instance-per-node semantics
recorded in spec question 15. Do not introduce reflection-based step construction; keep
configured nodes explicit via existing builder conventions.

## Out of scope
Visual DAG tooling, cron/scheduled starts, and provider adapters.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter "AC=JS-AC-001|AC=JS-AC-002|AC=JS-AC-003"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4B-05: dag builder validation (JS-001)"
