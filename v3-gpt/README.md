# OrcaCore v3-gpt

This folder is the current implementation track for OrcaCore. It contains the
active solution, source projects, tests, samples, benchmarks, and developer
documentation.

Start with:

- [Ephemeral Engine Developer Guide](docs/ephemeral-engine-developer-guide.md)
- [v3-gpt Documentation Index](docs/README.md)
- [Production Readiness Notes](docs/production-readiness.md)

Build and test from this folder:

```powershell
dotnet build OrcaCore.slnx
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj
```

The implementation targets `net10.0` and uses the SDK pinned by `global.json`.
