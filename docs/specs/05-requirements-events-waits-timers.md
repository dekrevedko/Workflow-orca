# 5. Events, Waits, and Timers Requirements (EV)

Scope: the event model, routing, active-wait matching, accepted-event deduplication, wait semantics, and
time-based waiting. These are core runtime semantics in **both** modes; durable mode adds
persistence guarantees (see document 06).

## 5.1 Event model

### EV-001 Canonical event envelope
A durable inbound event SHALL be represented by `WorkflowInboundEvent`: one contract name/version,
a globally unique caller-created `EventId`, required `CorrelationId`, optional causation `EventId`,
non-default UTC occurrence time, fixed-codec payload, and one closed `WorkflowEventRoute`. The route
SHALL be direct instance, correlation, definition fanout, or start-or-deliver. Application code
supplies the transport-independent envelope but never provider record identities, wait/fiber/scope
identities, or continuation details.

### EV-002 CorrelationId as request-reply identity
`CorrelationId` SHALL be treated as the identity linking a wait to its response across the
full cycle: outbound request carries it, the external system echoes it, the response event
matches on it. APIs and docs SHALL use the name `CorrelationId` consistently.

### EV-003 EventName is one exact matching contract
Authoring, `StepResult.WaitForEvent`, event envelopes, application routing operations,
correlation indexes, and active-wait projections SHALL use `EventName`, not interchangeable
raw strings. `EventName.Create(string)` construction and matching use the glossary's exact ordinal,
case-sensitive, no-trimming semantics. Providers SHALL certify equivalent lookup behavior
regardless of database collation. No implicit string conversion or parallel raw-string
overload is permitted.

## 5.2 Routing (how events reach instances)

### EV-010 Four self-routing durable routes
`IWorkflowEventIngress` SHALL accept exactly four route variants:

1. **Direct** — one `InstanceId`;
2. **Correlation** — one `(DefinitionId, EventName, CorrelationId)` wait route;
3. **Definition fanout** — the complete current persisted nonterminal target set for one
   `DefinitionId`, snapshotted atomically on first acceptance and reused on redelivery;
4. **Start-or-deliver** — exact `DefinitionId`, `DefinitionVersion`, `StartIdempotencyKey`, and
   fixed-codec workflow input distinct from the event payload.

An empty fanout snapshot is valid. Instances created later are excluded. Start-or-deliver SHALL
atomically reserve or reuse a compatible pending start intent and reject incompatible bindings
before any inbox ownership is created.

### EV-011 Correlation index
The engine SHALL maintain a unique correlation index over active waits keyed by
`(DefinitionId, EventName, CorrelationId)`. Registration of a second active wait for that key,
including a second same-pair wait within one instance, SHALL fail deterministically with
`AmbiguousWaitRegistrationException` before parking or changing the index. The index is updated
on wait registration, match, and cancellation. In durable mode it SHALL be derivable from
durable state, never dependent on hot memory for correctness.

### EV-012 Typed acceptance outcomes
`IWorkflowEventIngress.AcceptAsync` SHALL return `WorkflowEventAcceptanceResult` with one of
`Accepted`, `Duplicate`, or `Rejected(WorkflowEventAcceptanceRejection)`. Rejection is closed to
`EventConflict`, `DirectInstanceNotFound`, `DirectInstanceTerminal`, `StartConflict`, and
`FanoutLimitExceeded`. Invalid/default arguments remain exceptions. Acceptance results expose no
provider record, wait occurrence, fiber, scope, or continuation identity. Only `Accepted` and
`Duplicate` mean OrcaCore owns the event and allow an external source to acknowledge it.

### EV-013 Runtime keyed lookup; public bulk retrieval is deferred
Correlation routing SHALL use the runtime-owned key `(DefinitionId, EventName, CorrelationId)`
and SHALL NOT scan application-visible instance collections. This is an internal routing/index
requirement, not a public instance-enumeration or bulk-query promise. Public list/filter/count,
multi-ID retrieval, and bulk management remain deferred with no v1 member or placeholder.

## 5.3 Matching (how events match waits within an instance)

### EV-020 Matching rule
Within an instance, an event matches a wait iff `EventName` and `CorrelationId` both match an
`Active` wait record. `EventName` equality is exact ordinal and `CorrelationId` follows its
own value-type equality; no participant trims, case-folds, or culture-normalizes either value.
Additional dimensions (payload predicates) MAY be added later without breaking this rule.

### EV-021 Wait records
Entering a wait SHALL create a runtime-owned wait record with at least: `WaitId`,
`EventName`, `CorrelationId`, registration timestamp, branch identity when inside parallel
execution, optional timeout deadline, and `WaitStatus`
(`Active`/`Matched`/`Cancelled`). Durable residency is not authored or recorded as workflow
meaning. Wait records SHALL be inspectable while active.

### EV-022 Payload delivery on resume
When a wait matches, the matched envelope SHALL be available to the next executing step
through the step context (`ResumedEvent`-style accessor). It is set only for the first step
after resume; subsequent steps in the same run see no resumed event. Resume without payload
access is non-conforming.

### EV-023 Exactly-once resume
A matched wait SHALL resume its instance exactly once. Double resume from one event, or one
wait matched by two events, is non-conforming.

## 5.4 Delivery acceptance, deduplication, and event safety

### EV-030 Durable pre-wait acceptance is retained
Durable ingress SHALL persist an accepted event even when no matching wait is active. The retained
record SHALL remain claimable by a later matching wait without broker redelivery or a hot workflow
instance, and SHALL have no automatic TTL. An unresolved retained record SHALL remain observable
and progress through bounded failure handling to poison/dead-letter state rather than disappear.
Ephemeral waits are process-local and make no durable acceptance or acknowledgement promise.

### EV-031 Global identity before routing, per-target fanout ownership
The durable store SHALL bind each `EventId` to the full normalized inbound-envelope fingerprint
before evaluating route or target state. Identical redelivery returns `Duplicate`; changed content
returns `Rejected(EventConflict)`, including after target progression. Definition fanout SHALL
create stable per-target inbox ownership under the one retained membership snapshot, and each target
SHALL deduplicate and progress independently.

### EV-032 Transactional ownership and consumption (no event loss)
Acceptance SHALL atomically establish durable inbox/start-intent ownership before returning
`Accepted`. Matching and the resulting workflow transition commit atomically with inbox progression.
A crash before the transition leaves the accepted record re-matchable; a crash after commit cannot
double-apply it. External sources may acknowledge after `Accepted` or `Duplicate`, not after the
later consuming transition. Permanent inability to resolve or apply remains observable poison.

## 5.5 Wait semantics

### EV-040 One public `Wait`
`Wait` SHALL suspend the requesting fiber with one `Active` wait record. In ephemeral mode the
record is activation-local. In durable mode the registration and complete execution position
commit before suspension; the activation is then cold-evictable and a wake-up rehydrates it as
needed. A host MAY keep the activation resident as an optimization, but residency does not
change workflow meaning and is not author-configurable. `WaitLong` is removed with no alias,
tombstone, or forced-path support.

### EV-042 Wake-up semantics are explicit
Documentation SHALL distinguish a durable wait from ingress ownership. A durable wait records a
subscription and next-eligible wake-up; durable ingress separately retains accepted pre-wait events
until a matching wait claims them. This is not a promise to replay arbitrary historical occurrences
or broker traffic. Ephemeral waits remain process-local.

### EV-043 Repeated pairs have signal-stream semantics
Each loop iteration creates a fresh `WaitId`, but `(EventName, CorrelationId)` identifies a
signal stream rather than an iteration occurrence. After one event consumes the active wait, a
later iteration MAY register the same pair and a later-arriving event MAY satisfy it. OrcaCore
does not claim that an envelope without a wait-occurrence identity can distinguish iterations;
authors requiring occurrence-specific matching SHALL encode that occurrence in `CorrelationId`.
At no time may two active same-key registrations coexist (EV-011).

### EV-044 Wait cancellation
Wait records SHALL be cancellable by their own timeout, instance cancellation request,
workflow deadline, termination, or another owning-scope terminal transition, moving to
`Cancelled` and leaving the correlation index. V1 has no author-visible losing-branch race.
Unresolved waits at completion follow CR-032.

### EV-045 Static and dynamic wait authoring
Structural `Wait(EventName, ...)` SHALL be the preferred authoring form when the event name is
known while the definition is built because it permits structural validation and precise
authored-location diagnostics. Portable `StepResult.WaitForEvent(EventName, ...)` remains available in both
engines only for an event name or correlation selected after business-step execution. Both
forms lower to the same exactly owned wait obligation and durable cold-capable residency.

## 5.6 Timers and time-based waiting

### EV-050 Timer primitive distinct from event wait
Time-based waiting (`Delay`/`Timer`) SHALL be a first-class primitive distinct from
event-based `Wait`, composing with the same suspension model. In durable mode timers SHALL
be durable: modeled as a scheduled fact plus a wake-up command (`TimerScheduled` →
`FireTimer`) via the timer-scheduler provider port, surviving restarts and scale-down. Hidden
thread timers are non-conforming in durable mode.

In ephemeral mode timers are **transient**: in-process, activation-local, and lost on
process exit, with no durability claim. They exist because ephemeral scenarios still need
short timeout patterns (request-reply with a 30 s timeout, step timeout policies per
EV-052/MG-041); this mirrors the Orleans timer/reminder split adopted from the research —
transient timers in both modes, durable wake-ups durable-only.

`Delay` duration, structural wait timeout, `WithStepTimeout`, and `CompleteWithin` SHALL be
positive. A zero `Delay` is rejected rather than acting as author-controlled yield. Retry
`fixedDelay` is the only time value that may be zero; no timeout or duration may be negative.

### EV-051 Timer/event races are deterministic
When an authored wait timeout or another runtime policy arms an event and timer obligation
simultaneously, exactly one winner SHALL commit and the loser SHALL be cancelled. If a
structural `Wait(..., timeout)` timer wins, the current root/branch/item fails with
`WorkflowWaitTimeoutException` and its event obligation is cancelled before progression. An
enclosing `WhenAllOutcomes` observes that branch/item failure as data. There is no timeout
callback builder. This internal race does not expose `WhenFirst` in v1.

### EV-052 Workflow and per-attempt timeouts compose with timers
`WithStepTimeout` SHALL use the timer primitive to bound one business-step attempt. Its winner
fences/discards the attempt-local state and reports `StepAttemptTimeoutException`; a remaining
retry receives a new deadline and `AttemptNumber` while retaining `StepOperationId`.
`CompleteWithin` SHALL persist one absolute deadline measured from instance start and include
admission, fixed retry delay, delays, waits, lease queueing, every `ContinueAsNew` generation,
and cleanup/quarantine decisions. Its winner commits terminal `TimedOut` with
`WorkflowDeadlineExceededException`, stops admission, signals attempt tokens, cancels
runtime-owned obligations, and suppresses joins/merges. Neither timeout waits forever for a
token-ignoring body or proves protected external work stopped (MG-064).

## 5.7 Durable workflow-authored publication

### EV-060 Publish uses the transactional outbox
Durable sequential authoring SHALL expose `Publish` only at the approved durable placements. The
author supplies the event contract/payload and optional selectors; runtime owns event, causation,
correlation, origin, and time identities. The fixed-codec payload and schema identity SHALL commit
atomically with workflow state as a `workflow-event` outbox record.

`IWorkflowEventDispatcher.DispatchAsync(WorkflowOutboundEvent, CancellationToken)` SHALL receive
only public workflow events and return the closed success/retryable/permanent result union. Internal
continuations and provider `OutboxWrite` records SHALL never cross that boundary. Retry SHALL reuse
the committed event identity, cancellation SHALL release the claim, and permanent failure SHALL
record stable poison code/detail.
