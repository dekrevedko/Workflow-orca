# Events, Waits & Timers — Negative Tests & Edge Cases

Scope: event envelope, routing, mailbox, deduplication, wait matching, timers, step timeouts.
Requirements: EV-*, AC-1xx.

## Existing negative coverage (reference)

| Area | Test(s) | What is proven |
|------|---------|----------------|
| No active wait | `RoutingTests`, `WaitAcceptanceTests` (AC-103) | Correlation routing throws "no active wait" |
| Ambiguous correlation | `RoutingTests`, `RoutingAcceptanceTests` (AC-107) | Multiple instances → ambiguity error |
| Non-matching event | `WaitAcceptanceTests` (AC-103) | Wrong name/correlation leaves waiting |
| Duplicate EventId | `MailboxAcceptanceTests` (AC-105) | Second delivery deduped |
| Timer/event race | `TimerEventRaceTests`, `TimerAcceptanceTests` (AC-112) | One winner per policy |
| Parallel wrong branch | `ParallelTests.RaiseEventAsync_RemainingBranchWait_RejectsWrongBranchId` | Wrong BranchId rejected |
| Ambiguous parallel same correlation | `ParallelTests.RejectsAmbiguousDelivery` | Instance-targeted without BranchId throws |

---

## Missed negative tests

### NEG-EV-001 — Deliver to unknown InstanceId
- **Priority:** P1 | **AC:** EV-010 | **Status:** Missing
- **Given** random instance id
- **When** `RaiseEventAsync(instanceId, envelope)`
- **Then** not-found / routing error; no silent no-op

### NEG-EV-002 — Event to terminal instance
- **Priority:** P1 | **AC:** AC-005 | **Status:** Missing
- **Given** `Completed` instance
- **When** `RaiseEventAsync` with matching wait correlation (hypothetical stale wait)
- **Then** rejected; inbox not mutated

### NEG-EV-003 — Empty EventName / invalid envelope
- **Priority:** P1 | **AC:** EV-001 | **Status:** Missing
- **Given** envelope with empty name or default `EventId`
- **When** raise
- **Then** validation error before matching

### NEG-EV-004 — Correlation routing on completed instance only in registry
- **Priority:** P2 | **Status:** Missing
- **Given** instance evicted from memory but durable record exists
- **When** correlation raise (ephemeral)
- **Then** no match (ephemeral) vs rehydrate (durable) — mode-specific

### NEG-EV-005 — Fanout to wrong definition
- **Priority:** P1 | **AC:** AC-108 | **Status:** Partial
- **Given** two definitions waiting on same event name
- **When** `RaiseEventByDefinitionAsync` targets definition A only
- **Then** B unchanged — negative: event must not appear in B's mailbox

### NEG-EV-006 — Duplicate delivery after wait consumed
- **Priority:** P0 | **AC:** EV-023 | **Status:** Partial
- **Given** wait already consumed for `(EventName, CorrelationId[, BranchId])`
- **When** same correlation delivered again (new EventId)
- **Then** no second resume; event discarded or poisoned per policy

### NEG-EV-007 — Timeout path without consumed-wait record (R3 bug)
- **Priority:** P0 | **AC:** EV-043 | **Status:** Missing
- **Given** wait loses timer/event race (timeout wins)
- **When** buffered event same correlation arrives before next wait
- **Then** must not resume next wait spuriously — **regression test for R3 P1**

### NEG-EV-008 — WaitLong on forced ephemeral path
- **Priority:** P1 | **AC:** AC-303 | **Status:** Missing
- **Given** ephemeral engine
- **When** internal/command path attempts cold wait
- **Then** rejection

### NEG-EV-009 — Timer cancel on non-existent timer
- **Priority:** P2 | **AC:** EV-050 | **Status:** Missing
- **Given** instance with no active timer
- **When** cancel timer command
- **Then** no-op or clear error; no state corruption

### NEG-EV-010 — Fire timer for purged instance
- **Priority:** P0 | **AC:** AC-314 | **Status:** Missing (R5)
- **Given** instance purged; timer row orphaned
- **When** `ClaimDueAsync` / fire
- **Then** no command or safe no-op; no resurrection

### NEG-EV-011 — Step timeout on already-completing step
- **Priority:** P1 | **AC:** AC-113 | **Status:** Missing
- **Given** step about to return success
- **When** timeout fires same tick
- **Then** deterministic single outcome per policy

### NEG-EV-012 — Pause: event must not resume
- **Priority:** P1 | **AC:** AC-513 | **Status:** Partial (durable aggregate)
- **Given** paused instance with active wait
- **When** matching event delivered
- **Then** buffered only; status stays `Paused`

### NEG-EV-013 — Resume Discard: redelivered EventId rejected forever
- **Priority:** P1 | **AC:** AC-517 | **Status:** Partial
- **Given** paused, event buffered, resume with Discard
- **When** same `EventId` sent again later
- **Then** never matches; audit record present

### NEG-EV-014 — Durable early event poisoned (pre-fix behavior)
- **Priority:** P0 | **AC:** AC-104 | **Status:** Covered — R4 fix landed; tests inverted
  (`DurableInboxTests.EarlyDeliveryWithoutActiveWait_IsBufferedForFutureWait`,
  `DurableInboxTests.EarlyDelivery_IsMatchedWhenWaitRegisters`)
- **Given** event before wait registers (Running, not Paused)
- **When** deliver then register wait
- **Then** buffer and match

### NEG-EV-015 — Inbox redelivery after Applied
- **Priority:** P1 | **AC:** EV-031 | **Status:** Partial
- **Given** inbox record `Applied`
- **When** same `EventId` delivered third time
- **Then** `NoOp`; no duplicate stream events

### NEG-EV-016 — BranchId required but omitted on instance-targeted raise
- **Priority:** P1 | **AC:** AC-110 | **Status:** Partial
- **Given** two parallel waits same correlation
- **When** `RaiseEventAsync` without branch disambiguation
- **Then** `WorkflowRoutingException` ambiguous — durable side missing

---

## Edge-case scenarios

### EDGE-EV-001 — Out-of-order: event then wait (ephemeral)
- **Priority:** P0 | **AC:** AC-104 | **Status:** Covered (`MailboxAcceptanceTests`)
- Mirror on durable engine — **Missing**

### EDGE-EV-002 — Out-of-order: multiple events before wait
- **Priority:** P1 | **Status:** Missing
- **Given** 3 events buffered before wait registers
- **When** wait opens
- **Then** only first matching consumed; others remain or discarded per policy

### EDGE-EV-003 — Wait-in-loop: iteration boundary
- **Priority:** P1 | **AC:** AC-109 | **Status:** Covered (`LoopWaitAcceptanceTests`)
- **Edge:** event arrives during loop condition check (between iterations)

### EDGE-EV-004 — Same EventId, different payload (duplicate id misuse)
- **Priority:** P1 | **AC:** EV-031 | **Status:** Missing
- **Given** duplicate `EventId` with different payload bytes
- **When** second arrives
- **Then** dedup by id; second payload ignored; log/audit if applicable

### EDGE-EV-005 — Timer due exactly at wait registration
- **Priority:** P1 | **AC:** AC-112 | **Status:** Missing
- **Given** timer and wait registered same virtual tick
- **When** both fire
- **Then** documented race policy picks one winner

### EDGE-EV-006 — Zero-duration timer
- **Priority:** P2 | **AC:** EV-050 | **Status:** Missing
- **Given** `Delay(TimeSpan.Zero)`
- **When** schedule
- **Then** fires exactly once on next pump

### EDGE-EV-007 — Very long delay (near `TimeSpan.MaxValue`)
- **Priority:** P2 | **Status:** Missing
- **Given** extremely long timer
- **When** schedule + small clock advance
- **Then** no overflow; not due

### EDGE-EV-008 — Concurrent duplicate RaiseEventAsync same EventId
- **Priority:** P0 | **AC:** AC-105, AC-006 | **Status:** Covered
  (`MailboxTests.EDGE_EV_008_ConcurrentDuplicateRaiseEvent_SameEventId_ResumesExactlyOnce` — 8 gated
  parallel deliveries, exactly one resume)
- **Given** two threads same `EventId` to same waiting instance
- **When** parallel raise
- **Then** exactly one resume; one no-op/conflict

### EDGE-EV-009 — Correlation fan-in: N instances, one event
- **Priority:** P1 | **AC:** EV-010 | **Status:** Missing
- **Given** fanout mode delivers to all matching waits
- **When** single broadcast event
- **Then** each instance resumes once (if API supports fanout)

### EDGE-EV-010 — Wait payload type mismatch at resume
- **Priority:** P1 | **AC:** EV-002 | **Status:** Missing
- **Given** wait expecting `Payload<int>`
- **When** event carries incompatible payload shape
- **Then** clear deserialization/read failure

### EDGE-EV-011 — Mailbox pressure: max buffered events
- **Priority:** P2 | **AC:** EV-030 | **Status:** Missing
- **Given** policy limit on buffered events (if any)
- **When** exceed limit
- **Then** reject or drop oldest with audit

### EDGE-EV-012 — Timer service concurrent schedule + fire (R3)
- **Priority:** P0 | **AC:** CR-040 | **Status:** Missing
- **Given** N instances scheduling while `FireDueTimersAsync` runs
- **When** stress concurrent
- **Then** no lost timers / list corruption

### EDGE-EV-013 — FireDueTimers without yield drain (R3)
- **Priority:** P1 | **AC:** CR-017 | **Status:** Missing
- **Given** timer resume runs yielding step
- **When** timer fires
- **Then** yield continuation drains without unrelated command

### EDGE-EV-014 — Durable: paused timer buffered then resume replay
- **Priority:** P0 | **AC:** AC-513, AC-514 | **Status:** Missing (R4)
- **Given** paused; timer fires; resume Replay
- **When** processing continues
- **Then** timer outcome applied in order with buffered events

### EDGE-EV-015 — Active wait on two correlations same instance
- **Priority:** P1 | **Status:** Missing
- **Given** sequential waits registered overlapping (if possible)
- **When** wrong correlation delivered
- **Then** no match; instance stays waiting
