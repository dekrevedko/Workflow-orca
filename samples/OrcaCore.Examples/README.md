# OrcaCore examples

This console project contains runnable examples ordered from simple API usage to
durable host integration.

Run from the repository root:

```powershell
dotnet run --project samples\OrcaCore.Examples\OrcaCore.Examples.csproj
```

The examples cover:

- mode-selected in-process workflow authoring with `Workflow.Ephemeral<TState>` and
  `EphemeralWorkflowEngine`;
- event waits, correlation routing, and `EventEnvelope` payloads;
- `ForEach` fanout with management statistics;
- transient timers and explicit `FireDueTimersAsync` pumping;
- ephemeral saga compensation;
- durable DI registration, `DurableWorkflowRuntime.StartOrGetAsync`, external
  jobs, resource pools, inbox deduplication, and durable management queries.

For the most advanced operational sample, run the Blazor dashboard:

```powershell
dotnet run --project samples\OrcaCore.Dashboard\OrcaCore.Dashboard.csproj
```

The dashboard seeds a durable fanout workflow, a resource-pool-backed external
job, and a compensation-failed saga on startup so the UI has real management and
telemetry data to inspect. Its Kubernetes page uses the active local `kubectl`
context to create scheduled, dependency-chain, and DAG Kubernetes jobs while
tracking them as durable external jobs.
