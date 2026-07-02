# T2-07: Add durable checkpoints and rehydration

**Difficulty**: Haiku        **Depends on**: T2-06
**Spec**: DU-010, DU-013, DU-020, DU-021        **AC**: AC-301, AC-302, AC-316

## Goal
Persist and load checkpoints so durable instances recover from the last committed boundary.
Recovery must ignore partial in-flight work and require no shutdown hook.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/`
- `v3-gpt/tests/OrcaCore.ProviderCertification/`
- Spec: `docs/specs/06-requirements-durable-execution.md` sections 6.2 and 6.3

## Deliverables
- Checkpoint save/load integration in durable command processing
- Rehydration tests using InMemory provider
- Provider certification coverage for committed-state-only recovery.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Recovery/DurableRecoveryTests.cs`:
1. `[Trait("AC","AC-301")] WaitingInstance_RehydrateAfterRestart_RemainsResumable`
2. `[Trait("AC","AC-302")] CrashBeforeCommit_RehydratesLastCommittedStateOnly`
3. `[Trait("AC","AC-316")] HostKilledBeforeShutdownHook_LosesNoCommittedTransition`
4. `CheckpointPlusTail_RehydratesWithoutGenesisReplay`

## Implementation notes
Use explicit fake crash points from TestSupport. Avoid shutdown/deactivation persistence.

## Out of scope
WaitLong cold eviction, inbox dedup, projections, retention.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] AC-301, AC-302, and AC-316 are green
- [ ] PROGRESS.md updated; committed as "T2-07: durable checkpoints and rehydration (AC-301, AC-302, AC-316)"
