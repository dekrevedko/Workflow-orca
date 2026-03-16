# Durable Initial Acceptance Criteria

## Acceptance set

### DRI-AT-001: Durable wait survives restart

Given a workflow suspended in durable wait
When the host stops and starts again
Then the workflow remains resumable from durable state

### DRI-AT-002: Rehydration restores committed state only

Given a crash during execution
When the engine rehydrates the instance
Then only the last committed durable state is restored

### DRI-AT-003: WaitLong is supported only in durable mode

Given the durable builder/runtime surface
When a workflow uses `WaitLong`
Then the workflow is accepted and executes according to durable semantics

### DRI-AT-004: Durable instance is version-bound

Given a durable instance created under one workflow definition version
When the application deploys an incompatible definition version
Then the instance is not silently corrupted

### DRI-AT-005: Durable inspection remains queryable

Given many durable instances
When operators query by status, definition, and wait state
Then results are returned from durable metadata without depending on business-payload-only inspection

### DRI-AT-006: Concurrent durable resume attempts serialize

Given a durable waiting instance
When multiple resume attempts race
Then only one valid committed outcome is produced
