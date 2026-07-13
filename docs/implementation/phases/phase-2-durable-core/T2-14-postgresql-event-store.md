# T2-14: Add PostgreSQL event and checkpoint stores

**Difficulty**: Sonnet        **Depends on**: T2-13
**Spec**: PR-010, PR-016, PR-020, PR-021, DU-010, DU-020        **AC**: AC-301, AC-302, AC-309

## Goal
Create the PostgreSQL provider project and implement schema, event store, and checkpoint store.
Resolve IOQ-1 before choosing schema shape.

## Read first
- `docs/implementation/00-stack-decisions.md`
- `src/OrcaCore.Abstractions/Providers/`
- `tests/OrcaCore.ProviderCertification/`
- `Directory.Packages.props`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` sections 10.2 and 10.3

## Deliverables
- `src/OrcaCore.Providers.PostgreSql/` project with explicit registration
- PostgreSQL schema for event streams and checkpoints
- Npgsql package in central package management
- Provider integration tests using Testcontainers.
- IOQ-1 resolution logged in `docs/implementation/00-stack-decisions.md`.

## Tests to write FIRST
In `tests/OrcaCore.Providers.PostgreSql.Tests/PostgreSqlEventStoreTests.cs`:
1. `Append_WithExpectedVersion_CommitsEventsAndAdvancesVersion`
2. `Append_ConflictingExpectedVersion_ReturnsConflict`
3. `Checkpoint_SaveAndLoad_RoundTripsPayloadAndVersion`
4. `[Trait("AC","AC-301")] PostgreSql_DurableWaitSurvivesRestart`
5. `[Trait("AC","AC-302")] PostgreSql_CrashRestoresCommittedStateOnly`
6. `[Trait("AC","AC-309")] PostgreSql_ConcurrentResumeSerializes`

## Implementation notes
Use raw Npgsql only. No ORM, no Dapper. Keep SQL isolated inside the plugin.

## Out of scope
Inbox, outbox, projections, retention purge, RabbitMQ.

## Definition of done
- [ ] PostgreSQL event/checkpoint certification tests green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] IOQ-1 moved out of Open with dated rationale
- [ ] PROGRESS.md updated; committed as "T2-14: PostgreSQL event store (AC-301, AC-302, AC-309)"
