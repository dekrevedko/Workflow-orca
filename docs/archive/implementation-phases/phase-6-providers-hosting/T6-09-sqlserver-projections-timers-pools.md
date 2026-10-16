# T6-09: Complete SQL Server projections, timers, and pools

**Difficulty**: Sonnet        **Depends on**: T6-08
**Spec**: PR-013, PR-014, MG-062, MG-063, MG-064        **AC**: JS-AC-010, JS-AC-011, JS-AC-012

## Goal
Complete the SQL Server provider by adding projection queries, durable timer scheduling, and
durable resource pool ticket storage. The full provider certification suite must pass for
the ports SQL Server implements.

## Read first
- `src/OrcaCore.Providers.SqlServer/OrcaCore.Providers.SqlServer.csproj`
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlResourcePoolStore.cs`
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs`
- `tests/OrcaCore.ProviderCertification/TimerSchedulerCertificationTests.cs`
- `tests/OrcaCore.ProviderCertification/ResourcePoolStoreCertificationTests.cs`
- Spec: `docs/specs/09-requirements-management-operations.md` section 9.7
- Spec: `docs/specs/14-driving-scenario-eks-job-scheduler.md` JS-007 acceptance criteria

## Deliverables
- Add SQL Server projection query implementation
- Add SQL Server durable timer scheduler implementation
- Add SQL Server resource pool store implementation
- Add SQL Server certification tests for projections, timers, and resource pools
- Update `src/OrcaCore.Providers.SqlServer/README.md`

## Tests to write FIRST
In SQL Server provider tests:
1. `TimerSchedulerCertificationTests` inherited fixture passes for SQL Server.
2. `ResourcePoolStoreCertificationTests` inherited fixture passes for SQL Server.
3. `ProjectionQueries_MetadataFilters_ReturnExpectedRows` proves provider-side filtering without business payload deserialization.

## Implementation notes
Keep all provider writes transactional where the port requires a commit boundary. SQL Server
schema changes must remain provider-owned and not leak workflow definitions into table shape.

## Out of scope
RabbitMQ, Redis, DynamoDB, package publishing, and multi-node lease ownership.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter "FullyQualifiedName~SqlServer&Category=Certification"` passes when containers are enabled
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-09: sql server projections timers pools (PR-013, PR-014)"
