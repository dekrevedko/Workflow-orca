# OrcaCore samples

Run commands from the repository root so the local SDK pin and central package
versions are used.

| Sample | Purpose | Command |
|--------|---------|---------|
| `OrcaCore.Examples` | Runnable console walkthrough from simple ephemeral workflows to durable host APIs. | `dotnet run --project samples\OrcaCore.Examples\OrcaCore.Examples.csproj` |
| `OrcaCore.Dashboard` | Blazor operational dashboard over durable management, logs, metrics, traces, and the local Kubernetes job scheduler sample. Seeds advanced demo data on startup. | `dotnet run --project samples\OrcaCore.Dashboard\OrcaCore.Dashboard.csproj` |
| `OrcaCore.SampleHost` | Minimal generic host registration sample for `AddOrcaCore` and hosted services. | `dotnet run --project samples\OrcaCore.SampleHost\OrcaCore.SampleHost.csproj` |

Use `OrcaCore.Examples` first when learning the public API. Use
`OrcaCore.Dashboard` when you want to inspect the advanced durable operational
surface in a browser.

The Kubernetes dashboard sample uses the active `kubectl` context and the
`default` namespace. It starts one scheduled Kubernetes `Job` every 15 minutes
with a random 1-15 minute runtime, can start a 2-5 job dependency chain, and can
start a 3-9 node DAG. Each Kubernetes job is also tracked as a durable external
job in OrcaCore.
