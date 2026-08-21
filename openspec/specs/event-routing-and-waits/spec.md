## Purpose

Define wait registration, event delivery, correlation, buffering, and deduplication semantics shared by OrcaCore orchestration flows.
## Requirements
### Requirement: Waits match by declared event identity and correlation
The runtime SHALL register structural and dynamic waits against an explicit payloadless or typed
`WorkflowEventContract` descriptor plus `CorrelationId`. Matching SHALL use exact ordinal contract
name/version and correlation identity. A typed resumed payload SHALL materialize only through the
same compatible descriptor and the fixed workflow value codec; CLR type names, assembly names,
serializer metadata, broker destinations, attributes, and runtime discovery SHALL NOT become event
identity.

#### Scenario: Matching external event arrives
- **WHEN** an accepted event has the expected contract name/version and correlation for a waiting instance
- **THEN** the owning wait is consumed once and the workflow resumes with descriptor-checked access to its detached payload

#### Scenario: A correlation pair would become ambiguous
- **WHEN** a second active wait would use the same `(DefinitionId, event contract, CorrelationId)` as an existing active wait
- **THEN** registration fails with `AmbiguousWaitRegistrationException` before the second wait parks

### Requirement: Out-of-order events can be buffered and later consumed
Durable ingress SHALL persist an accepted direct or correlation event even when no matching wait is
active. Acceptance and wait registration/claim SHALL serialize so either the oldest eligible event
by durable acceptance order is consumed or it remains pending. Timeout, cancellation, host loss,
or a failed consumption commit SHALL NOT silently delete an unmatched accepted event. V1 SHALL
apply no automatic pending-event TTL; unresolved or terminal-target records SHALL remain observable
poison/dead-letter state for an explicit future retention policy. Ephemeral delivery MAY remain
process-local and SHALL make no durable broker-acknowledgement promise.

#### Scenario: Event arrives before the workflow reaches its wait
- **WHEN** a durable ingress host accepts a matching event before wait registration and the workflow host is replaced
- **THEN** the later wait claims that persisted event once without broker redelivery or a hot in-memory instance

#### Scenario: Wait times out before a pending event can be consumed
- **WHEN** timeout wins the serialized wait/event race
- **THEN** the wait's timer and ownership are cleaned up while an independently accepted unmatched event remains durably observable rather than being silently discarded

### Requirement: Duplicate event delivery does not resume twice
Durable event identity SHALL be global `EventId` plus the normalized complete envelope fingerprint.
Identity comparison SHALL precede current route or target-state evaluation. Redelivery of identical
normalized bytes SHALL return `Duplicate` even after the target progresses or terminalizes; reuse of
the same `EventId` with different normalized bytes SHALL return
`Rejected(EventConflict)` and SHALL NOT overwrite prior ownership. Per-target inbox consumption,
including fanout targets, SHALL commit at most one workflow transition.

#### Scenario: Provider redelivers the same event
- **WHEN** the same accepted envelope is delivered repeatedly before and after target progression or host replacement
- **THEN** ingress returns `Duplicate`, the same durable ownership is reused, and no wait resumes more than once

#### Scenario: Event identity is reused with another envelope
- **WHEN** a caller submits an existing `EventId` with changed contract, route, correlation, causation, time, input, or payload bytes
- **THEN** ingress returns `Rejected(EventConflict)` before target-state evaluation and commits no mutation

### Requirement: Event routing supports direct, correlation, and fanout targeting
`WorkflowInboundEvent` SHALL carry exactly one closed self-routing `WorkflowEventRoute`: direct
`InstanceId`; correlation within one `DefinitionId`; committed-snapshot fanout within one
`DefinitionId`; or exact-definition start-or-deliver containing `DefinitionId`,
`DefinitionVersion`, `StartIdempotencyKey`, and fixed-codec workflow input distinct from the event
payload. `IWorkflowEventIngress.AcceptAsync` SHALL return the closed
`Accepted`/`Duplicate`/`Rejected(WorkflowEventAcceptanceRejection)` union. Only `Accepted` and
`Duplicate` SHALL mean OrcaCore durably owns the complete envelope and route intent and an upstream
broker MAY acknowledge. A rejection SHALL commit no envelope ownership or partial fanout target set.

#### Scenario: Caller submits a definition-scoped fanout event
- **WHEN** the provider can atomically snapshot the complete current nonterminal persisted target set across definition versions
- **THEN** acceptance commits that stable set and independently deduplicated per-target deliveries, accepts an empty set, excludes later instances, and reuses the same membership on redelivery

#### Scenario: Caller submits an exact-definition start-or-deliver event
- **WHEN** the route carries exact definition identity/version, start idempotency key, workflow input, and a distinct event payload
- **THEN** acceptance atomically creates or reuses the compatible pending start intent and event or returns `Rejected(StartConflict)` before ownership

#### Scenario: Direct target is absent or terminal
- **WHEN** a new event directly names an absent or terminal instance
- **THEN** ingress returns the exact direct-target rejection without committing ownership, while an identical redelivery of a previously accepted event still returns `Duplicate`

### Requirement: Wait isolation is preserved across loops and branches
The runtime SHALL persist implementation-owned fiber, scope, and wait-sequence coordinates for
waits created inside loop, branch, and item occurrences. Those coordinates SHALL select and resume
the exact owner but SHALL NOT appear in public inbound envelopes, application snapshots, or event
contracts. Repeated authored local names or nested positions SHALL NOT become global event identity.

#### Scenario: Two branches wait on related event shapes
- **WHEN** owner-distinct branch or item occurrences register non-conflicting waits
- **THEN** each accepted event can consume only the exact matching persisted owner and unrelated parents or siblings remain unchanged

#### Scenario: Nested scopes reuse a branch name
- **WHEN** separate scopes contain branches with the same authored local name
- **THEN** internal hierarchical scope/fiber ownership keeps them distinct without exposing or routing by that name

### Requirement: Wait matching resumes the owning fiber
A matched event SHALL transition only the fiber that owns the committed wait from blocked to
runnable. Acceptance, wait registration, pending-event claim, timeout, cancellation, and
consumption SHALL serialize so exactly one winner commits. The runtime SHALL NOT reconstruct or
advance an unrelated parent or sibling position, poll for terminal state, or require broker
redelivery after durable acceptance.

#### Scenario: Event matches one nested branch wait
- **WHEN** one persisted event matches a wait owned by a nested child fiber
- **THEN** only that child becomes runnable and the scheduler retains its persisted ordering and merge rules

#### Scenario: Event and timeout race
- **WHEN** an accepted event and the wait timer contend for the same owner
- **THEN** one committed winner cancels the losing obligation and restart observes the same outcome

### Requirement: Wait cleanup follows scope lifecycle
Cancellation, failure, workflow deadline, forced termination, and normal scope completion SHALL
durably cancel or consume every wait, timer, and pending resume owned by affected descendants before
those descendants are removed. Cleanup SHALL NOT delete an accepted unmatched inbox event merely
because its former candidate wait or target scope terminalized; such owned records follow the
explicit observable poison/dead-letter and retention contract.

#### Scenario: Terminal action removes a branch that owns a wait
- **WHEN** cancellation, failure, deadline, or termination wins while a branch owns an active wait
- **THEN** wait/timer cleanup and scope state commit atomically without leaving an addressable orphan wait or silently dropping independently accepted event ownership

### Requirement: Accepted events reactivate cold durable work
An accepted event matching a persisted wait SHALL commit a continuation independently of process
residency. A definition-owning pump SHALL load the exact bound definition identity, version, and
fingerprint, rehydrate authoritative state, apply the inbox event once, and resume execution.
Callback-only ingress SHALL commit the same envelope, pending start intent when applicable, and
continuation handoff without loading workflow definitions or executing workflow code.

#### Scenario: Event is accepted by a callback-only host
- **WHEN** a callback-only ingress process owns no workflow catalog or engine and accepts an event for later definition-owned work
- **THEN** provider state retains the event and handoff until a compatible definition-owning host materializes or resumes it without upstream redelivery

#### Scenario: Definition is unavailable after acceptance
- **WHEN** a definition-owning pump cannot resolve the exact accepted definition/version/fingerprint
- **THEN** it records observable poison against the already owned event or start intent rather than silently deleting it or changing the broker acknowledgement result
