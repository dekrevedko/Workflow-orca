# T3-04: Implement durable timer providers

**Difficulty**: Sonnet        **Depends on**: T3-03
**Spec**: EV-050, MG-013, PR-014, PR-024        **AC**: AC-111, AC-513

## Goal
Implement provider-backed durable timer scheduling for InMemory and PostgreSQL providers and
add certification coverage. Timer firings targeting paused instances are buffered like
events and do not resume the instance.

## Read first
- `src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs`
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs`
- `tests/OrcaCore.ProviderCertification/EventStoreCertificationTests.cs`
- `tests/OrcaCore.ProviderCertification/InMemoryProviderCertificationTests.cs`
- `tests/OrcaCore.Providers.PostgreSql.Tests/PostgreSqlProviderCertificationTests.cs`
- Spec: `docs/specs/05-requirements-events-waits-timers.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- `ITimerScheduler` implementations in InMemory and PostgreSQL providers
- Provider certification timer tests
- PostgreSQL schema/storage updates owned by `OrcaCore.Providers.PostgreSql`
- Acceptance or provider tests covering AC-513's timer-firing clause

## Tests to write FIRST
In `tests/OrcaCore.ProviderCertification/TimerSchedulerCertificationTests.cs`:
1. `ScheduleAsync_DueTimer_IsClaimableOnce` - each provider exposes one due fire command.
2. `ScheduleAsync_NotDueTimer_IsNotClaimed` - due-time filtering honors `TimeProvider`.
3. `[Trait("AC","AC-513")] FireTimer_WhenInstancePaused_BuffersWithoutAdvancing`

## Implementation notes
If `ITimerScheduler` needs a claim/read method, change the port and certification suite
together. Providers still reference only `OrcaCore.Abstractions`.

## Out of scope
Timer/event race policy, timeout decorators, and hosted background scheduler loops.

## Definition of done
- [ ] New certification tests fail before implementation and pass for InMemory and PostgreSQL
- [ ] `dotnet test OrcaCore.slnx --filter "TimerScheduler|AC=AC-513"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] Provider dependency rules still hold
- [ ] PROGRESS.md updated; committed as "T3-04: durable timer providers (PR-014, AC-513)"
