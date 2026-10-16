# Durable Progress Checkpoint

Saved on 2026-03-16.

## Current status

Durable phase 1 was started with a strict TDD intent.

Implemented so far:
- added durable tests under `tests/OrcaCore.Tests/Durable/`
  - `DefinitionPathIndexTests.cs`
  - `ExecutionFrameNodePathTests.cs`
- added definition-owned node-list indexing to `WorkflowDefinition<TState>`
  - `ResolveNodes(string nodePath)`
  - `GetNodeListPaths()`
- added `NodePath` to `ExecutionFrame`
- updated `WorkflowRuntime` frame creation to carry node-list paths for:
  - root frame
  - `If`
  - `While`
  - `Parallel` branch frames
- added initial durable persistence DTOs:
  - `src/OrcaCore.Runtime/Durable/Persistence/PersistedFrame.cs`
  - `src/OrcaCore.Runtime/Durable/Persistence/PersistedExecutionPath.cs`
  - `src/OrcaCore.Runtime/Durable/Persistence/PersistedParallelFrameGroup.cs`

## Not fully finished

- `docs/durable/durable-implementation-plan.md` still contains the older draft
- the corrected durable tracker should still be written cleanly
- the last phase-1 changes should be revalidated after restart with a clean rebuild

## Recommended next step

After restart:
1. open this file
2. review:
   - `src/OrcaCore.Runtime/WorkflowDefinition.cs`
   - `src/OrcaCore.Runtime/RuntimeState.cs`
   - `src/OrcaCore.Runtime/WorkflowRuntime.cs`
   - `tests/OrcaCore.Tests/Durable/`
3. run a clean test build
4. rewrite the durable tracker doc
5. continue durable phase 1 validation before moving to phase 2

## Resume prompt

`Resume OrcaCore durable implementation from docs/durable/durable-progress-checkpoint.md. Start by validating the phase-1 node-path changes and the durable tests under tests/OrcaCore.Tests/Durable/.`
