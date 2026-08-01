## ADDED Requirements

### Requirement: Events are not buffered before wait registration
V1 SHALL NOT buffer an application event that arrives before its matching wait is registered. Delivery to a live instance with no matching active wait SHALL return `NoActiveWait` and SHALL NOT consume the `EventId`, so the caller MAY redeliver the same identity later. The closed public delivery result SHALL remain `Accepted`, `Duplicate`, `NoActiveWait`, `InstanceTerminal`, or `EventConflict`; no pending, buffered, or deferred-match result SHALL exist.

#### Scenario: Event arrives before the workflow reaches its wait
- **WHEN** a correlated event reaches a live instance before the corresponding wait is registered
- **THEN** delivery returns `NoActiveWait`, no state transition commits, and the unconsumed `EventId` remains redeliverable

#### Scenario: Caller redelivers after the wait exists
- **WHEN** the same `EventId` is delivered again after the matching wait has registered
- **THEN** it is accepted normally because the earlier `NoActiveWait` result consumed no event identity

### Requirement: Event routing is instance-targeted or correlation-targeted
`IWorkflowEventClient` SHALL expose exactly two route names and four overloads: payloadless and generic `WorkflowEvent<TPayload>` forms of `DeliverToInstanceAsync(InstanceId, event)` and `DeliverByCorrelationAsync(DefinitionId, event)`. Correlation routing SHALL resolve exactly one active wait by `(DefinitionId, EventName, CorrelationId)`. Definition-targeted fanout, broadcast delivery, and an ambiguous-match delivery result SHALL be absent from v1 public assemblies and SHALL remain recorded in the future-capability registry.

#### Scenario: Caller raises an event without an instance id
- **WHEN** the engine receives a correlation-targeted request
- **THEN** routing resolves the single active wait for that definition/event/correlation triple or returns the closed no-match result

#### Scenario: Caller looks for definition-scoped fanout
- **WHEN** a caller looks for a route that delivers one event to every instance in a definition scope
- **THEN** no such member exists in v1 and the capability remains deferred with recorded re-entry criteria

### Requirement: Ambiguous wait pairs are rejected at registration
Registering a second active wait for an already-active `(DefinitionId, EventName, CorrelationId)` triple, including within one instance, SHALL fail with `AmbiguousWaitRegistrationException` before the fiber parks. Delivery SHALL therefore never choose among competing identical waits, and v1 SHALL NOT define a match-time tie breaker. Occurrence-specific matching SHALL belong in `CorrelationId`. A triple released by a consumed or cancelled wait MAY be registered again by a later loop or item occurrence, because the pair carries signal-stream rather than buffered-queue semantics.

#### Scenario: Correlation pair would become ambiguous
- **WHEN** a workflow attempts to register an active wait whose definition/event/correlation triple is already active
- **THEN** registration fails deterministically before parking and no wait is committed

#### Scenario: Correlation pair is reused later
- **WHEN** one wait consumes an event and a later loop or item occurrence registers the same event/correlation pair
- **THEN** registration succeeds and a later event may satisfy the new wait

## MODIFIED Requirements

### Requirement: Waits match by declared event identity and correlation
The runtime SHALL register waits against event name and correlation metadata and SHALL resume only when an incoming event matches the intended wait contract. Matching SHALL be exact ordinal and case-sensitive on `EventName` and `CorrelationId`.

#### Scenario: Matching external event arrives
- **WHEN** an event with the expected name and correlation reaches a waiting instance
- **THEN** the intended wait is matched and the workflow resumes with access to the event payload

#### Scenario: Case-differing event identity arrives
- **WHEN** a delivered `EventName` or `CorrelationId` differs from the registered wait only by case or culture-sensitive comparison
- **THEN** it does not match that wait

### Requirement: Duplicate event delivery does not resume twice
Every accepted application event SHALL atomically deduplicate its fixed-codec normalized envelope per target instance so a matched wait resumes at most once. Redelivery of a consumed `EventId` with identical normalized bytes SHALL return `Duplicate`; redelivery with different normalized bytes SHALL return `EventConflict`. Neither SHALL satisfy another wait occurrence.

#### Scenario: Provider redelivers the same event
- **WHEN** the same accepted `EventId` is delivered again with identical normalized envelope bytes
- **THEN** `Duplicate` is returned and no second state transition commits

#### Scenario: Same identity carries different bytes
- **WHEN** a delivered `EventId` matches a consumed identity but its normalized envelope bytes differ
- **THEN** `EventConflict` is returned and no wait occurrence is satisfied

### Requirement: Wait isolation is preserved across loops and branches
The runtime SHALL persist explicit `FiberId`, `ScopeId`, and per-instance `WaitSequence` ownership for waits created inside loops, branch scopes, and item scopes. Event identity and correlation determine the matching candidate; owner identity determines the exact fiber resumed. `WaitSequence` SHALL be a deterministic persisted registration ordinal used for ordering, recovery, and audit, and SHALL NOT act as a match-time selector. Repeated local branch names or nested positions SHALL NOT be treated as global identity. These identities SHALL remain runtime-protocol and provider concerns and SHALL NOT appear on ordinary application snapshots.

#### Scenario: Two branches wait on related event shapes
- **WHEN** root fan-out branches or loop iterations register separate waits
- **THEN** each wait only consumes the event intended for its exact fiber and scope owner

#### Scenario: Nested scopes reuse a branch name
- **WHEN** separate scopes contain branches with the same authored local name
- **THEN** scope and fiber identity keeps their waits distinct without relying on the branch name as global identity

#### Scenario: Application inspects active waits
- **WHEN** an application reads `WorkflowInstanceSnapshot.ActiveWaits`
- **THEN** it observes opaque `WaitId`, immutable `AuthoredLocation`, `EventName`, `CorrelationId`, registration time, and optional deadline without `FiberId`, `ScopeId`, or wait sequence

### Requirement: Wait matching resumes the owning fiber
A matched event SHALL transition only the fiber that owns the selected wait from blocked to runnable. It SHALL NOT reconstruct or advance unrelated parent or sibling positions.

#### Scenario: Event matches one branch wait
- **WHEN** an event matches a wait owned by one branch or item fiber
- **THEN** only that fiber becomes runnable and the scheduler retains its persisted ordering rules

### Requirement: Wait cleanup follows scope lifecycle
Cancellation, failure, workflow deadline expiry, forced termination, and scope abandonment SHALL durably cancel or consume every wait, timer, and pending resume owned by the affected scope descendants before those descendants are removed. A suppressed merge SHALL NOT leave an addressable orphan wait.

#### Scenario: Failing branch owns a wait
- **WHEN** a root fan-out branch fails or is cancelled while a sibling branch owns an active wait
- **THEN** wait-cancellation facts and updated scope state commit without leaving an addressable orphan wait

#### Scenario: Workflow deadline expires with active waits
- **WHEN** `CompleteWithin` wins while waits remain registered
- **THEN** the terminalizing commit cancels every owned wait and timer obligation before the instance becomes `TimedOut`

## REMOVED Requirements

### Requirement: Out-of-order events can be buffered and later consumed
**Reason**: The approved v1 delivery contract is a non-buffering signal stream. Its closed public result set admits no buffered, pending, or deferred-match value, and an event with no matching active wait returns `NoActiveWait` without consuming the `EventId`. Replaced by "Events are not buffered before wait registration".

**Migration**: No released consumer migration exists. Callers that relied on pre-registration buffering redeliver the same `EventId` after the wait exists; the earlier `NoActiveWait` result consumed no event identity, so redelivery is accepted normally.

### Requirement: Event routing supports direct, correlation, and fanout targeting
**Reason**: Definition-targeted fanout is deferred beyond v1. It has no route, alias, command, or placeholder, because a future amendment must first define a committed target snapshot and per-target deduplication. Replaced by "Event routing is instance-targeted or correlation-targeted".

**Migration**: No released consumer migration exists. Deliver by instance or by correlation. Applications needing definition-wide delivery own that enumeration until the deferred capability is specified.
