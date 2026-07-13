# OrcaCore Benchmarks

This project hosts BenchmarkDotNet scenarios for performance-readiness work.
Normal pull-request CI builds this project but does not run benchmarks.

Implemented benchmark groups:

- Ephemeral execution loop: `EphemeralExecutionLoopBenchmarks`
- Provider serialization and materialization: `ProviderSerializationMaterializationBenchmarks`
- Management query and projection path: `ManagementProjectionBenchmarks`
- Resource pool and timer scheduling: `ResourcePoolTimerSchedulingBenchmarks`
- Provider commit path: `ProviderCommitBenchmarks`

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
