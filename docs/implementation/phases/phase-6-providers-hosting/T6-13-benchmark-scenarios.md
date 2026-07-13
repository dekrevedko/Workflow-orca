# T6-13: Add BenchmarkDotNet hot-path scenarios

**Difficulty**: Sonnet        **Depends on**: T6-12
**Spec**: NF-030, NF-031        **AC**: none

## Goal
Implement the IOQ-8 benchmark suite. Scenarios must cover the ephemeral execution loop,
provider serialization/materialization, management query/projection path, resource pool and
timer scheduling, and provider commit path.

## Read first
- `benchmarks/OrcaCore.Benchmarks/OrcaCore.Benchmarks.csproj`
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `src/OrcaCore.Engine.Durable/Management/ProjectionPredicateTranslator.cs`
- `src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs`
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs`
- Spec: `docs/specs/11-non-functional-requirements.md` section 11.4

## Deliverables
- Add benchmark classes under `benchmarks/OrcaCore.Benchmarks/Scenarios/`
- Add deterministic benchmark fixture builders under `benchmarks/OrcaCore.Benchmarks/Fixtures/`
- Update `benchmarks/OrcaCore.Benchmarks/README.md` with run instructions and scenario list

## Tests to write FIRST
No product tests. Benchmark compile and smoke-run are the verification surface.

## Implementation notes
Keep scenario setup deterministic and isolated from external services unless explicitly
guarded. Provider commit benchmarks should include in-memory by default; PostgreSQL may be
an opt-in benchmark profile if containers are required.

## Out of scope
Hard performance thresholds, CI benchmark execution, and broad refactoring to optimize
benchmarks.

## Definition of done
- [ ] `dotnet build benchmarks/OrcaCore.Benchmarks/OrcaCore.Benchmarks.csproj` passes with zero warnings
- [ ] A short local smoke run of the benchmark project succeeds
- [ ] Benchmark README documents the five IOQ-8 hot-path groups
- [ ] PROGRESS.md updated; committed as "T6-13: benchmark hot path scenarios (NF-030)"
