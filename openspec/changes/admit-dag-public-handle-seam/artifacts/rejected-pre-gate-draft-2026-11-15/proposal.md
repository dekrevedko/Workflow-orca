## Why

Reshape Task 8.6 must ship the public DAG facade that doc 17 §17.2.6 already specifies: `IDagDefinitionRegistry`, `DagDefinitionHandle<TRunInput>`, `DagRunHandle`, snapshots, result unions and statuses. The run runtime behind it exists (create/reopen, read, terminal wait, cancellation on the durable bridge), but no application can reach it: tests reflect into `DagCoordinator.CreateOrReopenAsync`, plans are installed only as a side effect of create, and cancellation has no production caller.

The facade cannot be written under the current contracts. Both handles are public sealed types in `OrcaCore.Dag` with internal constructors, and their behavior must be supplied by `OrcaCore.Dag.Hosting`. That assembly may reference only three internal DAG types and fifteen exact members (the runtime view), none of which can construct a handle or connect one to the bridge. The canonical friend requirement says any further seam needs its own approved amendment. This is that amendment.

## What Changes

- Propose a **facade-handle grant** on the existing `OrcaCore.Dag -> OrcaCore.Dag.Hosting` friend: exactly two internal constructors, one on `DagDefinitionHandle<TRunInput>` and one on `DagRunHandle`. Every parameter is a public type or a delegate over public types. No new internal type, friend assembly, package edge or test friend. The exact allowlist becomes three non-public types and seventeen non-public members.
- Keep typed exceptions, foreign-plan node rejection, declared-output type checking and result-union construction inside `OrcaCore.Dag`. The hosting assembly reports "absent" or "bound to another definition" through a delegate result and never constructs a DAG exception.
- Keep application-contract values with internal constructors (`DefinitionRegistrationConflict`, `StartIdempotencyConflict`, `WorkflowFailure`, `PayloadFingerprint`) on the durable side of the existing `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` bridge. They cross as opaque public values. The six-signature `OrcaCore -> OrcaCore.Dag` authoring allowlist is not widened and `OrcaCore.Dag.Hosting` does not become a friend of `OrcaCore`.
- **BREAKING, proposed (doc 17 §17.2.6, pre-release surface):** replace the listed internal constructors `DagDefinitionHandle()` and `DagRunHandle(DagRunId)` with the delegate constructors; extend `DagRunDefinitionMismatchException` with expected/actual version and fingerprint so a same-identity mismatch is diagnosable; add `DagRunFaultedException` so a run in the internal `IntegrityFault` state surfaces as a typed public failure from `WaitForTerminalAsync` and `RequestCancellationAsync` instead of an endless wait or a leaked `InvalidDataException`. These are decisions D1–D3 in design.md; the owner accepted D1 and D2 on 2026-11-15, and D3 follows from the grant.
- Record the source obligations Task 8.6 inherits: `Register` is the plan-install point for restart recovery; start replay must resolve the winner by start key as the approved run contract requires; the public status projection of `StartAuthorized` and `IntegrityFault` is frozen here as the run contract delegated.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `developer-facing-surface`: modify exactly "Implementation package boundaries use exact internal friends" to add the two-constructor facade-handle grant and its ownership rules.
- `repository-foundation`: modify exactly "Dependency direction remains one-way" to state that the grant adds no friend, edge or type reference and that the allowlist is three types and seventeen members.

## Impact

Eventual source affects `OrcaCore.Dag` (new public facade types, two internal constructors, one new public exception, one extended exception), `OrcaCore.Dag.Hosting` (registry and handle wiring), `OrcaCore.Durable.Hosting` and `OrcaCore.Engine.Durable` (facade projections and conflict/failure/fingerprint values behind the existing bridge), the `DagRuntimeViewBoundaryGuards` allowlist, the durable-bridge reference fixture, API baselines and package-consumer fixtures.

This draft changes no product source, canonical requirement, numbered document, guard or fixture. It adds only this change directory. Because the runtime-view and authoring-friend changes still own the same two headings, the repository's post-gate rules require a process-only registry gate (Task 0) before the contract is reviewed; that registry row and the provenance refreeze are not part of this draft.

A two-assembly standalone proof (`artifacts/task-1-1-public-handle-seam-proof-*.cs`) compiles with warnings as errors against the real `OrcaCore` and `OrcaCore.Dag`: the hosting stand-in implements the registry, start, reopen, snapshot, output, wait and cancellation using only public API plus the two proposed constructors.
