# 5. Events, Waits, and Timers Requirements (EV)

Scope: the event model, routing, matching, buffering, deduplication, wait semantics, and
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

## 5.2 Routing (how events reach instances)

### EV-010 Three routing modes
The engine SHALL support three routing entry points; routing intent is a caller decision
expressed by the API entry point, never a flag on the envelope:

1. **Instance-targeted** — deliver directly to a known `InstanceId`.
2. **Correlation-targeted** — engine resolves the instance by `(EventName, CorrelationId)`.
3. **Definition-targeted fanout** — deliver to all instances of one definition; fanout SHALL
   NOT affect other definitions and SHALL NOT exist engine-wide.

### EV-011 Correlation index
The engine SHALL maintain a correlation index over active waits as a multi-map
`(EventName, CorrelationId) → set of InstanceId`. Wait registration always succeeds (no
registration-time uniqueness). The index is updated on wait registration, match, and
cancellation. In durable mode the index SHALL be derivable from durable state (active-wait
projection), never dependent on hot memory for correctness.

### EV-012 Correlation-targeted uniqueness at routing time
Correlation-targeted delivery SHALL require exactly one matching instance:
- zero matches → fail with a clear "no active wait" error;
- more than one match → fail with a clear ambiguity error directing the caller to
  instance-targeted or fanout routing.

### EV-013 Bulk and efficient retrieval
Routing and inspection SHALL NOT require N single-instance calls or optional indexing
infrastructure; efficient lookup of waits and instances is part of the core contract.

## 5.3 Matching (how events match waits within an instance)

### EV-020 Matching rule
Within an instance, an event matches a wait iff `EventName` and `CorrelationId` both match an
`Active` wait record. Additional dimensions (payload predicates) MAY be added later without
breaking this rule.

### EV-021 Wait records
Entering a wait SHALL create a runtime-owned wait record with at least: `WaitId`,
`EventName`, `CorrelationId`, registration timestamp, branch identity when inside parallel
execution, optional timeout, `WaitStatus` (`Active`/`Matched`/`Cancelled`) and `WaitMode`
(`Resident`/`Cold`). Wait records SHALL be inspectable while active.

### EV-022 Payload delivery on resume
When a wait matches, the matched envelope SHALL be available to the next executing step
through the step context (`ResumedEvent`-style accessor). It is set only for the first step
after resume; subsequent steps in the same run see no resumed event. Resume without payload
access is non-conforming.

### EV-023 Exactly-once resume
A matched wait SHALL resume its instance exactly once. Double resume from one event, or one
wait matched by two events, is non-conforming.

## 5.4 Buffering, deduplication, and event safety

### EV-030 Pending-event buffering (mailbox)
Events arriving before their matching wait exists SHALL be buffered in a per-instance
mailbox. Matching SHALL be bidirectional: new events check active waits; new waits check the
mailbox. Correctness MUST NOT depend on arrival order. Mailbox contents are part of runtime
state, bounded to the instance, and cleaned up at terminal states.

### EV-031 Deduplication by EventId
Duplicate delivery of the same `EventId` SHALL NOT cause double resume, double continuation,
or duplicate buffering. Consumed event IDs SHALL be tracked in runtime state; in durable mode
dedup SHALL survive restart (inbox, DU-030).

### EV-032 Transactional event consumption (no event loss)
An event is consumed only when (1) it matched a wait AND (2) the resulting transition is
committed. Until both hold, the event SHALL remain available. Removal from the
mailbox/inbox happens after state commit, never before. A crash between match and commit
SHALL leave the wait `Active` and the event re-matchable. External event sources SHALL NOT be
acknowledged before the consuming transition is durably committed (durable mode).

## 5.5 Wait semantics

### EV-040 `Wait` (resident)
`Wait` SHALL suspend the instance (`Waiting` status) with a `Resident` wait record. In
ephemeral mode it is activation-local; in durable mode it is fully durable but the instance
may remain hot. Resume re-enters through the serialized mutation path.

### EV-041 `WaitLong` (cold, durable-only)
`WaitLong` SHALL exist only on durable authoring surfaces. It registers a `Cold` durable
wait; after the registration commits, the instance is immediately evictable. Resume
rehydrates. Attempted use in ephemeral mode SHALL be prevented by API absence and rejected
clearly on any forced path.

### EV-042 Wake-up semantics are explicit
The product documentation SHALL state exactly what a durable wait/timer does and does not
guarantee (durable subscription with next-eligible wake-up; missed intermediate occurrences
are not individually replayed) so users never assume missed-signal replay that does not
exist.

### EV-043 Waits in loops are iteration-scoped
Each loop iteration SHALL create fresh wait records. An event correlated to a previous
iteration's wait MUST NOT resume a later iteration.

### EV-044 Wait cancellation
Wait records SHALL be cancellable (by losing-branch policy, timeout policy, operator
cancellation, or terminal transitions), moving to `Cancelled` and leaving the correlation
index. Unresolved waits at completion follow CR-032.

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

### EV-051 Timer/event races are deterministic
When an instance waits on an event and a timer simultaneously (timeout pattern,
`WhenFirst`), exactly one winner SHALL be chosen per documented policy, and the loser SHALL
be cancelled or ignored per explicit policy. Terminal behavior is deterministic and
observable.

### EV-052 Timeout policies compose with timers
Step/scope timeout policies SHALL be built on the timer primitive with configurable outcomes:
retry, fail instance, cancel instance, cancel branch and continue, invoke
compensation/recovery, or mark timed-out awaiting operator action. The outcome SHALL be
deterministic and reflected in lifecycle events.

## 5.7 Publication (outbound events)

### EV-060 Runtime-owned publication
Workflow definitions MAY publish events (`Publish`). Business steps request publication
intent; the runtime owns the effect. In durable mode publication flows through the outbox
(DU-031) so it is consistent with committed state. In ephemeral mode delivery is best-effort
in-process and documented as such.
