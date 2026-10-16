# Event-Driven Durable Parity Plan

Reviewed on April 13, 2026.

## Purpose

This plan defines the work required to make the event-driven prototype a valid durable-engine replacement for OrcaCore.

Replacement means:

- the event-driven engine supports the same durable product semantics as the current durable engine
- backend selection can switch durable execution from snapshot-first to event-driven without changing workflow behavior
- the event-driven backend passes the durable contract test suite

This is stricter than "prototype feature growth."

It is a parity-and-replacement plan.

## Non-Negotiable End State

The event-driven engine is considered ready only when all of the following are true:

1. The same durable workflow definitions can run on either backend.
2. Shared durable acceptance and contract tests pass on both backends.
3. No known semantic drift remains in:
   - wait registration and resume
   - buffering and deduplication
   - `If` / `While` / `Parallel` / `WhenAll`
   - lifecycle transitions
   - durable start behavior, including duplicate-start policy and any exposed idempotent-start semantics
   - version binding and persisted artifact compatibility
   - query and management behavior
   - cold-instance recovery
   - inbox/outbox consistency
   - delete and purge behavior
4. Parity-critical durable mutation paths use one documented atomic commit boundary that preserves stream, checkpoint, inbox, outbox, and projection correctness.
5. At least one SQL-style durable store provider, or an equivalently strong transactional provider, plus one outbox-dispatch integration path pass the replacement suite, including injected crash/restart scenarios.
6. Durable backend choice is configuration, not a separate public programming model.
7. New durable instances can be started on the event-driven backend using the same frozen durable start contract as the snapshot backend.

## Important Constraint About Tests

"Pass all durable engine tests" must be interpreted correctly.

Some current durable tests validate product behavior and durable contracts.
Those must pass on the event-driven backend.

Some current durable tests validate snapshot-first implementation details such as:

- `StateMapper`
- persisted frame DTO shape
- current in-memory workflow store internals
- snapshot-specific path reconstruction

Those are not valid compatibility gates for an event-driven write model.

So the first task is to split the current durable test inventory into:

- shared durable contract tests
- snapshot-backend-specific tests
- event-driven-backend-specific tests

The event-driven replacement is not complete until it passes the shared durable contract suite plus its own backend-specific tests.

## Current Gap Summary

Current event-driven prototype status:

- implemented:
  - straight-line execution
  - `Wait`
  - instance-targeted event delivery
  - correlation-targeted delivery
  - pending-event buffering
  - inbox-based dedup
  - summary and active-wait projections
  - checkpoint reload after restart
- missing:
  - `If`
  - `While`
  - `Parallel`
  - `WhenAll`
  - definition fanout
  - `WaitLong`
  - provider contract beyond the in-memory prototype store
  - durable query/management surface parity
  - version-binding parity
  - outbox parity
  - retention/delete parity

Current durable engine already has those capabilities and corresponding tests.

## Explicit Architecture Decisions Required Up Front

The parity plan depends on several design decisions that must be made before implementation starts in earnest.

These are architecture gates, not implementation details.

### Decision 1: Definition model adoption

Decision:

- adopt the existing `WorkflowDefinition<TState>` / `IWorkflowNode` model as the event-driven durable definition model

Do not:

- extend `EventDrivenWorkflowDefinition<TState>` into a separate long-term control-flow model
- build a translation-only compatibility layer as the primary parity path

Reason:

- parity requires the same builder output
- `If`, `While`, `Parallel`, and `WhenAll` already exist in the current AST
- keeping two definition universes would create semantic drift immediately

When needed:

- before execution-model redesign begins

### Decision 2: Execution-position persistence model

Decision:

- replace `PrototypeCheckpointState.NextStepIndex` with serialized execution-position state that can represent:
  - root execution
  - nested branch paths
  - loop re-entry
  - parallel branch state
  - join state
  - scope activation identity

Recommended shape:

- event-driven backend persists its own execution-position DTOs
- those DTOs should be structurally equivalent to the durable engine's frame-stack semantics:
  - frame kind
  - node path
  - node index
  - scope id
  - parallel group or branch metadata where required

Reason:

- the current `int NextStepIndex` model cannot represent nested control flow
- restart parity for `If`, `While`, and `Parallel` depends on this

When needed:

- before control-flow parity implementation

### Decision 3: Durable commit boundary

Decision:

- every accepted durable mutation must commit through one atomic provider boundary that persists:
  - appended workflow events or equivalent stream facts
  - checkpoint update
  - inbox state
  - outbox records
  - routing-critical and management-critical projections, or durable projection-work scheduling that is itself part of the same commit
- parity-critical correctness must not depend on best-effort coordination across multiple provider calls
- provider contracts may be internally modular, but the durable contract must expose one `WorkflowCommit`-style boundary or an equivalent atomic commit model

Reason:

- crash safety, read-your-writes routing, restart-safe deduplication, and publish-after-commit all fail if the boundary is split
- provider-specific call ordering is not a substitute for a product invariant

When needed:

- before real provider contract design

### Decision 4: Correlation index ownership

Decision:

- do not make correlation-targeted routing depend on an eventually consistent async projection
- routing-critical active-wait lookup must be updated in the same durability boundary as the accepted mutation
- an in-memory hot cache may exist, but it is an optimization, not the source of truth

Recommended shape:

- authoritative durable active-wait index/query updated inline with command commit
- optional hot runtime cache rebuilt from the authoritative durable view

Reason:

- `MC_AT_017` and `MC_AT_019` require read-your-writes correctness for correlation-targeted routing
- an eventually consistent projection introduces a visibility race immediately after `WaitRegistered`

When needed:

- before real provider contract design

### Decision 5: Projection consistency model

Decision:

- routing-critical and management-critical projections for parity are synchronous inline projections in the same durability boundary as event append/checkpoint save
- asynchronous projection subscribers may be added later for analytics or secondary read models, but not for the parity-critical path

Reason:

- parity with the current durable engine requires immediate visibility for:
  - active waits
  - instance summary state
  - fanout enumeration inputs
  - delete and purge behaviors

When needed:

- before real provider contract design

### Decision 6: Fanout coordination

Decision:

- definition-scoped fanout enumerates candidate instances from the authoritative durable active-wait/query surface
- dispatch proceeds instance by instance
- partial-failure semantics remain:
  - continue after individual instance failure
  - return mixed per-instance results

Reason:

- this is already a shared durable contract requirement
- the event-driven backend cannot rely on a hot in-memory registry once it supports cold instances and real stores

When needed:

- before definition-fanout parity is implemented

### Decision 7: Durable artifact compatibility

Decision:

- workflow event schema, checkpoint schema, and projection rebuild rules are part of the durable compatibility surface
- supported runtime upgrades must either read older persisted artifacts directly or upcast them explicitly before use
- projections may be rebuildable instead of backward-readable, but rebuild inputs, ordering rules, and guarantees must be documented

Recommended shape:

- versioned runtime event catalog
- versioned checkpoint schema with a backward-compatible reader or explicit upcaster
- projection schemas treated as rebuildable read models, not authoritative write truth
- compatibility tests covering the supported prior persisted artifact window

Reason:

- definition version compatibility alone does not protect restart after deployment
- replacement readiness requires cold durable data written by an older build to survive upgrade safely

When needed:

- before provider contract and replacement-readiness gating

### Decision 8: Delete model in an append-only backend

Decision:

- `DeleteAsync` remains a hard-delete operator command at the public surface
- internally, the event-driven backend must record a durable deleted-state transition, tombstone, or equivalent marker before any physical stream or artifact removal that could hide the fact that delete already won
- late instance-targeted or correlation-targeted events against deleted instances must reject deterministically rather than silently recreating state
- `PurgeArtifactsAsync` prunes inbox, outbox, and history according to retention policy and remains semantically distinct from delete

Reason:

- append-only storage needs an explicit deleted-state model to keep replay, rebuild, and late-delivery behavior coherent
- hard delete without a durable deleted state risks accidental resurrection or ambiguous routing

When needed:

- before delete/purge parity implementation

### Decision 9: Durable start contract

Decision:

- replacement parity includes an explicit durable start contract
- that contract must define:
  - plain `Start(...)` duplicate-request behavior
  - whether client-supplied idempotency keys or `StartOrGet` are part of the parity freeze
  - how start binds definition id/version and initial durability metadata
- if the current durable backend exposes client-key idempotent start before Milestone 0 contract freeze, it becomes shared parity scope and test-gated on both backends
- if it does not, the first-switch report must still state the duplicate-start policy for plain `Start(...)`

Reason:

- the plan approves new durable starts, so start semantics cannot remain implicit
- durable users need deterministic behavior for lost responses and retried start requests

When needed:

- before backend selector and replacement-readiness approval

## Parity Scope

The parity target is the current durable engine surface, not future durable ideas.

Must match now:

- durable builder surface for regular durable workflows
- durable start surface:
  - `Start(...)`
  - duplicate-start policy
  - client-key idempotent start or `StartOrGet` if part of Milestone 0 contract freeze
- definition id/version registration rules
- straight-line execution
- `If`
- `While`
- `Parallel`
- `WhenAll`
- `Wait`
- `WaitLong`
- instance-targeted event raise
- correlation-targeted event raise
- definition-scoped fanout
- out-of-order event buffering
- duplicate-event suppression
- restart-safe resume
- cold-instance eviction and rehydration
- query surface:
  - `Instance(id).GetAsync()`
  - `GetStateAsync<T>()`
  - `GetActiveWaitsAsync()`
  - `All()`
  - `Where(...)`
  - `ListAsync()`
  - `CountAsync()`
- delete and artifact purge
- version mismatch diagnostics
- persisted runtime artifact compatibility for the supported upgrade window
- inbox/history/outbox artifacts
- background and manual outbox dispatch

Explicitly out of scope for this replacement plan unless the durable engine already exposes them:

- saga parity
- timer/timeout decorators beyond current durable tests
- multi-host leasing/partition ownership
- live instance migration from snapshot backend to event-driven backend

## Required Test Reorganization

Before major implementation work, reorganize tests into three layers.

### Layer A: Shared durable product and contract tests

These must run unchanged against both backends:

- `tests/OrcaCore.Tests/Acceptance/MC_AT_001_*` through `MC_AT_019_*`
- durable query and selection behavior from:
  - `DurableSelectionScopeTests.cs`
- durable version/registration behavior from:
  - `DefinitionVersionMismatchTests.cs`
- durable engine behavioral tests from `DurableWorkflowEngineTests.cs` that cover:
  - restart and rehydration
  - resident vs cold wait behavior
  - buffering
  - deduplication
  - correlation ambiguity behavior
  - fanout result behavior
  - state snapshot detachment
  - resumed context behavior
  - commit-failure recovery behavior
  - concurrency-token correctness at the backend boundary
  - delete and purge semantics
- durable advanced acceptance from:
  - `DurableAdvancedAcceptanceTests.cs`

If Milestone 0 freezes client-key idempotent start or `StartOrGet` as part of the current durable contract, the corresponding durable start tests also join Layer A immediately.

### Layer B: Snapshot-backend-specific tests

These remain with the current backend only:

- `StateMapperRoundTripTests.cs`
- snapshot-specific parts of `ExecutionFrameNodePathTests.cs`
- snapshot-store internals in `InMemoryWorkflowStoreTests.cs`
- any test that requires exact `PersistedFrame`, `PersistedExecutionPath`, or snapshot store layout

### Layer C: Event-driven-backend-specific tests

These are added for event-stream correctness:

- append ordering and expected-version enforcement
- atomic commit-boundary failure injection across stream, checkpoint, inbox, outbox, and projection work
- checkpoint plus stream-tail recovery
- compatible checkpoint and event-schema recovery across supported runtime artifact versions
- projection rebuild from stream
- event-log/outbox determinism on replay
- idempotent projection application
- delete-marker or tombstone handling with late-event rejection
- non-memory provider crash/restart replacement suite

## Immediate Prototype Correctness Blockers

The current prototype has correctness gaps that will fail shared contract tests even before full parity work begins.

These are pre-parity blockers.

### Blocker 1: Terminal instances currently accept unmatched events into the buffer

Current issue:

- `PrototypeEventRouting.ClassifyRaiseToInstance(...)` returns `BufferUnmatchedEvent` for non-duplicate events with no matching wait
- it does not reject `Completed` or `Failed` instances first

Required parity behavior:

- terminal instances must reject raised events with explicit diagnostics

Impact:

- shared terminal-state tests will fail
- terminal instances can accumulate invalid buffered events

### Blocker 2: Prototype store has no optimistic concurrency guard

Current issue:

- `InMemoryPrototypeStore.CommitAsync(...)` does not verify expected stream version before commit
- the store accepts overwrite-style writes

Required parity behavior:

- provider commit must enforce expected-version concurrency
- stale writers must fail explicitly

Impact:

- single-process lanes hide the problem locally
- correctness breaks immediately for any multi-lane or cross-host evolution
- parity tests for serialized durable mutation are not trustworthy without this

### Blocker 3: `StartAsync(...)` bypasses the instance command lane for buffered-event consumption

Current issue:

- `StartAsync(...)` commits the initial checkpoint
- then directly calls `TryConsumeBufferedEventsAsync(...)`
- that follow-up consumption does not run through `PrototypeInstanceCommandLane`

Required parity behavior:

- all post-start instance mutation must honor the same serialized command path

Impact:

- a concurrent external event can race the buffered-event consumption path
- the lane does not actually protect all mutations

### Blocker 4: Buffered-event consumption only handles one event per cycle

Current issue:

- `TryConsumeBufferedEventsAsync(...)` consumes only the first matching pending event

Required parity behavior:

- the design must support repeated drain cycles until no more progress is possible
- parallel waits and future multi-branch resumes depend on this

Impact:

- current logic is insufficient once `Parallel` and branch-scoped waits are implemented

## Workstreams

Implementation should run through eight workstreams with hard gates between them.

### Workstream 0: Prototype correctness hardening

Goal:

- remove existing prototype correctness defects that would invalidate parity signals

Tasks:

- add terminal-state rejection before unmatched-event buffering
- add expected-version enforcement to the prototype store
- make post-start buffered-event consumption run through the instance lane
- change buffered-event handling from one-shot consumption to a drain-until-stable model

Deliverables:

- corrected prototype routing/store semantics
- regression tests for the four blockers above

Exit criteria:

- prototype no longer accepts terminal-instance buffering
- stale writes are rejected explicitly
- all instance mutation paths flow through one serialized lane

### Workstream 1: Shared public durable contract and definition-model adoption

Goal:

- one durable programming model
- two backends behind it

Tasks:

- define a backend-agnostic durable runtime interface/factory
- explicitly adopt `WorkflowDefinition<TState>` / `IWorkflowNode` as the event-driven durable definition model
- make the event-driven backend consume the same durable definition model as the current durable engine
- retire the prototype-only requirement to author definitions as a raw `IReadOnlyList<IStep<TState>>`
- keep `WaitLong` durable-only at compile time
- keep instance/selection/query vocabulary identical
- freeze the durable start contract for parity, including duplicate-start behavior and whether client-key idempotent start is in scope now or deferred explicitly

Deliverables:

- shared durable engine contract
- explicit definition-model decision record
- explicit durable start-contract decision record
- shared durable typed runtime wrapper
- shared test harness that can create either backend from the same definition

Exit criteria:

- one test can exercise the same durable definition and the same frozen start contract on both backends without changing authoring code

### Workstream 2: Shared parity test harness

Goal:

- make semantic drift visible immediately

Tasks:

- build a backend matrix fixture for durable tests
- move Layer A tests onto the shared fixture
- tag snapshot-specific tests and remove them from the backend-neutral gate
- keep current durable backend green during the reorganization

Deliverables:

- backend matrix test fixture
- first red/green parity dashboard:
  - current durable backend
  - event-driven backend

Exit criteria:

- shared durable contract suite exists
- current durable backend passes it fully
- event-driven backend failures are visible as a concrete backlog

### Workstream 3: Execution-model redesign

Goal:

- replace the flat prototype execution core with a durable execution model capable of shared control-flow semantics

This is the largest foundational change in the plan.

It is not a small feature task.

Tasks:

- replace the flat `EventDrivenWorkflowDefinition<TState>` execution path with AST-based execution over `IWorkflowNode`
- replace `RegisteredPrototypeDefinition.Create<TState>(...)` flat step iteration with an execution interpreter that can represent:
  - frame stack
  - branch activation
  - loop re-entry
  - parallel branch progression
  - join completion
- replace `PrototypeCheckpointState.NextStepIndex` with serialized execution-position state
- define backend-specific execution-position DTOs and checkpoint schema
- document the frame-stack persistence format before implementation is finalized
- define versioning rules for runtime event schema, checkpoint schema, and projection rebuild inputs before those artifacts become provider-facing durability contracts
- choose the supported compatibility strategy for persisted runtime artifacts:
  - backward-compatible readers
  - explicit upcasters
  - rebuild from retained stream where applicable

Deliverables:

- execution-model design note
- event-driven interpreter over shared workflow AST
- checkpoint schema that supports nested control flow
- runtime artifact compatibility note for supported persisted data

Exit criteria:

- event-driven backend can persist and restore nested execution position without using the old flat step-index model
- supported runtime artifact compatibility rules are documented before provider work continues

### Workstream 4: Regular control-flow parity

Goal:

- make the event-driven backend semantically equivalent for regular durable workflows before advanced durable operations

Tasks:

- implement event-driven execution support for:
  - `If`
  - `While`
  - `Parallel`
  - `WhenAll`
- preserve current branch/wait semantics:
  - branch-scoped waits
  - deterministic join-once behavior
  - ordering invariance
  - correct resumed payload handoff after loop waits

Tests required green at this stage:

- `MC_AT_001` through `MC_AT_018`
  - except the durable-only operational pieces still blocked on later work
- behavioral subsets from `DurableWorkflowEngineTests.cs` that do not require `WaitLong`, delete/purge, or outbox

Exit criteria:

- event-driven backend passes all shared regular-workflow semantics tests

### Workstream 5: Durable residency, recovery, version, and routing parity

Goal:

- make the event-driven backend durable in the same operational sense as the current engine

Tasks:

- implement `WaitLong`
- distinguish resident durable waits from cold durable waits
- add immediate eviction policy for cold waits
- support lazy rehydration on instance access and resume
- introduce a real provider contract centered on one atomic `WorkflowCommit`-style boundary, or an equivalent explicit atomic model, for:
  - event stream
  - checkpoint
  - inbox
  - outbox
  - projections or durable projection-work scheduling
- add failure-injection coverage that proves no partial visible mutation leaks across that commit boundary
- implement authoritative routing-critical active-wait lookup in the same durability boundary as accepted mutations
- enforce definition id/version registration and mismatch behavior
- support checkpoint-plus-tail recovery explicitly, not just full checkpoint replacement
- preserve one logical mutator per instance across hot and cold paths
- define how correlation-targeted routing rebuilds hot state from the authoritative durable index on startup
- implement the supported compatibility path for runtime event and checkpoint artifacts and define how projections rebuild across runtime upgrades

Tests required green at this stage:

- `MC_AT_019`
- `DefinitionVersionMismatchTests.cs`
- cold/hot wait and restart tests from `DurableWorkflowEngineTests.cs`
- `DurableAdvancedAcceptanceTests.cs`
- event-driven artifact-compatibility and commit-boundary tests from Layer C

Exit criteria:

- event-driven backend matches durable restart, rehydration, version-binding, and routing semantics
- parity-critical provider writes are frozen behind one explicit atomic commit boundary
- supported prior persisted runtime artifacts can be rehydrated or rebuilt under the documented compatibility rules

### Workstream 6: Query, management, delete, purge, and fanout parity

Goal:

- operators and tests see the same management surface and behavior

Tasks:

- add instance summary projection fields needed for durable snapshots
- add active-wait projection parity
- add backend-neutral `InstanceScope` and `SelectionScope`
- support:
  - `GetAsync`
  - `GetStateAsync<T>`
  - `GetActiveWaitsAsync`
  - `All`
  - `Where`
  - `ListAsync`
  - `CountAsync`
- enforce query predicate validation parity
- implement definition fanout against the authoritative durable wait/query surface
- preserve mixed-result fanout behavior without aborting later instances after an earlier failure
- implement delete as a hard-delete operator behavior backed by an explicit deleted-state or tombstone model in the append-only backend
- ensure late events against deleted instances reject deterministically and do not recreate deleted state
- implement purge semantics with independent retention cutoffs for:
  - processed inbox artifacts
  - terminal outbox artifacts
  - history
- keep delete and purge behavior semantically distinct in queries, routing, and projection rebuild rules

Tests required green at this stage:

- `MC_AT_010_ManagementQueryTests.cs`
- `DurableSelectionScopeTests.cs`
- delete/purge tests from `DurableWorkflowEngineTests.cs`
- `DraAdvancedUnitTests.cs` portions that are backend-neutral after test split

Exit criteria:

- event-driven backend passes all shared management, fanout, and retention tests
- deleted instances are not resurrected by late delivery or projection rebuild

### Workstream 7: Inbox/outbox/history parity

Goal:

- finish operational durability parity so the event-driven backend can replace the current durable backend for new durable instances

Tasks:

- preserve restart-safe inbox semantics for:
  - processed duplicate suppression
  - buffered duplicate suppression
  - buffered-event promotion to processed
- add event-driven history artifact model sufficient for current operator tests
- add outbox record generation parity for lifecycle transitions
- preserve deterministic outbox identity and replay suppression
- add manual dispatch parity
- add background dispatch parity
- add retry, poison, observer, and delay-strategy behavior

Tests required green at this stage:

- inbox/history/outbox tests from `DurableWorkflowEngineTests.cs`
- `DurableOutboxPumpTests.cs`
- contract tests from:
  - `MessagingContractTests.cs`
  - `OutboxRecordContractTests.cs`
- backend-neutral outbox portions of `InMemoryWorkflowStoreTests.cs` after test split

Exit criteria:

- event-driven backend matches durable publish-after-commit behavior and outbox operational handling

### Workstream 8: Replacement readiness

Goal:

- make backend switching operationally safe for new durable instances

Tasks:

- add backend selector for new durable starts only after the durable start contract is frozen and test-gated
- keep legacy snapshot backend available for existing instances until drained
- run the replacement-readiness suite on at least one SQL-style durable store provider, or an equivalently strong transactional provider, plus one outbox-dispatch integration path
- inject crash, restart, and failure scenarios across start, resume, commit, delete, and outbox-dispatch boundaries
- publish final parity report and open-gap list, including:
  - frozen durable start contract
  - supported persisted runtime artifact compatibility window
  - proven provider and dispatch path

Deliverables:

- backend selector
- provider-proof replacement suite result
- final replacement-readiness report

Exit criteria:

- shared durable contract suite is green on both backends
- at least one SQL-style durable store provider, or equivalently strong transactional provider, plus one outbox-dispatch integration path passes the replacement-readiness suite
- the frozen durable start contract is green on both approved backends
- event-driven backend is approved for new durable instances

## Recommended Implementation Order

The order matters.

Do not start with outbox.

Recommended sequence:

1. prototype correctness hardening
2. shared durable contract plus definition-model adoption
3. shared backend-matrix test harness
4. freeze durable start contract, atomic commit boundary, delete model, and runtime artifact compatibility rules
5. execution-model design note
6. execution-position model implementation
7. `If`
8. `While`
9. `Parallel`
10. `WhenAll`
11. `WaitLong`
12. cold eviction and lazy rehydration
13. atomic provider commit boundary implementation
14. correlation/routing authority implementation
15. version-binding and runtime artifact compatibility implementation
16. query/management parity
17. fanout parity
18. delete/purge parity
19. inbox/history parity cleanup
20. outbox generation and dispatch parity
21. SQL-style provider proof and crash/restart suite
22. backend selector for new durable instances

Reason:

- outbox before control-flow parity hardens side-effect infrastructure around an incomplete execution model
- control-flow parity is blocked on execution-model replacement, not just feature additions
- provider work before the atomic commit boundary is frozen creates avoidable contract drift
- query/delete/purge before cold-wait parity gives a false sense of readiness
- replacement gating without non-memory provider proof is unsafe
- backend switching before shared tests is unsafe

## Concrete Milestones

### Milestone 0: Contract freeze

Deliver:

- shared durable parity checklist
- shared test classification
- explicit architecture decisions for:
   - definition-model adoption
   - execution-position persistence
   - durable commit boundary
   - correlation index ownership
   - projection consistency
   - fanout enumeration
   - durable artifact compatibility
   - delete model
   - durable start contract

Done when:

- every current durable test is labeled:
  - shared contract
  - snapshot-specific
  - future event-driven-specific
- the parity checklist explicitly states duplicate-start policy, delete semantics, atomic commit invariants, and the supported persisted artifact compatibility window

### Milestone 1: Prototype stabilized

Deliver:

- fixes for the current prototype correctness blockers

Done when:

- prototype no longer has the known correctness defects listed in this plan

### Milestone 2: Same definition, two backends

Deliver:

- event-driven backend can run `DurableWorkflowDefinition<TState>`
- frozen durable start contract for parity

Done when:

- a single shared fixture can instantiate both backends from the same definition and exercise the same frozen start contract

### Milestone 3: Execution-model replacement

Deliver:

- frame-stack-based event-driven execution model and checkpoint schema
- runtime artifact compatibility note

Done when:

- the flat `NextStepIndex` model is no longer part of the parity path
- supported runtime artifact compatibility and rebuild rules are documented

### Milestone 4: Regular semantic parity

Deliver:

- control-flow parity for regular durable workflows

Done when:

- shared acceptance matrix for regular workflows passes on event-driven backend

### Milestone 5: Durable restart parity

Deliver:

- `WaitLong`, cold rehydration, version-binding parity
- atomic provider commit contract and runtime artifact compatibility implementation

Done when:

- restart, commit-boundary, and compatible-artifact recovery tests pass on the event-driven backend

### Milestone 6: Management and fanout parity

Deliver:

- query, fanout, delete, and purge parity

Done when:

- operators can inspect and manage event-driven durable instances through the same API

### Milestone 7: Operational parity

Deliver:

- inbox/history/outbox parity

Done when:

- durable operational tests pass on event-driven backend

### Milestone 8: Replacement readiness

Deliver:

- backend selector for new durable instances
- provider-proof replacement suite result
- final parity report

Done when:

- shared durable contract suite is green on both backends
- at least one SQL-style durable store provider, or equivalently strong transactional provider, plus one outbox-dispatch integration path passes the replacement-readiness suite
- the frozen durable start contract is green on both approved backends
- event-driven backend is approved for new durable starts

## Definition of Done

This plan is complete only when:

- event-driven backend passes the shared durable contract suite
- snapshot-specific tests remain green on the legacy backend
- event-driven-specific stream/projection tests are green
- parity-critical durable mutation paths use one documented atomic commit boundary proved by tests
- supported persisted runtime artifacts from prior compatible versions can be rehydrated or rebuilt under the documented rules
- same durable definition authoring surface is used for both backends
- durable start contract is frozen, documented, and green on approved backends
- backend switch does not require user code changes for durable regular workflows
- delete and purge semantics are explicit and deleted instances are not revived by late delivery or rebuild
- at least one SQL-style durable store provider, or equivalently strong transactional provider, plus one outbox-dispatch integration path passes the replacement-readiness crash/restart suite
- no open known semantic differences remain in the parity checklist

## Explicit Non-Goals For First Switch

Do not block the first switch on:

- migrating in-flight snapshot-backed instances into event streams
- saga parity
- multi-host ownership
- timer framework beyond current durable contract expectations

For the first switch, it is enough that:

- new durable instances can start on the event-driven backend
- old durable instances can continue on the snapshot backend until drained or retired

## Recommended Next Coding Step

Start with Milestone 0, Milestone 1, and the definition-model decision together:

1. classify the current durable tests into shared vs backend-specific
2. fix the known prototype correctness blockers
3. explicitly adopt `WorkflowDefinition<TState>` / `IWorkflowNode`
4. freeze the durable start contract, atomic commit boundary, delete model, and runtime artifact compatibility rules
5. introduce a shared durable backend fixture
6. adapt the event-driven backend to consume `DurableWorkflowDefinition<TState>`

That creates the first honest parity signal.

Without that, every later feature addition risks solving the wrong contract.
