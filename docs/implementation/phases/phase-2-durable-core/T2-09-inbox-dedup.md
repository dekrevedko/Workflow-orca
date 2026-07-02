# T2-09: Add durable inbox deduplication

**Difficulty**: Sonnet        **Depends on**: T2-08
**Spec**: DU-030, EV-030, EV-031, EV-032        **AC**: AC-305, AC-114

## Goal
Implement durable inbox recording for inbound event deliveries with restart-safe deduplication and transactional consumption.
Events must remain re-matchable if a crash occurs after match but before commit.

## Read first
- `v3-gpt/src/OrcaCore.Abstractions/Providers/`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/`
- `v3-gpt/tests/OrcaCore.ProviderCertification/`
- `v3-gpt/tests/OrcaCore.TestSupport/Providers/`
- Spec: `docs/specs/06-requirements-durable-execution.md` section 6.4

## Deliverables
- Inbox states `Received`, `Applied`, `DuplicateIgnored`, `Poisoned`, and `DiscardedOnResume`
- Durable event delivery path using inbox and commit batch
- Certification/acceptance coverage for no lost events and restart-safe dedup.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Events/DurableInboxTests.cs`:
1. `[Trait("AC","AC-305")] DuplicateEvent_BeforeAndAfterRestart_ProducesOneOutcome`
2. `[Trait("AC","AC-114")] CrashAfterMatchBeforeCommit_LeavesWaitActiveAndEventAvailable`
3. `InboxRecord_AppliedOnlyAfterStateCommitSucceeds`
4. `PoisonedDelivery_IsRecordedWithClearFailureMetadata`

## Implementation notes
Commit-before-ack is represented by provider commit success. Do not dispatch outbox messages here.

## Out of scope
Pause buffering, outbox pump, timers, retention.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] AC-305 and AC-114 are green
- [ ] PROGRESS.md updated; committed as "T2-09: durable inbox dedup (AC-305, AC-114)"
