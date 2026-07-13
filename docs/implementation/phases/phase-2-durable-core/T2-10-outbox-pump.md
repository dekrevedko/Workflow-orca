# T2-10: Add durable outbox and pump

**Difficulty**: Sonnet        **Depends on**: T2-09
**Spec**: DU-031, DU-032, DU-033, PR-012, PR-015        **AC**: AC-310

## Goal
Persist outbox records in the same commit boundary as workflow events and dispatch them asynchronously.
The engine must never dispatch a message that was not first committed.

## Read first
- `src/OrcaCore.Abstractions/Providers/`
- `src/OrcaCore.Engine.Durable/Execution/`
- `tests/OrcaCore.ProviderCertification/`
- `docs/implementation/00-stack-decisions.md`
- Spec: `docs/specs/06-requirements-durable-execution.md` section 6.4

## Deliverables
- Durable outbox record model and append-in-commit integration
- Channels-based outbox pump with claim, dispatch, mark success/failure/poison
- Fake dispatcher in TestSupport
- IOQ-2 review note remains deferred to Phase 2 exit.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Durable.Tests/Outbox/DurableOutboxTests.cs`:
1. `[Trait("AC","AC-310")] CommitFailure_DoesNotExposeOutboxRecordToDispatcher`
2. `OutboxPump_ClaimsDispatchesAndMarksSuccess`
3. `OutboxPump_RetryableFailure_LeavesRecordRetryable`
4. `OutboxPump_PermanentFailure_MarksPoisoned`
5. `UnifiedOutbox_CarriesStatusAndExternalMessageRecords`

## Implementation notes
Use Channels/TPL only. Do not introduce TPL Dataflow in this task.

## Out of scope
RabbitMQ, timers, saga child commands, PostgreSQL claim SQL.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] AC-310 is green
- [ ] PROGRESS.md updated; committed as "T2-10: durable outbox pump (AC-310)"
