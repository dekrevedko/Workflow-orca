# Durable Persistence Implementation Plan

Saved on 2026-03-16.

## Current baseline

OrcaCore now has a working single-host durable runtime on top of the in-process engine, including persisted waits, cold `WaitLong` eviction, restart rehydration, inbox/outbox/history artifacts, and automatic outbox replay hooks.

Validated on 2026-03-16:
- `dotnet build OrcaCore.slnx`
- `dotnet test tests/OrcaCore.Tests/OrcaCore.Tests.csproj`
- durable tests under `tests/OrcaCore.Tests/Durable/`

Current test count: 242 passing.

## What is implemented

Phase 1 foundation is in place:
- `WorkflowDefinition<TState>` builds a deterministic node-list index rooted at `""`
- nested node lists are addressable by path:
  - `0/then`
  - `0/else`
  - `1/body`
  - `2/branch/<branchId>`
  - nested examples follow the same pattern, such as `1/body/0/then`
- `ExecutionFrame` now stores `NodePath`
- `WorkflowRuntime` stamps `NodePath` when creating frames for:
  - root
  - `If`
  - `While`
  - parallel branches
- initial durable DTOs exist under `src/OrcaCore.Runtime/Durable/Persistence/`:
  - `PersistedFrame`
  - `PersistedExecutionPath`
  - `PersistedParallelFrameGroup`

Phase 2 state mapping is in place:
- `PersistedInstance` and `PersistedRuntimeState` exist
- durable DTOs exist for:
  - errors
  - waits
  - pending events
  - inbox records
  - outbox records
  - history records
- `StateMapper` round-trips:
  - business state
  - runtime metadata
  - active waits
  - pending buffered events
  - consumed-event ids
  - main-path frames
  - active parallel branch paths
- round-trip coverage exists for:
  - root wait
  - `If`
  - `While`
  - parallel waits

Phase 3 provider foundation is in place:
- `IWorkflowStore` defines durable persistence operations
- `ConcurrencyException` exists for compare-and-swap failures
- `InMemoryWorkflowStore` implements:
  - create
  - load
  - commit with token bump
  - metadata query
  - outbox retrieval and dispatch marking
  - history append
  - delete cleanup
- store tests cover CRUD, query, CAS rejection, outbox, and history cleanup

Phase 4 authoring surface foundation is in place:
- `DurableWorkflowBuilder<TState>` exists as a separate builder surface
- `DurableBranchBuilder<TState>` and `DurableParallelBuilder<TState>` exist
- `DurableWorkflowDefinition<TState>` carries `DefinitionId` and `DefinitionVersion`
- `WaitLong` is exposed on durable builders only
- `WaitLongNode<TState>` now retains `EventName` and `CorrelationSelector`
- durable builder tests cover:
  - versioned durable definitions
  - durable-only `WaitLong`
  - deterministic nested node-path resolution

Durable wait policy is now explicit:
- `Wait` in durable mode is a persisted wait that may remain resident
- `WaitLong` means the wait is expected to outlive the hot in-memory window
- after a `WaitLong` suspends and checkpoints, the live instance is evicted
- resume lazily rehydrates the instance from store on demand

Phase 5 engine foundation is in place:
- `DurableWorkflowEngine` exists over `IWorkflowStore`
- `DurableInstanceScope` and `DurableInstanceSnapshot` exist
- durable start persists instances into the store
- durable definition registration rehydrates waiting instances from persisted state
- correlation routing is rebuilt from persisted active waits
- `WorkflowRuntime` now supports durable execution mode so `WaitLong` behaves like a real wait instead of failing
- durable instance events checkpoint both:
  - matched resume transitions
  - unmatched buffered events
- active waits persist both lifecycle and residency policy explicitly via:
  - `WaitStatus`
  - `WaitMode`
- `WaitLong` instances are evicted after checkpoint and rehydrated lazily on access/resume
- restart coverage exists for:
  - durable `WaitLong`
  - store-backed restart and correlation-targeted resume
  - unmatched event buffering persistence
  - immediate `WaitLong` eviction vs resident durable `Wait`
  - idempotent engine disposal and clean shutdown during active outbox pumping
- explicit `DefinitionVersionMismatchException` now exists and is thrown for version-mismatched waiting instances during rehydration
- durable query APIs now exist:
  - `DurableWorkflowEngine.All()`
  - `DurableWorkflowEngine.Where(...)`
  - typed `DurableWorkflowEngine<TState>.All()`
  - typed `DurableWorkflowEngine<TState>.Where(...)`
- durable queries run against persisted metadata, so they include both hot and cold instances
- inbox, outbox, and history records are now written during durable start and durable event commits
- in-memory store supports committing state together with inbox/outbox/history artifacts
- artifact coverage exists for:
  - processed inbox records on resume
  - buffered inbox records for unmatched events
  - waiting/completed outbox transition records
  - started/resumed/status history records
- processed inbox records now participate in duplicate-event suppression after restart, even if `ConsumedEventIds` are missing
- buffered inbox records now also participate in restart dedup, so duplicate unmatched events are suppressed even if hot `PendingEvents` are absent
- when a buffered event is later consumed by a newly registered wait, the matching inbox record is promoted to `Processed = true` as part of the durable commit
- durable engine now exposes pending outbox dispatch and marks records dispatched only after successful delivery
- durable engine now supports runtime-configurable automatic background outbox dispatch
- automatic outbox dispatch is enabled by default when an outbox dispatcher is configured
- durable management surface now supports:
  - `DurableInstanceScope.DeleteAsync(...)`
  - `DurableInstanceScope.PurgeArtifactsAsync(...)`
  - `DurableSelectionScope.DeleteAsync(...)`
  - `DurableSelectionScope.PurgeArtifactsAsync(...)`
- durable retention policy can now be expressed explicitly through:
  - `DurableArtifactRetentionPolicy`
  - `DurableArtifactRetentionCutoffs`
  - policy-based purge overloads on instance and selection scopes
- outbox pump hardening hooks now exist:
  - `IOutboxPumpObserver` for observability/logging adapters
  - `IOutboxPumpDelayStrategy` for retry timing, backoff, and jitter policies
  - `ExponentialOutboxPumpDelayStrategy` as a built-in backoff+jitter option
- restart coverage exists for:
  - pending outbox replay after engine restart
  - failed outbox delivery leaving the record pending
  - automatic outbox replay on startup
  - disabling automatic outbox replay via engine options
  - buffered duplicate suppression after restart
  - buffered-event inbox promotion when the buffered event is later consumed

Important implementation note:
- node paths are not stored on `IWorkflowNode`
- the canonical durable address is the node-list path held by the frame plus the frame index
- frame activation identity now also persists via `PersistedFrame.ScopeId`
- root frames keep `ScopeId = null`
- nested frame activations (`If`, `While`, parallel branches) get stable generated `ScopeId` values
- this is intended to support future decorators/policies that need activation identity, such as retry and timeout

## Durable model

The intended serialization model for execution position is:

```text
PersistedExecutionPath
  BranchId?
  Frames[]

PersistedFrame
  Kind
  NodePath
  Index
  ScopeId?
```

`NodePath` identifies a node list, not a single node. The active node is `Nodes[Index]` after resolving the list from the definition.

Rehydration shape:
1. load persisted execution path
2. for each frame, resolve `definition.ResolveNodes(frame.NodePath)`
3. recreate `ExecutionFrame(frame.Kind, frame.NodePath, resolvedNodes, frame.ScopeId)`
4. restore `Index`

## Remaining work

Core durable runtime plumbing is now in place. The remaining work is follow-on feature work and hardening:
- policy/decorator model that actually consumes `ScopeId`
- richer outbox pump production concerns such as metrics wiring, hosted-service integration, and transport-specific retry policies
- archival strategy and broader multi-host durability semantics once single-host restart behavior is fully productized

## Cleanup decisions

- Inbox vs runtime dedup:
  - keep `PendingEvents` and `ConsumedEventIds` as the hot in-memory fast path
  - treat inbox records as the committed restart boundary and audit trail
  - do not remove runtime dedup structures yet; inbox complements them rather than replacing them
- Automatic outbox pump API:
  - keep automatic replay enabled by default when an `IMessageDispatcher` is configured on `DurableWorkflowEngineOptions`
  - keep explicit `DispatchPendingOutboxAsync(...)` for tests and manual recovery
  - keep retry timing and observability pluggable through `IOutboxPumpDelayStrategy` and `IOutboxPumpObserver`

## Recommended next coding step

Start policy/decorator design on top of persisted `ScopeId`:
1. define timeout policy state and timer identity per scope activation
2. define retry policy state and attempt accounting per scope activation
3. decide how decorator-owned state is persisted alongside frames/runtime state
4. add acceptance tests for restart-safe timeout/retry behavior

Do not add multi-host or lease semantics before inbox/outbox and version-binding behavior are correct on single-host restart.
