# OT0-02: TestingHost smoke test

**Difficulty**: Haiku        **Depends on**: OT0-01
**Spec**: OE-080, OE-001, OE-003 guard        **AC**: OE-AC-040

## Goal
The test harness pattern every later task copies: an Orleans `TestCluster` starts in an
xUnit fixture, a trivial grain round-trips a call, and Orleans codegen provably runs for
the test assembly.

## Read first
- `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj` (test csproj template)
- `v3-gpt/tests/OrcaCore.TestSupport/` (skim folder listing only — reuse conventions, not code)
- [01-architecture.md](../01-architecture.md) §4 note on `global::Orleans`

## Deliverables
- `v3-gpt/tests/OrcaCore.Engine.Orleans.Tests/OrcaCore.Engine.Orleans.Tests.csproj` —
  references `OrcaCore.Engine.Orleans`, `OrcaCore.TestSupport`,
  `Microsoft.Orleans.TestingHost`; xUnit v3 per repo conventions; added to slnx.
- `Testing/OrleansClusterFixture.cs` — xUnit collection fixture building a 1-silo
  `TestCluster` via `TestClusterBuilder` with a `ISiloConfigurator` hook that later tasks
  extend (expose a static configurator delegate seam).
- `Testing/EchoGrain.cs` (test assembly): `IEchoGrain : IGrainWithGuidKey` with
  `Task<string> EchoAsync(string value)`; implementation returns input.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Orleans.Tests/Testing/ClusterSmokeTests.cs`:
1. `Cluster_Starts_AndStops` — fixture yields a deployed cluster; `Cluster.Client` non-null.
2. `EchoGrain_RoundTrips` — grain call returns the sent value (proves codegen + messaging).
3. `OrleansReferences_AreConfinedToWhitelist` — `[Trait("AC","OE-AC-040")]` — scan
   project references and source attributes so `Microsoft.Orleans.*` appears only in the
   Orleans engine project and Orleans test project; this guard remains green as later
   grain/transport files are added.

## Implementation notes
- Keep the fixture cheap: one silo, in-memory everything (TestingHost defaults).
- `[Collection]`-based sharing so the cluster boots once per test class group.
- No OrcaCore behavior yet — deliberately.

## Out of scope
Workflow grains, providers, envelopes.

## Definition of done
- [ ] Both tests green: `dotnet test v3-gpt/tests/OrcaCore.Engine.Orleans.Tests`
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` — zero warnings
- [ ] PROGRESS.md updated; committed as "OT0-02: TestingHost smoke (OE-080)"
