# Provider Ports For Outbox Implementation Guide

Drafted on April 10, 2026.

**Status (April 2026):** Phases 1–8 of this guide are implemented on the reference durable path (`OrcaCore.Runtime` + `InMemoryWorkflowStore` + tests). The document remains useful as a migration narrative and checklist for **new** SQL/bus providers; treat `src/` as the source of truth when any step below disagrees with the tree (for example `IOutboxDispatcher` and `IDurablePayloadTypeResolver` are removed).

This guide described how to implement the port changes proposed in [provider-ports-for-outbox-design.md](X:/Projects/GitHub/Workflow-orca/docs/architecture/provider-ports-for-outbox-design.md) without breaking the durable runtime incrementally.

It is implementation-oriented:

- concrete phase order
- expected file touchpoints
- temporary compatibility rules
- test expectations

It assumes [event-driven-outbox-design.md](X:/Projects/GitHub/Workflow-orca/docs/architecture/event-driven-outbox-design.md) remains the behavioral source of truth and the provider-ports design defines the provider-facing contract.

## Short Position

Do this as a staged, test-driven migration, not as one rewrite.

The safe order is:

1. add failing tests for the next contract slice
2. add the minimum abstractions or models required to compile those tests
3. implement the production change to make the tests pass
4. refactor while keeping the tests green
5. move to the next slice
6. remove compatibility shims only after the new tests fully cover the old behavior

The in-memory durable store should be the first full reference implementation. Do not start with SQL, Kafka, RabbitMQ, or SQS adapters.

## Delivery Rule

Every contract change in this guide should land in `red -> green -> refactor` order.

That means:

- write or update tests first
- watch them fail for the reason you expect
- implement the minimum production change to make them pass
- refactor only with the tests green

If a change cannot be driven by a focused test, the change is still too large.

## Scope

This guide covers:

- shared transport contracts
- shared payload-envelope serialization contracts
- durable `OutboxRecord` migration
- durable `IWorkflowStore` migration
- durable outbox pump migration
- durable event-router/outbox creation migration
- test coverage needed to lock the semantics

This guide does not cover:

- full inbox design beyond the minimum contract already defined
- child-workflow scheduler implementation
- saga compensation implementation details
- concrete broker/database adapters beyond in-memory

## Key Invariants To Preserve

Every implementation phase must preserve these invariants:

- workflow commit remains atomic for instance state, inbox, outbox, history, and projection work scheduling
- replay of the same accepted mutation produces the same `OutboxId`
- dispatch ordering is per `InstanceId` by `(StreamVersion, Sequence)`
- higher-sequence records are head-of-line blocked by lower non-terminal records
- transport adapters receive a shared dispatch envelope, not the durable persistence record
- payload serialization is explicit and stable

If any phase cannot preserve those invariants, stop and split the phase further.

## Current Code Touchpoints

The primary durable implementation files are:

- [IWorkflowStore.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Persistence/IWorkflowStore.cs)
- [OutboxRecord.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Persistence/OutboxRecord.cs)
- [InMemoryWorkflowStore.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Persistence/InMemoryWorkflowStore.cs)
- [DurableOutboxPump.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Outbox/DurableOutboxPump.cs)
- [DurableEventRouter.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Routing/DurableEventRouter.cs)
- [DurablePayloadTypeRegistry.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Serialization/DurablePayloadTypeRegistry.cs)
- [JsonPayloadEnvelopeSerializer.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Serialization/JsonPayloadEnvelopeSerializer.cs)
- Shared contracts: `src/OrcaCore.Abstractions/Messaging/` and `src/OrcaCore.Abstractions/Serialization/`

New shared abstractions will likely be added under:

- `src/OrcaCore.Abstractions/Messaging/`
- `src/OrcaCore.Abstractions/Serialization/`

Primary test touchpoints should be:

- `tests/OrcaCore.Tests/Durable/`
- [InMemoryWorkflowStoreTests.cs](X:/Projects/GitHub/Workflow-orca/tests/OrcaCore.Tests/Durable/InMemoryWorkflowStoreTests.cs)
- [DurableWorkflowEngineTests.cs](X:/Projects/GitHub/Workflow-orca/tests/OrcaCore.Tests/Durable/DurableWorkflowEngineTests.cs)
- [DurableAdvancedAcceptanceTests.cs](X:/Projects/GitHub/Workflow-orca/tests/OrcaCore.Tests/Durable/DurableAdvancedAcceptanceTests.cs)

## Phase Plan

## Phase 1: Add Shared Abstractions

Goal:

- introduce the cross-engine transport contract and shared serialization contracts without changing runtime behavior yet

Add:

- `DispatchPayload`
- `DispatchMessage`
- `DispatchOutcome`
- `IMessageDispatcher`
- `SerializedPayloadEnvelope`
- `IPayloadEnvelopeSerializer`
- `IPayloadSchemaResolver`

Suggested locations:

- `src/OrcaCore.Abstractions/Messaging/DispatchPayload.cs`
- `src/OrcaCore.Abstractions/Messaging/DispatchMessage.cs`
- `src/OrcaCore.Abstractions/Messaging/DispatchOutcome.cs`
- `src/OrcaCore.Abstractions/Messaging/IMessageDispatcher.cs`
- `src/OrcaCore.Abstractions/Serialization/SerializedPayloadEnvelope.cs`
- `src/OrcaCore.Abstractions/Serialization/IPayloadEnvelopeSerializer.cs`
- `src/OrcaCore.Abstractions/Serialization/IPayloadSchemaResolver.cs`

Rules:

- do not move durable logic into `Abstractions`
- keep these files dependency-light
- `DispatchMessage.Headers` must be non-null and may be empty

Test-first tasks:

- add contract tests for `DispatchMessage`, `DispatchPayload`, and `DispatchOutcome` in a new durable-facing test file such as `tests/OrcaCore.Tests/Durable/MessagingContractTests.cs`
- add tests that assert the shared abstractions can be referenced without depending on `Durable.Persistence`

Temporary compatibility (historical — completed):

- older trees kept `IOutboxDispatcher` and `IDurablePayloadTypeResolver` during the migration; both are removed in favor of `IMessageDispatcher` and `IPayloadSchemaResolver` / `IPayloadEnvelopeSerializer`.

Verification:

- shared abstraction tests exist (`MessagingContractTests`, serializer tests, outbox/store tests)
- abstractions compile and the durable runtime uses them end-to-end

## Phase 2: Uplift Durable Serialization

Goal:

- replace ad hoc JSON-element creation with a serializer that produces `SerializedPayloadEnvelope`

Add in durable runtime:

- default implementation of `IPayloadEnvelopeSerializer`
- adapter implementation for the current registry-based type mapping

Likely files:

- `src/OrcaCore.Runtime/Durable/Serialization/JsonPayloadEnvelopeSerializer.cs`
- update [DurablePayloadTypeRegistry.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Serialization/DurablePayloadTypeRegistry.cs)
- update engine options so durable runtime can resolve `IPayloadEnvelopeSerializer`

Implementation rules:

- keep `DurablePayloadTypeRegistry` as the default schema/type registry
- serializer must produce:
  - payload bytes
  - `ContentType`
  - `SchemaId`
  - `TypeKey`
- serializer must use `declaredType`, not only `value?.GetType()`
- serializer failures must become explicit runtime errors, not silent fallback

Test-first tasks:

- add serialization tests before implementing the serializer
- start with:
  - round-trip of a registered payload type
  - failure for unregistered payload type
  - null payload handling
  - declared-type-vs-runtime-type behavior

Suggested test file:

- `tests/OrcaCore.Tests/Durable/PayloadEnvelopeSerializerTests.cs`

Current code to remove later:

- direct `JsonSerializer.SerializeToElement(...)` calls in [DurableEventRouter.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Routing/DurableEventRouter.cs)

Verification:

- failing serialization tests were added before implementation
- round-trip tests for registered payload types pass
- tests for unregistered types pass
- tests for null payload and declared-type handling pass

## Phase 3: Introduce New Durable Persistence Models

Goal:

- add the new durable types before changing the store interface

Add:

- new `OutboxStatus`
- new `OutboxRecord`
- `ProjectionWorkItem`
- `WorkflowCommit`
- `OutboxLeaseRequest`
- `OutboxDispatchFailure`

Suggested approach:

- replace the old `OutboxRecord` outright if the compile blast radius is manageable
- otherwise add a temporary internal adapter layer while the store and pump are migrated

Implementation rules:

- `OutboxId` must be deterministic composite identity
- `PayloadEnvelope` must be stored, not raw `JsonElement`
- retryable failures return to `Pending`
- no separate `Failed` state
- `GroupId` stays optional but supported for child/fanout observability
- `StreamId` stays explicit even if currently equal to `InstanceId`

Test-first tasks:

- add record-shape and deterministic-identity tests before touching the pump or store
- start with:
  - deterministic `OutboxId` formatting
  - `Sequence` ordering expectations
  - retryable failure semantics returning to `Pending`

Suggested test files:

- `tests/OrcaCore.Tests/Durable/OutboxRecordContractTests.cs`
- or extend `tests/OrcaCore.Tests/Durable/InMemoryWorkflowStoreTests.cs` if the assertions are store-facing

Verification:

- failing contract tests existed first
- compile passes with the new record type referenced but not fully used

## Phase 4: Migrate `IWorkflowStore`

Goal:

- move the store from bag-of-pending-records semantics to atomic commit plus claim-aware leasing

Change [IWorkflowStore.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Persistence/IWorkflowStore.cs):

- replace `CreateAsync(PersistedInstance, ...)` overloads with `CreateAsync(WorkflowCommit, ...)`
- replace `CommitTransitionAsync(...)` overloads with `CommitAsync(WorkflowCommit, ...)`
- replace `GetPendingOutboxAsync`
- replace `MarkOutboxDispatchedAsync`
- replace `RecordOutboxDispatchFailureAsync`

New contract:

- `LeaseDispatchableOutboxAsync`
- `CompleteLeasedOutboxAsync`
- `FailLeasedOutboxAsync`

Compatibility strategy:

- if needed, add an internal adapter around the old interface for one intermediate commit
- do not keep both models long-term; it doubles correctness surface

Important store rules to implement:

- only `Pending` rows are leaseable
- expired leases are reclaimable
- head-of-line blocking is enforced in the store
- lost-lease completion/failure attempts must fail without mutation
- projection work items commit atomically with the rest of the `WorkflowCommit`
- inbox rows must satisfy the minimum dedupe contract from the design doc

Test-first tasks:

- extend [InMemoryWorkflowStoreTests.cs](X:/Projects/GitHub/Workflow-orca/tests/OrcaCore.Tests/Durable/InMemoryWorkflowStoreTests.cs) before changing the store contract
- add tests for:
  - atomic commit of `WorkflowCommit`
  - duplicate outbox suppression by deterministic key
  - leasing order by `(StreamVersion, Sequence)`
  - lost-lease rejection
  - projection work persistence

Verification:

- failing store tests existed first
- contract compiles
- store implementers have one canonical surface, not old and new mixed together

## Phase 5: Migrate `InMemoryWorkflowStore`

Goal:

- make the in-memory provider the first correct reference implementation of the new port contract

Change:

- [InMemoryWorkflowStore.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Persistence/InMemoryWorkflowStore.cs)

Implementation tasks:

- store `WorkflowCommit` atomically under the existing lock
- add projection work storage or projection-work scheduling persistence
- replace the current `_outbox` entry shape with the richer record
- implement leasing:
  - choose eligible records
  - enforce per-instance ordering
  - set `Status = Leased`
  - set `LeaseOwner`
  - set `LeaseExpiresAt`
- implement completion/failure transitions
- enforce lost-lease rejection

Reference behavior to lock:

- easiest valid strategy is still acceptable: lease at most one record per `InstanceId` per call

Verification:

- store tests now pass against the in-memory provider
- deterministic replay suppresses duplicate insertions
- lease recovery after simulated worker loss
- higher-sequence records never dispatch ahead of lower ones

## Phase 6: Migrate Transport Dispatch

Goal:

- durable engine uses `IMessageDispatcher` instead of a durable-record dispatcher (**landed**).

Touches:

- [DurableOutboxPump.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Outbox/DurableOutboxPump.cs)
- `DurableWorkflowEngineOptions` (`MessageDispatcher` / `OutboxDispatcher` init alias)

Implementation tasks (reference path):

- pump requests leased outbox records
- map `OutboxRecord -> DispatchMessage`
- call `IMessageDispatcher`
- interpret `Result<DispatchOutcome>`
- compute retry schedule in the pump
- convert outcome into `OutboxDispatchFailure`
- call claim-aware store methods

Compatibility:

- hosts that still have old `IOutboxDispatcher` implementations should wrap them in a small `IMessageDispatcher` adapter locally; the repository no longer ships a built-in bridge type.

Rules:

- transport adapters never inspect durable retry or lease metadata
- `IdempotencyKey` must be passed explicitly
- poison handling remains runtime-owned

Test-first tasks:

- add pump-focused tests before changing the pump implementation
- suggested file:
  - `tests/OrcaCore.Tests/Durable/DurableOutboxPumpTests.cs`
- start with:
  - successful dispatch marks leased row dispatched
  - retryable failure returns leased row to `Pending`
  - terminal failure poisons row
  - lost-lease completion/failure path is surfaced predictably

Verification:

- failing pump tests existed first
- success path updates leased row to `Dispatched`
- retryable failure returns row to `Pending`
- terminal failure moves row to `Poisoned`
- lost-lease updates fail predictably

## Phase 7: Migrate Durable Event Routing And Outbox Creation

Goal:

- remove ad hoc payload creation and non-deterministic outbox ids from the router

Change:

- [DurableEventRouter.cs](X:/Projects/GitHub/Workflow-orca/src/OrcaCore.Runtime/Durable/Routing/DurableEventRouter.cs)

Implementation tasks:

- update inbox creation to use the serializer port
- update transition outbox creation to use serializer-produced envelopes
- replace `Guid.NewGuid()` outbox ids with deterministic `(InstanceId, StreamVersion, Sequence)` identity
- assign `Sequence` from the ordered list of outgoing records in one commit
- populate `StreamId`, `RootInstanceId`, and other required metadata

Important note:

- the mapping layer is responsible for cancel-before-resume ordering when both records are produced in one commit
- first additional durable message kinds to validate should be:
  - `ChildWorkflowCancelRequested`
  - durable saga compensation messages

Test-first tasks:

- extend [DurableWorkflowEngineTests.cs](X:/Projects/GitHub/Workflow-orca/tests/OrcaCore.Tests/Durable/DurableWorkflowEngineTests.cs) or [DurableAdvancedAcceptanceTests.cs](X:/Projects/GitHub/Workflow-orca/tests/OrcaCore.Tests/Durable/DurableAdvancedAcceptanceTests.cs) first
- add tests for:
  - deterministic outbox identity on replay
  - serializer-produced inbox/outbox envelopes
  - same-commit ordering
  - ability to represent cancel and compensation messages through the unified outbox model

Verification:

- failing routing/engine tests existed first
- same decision replay yields the same outbox ids
- payload envelopes are stable
- message ordering in one commit is deterministic

## Phase 8: Update Engine Options And Public Wiring

Goal:

- make the new abstractions injectable without breaking the public setup story

Likely files:

- `DurableWorkflowEngineOptions`
- engine factory/setup code

Implementation tasks:

- replace payload-type-resolver-only dependency with schema resolver + serializer
- expose `IMessageDispatcher` as `DurableWorkflowEngineOptions.MessageDispatcher` (the `OutboxDispatcher` init-only property remains an optional alias for the same backing field)
- preserve reasonable defaults for in-memory/testing scenarios

Verification:

- engine can still be created with defaults
- public setup remains understandable

## Phase 9: Remove Compatibility Shims

Completed for the in-tree durable runtime:

- `IOutboxDispatcher` removed
- legacy pending/mark/fail-style `IWorkflowStore` outbox APIs removed in favor of leasing + `WorkflowCommit`
- transition outbox creation uses `IPayloadEnvelopeSerializer` rather than ad hoc JSON for the primary status-transition message

Remaining cleanup targets (if any appear over time):

- stray `JsonSerializer` call sites outside the serializer port
- temporary adapters hosts may still keep until their integrations move to `IMessageDispatcher`

Do not remove these until:

- the in-memory provider passes the new test matrix
- the durable engine compiles only against the new ports

## Test Matrix

Minimum test coverage to add before calling the port migration complete:

These tests should be added incrementally before each production slice, not collected at the end.

### Serialization

- registered payload type serializes to stable envelope
- unregistered payload type fails clearly
- deserialize uses `TypeKey` and declared target type correctly
- null payload behavior is explicit

### Store commit

- `CreateAsync` commits instance, inbox, outbox, history, and projection work atomically
- `CommitAsync` does the same
- duplicate outbox insert by deterministic key is suppressed

### Leasing and ordering

- lease only pending and eligible rows
- higher-sequence rows are blocked by lower pending rows
- retryable failed rows block later rows until success or poison
- expired lease can be reclaimed
- lost-lease completion/failure is rejected

### Pump

- maps `OutboxRecord` to `DispatchMessage` correctly
- honors `DispatchOutcome.Retryable`
- computes `OutboxDispatchFailure.Poison` correctly
- invokes poison handler only for terminal poison cases

### Router and outbox creation

- same accepted mutation recreates same `OutboxId`
- `Sequence` is gap-free and 0-based within `(InstanceId, StreamVersion)`
- serializer-produced payload envelope is persisted
- `ChildWorkflowCancelRequested` can be represented without a second outbox model
- compensation messages can be represented by `MessageType` plus serializer envelope

## Recommended Commit Slices

Keep the migration reviewable. A good slice plan is:

1. add abstraction tests, then shared abstractions
2. add serializer tests, then serializer abstraction and default implementation
3. add outbox/store contract tests, then new durable persistence models
4. add store-behavior tests, then migrate `IWorkflowStore` and `InMemoryWorkflowStore`
5. add pump tests, then migrate pump and engine options to shared dispatcher
6. add router/engine tests, then migrate router/inbox/outbox creation to serializer + deterministic ids
7. remove old contracts and cleanup only after all replacement tests are green

If a slice mixes shared abstractions, store migration, and router migration together, it will be harder to review and easier to break.

## Common Failure Modes

Watch for these specific mistakes:

- keeping `Guid.NewGuid()` anywhere in outbox creation
- computing retry backoff inside the store instead of the pump
- letting transport adapters deserialize durable payloads
- allowing the pump to decide eligibility after the store has already returned a bag of rows
- silently treating projection work as "someone else's problem"
- reintroducing durable-only types into the shared transport abstraction
- assuming `StreamId == InstanceId` in store logic

## Exit Criteria

For the **reference** durable implementation in this repository, the bullets below are already satisfied. Third-party `IWorkflowStore` and broker adapters should meet the same bar before claiming parity:

- durable runtime compiles against `WorkflowCommit`, the new `OutboxRecord`, and claim-aware outbox lifecycle methods
- durable runtime uses `IPayloadEnvelopeSerializer` instead of ad hoc JSON-element construction for persisted envelopes
- durable and ephemeral engines can both target `IMessageDispatcher`
- in-memory durable store passes ordering, leasing, retry, poison, and replay-idempotency tests
- legacy outbox/store/dispatcher-only compatibility surfaces are removed from `OrcaCore.Runtime` (hosts may still carry private shims until their integrations catch up)

## Recommendation

Implement this guide in order and keep the in-memory durable provider plus the durable test suite as the correctness harness.

Only after that should you add:

- one production-grade durable store provider
- one real transport adapter

That is enough to validate the port design before expanding the provider matrix.
