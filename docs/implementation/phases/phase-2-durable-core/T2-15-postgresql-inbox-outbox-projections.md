# T2-15: Add PostgreSQL inbox, outbox, and projections

**Difficulty**: Sonnet        **Depends on**: T2-14
**Spec**: PR-011, PR-012, PR-013, PR-020, PR-022, PR-023, DU-030, DU-031, DU-051        **AC**: AC-305, AC-310, AC-314

## Goal
Complete the PostgreSQL provider by implementing inbox, outbox claim semantics, projection stores, and retention-safe purge.
The provider must pass certification for all Phase 2 ports.

## Read first
- `src/OrcaCore.Providers.PostgreSql/`
- `tests/OrcaCore.Providers.PostgreSql.Tests/`
- `tests/OrcaCore.ProviderCertification/`
- `src/OrcaCore.Abstractions/Providers/`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` sections 10.2 and 10.3

## Deliverables
- PostgreSQL inbox store with restart-safe dedup states
- PostgreSQL outbox store with row-lock claim semantics (`FOR UPDATE SKIP LOCKED`)
- PostgreSQL projection store for durable queries/routing
- Retention-safe purge certification.

## Tests to write FIRST
In `tests/OrcaCore.Providers.PostgreSql.Tests/PostgreSqlProviderCertificationTests.cs`:
1. `[Trait("AC","AC-305")] PostgreSql_DuplicateEventsBeforeAndAfterRestartDedup`
2. `[Trait("AC","AC-310")] PostgreSql_OutboxDispatchesOnlyCommittedRecords`
3. `[Trait("AC","AC-314")] PostgreSql_PurgeNeverRemovesActiveInstancesOrClaimedOutbox`
4. `OutboxClaim_ConcurrentWorkers_DoNotClaimSameRecord`
5. `ProjectionQuery_ByWaitCorrelation_ReturnsColdInstances`

## Implementation notes
Use one transaction boundary where possible; if following the T2-02 transactional-chain decision, tests must prove observable consistency.

## Out of scope
RabbitMQ dispatcher, timers, saga projections, multi-node leases.

## Definition of done
- [ ] PostgreSQL provider passes all Phase 2 certification tests
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] AC-305, AC-310, and AC-314 are green for PostgreSQL
- [ ] PROGRESS.md updated; committed as "T2-15: PostgreSQL inbox outbox projections (AC-305, AC-310, AC-314)"
