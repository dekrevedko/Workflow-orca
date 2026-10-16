# OrcaCore Benchmarks

This project hosts BenchmarkDotNet scenarios for performance-readiness work.
Normal pull-request CI builds this project but does not run benchmarks.

Implemented benchmark groups:

- Ephemeral execution loop: `EphemeralExecutionLoopBenchmarks`
- Provider serialization and materialization: `ProviderSerializationMaterializationBenchmarks`
- Management query and projection path: `ManagementProjectionBenchmarks`
- Resource pool and timer scheduling: `ResourcePoolTimerSchedulingBenchmarks`
- Provider commit path: `ProviderCommitBenchmarks`
- Structured public authoring: `StructuredFiberExecutionBenchmarks`

The benchmarks use the public engine registrations, typed definition handles, and provider
ports. Projection scenarios measure exact-instance lookup and recovery enumeration. JSON
materialization measures application payloads, not the internal fixed codec. Structured
authoring replaces the retired internal scheduler/reducer/envelope microbenchmarks; it does
not claim to measure those internal operations. Mutating scenarios reset their providers
between iterations to bound retained state.

CI runs one correctness check of every scenario (without performance measurements):

```powershell
dotnet run --project benchmarks/OrcaCore.Benchmarks/OrcaCore.Benchmarks.csproj -c Release -- --smoke
```

Run a short local smoke pass from the repository root with:

```powershell
dotnet run --project benchmarks/OrcaCore.Benchmarks/OrcaCore.Benchmarks.csproj -c Release -- --filter *ProviderCommitBenchmarks.AppendCommitBatch* --job Dry
```

Run the full local benchmark suite with:

```powershell
dotnet run --project benchmarks/OrcaCore.Benchmarks/OrcaCore.Benchmarks.csproj -c Release
```

All default scenarios are deterministic and in-process only. Provider-specific external
profiles, hard thresholds, and CI benchmark execution are intentionally out of scope for
this phase.
