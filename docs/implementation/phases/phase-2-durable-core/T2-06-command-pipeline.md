# T2-06: Add durable command pipeline

**Difficulty**: Sonnet        **Depends on**: T2-05
**Spec**: CR-040, CR-041, DU-011, DU-022        **AC**: AC-309

## Goal
Wire durable commands through a per-instance command lane, aggregate rehydration, decision, and expected-version commit.
Concurrent durable commands must serialize to one committed winner or a clear conflict/retry result.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/InstanceExecutionLane.cs`
- `v3-gpt/tests/OrcaCore.ProviderCertification/`
- Spec: `docs/specs/06-requirements-durable-execution.md` sections 6.2 and 6.3

## Deliverables
- Durable command processor in `v3-gpt/src/OrcaCore.Engine.Durable/Execution/`
- Expected-version append path using provider ports
- Conflict outcome policy for racing commands
- Acceptance coverage for concurrent durable resume serialization.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Execution/DurableCommandPipelineTests.cs`:
1. `ProcessCommand_LoadsCheckpointAndTailBeforeDecision`
2. `ProcessCommand_AppendsWithExpectedVersionFromAggregate`
3. `ProcessCommand_VersionConflict_ReturnsClearConflict`
4. `[Trait("AC","AC-309")] ConcurrentResumeAttempts_CommitExactlyOneOutcome`

## Implementation notes
Use Channels/TPL only. Do not add leases or multi-node ownership beyond expected-version append.

## Out of scope
Checkpoints policy, cold waits, inbox/outbox pump, PostgreSQL.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] AC-309 green in durable/certification coverage
- [ ] PROGRESS.md updated; committed as "T2-06: durable command pipeline (AC-309)"
