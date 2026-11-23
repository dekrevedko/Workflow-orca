## ADDED Requirements

### Requirement: Attempt-local replacements preserve serialized state
Ephemeral and durable workflows SHALL apply the fixed-codec representation guarantee when constructing or replacing runtime-owned state and producing an attempt-local snapshot. A failed state replacement SHALL follow the runtime's owning execution failure path and SHALL NOT publish the changed value, return a silently altered snapshot or replace the last good state.

#### Scenario: State written by an attempt cannot be reconstructed
- **WHEN** a callback constructs state whose encoded representation changes under a decode and re-encode through its declared metadata
- **THEN** the owning execution fails and later queries cannot observe that replacement as committed state
