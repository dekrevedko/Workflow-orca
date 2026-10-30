# Install and publish OrcaCore prerelease packages

Status: GUIDE. Requires .NET 10. APIs, provider schemas, and persistence formats can change.
Durable DAG execution is still incomplete; packaging does not complete the implementation gates
in the active OpenSpec changes. Start with the ephemeral engine guide or the durable samples.

The [2026-10-16 publishing audit](github-publishing-status.md) records the preparation checks
and the owner-approved 75% coverage gate. Version `0.1.0-preview.2` is available on
[NuGet.org](https://www.nuget.org/packages/OrcaCore.Engine.Ephemeral/0.1.0-preview.2)
and as [GitHub release downloads](https://github.com/dekrevedko/Workflow-orca/releases).

The next candidate is **`0.1.0-preview.3` (prepared, not published)**. Its bounded
native-support changes are merged in PR #14. See the
[release preparation](release-0.1.0-preview.3.md) and
[draft release notes](prerelease-notes.md). The installation commands below retain
the currently published preview.2 until preview.3 passes both registry checks.

## Install in another project

Install the engine role you need directly from NuGet.org:

```powershell
dotnet add YourProject.csproj package OrcaCore.Engine.Ephemeral --version 0.1.0-preview.2
```

The application contracts package `OrcaCore` and internal kernel are transitive dependencies.
The [ephemeral guide](ephemeral-engine-developer-guide.md) shows registration and typed workflows.
For durable applications, reference `OrcaCore.Durable.Hosting` and the selected
`OrcaCore.Providers.PostgreSql` or `OrcaCore.Providers.SqlServer` package at the same version.
`OrcaCore.Providers.InMemory` is for development and testing.

The [NuGet.org publishing run](https://github.com/dekrevedko/Workflow-orca/actions/runs/39905678249)
verified all twelve public versions and their source metadata. A fresh external consumer
restored exclusively from NuGet.org and ran the typed workflow, returning `42`.

### Install from GitHub Packages

The additional NuGet feed is `https://nuget.pkg.github.com/dekrevedko/index.json`.
GitHub requires authentication even for public NuGet packages. For local use, provide
a classic personal access token with `read:packages` through a secret store or an
environment variable. Keep credentials out of committed configuration. Actions in
the linked repository use their `GITHUB_TOKEN` instead. See
[GitHub's registry guide](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).

Use an explicit `NuGet.Config` to route OrcaCore packages to GitHub and other
dependencies to NuGet.org:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="github" value="https://nuget.pkg.github.com/dekrevedko/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="github">
      <package pattern="OrcaCore" />
      <package pattern="OrcaCore.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

With `GH_PACKAGES_READ_TOKEN` already supplied securely, authenticate for this shell
and restore the project:

```powershell
$env:NuGetPackageSourceCredentials_github = "Username=YOUR_GITHUB_USERNAME;Password=$env:GH_PACKAGES_READ_TOKEN;ValidAuthenticationTypes=Basic"
dotnet add YourProject.csproj package OrcaCore.Engine.Ephemeral --version 0.1.0-preview.2 --no-restore
dotnet restore YourProject.csproj --configfile NuGet.Config
```

The release workflow verifies this route with a fresh package cache, checks that all
twelve OrcaCore packages actually came from GitHub, and compares their downloaded
SHA-256 hashes with the original release assets. Availability requires the
`Publish and verify GitHub Packages` job to succeed. NuGet.org remains available
without a GitHub credential.

### Install from GitHub release downloads

Download and extract the `orcacore-packages` artifact from a successful CI run, or download all
twelve `.nupkg` assets and `SHA256SUMS` from a
[GitHub prerelease](https://github.com/dekrevedko/Workflow-orca/releases).
Keep the full set together so NuGet can resolve OrcaCore's package dependencies.

Register the extracted directory as a local NuGet source, then reference the desired role:

```powershell
# Replace the directory and version with those you downloaded.
dotnet nuget add source C:\packages\OrcaCore --name OrcaCore-local
dotnet add YourProject.csproj package OrcaCore.Engine.Ephemeral --version 0.1.0-preview.2
```

GitHub release downloads work without a package-registry credential. They are a directory feed
after download; the release URL itself is not a NuGet source.

NuGet.org publication uses [trusted publishing](nuget-publishing.md). Its final job verifies
all twelve packages on the public feed and runs the clean consumer before reporting availability.

## Build local packages immediately

From the repository root in PowerShell 7:

```powershell
./eng/Pack-Packages.ps1 -Version 0.1.0-preview.local.1 -OutputDirectory artifacts/my-packages
./eng/Test-Packages.ps1 -Version 0.1.0-preview.local.1 -PackageDirectory artifacts/my-packages
```

Use an empty output directory for each pack. The script packs exactly the twelve source projects
in the current package manifest, applies one prerelease version, records the source commit in
NuGet metadata, and writes SHA-256 checksums. `-NoBuild` is only for a current Release build.
The consumer check restores all twelve packages with a fresh cache outside the repository and
executes the typed sample, asserting output `42`. It leaves its temporary directory for inspection.
This checks package installation and an ephemeral journey, not full provider or DAG certification.

## Publish on GitHub

Open a pull request against `master`. CI runs the solution build (including benchmarks),
benchmark smoke checks, unit and acceptance suites, infrastructure contract guards, real PostgreSQL
and SQL Server tests, and integration tests. It also enforces the 75% engine line-coverage gate.
Only a successful run can upload the installable package artifact.

Create and push a new, unused prerelease tag on the reviewed commit (for example, preview 3):

```powershell
git tag v0.1.0-preview.3
git push origin v0.1.0-preview.3
```

The `Publish prerelease` workflow reruns CI, the external package consumer and the six native
consumer cells against the exact same twelve-package artifact before creating the
GitHub prerelease with all twelve packages and checksums. It only grants `contents: write` to the
final publishing job and needs no additional secrets. After the workflow exists on the default
branch, it can also be run manually with a prerelease version. Stable versions are rejected.
Do not reuse a published version; choose a new version for changed source.

The [publishing guide](nuget-publishing.md) covers NuGet.org trusted publishing and
mirroring an existing prerelease to GitHub Packages without rebuilding it. The
registries use separate authentication and permissions. Signing, SourceLink, and stable API
compatibility remain outside this prerelease workflow.

## Issue #13 candidate compatibility boundary (source-qualified; release pending)

User-approved corrected checkpoint `d8c55f3` and review-only child `25fa36c` qualify the selected
`OrcaCore` / `OrcaCore.Core` / `OrcaCore.Engine.Ephemeral` recipe described in
[the native support guide](ephemeral-native-support.md). The approved six-cell
candidate feed uses validation version `0.0.0-phase0`; it is not a published
package version. Preview.2 lacks `WorkflowJsonMetadata` and remains unsupported
for the earlier assessed trimmed/AOT profiles.

A future prerelease must use a new, unused version, pass release/registry gates
and verify its source/profile before installation instructions name it as
available. This release-preparation target selects `0.1.0-preview.3` as the candidate;
it remains unavailable until publication succeeds. Local source builds can use a distinct private prerelease label; do not
substitute preview.2 or mix versions across the selected closure.

The existing product `StrongStringValueJsonConverterFactory` is public for
ordinary generated strong-value DTOs. Keep product attributes; explicit context
converter options, including this factory, remain prohibited. The twelve-package
inventory grants no all-package native badge. Existing NuGet.org/GitHub Packages
authentication and published-version instructions above are unchanged.
