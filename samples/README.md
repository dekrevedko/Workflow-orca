# OrcaCore samples

Run commands from the repository root so the local SDK pin and central package
versions are used. Every active sample project is included in `OrcaCore.slnx`
and therefore participates in the normal build.

| Sample | Purpose | Command |
|--------|---------|---------|
| `OrcaCore.Examples` | Complete typed ephemeral start-to-output journey. | `dotnet run --project samples\OrcaCore.Examples\OrcaCore.Examples.csproj` |
| `OrcaCore.SampleHost` | Minimal explicit durable provider and engine role registration. | `dotnet run --project samples\OrcaCore.SampleHost\OrcaCore.SampleHost.csproj` |
| `OrcaCore.Dashboard` | Browser view over process-local public BCL metrics, logs, and activities. | `dotnet run --project samples\OrcaCore.Dashboard\OrcaCore.Dashboard.csproj` |

`OrcaCore.SampleHost/BrokerAdapters` contains compile-checked MassTransit-,
Rebus-, and SNS/SQS-style application adapters. They demonstrate durable ingress
acknowledgement and outbound dispatch-result mapping while keeping broker SDKs
outside OrcaCore packages.
