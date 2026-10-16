# T3-12: Add statistics and pressure metrics

**Difficulty**: Haiku        **Depends on**: T3-11
**Spec**: MG-030, MG-031, DU-052        **AC**: AC-503, AC-312

## Goal
Extend operational statistics with grouped counts and durable pressure metrics. Operators
can detect stream, checkpoint, outbox, and active-instance pressure through supported
statistics.

## Read first
- `src/OrcaCore.Abstractions/Instances/WorkflowStatistics.cs`
- `src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `src/OrcaCore.Engine.Durable/Management/DurableManagement.cs`
- `src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs`
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs`
- Spec: `docs/specs/09-requirements-management-operations.md`
- Spec: `docs/specs/06-requirements-durable-execution.md`

## Deliverables
- Statistics model extensions in Abstractions
- Projection/provider support for pressure metrics
- Management query tests and acceptance coverage for AC-312

## Tests to write FIRST
In `tests/OrcaCore.Engine.Durable.Tests/Management/DurableStatisticsTests.cs`:
1. `Statistics_GroupsCountsByDefinitionVersionAndStatus`
2. `Statistics_IncludesHistoryCheckpointAndOutboxPressure`
In `tests/OrcaCore.Acceptance.Tests/ManagementAcceptanceTests.cs`:
3. `[Trait("AC","AC-503")] Statistics_GroupsByDefinitionAndStatus`
4. `[Trait("AC","AC-312")] Statistics_ShowHistoryPressure`

## Implementation notes
Do not deserialize business payloads for statistics. PostgreSQL metrics should be derived
from provider-owned tables/projections.

## Out of scope
BenchmarkDotNet, CI coverage gates, continue-as-new, and retention policy tuning.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter "Statistics|AC=AC-312|AC=AC-503"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-12: statistics and pressure metrics (MG-030, AC-312)"
