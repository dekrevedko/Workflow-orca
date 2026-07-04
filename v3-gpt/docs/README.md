# OrcaCore v3-gpt Documentation

This folder contains GitHub-readable documentation for the current `v3-gpt`
implementation track.

## Developer Guides

- [Ephemeral Engine Developer Guide](ephemeral-engine-developer-guide.md) -
  self-contained usage guide for the in-process engine, with examples for
  authoring, events, timers, management, policies, fanout, and limitations.

## Operations And Handoffs

- [End-to-End Plan](end-to-end-plan.md) - current integration baseline,
  missing host/runtime surfaces, staged e2e workstreams, and verification gates.
- [Production Readiness Notes](production-readiness.md) - current guarantees,
  deferred packaging work, security checklist, benchmark commands, and sample
  host notes.
- [EKS Scheduler Enablement Handoff](eks-scheduler-handoff.md) - boundary notes
  for applications that map OrcaCore durable workflow events to Kubernetes job
  scheduling.

## Build And Test

Run commands from `v3-gpt` so the local SDK pin is used:

```powershell
cd v3-gpt
dotnet build OrcaCore.slnx
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj
```
