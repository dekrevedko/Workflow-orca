# T1-02: Lifecycle state machine

**Difficulty**: Haiku        **Depends on**: T1-01
**Spec**: CR-030, CR-031 (structure only)        **AC**: feeds AC-005

## Goal
An explicit, table-driven lifecycle state machine for workflow instances: named triggers,
legal transitions, illegal triggers rejected with clear errors, terminal states rejecting
everything. This is the Stateless-inspired discipline from the spec — one component inside
the runtime, not a framework.

## Read first
- Spec: [specs/04-requirements-core-runtime.md](../../../specs/04-requirements-core-runtime.md) §4.4
- `src/OrcaCore.Abstractions/Instances/WorkflowStatus.cs` (T1-01)

## Deliverables
In `src/OrcaCore.Core/Lifecycle/` (all internal):
- `LifecycleTrigger` enum — named triggers: `Start`, `EnterWait`, `MatchWait`, `Complete`,
  `Fail`, `Cancel`, `Terminate`, `Pause`, `Resume`. The `Pause`/`Resume` rows are part of
  the shared transition table (spec CR-030 defines one table for both modes); they are
  **unreachable from ephemeral mode** — this type is `internal` to Core and the ephemeral
  engine never fires them. Defining the full table once, data-driven and tested, beats
  editing a published table in Phase 2.
- `LifecycleMachine` — pure, stateless service: `Result<WorkflowStatus>
  Fire(WorkflowStatus current, LifecycleTrigger trigger)`; failure carries
  `WorkflowLifecycleException` with a message naming current status + rejected trigger.
- The transition table as data (readonly dictionary/frozen collection), asserted complete
  by tests — not `if` chains.

## Tests to write FIRST
In `tests/OrcaCore.Core.Tests/Lifecycle/LifecycleMachineTests.cs`:
1. `Fire_LegalTransitions_ReturnTargetStatus` — `[Theory]` over the full legal set:
   Running→Waiting (EnterWait), Waiting→Running (MatchWait), Running→Completed,
   Running→Failed, Running/Waiting→Cancelled (Cancel), Running/Waiting→Terminated,
   Running/Waiting→Paused (Pause), Paused→Running (Resume), Paused→Terminated,
   Paused→Cancelled
2. `Fire_IllegalTrigger_FailsWithClearMessage` — `[Theory]`: Completed×all triggers,
   Failed×all, Cancelled×all, Terminated×all, Waiting×Complete, Paused×MatchWait
3. `Table_CoversEveryStatus` — every enum value appears as a source in the table or is
   terminal by declaration (guards against silently unreachable statuses)
4. `TerminalStatuses_AreExactly_Completed_Failed_Cancelled_Terminated`

## Implementation notes
- Return `Result<>`, don't throw, so engines choose throw-vs-handle at their boundary
  (conventions §2). The exception inside the failure is pre-built with the clear message —
  it IS the user-facing error for AC-005 later.

## Out of scope
- Saga statuses (`Compensated`, `CompensationFailed`) — added by Phase 5 with their table
  rows; step/branch lifecycle; any engine integration.

## Definition of done
- [ ] All listed tests green; zero warnings
- [ ] The table is data, not control flow; machine is stateless and pure
- [ ] PROGRESS.md updated; committed as "T1-02: lifecycle state machine (CR-030)"
