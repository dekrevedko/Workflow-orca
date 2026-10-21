# Section 3 package-only shutdown consumer

This ordinary Release executable references only the candidate `OrcaCore` and
`OrcaCore.Engine.Ephemeral` packages plus Generic Host 10.0.9. It has no project
reference, friend, reflection bridge, internal engine access or test-framework
dependency. `NuGet.Config` names the repository's exact candidate feed and NuGet.org
for Microsoft dependencies. Qualify it at a short Windows path with a fresh,
isolated package cache; the root source worktree's long cache path is unsuitable.

After the full Release build and `pack-exact-package-feed.ps1`, run this project
with `dotnet run -c Release` and an explicit isolated `RestorePackagesPath`.
Preserve its single JSON result and the source/package digest in the qualification
record, then clean the generated cache and build outputs.

The executable asserts 48 cases: each of three ownership recipes repeats on three
fresh roots with natural completion of a pre-handle start, an application event,
a workflow delay, retry backoff, and Incomplete ownership. It also checks local
output-wait cancellation and concurrent hosted stops without a drain promise. A
deliberate RunAsync negative proves its incomplete-stop exception and final host
disposal can permit dependency use after disposal; that disposed root cannot be
reobserved as joined, and is never counted as a safe ownership recipe.
Every recipe keeps a provider-owned singleton, an actual child scope and its scoped
dependency, and an externally supplied dependency alive until Completed. Disposal
then follows other hosted stops, scope disposal, explicit external disposal and
root disposal. Steps detect dependency use after disposal. Child scopes resolve
the same engine lifetime. Plain DI uses an application submission gate and joins
its pending start before observing handles; it calls no invented close-only API.

The ordered Generic Host drainer is registered after the engine, uses sequential
stops and the shared shutdown budget/token, and yields on drain expiry. Explicit
host ownership observes the engine before stopping/disposal of the host. Incomplete
retains each actual owner, scope and external dependency for a later observation.

The public ephemeral package has no public workflow-event ingress binding. The
consumer's `application-event` case delivers an application dependency's signal to
an already running step. Real workflow-router ingress during Closed is exercised
separately through the production router by the shutdown unit tests; this consumer
does not claim public ephemeral router coverage or introduce a new API. The manual
clock only controls engine delay/retry timers, while test guards and Generic Host's
shared budget remain independent. These cases prove ownership/order, not process
isolation or forced cancellation of noncooperative external work.
