# T2-02: Add durable provider ports

**Difficulty**: Sonnet        **Depends on**: T2-01
**Spec**: PR-010, PR-011, PR-012, PR-013, PR-014, PR-015, PR-016, PR-020, DU-011        **AC**: none

## Goal
Define provider-neutral durable ports for event storage, inbox, outbox, projections, timers, dispatch, and serialization.
Resolve IOQ-3 explicitly before choosing whether projection updates share the append transaction or use a transactional chain.

## Read first
- `src/OrcaCore.Abstractions/Durable/`
- `src/OrcaCore.Abstractions/Primitives/Result.cs`
- `src/OrcaCore.Abstractions/Primitives/Option.cs`
- `docs/implementation/00-stack-decisions.md`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` sections 10.2 and 10.3

## Deliverables
- Provider port interfaces in `src/OrcaCore.Abstractions/Providers/`
- Commit-batch/result records for append, checkpoint, inbox, outbox, and projections
- IOQ-3 resolution logged in `docs/implementation/00-stack-decisions.md`
- XML docs describing atomicity and conflict outcomes.

## Tests to write FIRST
In `tests/OrcaCore.Core.Tests/Providers/ProviderPortContractTests.cs`:
1. `EventStoreAppendResult_RepresentsSuccessAndVersionConflictWithoutExceptions`
2. `CommitBatch_CarriesEventsCheckpointInboxOutboxAndProjectionOperations`
3. `ProjectionCommitDecision_MatchesRecordedIoq3Resolution`
4. `ProviderPorts_DoNotExposeProviderSpecificTypes`
5. `TimerSchedulerPort_CarriesDurableWakeupCommandData`

## Implementation notes
Ports are contracts only. Use `Result<T>` for expected conflicts and failures; do not leak PostgreSQL or in-memory types.

## Out of scope
Fake stores, certification tests, InMemory implementation, PostgreSQL schema, command processing.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] IOQ-3 moved out of Open with a dated rationale
- [ ] Abstractions has no third-party package references
- [ ] PROGRESS.md updated; committed as "T2-02: provider ports (PR-010-016, PR-020)"
