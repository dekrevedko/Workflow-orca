# 5. Events, Waits, and Timers Requirements (EV)

Scope: the event model, routing, active-wait matching, accepted-event deduplication, wait semantics, and
time-based waiting. These are core runtime semantics in **both** modes; durable mode adds
persistence guarantees (see document 06).

## 5.1 Event model

### EV-001 Canonical event envelope
Inbound events SHALL be normalized to a canonical envelope carrying at least: `EventId`
(globally unique dedup identity), `EventName`, `CorrelationId`, payload, and an occurrence
timestamp. Provider- or transport-specific shapes SHALL be adapted to the envelope at the
boundary.

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

### EV-010 Two first-release routing modes
The engine SHALL support two routing entry points; routing intent is a caller decision
expressed by the API entry point, never a flag on the envelope:

1. **Instance-targeted** — `DeliverToInstanceAsync` delivers to a known `InstanceId`.
2. **Correlation-targeted** — `DeliverByCorrelationAsync` resolves the unique active wait by
   `(DefinitionId, EventName, CorrelationId)`.

Definition-targeted fanout is explicitly deferred. It has no v1 route, alias, command, or
placeholder; a future amendment must define a committed target snapshot and per-target dedup.

### EV-011 Correlation index
The engine SHALL maintain a unique correlation index over active waits keyed by
`(DefinitionId, EventName, CorrelationId)`. Registration of a second active wait for that key,
including a second same-pair wait within one instance, SHALL fail deterministically with
`AmbiguousWaitRegistrationException` before parking or changing the index. The index is updated
on wait registration, match, and cancellation. In durable mode it SHALL be derivable from
durable state, never dependent on hot memory for correctness.

### EV-012 Typed delivery outcomes
`IWorkflowEventClient` SHALL return `EventDeliveryResult` with one of `Accepted`, `Duplicate`,
`NoActiveWait`, `InstanceTerminal`, or `EventConflict`. Zero correlation matches and a live
instance target without a matching wait return `NoActiveWait`; ambiguity is prevented at
registration by EV-011. Same target `EventId` plus the same normalized envelope is `Duplicate`;
the same ID with different content is `EventConflict`. Invalid/default arguments remain
exceptions. Delivery results do not expose internal command, wait-occurrence, fiber, scope,
checkpoint, or provider-generation identities.

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

### EV-030 Pre-wait delivery is non-consuming
Instance-targeted and correlation-targeted delivery require a matching active wait. A live
instance without that wait and a correlation key with no active match SHALL return
`NoActiveWait`, SHALL NOT buffer the envelope, and SHALL NOT write accepted-delivery or dedup
state. After the intended wait is observed, the caller SHALL redeliver the same `EventId` and
identical normalized envelope. That redelivery is a first acceptance rather than `Duplicate`.
V1 has no pending-event mailbox, pre-wait buffering guarantee, or arrival-order-independent
consumption claim.

### EV-031 Deduplication by EventId
After a target has accepted an event, duplicate delivery of the same `EventId` to that target
SHALL NOT cause double resume or continuation. The normalized envelope fingerprint is retained:
identical redelivery returns `Duplicate`, while changed content under the same ID returns
`EventConflict`. `NoActiveWait` is non-consuming and creates no dedup record, so same-ID
redelivery after wait registration remains eligible for first acceptance. Accepted/consumed
event IDs SHALL be tracked in runtime state; in durable mode dedup SHALL survive restart.

### EV-032 Transactional event consumption (no event loss)
An event is consumed only when (1) it matched a wait AND (2) the resulting transition is
committed. Once accepted, its durable inbox/dedup record SHALL remain available until that
transition commits. A crash between match and commit SHALL leave the wait `Active` and the
accepted event re-matchable. `NoActiveWait` creates no inbox/dedup record and therefore requires
source redelivery after the wait exists. External event sources SHALL NOT acknowledge an
accepted event before the consuming transition is durably committed (durable mode).

## 5.5 Wait semantics

### EV-040 One public `Wait`
`Wait` SHALL suspend the requesting fiber with one `Active` wait record. In ephemeral mode the
record is activation-local. In durable mode the registration and complete execution position
commit before suspension; the activation is then cold-evictable and a wake-up rehydrates it as
needed. A host MAY keep the activation resident as an optimization, but residency does not
change workflow meaning and is not author-configurable. `WaitLong` is removed with no alias,
tombstone, or forced-path support.

### EV-042 Wake-up semantics are explicit
The product documentation SHALL state exactly what a durable wait/timer does and does not
guarantee (durable subscription with next-eligible wake-up; missed intermediate occurrences
are not buffered or replayed) so users never assume a mailbox that does not exist. An event sent
before the wait returns non-consuming `NoActiveWait`; the source observes wait registration and
redelivers the same `EventId`/normalized envelope.

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

## 5.7 Deferred publication

### EV-060 Workflow-authored publication is deferred
`Publish` has no first-release builder member, `StepResult` variant, alias, obsolete tombstone,
or positive fixture. Runtime-owned continuation, lifecycle/status, timer, and internal DAG
records may still use the durable outbox. A future workflow-publication amendment must define
typed payload/destination, stable event identity, commit boundary, dispatch, and dedup semantics
before adding any authoring surface.
