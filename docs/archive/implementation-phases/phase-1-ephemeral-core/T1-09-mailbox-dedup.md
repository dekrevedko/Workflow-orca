# T1-09: Add mailbox buffering, deduplication, and transactional consumption

**Difficulty**: Sonnet        **Depends on**: T1-08
**Spec**: EV-030, EV-031, EV-032, CR-032        **AC**: AC-010, AC-104, AC-105

## Goal
Make event delivery order-insensitive and idempotent for one instance. Events that arrive
before their wait are stored in the instance mailbox, duplicate `EventId`s do not cause
duplicate effects, and mailbox removal happens only after the resumed transition commits.

## Read first
- `src/OrcaCore.Abstractions/Events/EventEnvelope.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs`
- Spec: `docs/specs/05-requirements-events-waits-timers.md` section 5.4

## Deliverables
- Per-instance pending-event mailbox.
- Runtime dedup set for delivered and consumed `EventId`s.
- Bidirectional matching: delivered events check active waits, and new waits check pending
  events before remaining suspended.
- Completion guard for unresolved runtime-owned waits/events per CR-032.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/MailboxTests.cs`:
1. `RaiseEventAsync_BeforeWait_BuffersEvent`
2. `Run_RegisteringMatchingWait_ConsumesBufferedEventAndContinues`
3. `RaiseEventAsync_DuplicatePendingEvent_BuffersOnlyOnce`
4. `RaiseEventAsync_DuplicateConsumedEvent_DoesNotContinueAgain`
5. `RaiseEventAsync_ResumeTransitionFails_EventRemainsAvailable`
6. `Run_EndWithActiveWait_FailsOrCancelsByExplicitPolicy`

In `tests/OrcaCore.Acceptance.Tests/MailboxAcceptanceTests.cs`:
7. `[Trait("AC","AC-104")] OutOfOrderEvent_IsBufferedThenConsumed`
8. `[Trait("AC","AC-105")] DuplicateEventId_ProducesOneConsumptionAndContinuation`
9. `[Trait("AC","AC-010")] CompletionBlockedByUnresolvedRuntimeWork`

Use AwesomeAssertions for assertions.

## Implementation notes
For ephemeral mode, "transactional" means the in-memory state update order preserves the
observable invariant: an event is removed only after the continuation's mutation succeeds.
Keep the durable crash-proof parts out of scope; provider certification covers those later.

## Out of scope
Durable inbox/outbox, cross-process crash recovery, correlation-targeted routing, fanout.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] AC-010, AC-104, and AC-105 are green in `OrcaCore.Acceptance.Tests`
- [ ] PROGRESS.md updated; committed as "T1-09: mailbox buffering and dedup (AC-010, AC-104-105)"
