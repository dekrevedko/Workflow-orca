# OT0-04: Silo composition entry point

**Difficulty**: Haiku        **Depends on**: OT0-02 (fixture) — independent of OT0-03;
may run in parallel with it
**Spec**: OE-070, OE-020        **AC**: OE-AC-042

## Goal
One extension method composes the Orleans engine into a silo: ports from DI, one shared
`DurableCommandProcessor` singleton, and an options record — so every later task has a
single wiring point.

## Read first
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs` (constructors only)
- `src/OrcaCore.Providers.InMemory/` — locate the in-memory event store registration
  pattern (read the DI extension file if one exists, else the store type)
- [01-architecture.md](../01-architecture.md) §3, §5

## Deliverables
In `src/OrcaCore.Engine.Orleans/Hosting/`:
- `OrcaCoreOrleansOptions` — plain record: placeholders for pump intervals and
  activation-collection idle age (defaults only; hardening is OT4-01).
- `OrcaCoreOrleansSiloExtensions.UseOrcaCoreOrleans(this ISiloBuilder, Action<OrcaCoreOrleansOptions>? configure = null)` —
  registers: options; singleton `DurableCommandRuntime` + `DurableCommandProcessor` resolved
  from the DI-provided `IWorkflowEventStore` (+ optional `IResourcePoolStore`,
  `IWorkflowRuntimeObserver`); nothing else yet.
- The host remains responsible for registering provider ports (in-memory or PostgreSQL) —
  document this in the extension's XML-doc, including: **every silo in a cluster MUST
  register the identical durable definition set and versions** (rehydration on any silo
  needs the bound definition — DU-040/041; version skew between silos is a deployment
  error surfaced by version binding, not something the engine papers over).

## Tests to write FIRST
In `tests/OrcaCore.Engine.Orleans.Tests/Hosting/SiloCompositionTests.cs`:
1. `UseOrcaCoreOrleans_ResolvesProcessor` — `[Trait("AC","OE-AC-042")]` — TestingHost silo configured with in-memory
   event store + `UseOrcaCoreOrleans()`; a probe (test grain or `IServiceProvider` check via
   silo services) resolves the singleton `DurableCommandProcessor`.
2. `MissingEventStore_FailsFast` — silo startup (or first resolve) without an
   `IWorkflowEventStore` registration produces a clear failure, not a null-ref later.

## Implementation notes
- Extend `OrleansClusterFixture`'s configurator seam from OT0-02 rather than a new fixture.
- Follow the explicit-registration rule: no scanning, no reflection discovery.

## Out of scope
Grain registration specifics (implicit with codegen anyway), pumps (OT2-01/OT3-01),
facade (OT1-03), options validation (OT4-01).

## Definition of done
- [ ] Both tests green; solution builds zero-warning
- [ ] `OrcaCore.Providers.InMemory` still has no Orleans reference (test project wires it)
- [ ] PROGRESS.md updated; committed as "OT0-04: silo composition (OE-070)"
