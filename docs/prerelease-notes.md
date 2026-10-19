OrcaCore prerelease for .NET 10. The assets contain all twelve NuGet packages and a
`SHA256SUMS` manifest. APIs and persistence formats may change.

Download all `.nupkg` assets into one directory, register that directory as a local NuGet
source, then reference `OrcaCore.Engine.Ephemeral`, or `OrcaCore.Durable.Hosting` together
with the chosen provider, at this release's package version. Keep the full package set
together so NuGet can resolve its OrcaCore dependencies. See the
[installation guide](https://github.com/dekrevedko/Workflow-orca/blob/feature/v3-rebuild/docs/packages.md).
The GitHub release URL itself is not a NuGet feed.

Current limitations:

- Completed ephemeral instances and their start bindings remain retained for the host lifetime.
  Use bounded, disposable job hosts until the approved retention contract has runtime and
  consumer qualification; approval alone does not add the policy to these binaries:
  [issue #1](https://github.com/dekrevedko/Workflow-orca/issues/1).
- Applications must request cancellation and settle active work before disposing dependencies.
  Host shutdown does not yet provide an integrated stop-and-join guarantee:
  [issue #2](https://github.com/dekrevedko/Workflow-orca/issues/2).
- Native AOT and trimming are not qualified:
  [issue #3](https://github.com/dekrevedko/Workflow-orca/issues/3).
- Durable DAG execution remains incomplete. Packaging does not complete its implementation gates.
