# Durable Review Remediation Plan

Saved on 2026-03-16.

## Purpose

This document turns the consolidated durable code review into an implementation plan that can be executed in order.

It is intentionally different from `docs/durable/durable-implementation-plan.md`:
- `durable-implementation-plan.md` tracks what the durable runtime already does
- this document tracks the remediation work needed to address review findings and close the remaining design gaps

## Summary

The remediation work is split into three layers, in strict order:

1. correct public-surface and correctness issues
2. persistence and routing hardening
3. structural cleanup and lower-priority quality/performance work

The plan is decision-complete for the current single-host durable runtime. It specifies:
- the target public API changes
- the routing/index design for cold `WaitLong` instances
- the commit and rollback boundaries
- failure handling for store, routing, and outbox paths
- what remains explicitly deferred

The plan assumes:
- breaking durable public API changes are allowed
- durable event payload types move to a registered/allow-listed model
- `WaitLong` must remain cold after checkpoint and restart until explicit access or matched resume

## Progress Tracking

Status as of 2026-03-16.

Completed:
- Priority 1.1 async durable surface end to end
- Priority 1.2 synchronous durable engine factory (`Create`) replaces fake-async `CreateAsync`
- Priority 1.3 cold `WaitLong` correlation lookup and resident `Wait` registration behavior
- Priority 1.4 authoritative store concurrency tokens
- Priority 1.4 staged hot correlation publication for durable start/resume
- Priority 1.4 resident-wait in-memory rollback from pre-mutation snapshot
- Priority 1.6 observer isolation on the outbox correctness path
- Priority 1.6 manual outbox dispatch returns structured batch results and continues after individual failure
- Priority 1.6 poison outbox handling with persisted failure counts and poison-handler hook
- Priority 1.5 eviction no longer disposes execution locks on the hot path
- Priority 2.9 payload registry and fail-fast durable payload deserialization
- Priority 2.10 invalid persisted `NodePath` diagnostic exception
- Priority 2.11 atomic in-memory store transition staging
- Priority 3.14 single per-instance registration record
- Priority 3.15 outbox pump extracted from `DurableWorkflowEngine`
- Priority 3.15 loaded-instance registry and instance-manager extraction from `DurableWorkflowEngine`
- Priority 2.8 dedicated definition registry boundary object
- Priority 3.15 definition registration/loading extracted from `DurableWorkflowEngine`
- Priority 3.15 `DurableEventRouter` extraction
- Priority 3.16 reflection-based typed loading replacement
- Priority 3.17 durable `GetStateAsync<TState>()` no longer clones through JSON string round-trip
- Priority 3.18 query/validator cleanup
- Priority 3.19 durable step-context metadata (`DefinitionId`, `CurrentNodePath`, `IsResumed`)
- Priority 3.20 abstraction cleanup:
  - persisted frame contract no longer reuses runtime `FrameKind`
  - explicit live-vs-persisted `WorkflowError` factory boundary
  - wait lifecycle and residency policy are split explicitly between `WaitStatus` and `WaitMode`
- Priority 2.13 public durable delete/purge surface via instance and selection scopes
- Priority 2.12 retention policy model via `DurableArtifactRetentionPolicy` and per-artifact cutoffs
- exception hierarchy foundations from Priority 1.4 / Priority 2 routing work:
  - `WorkflowEngineException`
  - `WorkflowDefinitionException`
  - `WorkflowStoreException`
  - `WorkflowRoutingException`
  - explicit routing exceptions for no-match and ambiguous correlation

Partially completed:
- Priority 1.4 concurrency failure safety
  - completed: authoritative tokens, staged hot correlation, resident wait rollback, post-failure reload for cold paths
  - remaining: a fuller staged runtime-state model so live runtime mutation is no longer optimistic before commit
- Priority 1.5 execution-lock ownership cleanup
  - completed: long-wait eviction no longer disposes execution locks on the hot path
  - completed: durable engine disposal is idempotent and waits for active outbox-pump cancellation to settle cleanly
  - completed: final loaded-lock teardown is centralized in instance-manager shutdown rather than raw facade/registration loops
- Priority 2.12 inbox/outbox/history retention policy work
  - completed: provider-level artifact purge primitive for old processed inbox, terminal outbox records, and old history
  - completed: public durable management surface can invoke purge for instance and selection scopes
  - completed: public retention-policy model supports separate windows for processed inbox, terminal outbox, and history
  - remaining: archival strategy and higher-level operational policy defaults
- Priority 3.15 split `DurableWorkflowEngine` by role
  - completed: `DurableOutboxPump`, `DurableInstanceRegistry`, `DurableInstanceManager`, `DurableInstanceRegistration`, `DurableDefinitionRegistry`, `DurableEventRouter`
  - remaining: continued facade thinning and lower-priority cleanup only

Not yet completed:
- Priority 3.21 deferred-items documentation is defined here but not yet fully reflected in user-facing docs

## Follow-Up API Cleanup

These items remain intentionally deferred while the structural remediation is completed:
- archival strategy and higher-level operational policy defaults

## Priority 1: Correctness and Public API Repair

### 1. Async durable surface end to end

Replace sync-over-async on all store-backed durable operations.

Implement:
- `DurableWorkflowEngine.ForDefinitionAsync(...)`
- `DurableInstanceScope.GetAsync(...)`
- `DurableInstanceScope.GetStateAsync<T>(...)`
- `DurableInstanceScope.GetActiveWaitsAsync(...)`
- `DurableSelectionScope.ListAsync(...)`
- `DurableSelectionScope.CountAsync(...)`

Remove:
- blocking `.GetAwaiter().GetResult()` usage from durable registration, loading, snapshot queries, and scope reads

Rules:
- all durable store-backed public APIs accept `CancellationToken`
- sync durable wrappers are removed rather than preserved
- ephemeral APIs remain unchanged unless they also become store-backed

Specific fixes covered:
- H1, H2, H3, H4 from review

### 2. CreateAsync factory decision

`CreateAsync` is currently a synchronous factory wrapped in `Task.FromResult(...)`.

Decision:
- keep `CreateAsync` only if engine startup performs real async initialization after the refactor
- otherwise replace it with a synchronous factory/constructor and move async work to explicit startup paths

This must be decided while the async API migration is implemented, not later.

Specific fixes covered:
- M1
- G2

### 3. WaitLong cold-instance semantics

Current problem:
- definition registration eagerly rehydrates waiting instances
- cold `WaitLong` instances are warmed on restart, which breaks the intended contract

Required end state:
- definition registration does not eagerly hydrate cold `WaitLong` instances
- cold waits are still routable by correlation without loading all waiting instances
- actual hydration happens only on:
  - explicit instance access
  - matched resume

Concrete design choice:
- add store-level correlation wait lookup metadata rather than scanning all waiting persisted instances
- extend the durable store contract with a lightweight correlation query for active waits:
  - input: `EventName`, `CorrelationId`
  - output: matching instance ids and enough metadata to detect ambiguity without full instance hydration
- add an explicit store contract method for this lookup, implemented by every durable provider:
  - preferred shape:
    - `Task<CorrelationLookupResult> LookupByCorrelationAsync(string eventName, string correlationId, CancellationToken ct)`
    - `record CorrelationLookupResult(CorrelationMatchType MatchType, IReadOnlyList<CorrelationMatch> Matches)`
    - `record CorrelationMatch(string InstanceId, string DefinitionId, string DefinitionVersion, bool IsLongWait)`
    - `enum CorrelationMatchType { NoMatch, SingleMatch, Ambiguous }`
- keep full persisted instance load only for the final selected instance
- add a dedicated durable correlation index abstraction in the runtime that has two sources:
  - hot in-memory registrations for loaded instances
  - persistent store lookups for cold instances
- correlation-targeted routing always checks hot registrations first, then store-backed correlation lookup on miss
- the store-backed lookup must not require deserializing every waiting instance or scanning all `PersistedRuntimeState.ActiveWaits`
- the store-backed lookup must not scan all waiting instances; it is backed by dedicated correlation metadata/indexes in the provider
- registration behavior after this change is explicit:
  - regular durable `Wait` instances may still be eagerly rehydrated at definition registration so they remain resident
  - `WaitLong` instances must not be eagerly rehydrated at definition registration
  - if future work removes eager rehydration for regular `Wait` too, that is a separate optimization, not part of this remediation

Do not use:
- eager startup hydration
- unbounded `QueryAsync(status: Waiting)` + in-memory scan as the long-term solution

Specific fixes covered:
- my added finding about eager rehydration
- G1
- G12

### 4. Concurrency failure safety

Current problem:
- runtime state and correlation are mutated before durable commit
- `ConcurrencyException` can leave in-memory state diverged from store state

Decision:
- remove the engine-side `_concurrencyTokens` mirror and treat store-committed tokens as authoritative
- move the engine to a staged transition model:
  - compute the next runtime state and correlation changes without mutating the live registration
  - commit to the store
  - only after successful commit, publish the staged in-memory state and routing/index changes
- on store CAS failure:
  - discard staged changes
  - reload the authoritative instance snapshot from store
  - retry only if the call path explicitly supports retry; otherwise return/throw with a clean in-memory state
- CAS retry policy is explicit:
  - correlation-targeted routing may retry once after reload
  - instance-targeted operations do not auto-retry
  - definition fanout does not auto-retry per instance
  - max CAS retries: 1
  - no backoff is used for CAS retry
- correlation index changes participate in the same staged transition unit:
  - stage add/remove operations against a transactional correlation-index abstraction
  - apply only after store commit succeeds
  - never mutate the live correlation index before durable commit success
- pending-event buffer lifecycle must survive eviction and reload without duplication:
  - persisted pending events are restored into `RuntimeState.PendingEvents`
  - restore logic must deduplicate against `ConsumedEventIds`
  - repeated reloads must not duplicate pending events in memory
- store-originated failures must be distinguishable from business/runtime failures:
  - introduce a workflow-engine exception hierarchy rooted in `WorkflowEngineException`
  - required hierarchy:
    - `WorkflowEngineException`
      - `WorkflowStoreException`
        - `WorkflowNotFoundException`
        - `WorkflowConcurrencyException`
        - `WorkflowStoreUnavailableException`
      - `WorkflowDefinitionException`
        - `DefinitionVersionMismatchException`
        - `DefinitionAlreadyRegisteredException`
      - `WorkflowRoutingException`
        - `AmbiguousCorrelationException`
        - `NoActiveWaitException`
  - routing/load/query failures caused by store unavailability must not be reported as "not found" or business step failures

Specific fixes covered:
- M2
- M3
- G7
- G9
- additional gap 2
- additional gap 3
- additional gap 6

### 5. ExecutionLock ownership and eviction safety

Current problem:
- lock lifetime is not clearly owned
- eviction can dispose a lock while another caller is racing to acquire it

Decision:
- `WorkflowInstance` no longer exposes disposal responsibility to external cache/store code
- normal cache eviction must not dispose the instance lock
- lock disposal happens only when the whole engine/cache entry is permanently torn down
- use a cache-entry abstraction that owns:
  - instance
  - execution lock
  - definition metadata
  - persistence delegates

Specific fixes covered:
- concern 1 about semaphore disposal
- S4
- C2

### 6. Outbox correctness path

Implement:
- observer callbacks must never affect dispatch success/failure
- manual dispatch processes all pending records in one pass
- manual dispatch returns a structured result:
  - dispatched count
  - failed records
  - last/aggregate exception details as needed
- update `OutboxPumpCycleResult` and `OutboxPumpDelayContext` to align with structured dispatch results

Decision:
- manual fanout/dispatch semantics are best-effort with error collection, not all-or-nothing
- outbox remains at-least-once delivery; consumers are required to be idempotent
- poison-message handling is added to the remediation scope:
  - track consecutive dispatch failures per outbox record
  - extend persisted outbox record state with failure metadata:
    - `FailureCount`
    - `LastFailureAt`
    - optional `LastError`
  - add engine options for:
    - `MaxOutboxDispatchAttempts`
    - poison-message handling callback or policy
  - when a record exceeds the configured threshold, it is marked poisoned and removed from the active retry loop
- `OutboxPumpCycleResult` and related options must be revised to include structured success/failure/poison outcomes

Specific fixes covered:
- my observer finding
- M4
- minor omission about `OutboxPumpCycleResult` / `OutboxPumpDelayContext`
- additional gap 5

### 7. Durable fanout semantics

Current problem:
- `DurableWorkflowEngine<TState>.RaiseEvent(...)` fans out sequentially
- first failure aborts the remainder

Decision:
- keep fanout sequential in this remediation pass
- change fanout semantics to best-effort with per-instance result collection
- return a structured dispatch result for definition-scoped fanout instead of failing on the first error
- use this result shape:
  - `record FanoutDispatchResult(int AttemptedCount, int SucceededCount, int FailedCount, IReadOnlyList<FanoutInstanceResult> InstanceResults)`
  - `record FanoutInstanceResult(string InstanceId, bool Succeeded, WorkflowEngineException? Error)`
- failures do not abort later instances; per-instance errors are accumulated in the result
- defer parallelized/batched fanout and `IAsyncEnumerable` redesign

Specific fixes covered:
- G5
- "Missing AsyncEnumerable for bulk operations" is explicitly deferred

## Priority 2: Persistence, Routing, and Deserialization Hardening

### 8. Version mismatch and registration boundary

Version mismatch detection must remain a registration-boundary concern even after engine decomposition.

Decision:
- `DurableDefinitionRegistry.RegisterAsync(...)` is the single boundary for durable definition registration
- version mismatch checks happen there, before any:
  - loader interaction
  - cache population
  - correlation reconstruction
  - subsystem delegation
- duplicate registration semantics are explicit:
  - same `DefinitionId` + same `DefinitionVersion` + same state type: idempotent no-op
  - same `DefinitionId` + same `DefinitionVersion` + different state type: reject
  - same `DefinitionId` + different version in the same engine: reject unless a future multi-version hosting mode is explicitly added
- the current two-query registration pattern must be removed:
  - one registration-time mismatch check remains
  - no second "load all waiting instances" query is allowed for `WaitLong`
  - eager load of resident `Wait` instances, if kept, must use a targeted resident-wait load path rather than the old mixed query behavior

Specific fixes covered:
- existing version-mismatch findings
- G8
- additional gap 1
- additional gap 10

### 9. Payload type registry

Replace `AssemblyQualifiedName` payload persistence with a stable registered type key.

Implement:
- durable payload type registry/resolver in durable engine options
- `PersistedEventEnvelope.PayloadType` changes meaning from CLR type name to durable payload type key
- only registered keys are deserializable
- unknown keys fail fast with explicit durable deserialization exceptions

Rules:
- no fallback to boxed `JsonElement` for unknown types
- preserve support for:
  - primitives
  - strings
  - registered DTO payloads

This work should be designed together with the async surface migration because it touches the same serialization paths.

Specific fixes covered:
- S1
- S2
- G10
- minor omission about `PersistedEventEnvelope.PayloadType`

### 10. Rehydration diagnostics

Improve persisted-state failure diagnostics.

Implement:
- invalid `NodePath` rehydration errors that include:
  - `InstanceId`
  - `DefinitionId`
  - `DefinitionVersion`
  - offending `NodePath`
- explicit payload type resolution errors that include the durable type key

Specific fixes covered:
- S3

### 11. Atomic store transition contract

Current problem:
- reference store updates instance state before all artifacts are safely staged

Decision:
- store transition contract is atomic:
  - state
  - inbox additions
  - inbox processed promotions
  - outbox additions
  - history additions
- in-memory store must stage and validate all changes before replacing the committed snapshot
- if any part fails, no visible state change occurs
- parallel branch execution state must restore exactly across eviction/reload:
  - `ActiveParallel.BranchPaths` remain authoritative persisted branch state
  - completed branches stay absent from the branch-path map
  - reloaded instances must still be able to complete the remaining branch and trigger the join exactly once

Also define and document transaction boundaries:
- atomic in one durable commit:
  - instance runtime state
  - business state
  - pending events
  - inbox additions/promotions
  - outbox additions
  - history additions
  - staged in-memory correlation index updates published after the durable commit succeeds
- not atomic across multiple instances:
  - definition-scoped fanout deliveries
- not atomic with external side effects:
  - outbox dispatch and mark-dispatched remain at-least-once
- parallel execution semantics remain unchanged in this remediation:
  - `Parallel` is coordinated sequential execution in the current runtime
  - true concurrent branch execution is deferred
  - persistence/recovery must preserve the current sequential branch behavior exactly

Specific fixes covered:
- my in-memory store atomicity finding
- additional gap 4
- additional gap 12

### 12. Inbox retention and growth

Current problem:
- inbox grows forever for long-lived instances

Decision:
- do not prune inbox records in this remediation pass unless they have become redundant due to explicit retention rules
- document and implement a retention strategy placeholder:
  - terminal-instance delete removes inbox
  - long-lived-instance pruning policy is deferred and tracked as follow-on work
- the same deferral applies to outbox and history growth
- document current phase-1 behavior explicitly:
  - inbox is append-only for the lifetime of the instance
  - outbox is append-only aside from dispatch/poison status changes
  - history is append-only

Add explicit deferral:
- inbox retention / archival policy
- retention windows for processed dedup records
- retention and archival for outbox/history
- optional purge APIs for old records

Specific fixes covered:
- G6
- additional gap 7

### 13. Durable delete/purge surface

Current problem:
- store supports delete but durable public surface does not

Decision:
- do not add public delete/purge during this remediation pass
- record delete/purge as an explicit deferred API design item because it interacts with retention, outbox history, and management semantics

Specific fixes covered:
- G3

## Priority 3: Structural Cleanup and Lower-Priority Remediation

### 14. Collapse per-instance dictionaries into one registration record

Replace the current parallel dictionaries with one cache/registration object keyed by `InstanceId`.

That record owns:
- loaded instance
- definition version
- authoritative concurrency token
- state snapshot factory
- persistence delegate
- resume delegate
- lifecycle state needed by eviction/cache management

Cache eviction rules for this remediation:
- evict immediately:
  - `WaitLong` instances after successful checkpoint
  - terminal instances after terminal processing finishes
- do not add size-based eviction in this remediation
- do not add idle-time eviction in this remediation
- do not evict resident non-long waits in this remediation

This refactor should happen immediately after the staged-transition work is shaped, because it is the clean foundation for the remaining fixes.

Specific fixes covered:
- C2
- G9 dependency tension

### 15. Split DurableWorkflowEngine by role

Extract internal collaborators:
- `DurableDefinitionRegistry`
- `DurableInstanceRegistry` or `DurableInstanceCache`
- `DurableInstanceLoader`
- `DurableEventRouter`
- `DurableCorrelationIndex`
- `OutboxPump`

Keep `DurableWorkflowEngine` as the public facade only.

Typed wrapper decision:
- `DurableWorkflowEngine<TState>` stays as the typed public entry point for:
  - typed start
  - definition-scoped fanout
  - definition-scoped query convenience
- it becomes a thin pass-through over typed collaborators owned by the definition registry
- it must stop building expression trees manually
- definition-scoped query filtering moves into a dedicated typed query collaborator or definition-scoped query factory
- the typed wrapper must not own business logic beyond argument forwarding and typed scoping

This also addresses:
- long methods like `RaiseEventToInstanceAsync(...)` and `StartAsync(...)`
- clearer ownership boundaries for routing, caching, and startup behavior

Specific fixes covered:
- C1
- long methods finding

### 16. Replace reflection-based typed loading

Current problem:
- typed load uses cached reflection over `LoadTypedInstance<TState>(...)`

Decision:
- register typed loader delegates when definitions are registered
- remove reflection-based loader invocation from hot paths
- keep AOT compatibility in mind for the replacement

Specific fixes covered:
- H5
- reflection concern

### 17. State copy and inspection path

Durable `GetStateAsync<TState>()` must not round-trip through JSON for every read.

Decision:
- move state snapshot copying behind an internal clone abstraction
- use the same clone mechanism for durable and ephemeral inspection surfaces where practical
- keep JSON fallback only as an internal last resort, not the primary hot-path copy mechanism

Specific fixes covered:
- G4
- concern 2 about deep copy

### 18. Query compilation and organization

Decision:
- do not add an expression compilation cache in this remediation pass
- document durable and ephemeral query APIs as admin/management surfaces, not high-frequency query engines
- if extraction happens, move `DurableWorkflowQueryValidator` out of `DurableSelectionScope.cs` into a dedicated internal query component

Specific fixes covered:
- concern 3 about expression compilation
- minor omission about inline `DurableWorkflowQueryValidator`

### 19. Step context and runtime metadata follow-up

Add a narrow durable-execution context enhancement only if needed to complete the correctness work.

Decision:
- do not redesign `StepContext<TState>` broadly in this remediation pass
- if durable retry/resume correctness requires it later, the next permitted additions are:
  - `IsResumed`
  - stable per-activation execution metadata
- do not add persistence token exposure or broad definition metadata yet

Specific fixes covered:
- additional gap 9

### 20. Abstraction cleanup

Implement:
- split wait lifecycle from residency policy explicitly:
  - `WaitStatus` remains lifecycle state such as active/matched/cancelled
  - `WaitMode` carries residency policy such as resident/cold
- split live runtime `WorkflowError` from persisted error representation
- review `FrameKind` visibility and either:
  - document it as part of the durable persistence contract
  - or give persistence DTOs their own enum
- keep `RuntimeState` lifecycle mutations funneled through lifecycle helpers where status rules matter

Specific fixes covered:
- C3
- C4
- C5
- runtime-state mutability concern

### 21. Explicit deferrals and documentation

Document as deferred, not part of this remediation implementation:
- public durable delete/purge API
- inbox pruning/retention policy for long-lived instances
- `IAsyncEnumerable`/parallel fanout redesign
- saga abstractions
- true multi-node/shared correlation infrastructure
- making `Parallel` truly concurrent instead of coordinated sequential execution
- richer step API redesign (`IStep.StepId`, `EventEnvelope` constructor ordering, broader `StepContext` metadata)
- persistent correlation index optimized for large-scale production deployments beyond the current single-host phase
- performance-driven query compilation caching
- workflow-to-workflow integration design

Also document as current limitations:
- `CorrelationIndex` is single-engine/single-host phase-1 infrastructure
- node paths like `0/then/0` are internal durable metadata, not stable user API
- current routing scalability target is limited; large-scale persistent correlation indexing is a follow-on design
- `Parallel` remains coordinated sequential execution, not true concurrent branch execution

Specific fixes covered:
- G3
- G6
- architecture/test omissions that are intentionally out of scope here
- additional gap 11

## Test Plan

### Required correctness tests

Add or update tests for:
- async durable surface and cancellation propagation
- cold `WaitLong` resume path through the new correlation lookup path
- `ResolveInstanceIdAsync(...)` store-backed cold-instance lookup without eager hydration
- concurrent `RaiseEvent` calls to the same durable instance
- engine disposal while a durable event resume is active
- CAS failure leaving in-memory state aligned with the store
- no `ObjectDisposedException` during eviction/disposal races
- observer exceptions not affecting outbox dispatch success
- manual outbox dispatch returning mixed success/failure results
- invalid payload type key failure
- invalid `NodePath` diagnostic exception content
- duplicate definition registration semantics
- version mismatch rejection happens at definition registration before loader/cache interaction
- pending-event buffer does not duplicate across repeated eviction/reload cycles
- parallel branch state survives reload mid-execution and still joins correctly
- explicit hard-case parallel reload:
  - instance persisted with 1 branch completed
  - 1 branch waiting
  - 1 branch not yet completed
  - reload must preserve exact branch/join behavior
- correlation-targeted routing reports store failure distinctly from "no active wait"
- poison outbox record moves to poison handling after configured threshold
- two concurrent resume attempts for the same durable instance result in one committed winner and one clean failure/reload path
- inbox dedup after restart:
  - same buffered event redelivered after restart does not create a second inbox record
- dispose safety:
  - `DisposeAsync` during active event resume cancels or completes safely
  - `DisposeAsync` during outbox pump completes without corrupting pump state
  - `DisposeAsync` called twice does not throw

### Required store/persistence tests

Add tests for:
- atomic transition rollback on outbox/history/inbox staging failure
- authoritative concurrency token behavior after commit and rehydration
- payload registry round-trip for primitive and DTO payloads
- store-backed correlation lookup returns ambiguity/no-match/single-match correctly
- store failures propagate as store exceptions rather than business-state failures
- store-backed correlation lookup does not require full instance deserialization in the reference store implementation
- version mismatch at registration throws before cache or loader interaction

### Secondary test backlog

Track, but do not gate this remediation on:
- memory pressure / benchmark coverage
- correlation index performance benchmarks
- workflow-to-workflow integration scenarios
- timeout policy scenarios
- saga-specific scenarios
- full test folder reorganization

## Recommended implementation order

1. async durable API migration plus `CreateAsync` decision
2. store-backed correlation lookup design for cold waits
3. version-boundary registration rules plus idempotent definition registration
4. staged transitions, authoritative concurrency token, pending-event recovery, and eviction safety
5. outbox observer/manual dispatch semantics plus poison-message handling
6. payload registry plus persisted DTO updates and diagnostics
7. atomic store contract plus branch-state reload guarantees
8. single registration record and engine decomposition
9. state-copy cleanup, abstraction cleanup, and documentation of deferrals

## Acceptance criteria

This remediation is complete when all of the following are true:
- no durable public API blocks on async store work
- cold `WaitLong` instances remain cold across restart and are still routable
- `IWorkflowStore` exposes a lightweight correlation-wait query that does not require full instance deserialization
- store is authoritative for concurrency token progression
- durable in-memory state cannot diverge permanently after CAS failure
- correlation index updates are staged and never survive a failed durable commit
- pending-event buffers restore without duplication across reloads
- observers cannot break outbox correctness
- poison outbox records do not retry forever
- definition fanout returns structured per-instance results
- payload deserialization uses registered durable type keys only
- duplicate definition registration behavior is explicit and tested
- version mismatch detection happens at the registration boundary before loader/cache work
- regular `Wait` versus `WaitLong` registration behavior is explicit and tested
- transaction boundaries are documented for single-instance commits, fanout, and outbox delivery
- parallel semantics are explicitly documented as coordinated sequential execution
- the remaining non-implemented items are explicitly documented as deferred rather than silently omitted
