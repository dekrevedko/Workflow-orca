# OrcaCore Active Implementation Documentation

This folder contains GitHub-readable documentation for the active implementation
at the repository root.

## Developer Guides

- [Ephemeral Engine Developer Guide](ephemeral-engine-developer-guide.md) -
  self-contained usage guide for the in-process engine, with examples for
  authoring, events, timers, management, policies, fanout, and limitations.
- [Ephemeral Engine Runtime Diagrams](ephemeral-engine-diagrams.md) -
  architecture diagrams: class relationships, concurrency model, lifecycle,
  sequences for start/event/timer/yield, control flow, and management surface.
- [Durable Driver Lane Host](durable-driver-lane-host.md) - how registered durable
  definitions advance (segments, restart-safe continuation signal, poison parking) and
  the honest multi-host contention model (DR-030..037).
- [Durable Driver Status](durable-driver-status.md) - review + continuation handoff:
  what DR-P1/P2 delivered, latent bugs fixed, and the remaining DR-P3/P4 scope.

## Operations And Handoffs

- [Samples](../samples/README.md) - runnable examples from simple engine usage
  through durable host APIs, plus the advanced dashboard sample.
- [End-to-End Plan](end-to-end-plan.md) - current integration baseline,
  missing host/runtime surfaces, staged e2e workstreams, and verification gates.
- [Production Readiness Notes](production-readiness.md) - current guarantees,
  deferred packaging work, security checklist, benchmark commands, and sample
  host notes.
- [EKS Scheduler Enablement Handoff](eks-scheduler-handoff.md) - boundary notes
  for applications that map OrcaCore durable workflow events to Kubernetes job
  scheduling.

## Build And Test

Run commands from the repository root so the local SDK pin is used:

```powershell
dotnet build OrcaCore.slnx
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj
```
