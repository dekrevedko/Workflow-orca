# v3-cursor Implementation Progress

Local progress log for the `v3-cursor/` workspace. Phase task specs live under
`docs/implementation/phases/`.

## Phase 0 — Repository Skeleton

| Task | Status | Date | Notes |
|------|--------|------|-------|
| T0-01 | done | 2026-07-02 | global.json pins SDK 10.0.301; xUnit v3 uses mtp-v2; ProviderCertification uses xunit.v3.extensibility.core |
| T0-02 | skipped | — | CI phase skipped per owner |
| T0-03 | done | 2026-07-02 | functional primitives (PR-050) |
| T0-04 | done | 2026-07-02 | TestSupport clock/race/traits |

## Phase 1 — Ephemeral Engine Core

| Task | Status | Date | Notes |
|------|--------|------|-------|
| T1-01 | done | 2026-07-02 | core contracts (CR-011/020/022, EV-001) |
| T1-03 | done | 2026-07-02 | definition model (CR-003/015); public nodes + WorkflowDefinition/ExecutionPointer |
| T1-04 | done | 2026-07-02 | builder + validation (CR-001/002/005/008, AC-008) |
| T1-02 | done | 2026-07-02 | lifecycle state machine (CR-030); table-driven LifecycleMachine + 4 tests |
| T1-05 | done | 2026-07-02 | straight-line interpreter (AC-001, AC-004); internal `WorkflowInstance<TState>`/`Interpreter<TState>` + `IInstanceRegistry` seam; public `EphemeralWorkflowEngine` facade (`RegisterDefinition`, `StartAsync`); `WaitForEvent`/`Yield` throw `NotSupportedException` naming T1-08/T1-15; removed Ephemeral/Acceptance `SkeletonTests` |
| T1-07 | done | 2026-07-02 | If/While control flow (AC-002, AC-003); pointer-driven interpreter with branch/loop frames (CR-015); `LoopIterationCounters` on `WorkflowInstance`; `InterpreterControlFlowTests` + `ControlFlowAcceptanceTests` |
| T1-06 | done | 2026-07-02 | per-instance execution lane (CR-040/042); internal `InstanceExecutionLane` async serializer; `StartAsync` enters through lane; 4 ExecutionLaneTests |
