# OrcaCore examples

This console project runs one complete, typed ephemeral workflow through the
current public application surface: stage a definition, resolve its exact
reference, start it idempotently, and await its typed output.

Run from the repository root:

```powershell
dotnet run --project samples\OrcaCore.Examples\OrcaCore.Examples.csproj
```

For durable role registration, see `OrcaCore.SampleHost`. For BCL telemetry
collection in a browser, see `OrcaCore.Dashboard`.
